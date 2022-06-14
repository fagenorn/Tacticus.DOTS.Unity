using System;
using System.Collections.Generic;

using Sandbox.ECS.CastleWars;
using Sandbox.ECS.FlowField;
using Sandbox.ECS.KNN;

using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace Sandbox.ECS.Boids
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(BuildPhysicsWorld)), UpdateBefore(typeof(StepPhysicsWorld))]
    public partial class BoidSystem : SystemBase
    {
        private EntityQuery _boidQuery;

        private EntityQuery _mainTargetsQuery;

        private EntityQuery _obstacleQuery;

        // In this sample there are 3 total unique boid variants, one for each unique value of the
        // Boid SharedComponent (note: this includes the default uninitialized value at
        // index 0, which isnt actually used in the sample).
        private readonly List<Boid> _uniqueTypes = new List<Boid>(3);

        protected override void OnUpdate()
        {
            var obstacleCount     = _obstacleQuery.CalculateEntityCount();
            var obstaclePositions = CollectionHelper.CreateNativeArray<float3, RewindableAllocator>(obstacleCount, ref World.Unmanaged.UpdateAllocator);

            var copyObstaclePositionsJobHandle = Entities
                                                 .WithName("CopyObstaclePositionsJob")
                                                 .WithAll<ObstacleTag>()
                                                 .WithStoreEntityQueryInField(ref _obstacleQuery)
                                                 .ForEach((int entityInQueryIndex, in CellData cellData) => { obstaclePositions[entityInQueryIndex] = new float3(cellData.worldPos.x, 0, cellData.worldPos.z); })
                                                 .ScheduleParallel(Dependency);

            EntityManager.GetAllUniqueSharedComponentData(_uniqueTypes);

            // Each variant of the Boid represents a different value of the SharedComponentData and is self-contained,
            // meaning Boids of the same variant only interact with one another. Thus, this loop processes each
            // variant type individually.
            for ( int boidVariantIndex = 0; boidVariantIndex < _uniqueTypes.Count; boidVariantIndex++ )
            {
                var settings = _uniqueTypes[boidVariantIndex];
                _boidQuery.AddSharedComponentFilter(settings);
                _mainTargetsQuery.AddSharedComponentFilter(new MainTargetComponent { TargetFor = settings.Team });

                var boidCount   = _boidQuery.CalculateEntityCount();
                var targetCount = _mainTargetsQuery.CalculateEntityCount();

                if ( boidCount == 0 || targetCount != 1 )
                {
                    // Early out. If the given variant includes no Boids, move on to the next loop.
                    // For example, variant 0 will always exit early bc it's it represents a default, uninitialized
                    // Boid struct, which does not appear in this sample.
                    _boidQuery.ResetFilter();
                    _mainTargetsQuery.ResetFilter();

                    continue;
                }

                var flowFieldEntity            = _mainTargetsQuery.GetSingletonEntity();
                var flowFieldData              = GetComponent<FlowFieldData>(flowFieldEntity);
                var cellBuffers                = GetBufferFromEntity<EntityBufferElement>();
                var cellBuffer                 = cellBuffers[flowFieldEntity].Reinterpret<Entity>();
                var cellDataComponentAllocator = GetComponentDataFromEntity<CellData>();

                // The following calculates spatial cells of neighboring Boids
                // note: working with a sparse grid and not a dense bounded grid so there
                // are no predefined borders of the space.
                var world                     = World.Unmanaged;
                var hashMap                   = new NativeMultiHashMap<int, int>(boidCount, world.UpdateAllocator.ToAllocator);
                var cellIndices               = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(boidCount, ref world.UpdateAllocator);
                var cellCount                 = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(boidCount, ref world.UpdateAllocator);
                var cellAlignment             = CollectionHelper.CreateNativeArray<float3, RewindableAllocator>(boidCount, ref world.UpdateAllocator);
                var cellSeparation            = CollectionHelper.CreateNativeArray<float3, RewindableAllocator>(boidCount, ref world.UpdateAllocator);
                var cellObstacleDistance      = CollectionHelper.CreateNativeArray<float, RewindableAllocator>(boidCount, ref world.UpdateAllocator);
                var cellObstaclePositionIndex = CollectionHelper.CreateNativeArray<int, RewindableAllocator>(boidCount, ref world.UpdateAllocator);

                // The following jobs all run in parallel because the same JobHandle is passed for their
                // input dependencies when the jobs are scheduled; thus, they can run in any order (or concurrently).
                // The concurrency is property of how they're scheduled, not of the job structs themselves.

                // These jobs extract the relevant position, heading component
                // to NativeArrays so that they can be randomly accessed by the `MergeCells` and `Steer` jobs.
                // These jobs are defined inline using the Entities.ForEach lambda syntax.
                var initialCellAlignmentJobHandle = Entities
                                                    .WithSharedComponentFilter(settings)
                                                    .WithName("InitialCellAlignmentJob")
                                                    .ForEach((int entityInQueryIndex, in LocalToWorld localToWorld) => { cellAlignment[entityInQueryIndex] = new float3(localToWorld.Forward.x, 0, localToWorld.Forward.y); })
                                                    .ScheduleParallel(Dependency);

                var initialCellSeparationJobHandle = Entities
                                                     .WithSharedComponentFilter(settings)
                                                     .WithName("InitialCellSeparationJob")
                                                     .ForEach((int entityInQueryIndex, in LocalToWorld localToWorld) => { cellSeparation[entityInQueryIndex] = new float3(localToWorld.Position.x, 0, localToWorld.Position.z); })
                                                     .ScheduleParallel(Dependency);

                // Populates a hash map, where each bucket contains the indices of all Boids whose positions quantize
                // to the same value for a given cell radius so that the information can be randomly accessed by
                // the `MergeCells` and `Steer` jobs.
                // This is useful in terms of the algorithm because it limits the number of comparisons that will
                // actually occur between the different boids. Instead of for each boid, searching through all
                // boids for those within a certain radius, this limits those by the hash-to-bucket simplification.
                var parallelHashMap = hashMap.AsParallelWriter();
                var hashPositionsJobHandle = Entities
                                             .WithName("HashPositionsJob")
                                             .WithAll<Boid>()
                                             .ForEach((int entityInQueryIndex, in LocalToWorld localToWorld) =>
                                                      {
                                                          var hash = (int)math.hash(new int3(math.floor(localToWorld.Position / settings.CellRadius)));
                                                          parallelHashMap.Add(hash, entityInQueryIndex);
                                                      })
                                             .ScheduleParallel(Dependency);

                var initialCellCountJob       = new MemsetNativeArray<int> { Source = cellCount, Value = 1 };
                var initialCellCountJobHandle = initialCellCountJob.Schedule(boidCount, 64, Dependency);

                var initialCellBarrierJobHandle = JobHandle.CombineDependencies(initialCellAlignmentJobHandle, initialCellSeparationJobHandle, initialCellCountJobHandle);
                var mergeCellsBarrierJobHandle  = JobHandle.CombineDependencies(hashPositionsJobHandle, initialCellBarrierJobHandle, copyObstaclePositionsJobHandle);

                var mergeCellsJob = new MergeCells {
                                                       cellIndices               = cellIndices,
                                                       cellAlignment             = cellAlignment,
                                                       cellSeparation            = cellSeparation,
                                                       cellCount                 = cellCount,
                                                       cellObstacleDistance      = cellObstacleDistance,
                                                       cellObstaclePositionIndex = cellObstaclePositionIndex,
                                                       obstaclePositions         = obstaclePositions
                                                   };

                var mergeCellsJobHandle = mergeCellsJob.Schedule(hashMap, 64, mergeCellsBarrierJobHandle);

                // This reads the previously calculated boid information for all the boids of each cell to update
                // the `localToWorld` of each of the boids based on their newly calculated headings using
                // the standard boid flocking algorithm.
                var deltaTime = math.min(0.05f, Time.DeltaTime);
                var steerJobHandle = Entities
                                     .WithName("Steer")
                                     .WithSharedComponentFilter(settings) // implies .WithAll<Boid>()
                                     .WithReadOnly(cellIndices)
                                     .WithReadOnly(cellCount)
                                     .WithReadOnly(cellAlignment)
                                     .WithReadOnly(cellSeparation)
                                     .WithReadOnly(cellBuffer)
                                     .WithReadOnly(cellDataComponentAllocator)
                                     .WithReadOnly(cellObstacleDistance)
                                     .WithReadOnly(cellObstaclePositionIndex)
                                     .WithReadOnly(obstaclePositions)
                                     .ForEach((int entityInQueryIndex, Entity entity, ref LocalToWorld localToWorld) =>
                                              {
                                                  // temporarily storing the values for code readability
                                                  var forward                      = localToWorld.Forward;
                                                  var currentPosition              = new float3(localToWorld.Position.x, 0, localToWorld.Position.z);
                                                  var cellIndex                    = cellIndices[entityInQueryIndex];
                                                  var neighborCount                = cellCount[cellIndex];
                                                  var alignment                    = cellAlignment[cellIndex];
                                                  var separation                   = cellSeparation[cellIndex];
                                                  var nearestObstacleDistance      = cellObstacleDistance[cellIndex];
                                                  var nearestObstaclePositionIndex = cellObstaclePositionIndex[cellIndex];
                                                  var nearestObstaclePosition      = obstaclePositions[nearestObstaclePositionIndex];
                                                  var cells                        = cellBuffer;

                                                  // Setting up the directions for the three main biocrowds influencing directions adjusted based
                                                  // on the predefined weights:
                                                  // 1) alignment - how much should it move in a direction similar to those around it?
                                                  // note: we use `alignment/neighborCount`, because we need the average alignment in this case; however
                                                  // alignment is currently the summation of all those of the boids within the cellIndex being considered.
                                                  var alignmentResult = settings.AlignmentWeight
                                                                        * math.normalizesafe((alignment / neighborCount) - forward);

                                                  // 2) separation - how close is it to other boids and are there too many or too few for comfort?
                                                  // note: here separation represents the summed possible center of the cell. We perform the multiplication
                                                  // so that both `currentPosition` and `separation` are weighted to represent the cell as a whole and not
                                                  // the current individual boid.
                                                  var separationResult = settings.SeparationWeight
                                                                         * math.normalizesafe((currentPosition * neighborCount) - separation);

                                                  var percentX = currentPosition.x / (flowFieldData.gridSize.x * flowFieldData.cellRadius * 2);
                                                  var percentY = currentPosition.z / (flowFieldData.gridSize.y * flowFieldData.cellRadius * 2);

                                                  percentX = math.clamp(percentX, 0f, 1f);
                                                  percentY = math.clamp(percentY, 0f, 1f);

                                                  var cellGrid         = new int2 { x = math.clamp((int)math.floor((flowFieldData.gridSize.x) * percentX), 0, flowFieldData.gridSize.x - 1), y = math.clamp((int)math.floor((flowFieldData.gridSize.y) * percentY), 0, flowFieldData.gridSize.y - 1) };
                                                  var cellCurrentIndex = flowFieldData.gridSize.y * cellGrid.x + cellGrid.y;
                                                  var cell             = cellDataComponentAllocator[cells[cellCurrentIndex]];
                                                  var direction        = cell.bestDirection;

                                                  if ( cell.cost == byte.MaxValue )
                                                  {
                                                      alignmentResult  = 0;
                                                      separationResult = 0;
                                                  }

                                                  var flowHeading = settings.TargetWeight * math.normalizesafe(new float3(direction.x, 0, direction.y));

                                                  // creating the obstacle avoidant vector s.t. it's pointing towards the nearest obstacle
                                                  // but at the specified 'ObstacleAversionDistance'. If this distance is greater than the
                                                  // current distance to the obstacle, the direction becomes inverted. This simulates the
                                                  // idea that if `currentPosition` is too close to an obstacle, the weight of this pushes
                                                  // the current boid to escape in the fastest direction; however, if the obstacle isn't
                                                  // too close, the weighting denotes that the boid doesnt need to escape but will move
                                                  // slower if still moving in that direction (note: we end up not using this move-slower
                                                  // case, because of `targetForward`'s decision to not use obstacle avoidance if an obstacle
                                                  // isn't close enough).
                                                  // var obstacleSteering = currentPosition - nearestObstaclePosition;
                                                  // var avoidObstacleHeading = (nearestObstaclePosition + math.normalizesafe(obstacleSteering + flowHeading)
                                                  //                             * settings.ObstacleAversionDistance) - currentPosition;

                                                  // the updated heading direction. If not needing to be avoidant (ie obstacle is not within
                                                  // predefined radius) then go with the usual defined heading that uses the amalgamation of
                                                  // the weighted alignment, separation, and target direction vectors.
                                                  // var nearestObstacleDistanceFromRadius = nearestObstacleDistance - settings.ObstacleAversionDistance;

                                                  // if ( nearestObstacleDistanceFromRadius < 0 )
                                                  // {
                                                  //     avoidObstacleHeading = math.normalizesafe(flowHeading);
                                                  // }

                                                  var normalHeading = math.normalizesafe(alignmentResult + separationResult + flowHeading);
                                                  var targetForward = normalHeading; // math.select(normalHeading, avoidObstacleHeading, nearestObstacleDistanceFromRadius < 0);

                                                  // updates using the newly calculated heading direction
                                                  var nextHeading = math.normalizesafe(forward + deltaTime * (targetForward - forward));

                                                  // var targetPos       = new float3(localToWorld.Position + (nextHeading * settings.MoveSpeed * deltaTime));
                                                  // var targetRot       = quaternion.LookRotationSafe(targetForward, math.up());
                                                  // var targetTransform = new RigidTransform(targetRot, targetPos);
                                                  // var rot             = math.rotate(targetRot, pv.Angular);

                                                  // pv = PhysicsVelocity.CalculateVelocityToTarget(pm, translation, rotation, targetTransform, 1/deltaTime);

                                                  // var angularVelocity = math.rotate(quaternion.LookRotationSafe(targetForward, math.up()), math.up()) * deltaTime;
                                                  // pv.Linear += targetForward * settings.MoveSpeed * deltaTime;

                                                  // pv.Linear      = nextHeading * settings.MoveSpeed;  // math.lerp(pv.Linear, nextHeading * settings.MoveSpeed, deltaTime * settings.MoveSpeed)  ;
                                                  // rotation.Value = math.slerp(rotation.Value, quaternion.LookRotationSafe(targetForward, math.up()), deltaTime * 10) ;

                                                  // pv.ApplyImpulse(pm, translation, rotation, nextHeading, translation.Value);
                                                  // pm.InverseInertia.
                                                  // pv.Linear  += nextHeading * settings.MoveSpeed * deltaTime;
                                                  // pv.Angular =  -targetForward * settings.MoveSpeed * math.up();
                                                  // pv.SetAngularVelocityWorldSpace(pm, rotation, targetForward);
                                                  // pv.Angular = targetForward;

                                                  // pv.ApplyLinearImpulse(pm, normalHeading);
                                                  // pv.ApplyLinearImpulse(pm, flowHeading);

                                                  var finalPos = math.lerp(currentPosition, new float3(currentPosition + (nextHeading * settings.MoveSpeed * deltaTime)), settings.MoveSpeed * deltaTime);
                                                  var finalRot = math.slerp(localToWorld.Rotation, quaternion.LookRotationSafe(targetForward, math.up()), 10 * deltaTime);

                                                  localToWorld = new LocalToWorld {
                                                                                      Value = float4x4.TRS(finalPos,
                                                                                                           finalRot,
                                                                                                           new float3(1.0f, 1.0f, 1.0f))
                                                                                  };
                                              }).ScheduleParallel(mergeCellsJobHandle);

                // Dispose allocated containers with dispose jobs.
                Dependency = steerJobHandle;

                // We pass the job handle and add the dependency so that we keep the proper ordering between the jobs
                // as the looping iterates. For our purposes of execution, this ordering isn't necessary; however, without
                // the add dependency call here, the safety system will throw an error, because we're accessing multiple
                // pieces of boid data and it would think there could possibly be a race condition.

                _boidQuery.AddDependency(Dependency);
                _boidQuery.ResetFilter();

                _mainTargetsQuery.AddDependency(Dependency);
                _mainTargetsQuery.ResetFilter();
            }

            _uniqueTypes.Clear();
        }

        protected override void OnCreate()
        {
            _boidQuery        = GetEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<Boid>(), ComponentType.ReadWrite<LocalToWorld>() }, });
            _mainTargetsQuery = GetEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<MainTargetComponent>(), ComponentType.ReadOnly<CompleteFlowFieldTag>() }, });
            _obstacleQuery    = GetEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<ObstacleTag>(), ComponentType.ReadOnly<CellData>() }, });

            RequireForUpdate(_boidQuery);
            RequireForUpdate(_mainTargetsQuery);
            RequireForUpdate(_obstacleQuery);
        }

        [BurstCompile]
        struct MergeCells : IJobNativeMultiHashMapMergedSharedKeyIndices
        {
            public NativeArray<int> cellIndices;

            public NativeArray<float3> cellAlignment;

            public NativeArray<float3> cellSeparation;

            public NativeArray<int> cellObstaclePositionIndex;

            public NativeArray<float> cellObstacleDistance;

            public NativeArray<int> cellCount;

            [ReadOnly] public NativeArray<float3> obstaclePositions;

            public void ExecuteFirst(int index)
            {
                // var position = cellSeparation[index] / cellCount[index];

                // NearestPosition(obstaclePositions, position, out var obstaclePositionIndex, out var obstacleDistance);
                // cellObstaclePositionIndex[index] = obstaclePositionIndex;
                // cellObstacleDistance[index]      = obstacleDistance;

                cellIndices[index] = index;
            }

            // Sums the alignment and separation of the actual index being considered and stores
            // the index of this first value where we're storing the cells.
            // note: these items are summed so that in `Steer` their average for the cell can be resolved.
            public void ExecuteNext(int cellIndex, int index)
            {
                cellCount[cellIndex]      += 1;
                cellAlignment[cellIndex]  += cellAlignment[index];
                cellSeparation[cellIndex] += cellSeparation[index];
                cellIndices[index]        =  cellIndex;
            }

            void NearestPosition(NativeArray<float3> targets, float3 position, out int nearestPositionIndex, out float nearestDistance)
            {
                nearestPositionIndex = 0;
                nearestDistance      = math.lengthsq(position - targets[0]);
                for ( int i = 1; i < targets.Length; i++ )
                {
                    var targetPosition = targets[i];
                    var distance       = math.lengthsq(position - targetPosition);
                    var nearest        = distance < nearestDistance;

                    nearestDistance      = math.select(nearestDistance, distance, nearest);
                    nearestPositionIndex = math.select(nearestPositionIndex, i, nearest);
                }

                nearestDistance = math.sqrt(nearestDistance);
            }
        }
    }
}