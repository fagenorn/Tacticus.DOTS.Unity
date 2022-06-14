using System;
using System.Collections.Generic;

using Sandbox.ECS.CastleWars;

using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

using UnityEngine;

namespace Sandbox.ECS.FlowField
{
    public partial class CalculateFlowFieldSystem : SystemBase
    {
        private EntityQuery _mainTargetsQuery;

        private EntityQuery _waypointQuery;

        private EntityCommandBufferSystem _ecbSystem;

        private List<MainTargetComponent> _targets = new List<MainTargetComponent>(2);

        protected override void OnCreate()
        {
            _mainTargetsQuery = GetEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<MainTargetComponent>(), ComponentType.ReadOnly<LocalToWorld>(), ComponentType.ReadOnly<FlowFieldData>(), ComponentType.ReadOnly<EntityBufferElement>(), ComponentType.ReadOnly<CalculateFlowFieldTag>(), }, });
            _waypointQuery    = GetEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<CellData>(), ComponentType.ReadOnly<WaypointDistanceToTargetComponent>(), ComponentType.ReadOnly<WaypointComponent>() } });

            _ecbSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            EntityManager.GetAllUniqueSharedComponentData(_targets);

            var commandBuffer = _ecbSystem.CreateCommandBuffer();

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

                var world     = World.Unmanaged;
                var waypoints = CollectionHelper.CreateNativeArray<WaypointDistanceToTargetComponent, RewindableAllocator>(waypointCount, ref world.UpdateAllocator);

                var flowFieldEntity = _mainTargetsQuery.GetSingletonEntity();
                var targetPosition  = GetComponent<LocalToWorld>(flowFieldEntity).Position;
                var flowFieldData   = GetComponent<FlowFieldData>(flowFieldEntity);
                var targetGridIndex = FlowFieldHelper.GetCellIndexFromWorldPos(targetPosition, flowFieldData.gridSize, flowFieldData.cellRadius * 2);

                Entities
                    .WithAll<WaypointDistanceToTargetComponent>()
                    .WithSharedComponentFilter(waypointSettings)
                    .WithName("GetWaypointsJob")
                    .ForEach((int entityInQueryIndex, in WaypointDistanceToTargetComponent waypoint) => { waypoints[entityInQueryIndex] = waypoint; })
                    .Run();

                Entities
                    .WithoutBurst()
                    .WithSharedComponentFilter(teamSettings)
                    .ForEach((Entity                                 entity,
                              ref DynamicBuffer<EntityBufferElement> buffer,
                              in  CalculateFlowFieldTag              calculateFlowFieldTag,
                              in  FlowFieldData                      flowFieldData) =>
                             {
                                 commandBuffer.RemoveComponent<CalculateFlowFieldTag>(entity);

                                 var entityBuffer      = buffer.Reinterpret<Entity>();
                                 var cellDataContainer = new NativeArray<CellData>(entityBuffer.Length, Allocator.TempJob);

                                 var gridSize = flowFieldData.gridSize;

                                 for ( int i = 0; i < entityBuffer.Length; i++ )
                                 {
                                     cellDataContainer[i] = GetComponent<CellData>(entityBuffer[i]);
                                 }

                                 var neighborIndices = new NativeList<int2>(Allocator.TempJob);
                                 var indicesToCheck  = new NativeQueue<int2>(Allocator.TempJob);

                                 indicesToCheck.Enqueue(targetGridIndex);

                                 var flatDestinationIndexz = FlowFieldHelper.ToFlatIndex(targetGridIndex, gridSize.y);
                                 var destinationCellz      = cellDataContainer[flatDestinationIndexz];

                                 destinationCellz.cost                    = 0;
                                 destinationCellz.bestCost                = 0;
                                 cellDataContainer[flatDestinationIndexz] = destinationCellz;

                                 new CalculateFlowfieldForTargetJob {
                                                                        IsFirst        = true,
                                                                        Cells          = cellDataContainer,
                                                                        TargetIndex    = targetGridIndex,
                                                                        IndicesToCheck = indicesToCheck,
                                                                        FlowFieldData  = flowFieldData
                                                                    }.Run();

                                 var setCellTargetJob = new SetCellTargetJob { TargetGridIndex = flowFieldData.TargetGridIndex, FlowFieldData = flowFieldData, Waypoints = waypoints, Cells = cellDataContainer };
                                 setCellTargetJob.Run(cellDataContainer.Length);

                                 foreach ( var waypoint in waypoints )
                                 {
                                     indicesToCheck.Enqueue(waypoint.GridIndex);

                                     var flatDestinationIndexzz = FlowFieldHelper.ToFlatIndex(waypoint.GridIndex, gridSize.y);
                                     var destinationCellzz      = cellDataContainer[flatDestinationIndexzz];

                                     var smallestCost = ushort.MaxValue;
                                     FlowFieldHelper.GetNeighborIndices(destinationCellzz.gridIndex, GridDirection.CardinalAndIntercardinalDirections, flowFieldData.gridSize, ref neighborIndices);
                                     foreach ( var neighborIndex in neighborIndices )
                                     {
                                         int      flatNeighborIndex = FlowFieldHelper.ToFlatIndex(neighborIndex, gridSize.y);
                                         CellData neighborCellData  = cellDataContainer[flatNeighborIndex];

                                         if ( neighborCellData.bestCost < smallestCost )
                                         {
                                             smallestCost = neighborCellData.bestCost;
                                         }
                                     }

                                     neighborIndices.Clear();

                                     cellDataContainer[flatDestinationIndexzz] = destinationCellzz;

                                     new CalculateFlowfieldForTargetJob { Cells = cellDataContainer, TargetIndex = waypoint.GridIndex, IndicesToCheck = indicesToCheck, FlowFieldData = flowFieldData }.Run();

                                     destinationCellzz                         = cellDataContainer[flatDestinationIndexzz];
                                     destinationCellzz.targetIndex             = destinationCellzz.gridIndex;
                                     cellDataContainer[flatDestinationIndexzz] = destinationCellzz;
                                 }

                                 // Flow Field
                                 for ( int i = 0; i < cellDataContainer.Length; i++ )
                                 {
                                     CellData curCullData = cellDataContainer[i];
                                     neighborIndices.Clear();
                                     FlowFieldHelper.GetNeighborIndices(curCullData.gridIndex, GridDirection.AllDirections, gridSize, ref neighborIndices);
                                     ushort bestCost         = curCullData.bestCost;
                                     int2   bestDirection    = int2.zero;
                                     bool   isNextToObstacle = false;
                                     foreach ( int2 neighborIndex in neighborIndices )
                                     {
                                         int      flatNeighborIndex = FlowFieldHelper.ToFlatIndex(neighborIndex, gridSize.y);
                                         CellData neighborCellData  = cellDataContainer[flatNeighborIndex];

                                         if ( neighborCellData.cost == byte.MaxValue )
                                         {
                                             isNextToObstacle = true;

                                             break;
                                         }

                                         if ( !neighborCellData.targetIndex.Equals(curCullData.targetIndex) )
                                         {
                                             continue;
                                         }

                                         if ( neighborCellData.bestCost < bestCost )
                                         {
                                             bestCost      = neighborCellData.bestCost;
                                             bestDirection = neighborCellData.gridIndex - curCullData.gridIndex;
                                         }
                                     }

                                     if ( isNextToObstacle )
                                     {
                                         bestCost      = curCullData.bestCost;
                                         bestDirection = int2.zero;
                                         neighborIndices.Clear();
                                         FlowFieldHelper.GetNeighborIndices(curCullData.gridIndex, GridDirection.CardinalDirectionsAndNone, gridSize, ref neighborIndices);
                                         foreach ( int2 neighborIndex in neighborIndices )
                                         {
                                             int      flatNeighborIndex = FlowFieldHelper.ToFlatIndex(neighborIndex, gridSize.y);
                                             CellData neighborCellData  = cellDataContainer[flatNeighborIndex];

                                             if ( !neighborCellData.targetIndex.Equals(curCullData.targetIndex) )
                                             {
                                                 continue;
                                             }

                                             if ( neighborCellData.bestCost < bestCost )
                                             {
                                                 bestCost      = neighborCellData.bestCost;
                                                 bestDirection = neighborCellData.gridIndex - curCullData.gridIndex;
                                             }
                                         }
                                     }

                                     if ( bestDirection.Equals(int2.zero) )
                                     {
                                         foreach ( int2 neighborIndex in neighborIndices )
                                         {
                                             int      flatNeighborIndex = FlowFieldHelper.ToFlatIndex(neighborIndex, gridSize.y);
                                             CellData neighborCellData  = cellDataContainer[flatNeighborIndex];

                                             if ( neighborCellData.bestCost < bestCost )
                                             {
                                                 bestCost      = neighborCellData.bestCost;
                                                 bestDirection = neighborCellData.gridIndex - curCullData.gridIndex;
                                             }
                                         }
                                     }

                                     curCullData.bestDirection = bestDirection;
                                     cellDataContainer[i]      = curCullData;
                                 }

                                 for ( int i = 0; i < entityBuffer.Length; i++ )
                                 {
                                     commandBuffer.SetComponent(entityBuffer[i], cellDataContainer[i]);
                                 }

                                 neighborIndices.Dispose();
                                 cellDataContainer.Dispose();
                                 indicesToCheck.Dispose();

                                 commandBuffer.AddComponent<CompleteFlowFieldTag>(entity);
                             })
                    .Run();

                _mainTargetsQuery.ResetFilter();
                _waypointQuery.ResetFilter();
            }

            _targets.Clear();
        }

        [BurstCompatible]
        public struct CalculateFlowfieldForTargetJob : IJob
        {
            public NativeQueue<int2> IndicesToCheck;

            public NativeArray<CellData> Cells;

            public int2 TargetIndex;

            public FlowFieldData FlowFieldData;

            public bool IsFirst;

            public void Execute()
            {
                var neighborIndices = new NativeList<int2>(Allocator.Temp);

                while ( IndicesToCheck.Count > 0 )
                {
                    int2     cellIndex     = IndicesToCheck.Dequeue();
                    int      cellFlatIndex = FlowFieldHelper.ToFlatIndex(cellIndex, FlowFieldData.gridSize.y);
                    CellData curCellData   = Cells[cellFlatIndex];

                    neighborIndices.Clear();
                    FlowFieldHelper.GetNeighborIndices(cellIndex, GridDirection.CardinalDirections, FlowFieldData.gridSize, ref neighborIndices);
                    foreach ( int2 neighborIndex in neighborIndices )
                    {
                        int      flatNeighborIndex = FlowFieldHelper.ToFlatIndex(neighborIndex, FlowFieldData.gridSize.y);
                        CellData neighborCellData  = Cells[flatNeighborIndex];

                        if ( neighborCellData.cost == byte.MaxValue )
                        {
                            continue;
                        }

                        if ( !IsFirst && !neighborCellData.targetIndex.Equals(TargetIndex) )
                        {
                            continue;
                        }

                        if ( neighborCellData.cost + curCellData.bestCost >= neighborCellData.bestCost )
                        {
                            continue;
                        }

                        neighborCellData.bestCost = (ushort)(neighborCellData.cost + curCellData.bestCost);

                        Cells[flatNeighborIndex] = neighborCellData;
                        IndicesToCheck.Enqueue(neighborIndex);
                    }
                }

                neighborIndices.Dispose();
            }
        }

        [BurstCompile]
        private struct SetCellTargetJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<WaypointDistanceToTargetComponent> Waypoints;

            public NativeArray<CellData> Cells;

            public FlowFieldData FlowFieldData;

            public int2 TargetGridIndex;

            public void Execute(int index)
            {
                var cell = Cells[index];

                if ( Waypoints.Length == 0 )
                {
                    cell.targetIndex = FlowFieldData.TargetGridIndex;
                    Cells[index]     = cell;

                    return;
                }

                int2 nearestGridIndex;
                var  distanceToTarget = math.sqrt(math.lengthsq(cell.gridIndex - TargetGridIndex));
                if ( TryGetNearestWaypointIndex(Waypoints, cell, distanceToTarget, out var nearestWaypointIndex, out _) )
                {
                    var nearestWaypoint = Waypoints[nearestWaypointIndex];
                    nearestGridIndex = nearestWaypoint.GridIndex;
                }
                else
                {
                    nearestGridIndex = FlowFieldData.TargetGridIndex;
                }

                cell.targetIndex = nearestGridIndex;
                Cells[index]     = cell;
            }

            private bool TryGetNearestWaypointIndex(NativeArray<WaypointDistanceToTargetComponent> waypoints, CellData cellData, float distanceToTarget, out int nearestWaypointIndex, out float nearestDistance)
            {
                nearestWaypointIndex = -1;
                nearestDistance      = float.MaxValue;

                for ( var i = 0; i < waypoints.Length; i++ )
                {
                    var waypoint         = waypoints[i];
                    var flatIndex        = FlowFieldHelper.ToFlatIndex(waypoint.GridIndex, FlowFieldData.gridSize.y);
                    var waypointCellData = Cells[flatIndex];

                    // Unreachable
                    if ( waypointCellData.bestCost >= cellData.bestCost )
                    {
                        continue;
                    }

                    if ( cellData.gridIndex.Equals(waypoint.GridIndex) )
                    {
                        continue;
                    }

                    if ( distanceToTarget - waypoint.Distance <= 0 )
                    {
                        continue;
                    }

                    var distance = math.lengthsq(cellData.gridIndex - waypoint.GridIndex);
                    var nearest  = nearestDistance - distance > 0;

                    nearestDistance      = math.select(nearestDistance, distance, nearest);
                    nearestWaypointIndex = math.select(nearestWaypointIndex, i, nearest);
                }

                return nearestWaypointIndex != -1;
            }
        }
    }
}