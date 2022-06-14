using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Sandbox.ECS.CastleWars;

using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

using UnityEngine;

namespace Sandbox.ECS.FlowField
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(BuildPhysicsWorld))]
    [UpdateBefore(typeof(StepPhysicsWorld))]
    public partial class InitializeFlowFieldGridSystem : SystemBase
    {
        private readonly List<MainTargetComponent> _targets = new List<MainTargetComponent>(2);

        private EntityQuery _cellQuery;

        private EntityCommandBufferSystem _ecbSystem;

        private EntityQuery _mainTargetsQuery;

        private BuildPhysicsWorld _physicsWorldSystem;

        private EntityQuery _waypointQuery;

        private NativeArray<int2> _directions;

        protected override void OnStartRunning() { this.RegisterPhysicsRuntimeSystemReadOnly(); }

        protected override void OnCreate()
        {
            _mainTargetsQuery = GetEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<MainTargetComponent>(), ComponentType.ReadOnly<LocalToWorld>(), ComponentType.ReadOnly<FlowFieldData>(), ComponentType.ReadOnly<EntityBufferElement>(), ComponentType.ReadOnly<InitializeFlowFieldTag>() } });

            _cellQuery = GetEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<CellData>() } });
            _cellQuery.AddChangedVersionFilter(ComponentType.ReadWrite<CellData>());

            _waypointQuery = GetEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<CellData>(), ComponentType.ReadOnly<WaypointDistanceToTargetComponent>(), ComponentType.ReadOnly<WaypointComponent>() } });

            _ecbSystem          = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            _physicsWorldSystem = World.GetExistingSystem<BuildPhysicsWorld>();

            _directions = new NativeArray<int2>(GridDirection.AllDirections.Count, Allocator.Persistent);
            for ( var index = 0; index < GridDirection.AllDirections.Count; index++ )
            {
                var direction = GridDirection.AllDirections[index];
                _directions[index] = direction.Vector;
            }

            RequireForUpdate(_mainTargetsQuery);
        }

        protected override void OnDestroy() { _directions.Dispose(); }

        protected override void OnUpdate()
        {
            EntityManager.GetAllUniqueSharedComponentData(_targets);

            var physicsWorld = _physicsWorldSystem.PhysicsWorld;
            var ecb          = _ecbSystem.CreateCommandBuffer().AsParallelWriter();

            for ( var teamIndex = 0; teamIndex < _targets.Count; teamIndex++ )
            {
                var teamSettings = _targets[teamIndex];
                _mainTargetsQuery.AddSharedComponentFilter(teamSettings);
                var targetCount = _mainTargetsQuery.CalculateEntityCount();

                // There should only be one target
                if ( targetCount != 1 )
                {
                    _mainTargetsQuery.ResetFilter();

                    continue;
                }

                var waypointSettings = new WaypointComponent { TargetFor = teamSettings.TargetFor };
                _waypointQuery.AddSharedComponentFilter(waypointSettings);
                var waypointCount = _waypointQuery.CalculateEntityCount();

                var world           = World.Unmanaged;
                var waypoints       = CollectionHelper.CreateNativeArray<WaypointDistanceToTargetComponent, RewindableAllocator>(waypointCount, ref world.UpdateAllocator);
                var flowFieldEntity = _mainTargetsQuery.GetSingletonEntity();
                var flowFieldData   = GetComponent<FlowFieldData>(flowFieldEntity);
                var cellsBuffer     = GetBuffer<EntityBufferElement>(flowFieldEntity).Reinterpret<Entity>();
                var cells           = cellsBuffer.AsNativeArray();

                Entities
                    .WithName("InitializeCellsJob")
                    .WithAll<InitializeFlowFieldTag>()
                    .WithSharedComponentFilter(teamSettings)
                    .WithStructuralChanges()
                    .ForEach((Entity entity) =>
                             {
                                 EntityManager.RemoveComponent<InitializeFlowFieldTag>(entity);
                                 EntityManager.AddComponent<CalculateFlowFieldTag>(entity);
                             })
                    .Run();

                var calculateDistanceToTargetJob       = new CalculateDistanceWaypointToTargetJob { TargetGridIndex = flowFieldData.TargetGridIndex, CellDataTypeHandle = GetComponentTypeHandle<CellData>(), WaypointDistanceToTargetTypeHandle = GetComponentTypeHandle<WaypointDistanceToTargetComponent>() };
                var calculateDistanceToTargetJobHandle = calculateDistanceToTargetJob.ScheduleParallel(_waypointQuery, Dependency);

                var getWaypointsJobHandle = Entities
                                            .WithAll<WaypointDistanceToTargetComponent>()
                                            .WithSharedComponentFilter(waypointSettings)
                                            .WithName("GetWaypointsJob")
                                            .ForEach((int entityInQueryIndex, in WaypointDistanceToTargetComponent waypoint) => { waypoints[entityInQueryIndex] = waypoint; })
                                            .ScheduleParallel(calculateDistanceToTargetJobHandle);

                var evaluateCostJob = new EvaluateCellCostJob {
                                                                  World = physicsWorld.CollisionWorld, CellHalfExtents = Vector3.one * 1, CellDataComponentDataFromEntity = GetComponentDataFromEntity<CellData>(), Cells = cells,
                                                              };

                var evaluateCostJobHandle = evaluateCostJob.Schedule(cells.Length, 8, getWaypointsJobHandle);

                var markObstacleCellJob = new MarkObstacleCellJob {
                                                                      Cells                           = cells,
                                                                      Directions                      = _directions,
                                                                      Ecb                             = ecb,
                                                                      FlowFieldData                   = flowFieldData,
                                                                      CellDataComponentDataFromEntity = GetComponentDataFromEntity<CellData>()
                                                                  };

                var markObstacleCellJobHandle = markObstacleCellJob.Schedule(cells.Length, 64, evaluateCostJobHandle);

                Dependency = markObstacleCellJobHandle;

                _cellQuery.AddDependency(Dependency);
                _mainTargetsQuery.AddDependency(Dependency);
                _mainTargetsQuery.ResetFilter();
                _waypointQuery.ResetFilter();
                _ecbSystem.AddJobHandleForProducer(Dependency);
            }

            _targets.Clear();
        }

        [BurstCompile]
        private struct EvaluateCellCostJob : IJobParallelFor
        {
            [ReadOnly] public CollisionWorld World;

            [ReadOnly] public Vector3 CellHalfExtents;

            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction]
            public NativeArray<Entity> Cells;

            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction]
            public ComponentDataFromEntity<CellData> CellDataComponentDataFromEntity;

            private static readonly CollisionFilter Filter = new CollisionFilter { BelongsTo = 1 << 0, CollidesWith = 1 << 1 };

            public void Execute(int index)
            {
                var entity = Cells[index];
                var cell   = CellDataComponentDataFromEntity[entity];

                byte cost = 1;
                var  hits = new NativeList<DistanceHit>(Allocator.Temp);

                if ( World.OverlapBox(cell.worldPos, quaternion.identity, CellHalfExtents, ref hits, Filter) )
                {
                    cost = byte.MaxValue;
                }

                cell.cost = cost;

                CellDataComponentDataFromEntity[entity] = cell;

                hits.Dispose();
            }
        }

        [BurstCompile]
        private struct MarkObstacleCellJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<int2> Directions;

            public FlowFieldData FlowFieldData;

            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction]
            public NativeArray<Entity> Cells;

            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction]
            public ComponentDataFromEntity<CellData> CellDataComponentDataFromEntity;

            public EntityCommandBuffer.ParallelWriter Ecb;

            public void Execute(int index)
            {
                var entity = Cells[index];
                var cell   = CellDataComponentDataFromEntity[entity];

                if ( cell.cost != byte.MaxValue )
                {
                    return;
                }

                // Ignore obstacle cells that only border other obstacle cells.
                var neighborIndices = new NativeList<int2>(9, Allocator.Temp);
                GetNeighborIndices(cell.gridIndex, Directions, FlowFieldData.gridSize, ref neighborIndices);
                foreach ( var neighborIndex in neighborIndices )
                {
                    var flatNeighborIndex = FlowFieldHelper.ToFlatIndex(neighborIndex, FlowFieldData.gridSize.y);
                    var neighborEntity    = Cells[flatNeighborIndex];
                    var neighborCell      = CellDataComponentDataFromEntity[neighborEntity];

                    if ( neighborCell.cost != byte.MaxValue )
                    {
                        Ecb.AddComponent<ObstacleTag>(index, entity);

                        return;
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void GetNeighborIndices(int2 originIndex, NativeArray<int2> directions, int2 gridSize, ref NativeList<int2> results)
            {
                foreach ( var curDirection in directions )
                {
                    var neighborIndex = GetIndexAtRelativePosition(originIndex, curDirection, gridSize);

                    if ( neighborIndex.x >= 0 )
                    {
                        results.Add(neighborIndex);
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int2 GetIndexAtRelativePosition(int2 originPos, int2 relativePos, int2 gridSize)
            {
                var finalPos = originPos + relativePos;
                if ( finalPos.x < 0 || finalPos.x >= gridSize.x || finalPos.y < 0 || finalPos.y >= gridSize.y )
                {
                    return new int2(-1, -1);
                }
                else
                {
                    return finalPos;
                }
            }
        }

        [BurstCompile]
        private struct CalculateDistanceWaypointToTargetJob : IJobEntityBatch
        {
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction]
            public ComponentTypeHandle<WaypointDistanceToTargetComponent> WaypointDistanceToTargetTypeHandle;

            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction]
            public ComponentTypeHandle<CellData> CellDataTypeHandle;

            public int2 TargetGridIndex;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                var cells     = batchInChunk.GetNativeArray(CellDataTypeHandle);
                var waypoints = batchInChunk.GetNativeArray(WaypointDistanceToTargetTypeHandle);

                for ( var i = 0; i < batchInChunk.Count; i++ )
                {
                    var waypoint = waypoints[i];
                    var cell     = cells[i];

                    var distance = math.sqrt(math.lengthsq(cell.gridIndex - TargetGridIndex));
                    waypoint.Distance = distance;

                    waypoints[i] = waypoint;
                }
            }
        }
    }
}