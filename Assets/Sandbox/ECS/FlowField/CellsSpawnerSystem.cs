using System.Collections.Generic;

using Sandbox.ECS.CastleWars;

using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

using UnityEngine.Profiling;

namespace Sandbox.ECS.FlowField
{
    [UpdateBefore(typeof(InitializeFlowFieldGridSystem))]
    public partial class CellsSpawnerSystem : SystemBase
    {
        private List<MainTargetComponent> _mainTargets = new List<MainTargetComponent>(2);

        private EntityCommandBufferSystem _ecbSystem;

        private EntityQuery _newFlowFieldQuery;

        protected override void OnCreate()
        {
            _newFlowFieldQuery = GetEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<NewFlowFieldData>() } });
            _ecbSystem         = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();

            RequireSingletonForUpdate<FlowFieldControllerData>();
            RequireForUpdate(_newFlowFieldQuery);
        }

        protected override void OnUpdate()
        {
            var flowFieldControllerData = GetSingleton<FlowFieldControllerData>();
            var cb                      = _ecbSystem.CreateCommandBuffer().AsParallelWriter();

            EntityManager.GetAllUniqueSharedComponentData(_mainTargets);

            var cellRadius        = flowFieldControllerData.cellRadius;
            var gridSize          = flowFieldControllerData.gridSize;
            var cellCount         = gridSize.x * gridSize.y;
            var cellDataArchetype = EntityManager.CreateArchetype(typeof(CellData));

            var world                  = World.Unmanaged;
            var waypointQuery          = GetEntityQuery(typeof(WaypointComponent));
            var cellsQuery             = GetEntityQuery(typeof(CellData));
            var localToWorldTypeHandle = GetComponentTypeHandle<LocalToWorld>();
            var entityTypeHandle       = GetEntityTypeHandle();
            var cellDataTypeHandle     = GetComponentTypeHandle<CellData>();

            for ( var mainTargetIndex = 0; mainTargetIndex < _mainTargets.Count; mainTargetIndex++ )
            {
                var settings = _mainTargets[mainTargetIndex];
                var cells    = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(cellCount, ref world.UpdateAllocator);

                Entities
                    .WithStructuralChanges()
                    .WithSharedComponentFilter(settings)
                    .ForEach((Entity entity, in NewFlowFieldData newFlowFieldData) =>
                             {
                                 Profiler.BeginSample("Instantiate");
                                 EntityManager.CreateEntity(cellDataArchetype, cells);
                                 Profiler.EndSample();

                                 var cellDataFromEntity   = GetComponentDataFromEntity<CellData>();
                                 var setCellDataJob       = new SetCellDataJob { CellDataFromEntity = cellDataFromEntity, Entities = cells, CellRadius = cellRadius, GridHeight = gridSize.y };
                                 var setCellDataJobHandle = setCellDataJob.Schedule(cellCount, 64, Dependency);

                                 Dependency = setCellDataJobHandle;
                             })
                    .Run();

                var setCellBufferJobHandle = Entities
                                             // .WithoutBurst()
                                             .WithSharedComponentFilter(settings)
                                             .ForEach((Entity entity, in NewFlowFieldData newFlowFieldData) =>
                                                      {
                                                          var cellBuffer = (newFlowFieldData.isExistingFlowField ? GetBuffer<EntityBufferElement>(entity) : cb.AddBuffer<EntityBufferElement>(0, entity)).Reinterpret<Entity>();
                                                          cellBuffer.ResizeUninitialized(cellCount);

                                                          for ( var index = 0; index < cells.Length; index++ )
                                                          {
                                                              cellBuffer[index] = cells[index];
                                                          }

                                                          cb.RemoveComponent<NewFlowFieldData>(0, entity);
                                                          cb.AddComponent<InitializeFlowFieldTag>(0, entity);
                                                      }).Schedule(Dependency);

                var waypointSettings = new WaypointComponent { TargetFor = settings.TargetFor };
                waypointQuery.AddSharedComponentFilter(waypointSettings);

                var waypointCount = waypointQuery.CalculateEntityCount();
                var waypoints     = CollectionHelper.CreateNativeArray<int2, RewindableAllocator>(waypointCount, ref world.UpdateAllocator);

                var getWaypointJob       = new GetWaypointIndexJob { CellRadius = cellRadius, GridSize = gridSize, LocalToWorldTypeHandle = localToWorldTypeHandle, WaypointIndexes = waypoints };
                var getWaypointJobHandle = getWaypointJob.ScheduleParallel(waypointQuery, Dependency);

                var setWaypointJob = new SetWaypointJob {
                                                            Ecb                = cb,
                                                            EntityTypeHandle   = entityTypeHandle,
                                                            CellDataTypeHandle = cellDataTypeHandle,
                                                            Waypoints          = waypoints,
                                                            TargetFor          = settings.TargetFor
                                                        };

                var setWaypointJobHandle = setWaypointJob.ScheduleParallel(cellsQuery, getWaypointJobHandle);

                Dependency = JobHandle.CombineDependencies(setCellBufferJobHandle, setWaypointJobHandle);

                waypointQuery.ResetFilter();
            }
        }

        [BurstCompile]
        private struct SetCellDataJob : IJobParallelFor
        {
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction]
            public ComponentDataFromEntity<CellData> CellDataFromEntity;

            public NativeArray<Entity> Entities;

            public float CellRadius;

            public int GridHeight;

            public void Execute(int i)
            {
                var entity    = Entities[i];
                var cellData  = CellDataFromEntity[entity];
                var gridIndex = FromFlatIndex(i, GridHeight);
                cellData.worldPos      = new float3(CellRadius * 2 * gridIndex.x + CellRadius, 0, CellRadius * 2 * gridIndex.y + CellRadius);
                cellData.gridIndex     = gridIndex;
                cellData.bestCost      = ushort.MaxValue;
                cellData.bestDirection = int2.zero;
                cellData.targetIndex   = int2.zero;

                CellDataFromEntity[entity] = cellData;
            }

            private int2 FromFlatIndex(int index, int height)
            {
                var y = index % height;
                var x = (index - y) / height;

                return new int2(x, y);
            }
        }

        [BurstCompile]
        public struct GetWaypointIndexJob : IJobEntityBatchWithIndex
        {
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction]
            public ComponentTypeHandle<LocalToWorld> LocalToWorldTypeHandle;

            public NativeArray<int2> WaypointIndexes;

            public int2 GridSize;

            public float CellRadius;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex, int indexOfFirstEntityInQuery)
            {
                var positions = batchInChunk.GetNativeArray(LocalToWorldTypeHandle);

                for ( var i = 0; i < batchInChunk.Count; i++ )
                {
                    var position = positions[i].Position;
                    var percentX = position.x / (GridSize.x * CellRadius * 2);
                    var percentY = position.z / (GridSize.y * CellRadius * 2);

                    percentX = math.clamp(percentX, 0f, 1f);
                    percentY = math.clamp(percentY, 0f, 1f);

                    var cellIndex = new int2 { x = math.clamp((int)math.floor((GridSize.x) * percentX), 0, GridSize.x - 1), y = math.clamp((int)math.floor((GridSize.y) * percentY), 0, GridSize.y - 1) };
                    WaypointIndexes[indexOfFirstEntityInQuery + batchIndex + i] = cellIndex;
                }
            }
        }

        // AddSharedComponent doesn't seem to be burst compatible currently.
        // [BurstCompile]
        public struct SetWaypointJob : IJobEntityBatch
        {
            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction]
            public EntityCommandBuffer.ParallelWriter Ecb;

            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction]
            public ComponentTypeHandle<CellData> CellDataTypeHandle;

            [NativeDisableContainerSafetyRestriction] [NativeDisableParallelForRestriction]
            public EntityTypeHandle EntityTypeHandle;

            [ReadOnly] public NativeArray<int2> Waypoints;

            [ReadOnly] public TeamEnum TargetFor;

            public void Execute(ArchetypeChunk batchInChunk, int batchIndex)
            {
                var cells    = batchInChunk.GetNativeArray(CellDataTypeHandle);
                var entities = batchInChunk.GetNativeArray(EntityTypeHandle);

                for ( var i = 0; i < batchInChunk.Count; i++ )
                {
                    var cell   = cells[i];
                    var entity = entities[i];

                    foreach ( var waypoint in Waypoints )
                    {
                        if ( !cell.gridIndex.Equals(waypoint) )
                        {
                            continue;
                        }
                        
                        var waypointDistanceComponent = new WaypointDistanceToTargetComponent { GridIndex = cell.gridIndex };
                        Ecb.AddComponent<WaypointDistanceToTargetComponent>(batchIndex, entity);
                        Ecb.SetComponent(batchIndex, entity, waypointDistanceComponent);
                        
                        var waypointComponent = new WaypointComponent { TargetFor = TargetFor };
                        Ecb.AddSharedComponent(batchIndex, entity, waypointComponent);
                    }
                }
            }
        }
    }
}