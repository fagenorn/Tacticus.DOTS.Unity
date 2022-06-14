using System.Collections.Generic;

using Sandbox.ECS.Boids;
using Sandbox.ECS.FlowField;
using Sandbox.ECS.KNN;

using Unity.Assertions;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;

using UnityEngine;
using UnityEngine.Profiling;

namespace Sandbox.ECS.CastleWars
{
    [UpdateBefore(typeof(ShootTargetSystem))]
    public partial class LocateTargetSystem : SystemBase
    {
        private readonly List<Boid> _uniqueTypes = new List<Boid>(3);

        private EntityQuery _boidWithoutTargetQuery;

        private EntityQuery _boidWithTargetsQuery;

        private EntityQuery _boidQuery;

        private EntityCommandBufferSystem _ecbSystemBegin;

        private double LastRun;

        protected override void OnCreate()
        {
            _ecbSystemBegin         = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            _boidWithoutTargetQuery = GetEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<Boid>(), ComponentType.ReadOnly<LocalToWorld>(), ComponentType.ReadOnly<UnitAttackDetailsComponent>(), }, None = new[] { ComponentType.ReadOnly<UnitTargetComponent>() } });
            _boidWithTargetsQuery   = GetEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<Boid>(), ComponentType.ReadOnly<LocalToWorld>(), ComponentType.ReadOnly<UnitAttackDetailsComponent>(), ComponentType.ReadOnly<UnitTargetComponent>() } });
            _boidQuery              = GetEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<Boid>(), ComponentType.ReadOnly<LocalToWorld>(), ComponentType.ReadOnly<UnitAttackDetailsComponent>(), } });
        }

        protected override void OnUpdate()
        {
            var time = Time.ElapsedTime;
            if ( time - LastRun < .2 )
            {
                return;
            }

            LastRun = time;

            var cb = _ecbSystemBegin.CreateCommandBuffer().AsParallelWriter();

            _uniqueTypes.Clear();
            EntityManager.GetAllUniqueSharedComponentData(_uniqueTypes);

            var boidWithTargetCount = _boidWithTargetsQuery.CalculateEntityCount();

            var world        = World.Unmanaged;
            var mainEntities = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(boidWithTargetCount, ref world.UpdateAllocator);

            var jobHandle = Entities
                            .WithAll<UnitTargetComponent>()
                            .WithStoreEntityQueryInField(ref _boidWithTargetsQuery)
                            .ForEach((int entityInQueryIndex, Entity entity) => { mainEntities[entityInQueryIndex] = entity; })
                            .ScheduleParallel(Dependency);

            var checkTargetIsValidJob = new CheckTargetIsValidJob {
                                                                      Ecb                               = cb,
                                                                      Entities                          = mainEntities,
                                                                      LocalToWorldFromEntity            = GetComponentDataFromEntity<LocalToWorld>(),
                                                                      UnitAttackDetailsFromEntity       = GetComponentDataFromEntity<UnitAttackDetailsComponent>(),
                                                                      UnitTargetComponentDataFromEntity = GetComponentDataFromEntity<UnitTargetComponent>(),
                                                                  };

            Dependency = checkTargetIsValidJob.ScheduleBatch(boidWithTargetCount, boidWithTargetCount / 32, jobHandle);

            Assert.AreEqual(_uniqueTypes.Count, 3);

            var team1Settings = _uniqueTypes[1];
            var team2Settings = _uniqueTypes[2];

            InitializeCollections(team1Settings, team2Settings, cb);
            InitializeCollections(team2Settings, team1Settings, cb);
        }

        private void InitializeCollections(Boid mainSettings, Boid targetSettings, EntityCommandBuffer.ParallelWriter ecb)
        {
            _boidWithoutTargetQuery.SetSharedComponentFilter(mainSettings);
            _boidQuery.SetSharedComponentFilter(targetSettings);

            var mainCount   = _boidWithoutTargetQuery.CalculateEntityCount();
            var targetCount = _boidQuery.CalculateEntityCount();

            var world           = World.Unmanaged;
            var mainEntities    = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(mainCount, ref world.UpdateAllocator);
            var targetEntities  = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(targetCount, ref world.UpdateAllocator);
            var mainPositions   = CollectionHelper.CreateNativeArray<float3, RewindableAllocator>(mainCount, ref world.UpdateAllocator);
            var targetPositions = CollectionHelper.CreateNativeArray<float3, RewindableAllocator>(targetCount, ref world.UpdateAllocator);
            var rangeResults    = CollectionHelper.CreateNativeArray<RangeQueryResult, RewindableAllocator>(mainCount, ref world.UpdateAllocator);

            var jobHandle = Entities
                            .WithSharedComponentFilter(mainSettings)
                            .WithNone<UnitTargetComponent>()
                            .ForEach((int entityInQueryIndex, Entity entity, in LocalToWorld localToWorld) =>
                                     {
                                         mainEntities[entityInQueryIndex]  = entity;
                                         mainPositions[entityInQueryIndex] = localToWorld.Position;
                                     })
                            .ScheduleParallel(Dependency);

            var jobHandle2 = Entities
                             .WithSharedComponentFilter(targetSettings)
                             .ForEach((int entityInQueryIndex, Entity entity, in LocalToWorld localToWorld) =>
                                      {
                                          targetEntities[entityInQueryIndex]  = entity;
                                          targetPositions[entityInQueryIndex] = localToWorld.Position;
                                      })
                             .ScheduleParallel(Dependency);

            var jobHandle6 = JobHandle.CombineDependencies(jobHandle, jobHandle2);

            for ( var i = 0; i < rangeResults.Length; ++i )
            {
                rangeResults[i] = new RangeQueryResult(1, Allocator.TempJob);
            }

            var knnContainer = new KnnContainer(targetPositions, false, Allocator.TempJob);
            var rebuildJob   = new KnnRebuildJob(knnContainer);
            var batchRange   = new QueryRangeBatchJob(knnContainer, mainPositions, 25f, rangeResults);

            var jobHandle7 = rebuildJob.Schedule(jobHandle6);
            var jobHandle8 = batchRange.ScheduleBatch(mainCount, mainCount / 32, jobHandle7);

            var jobHandle9 = knnContainer.Dispose(jobHandle8);

            var jobHandle10 = new CheckRangeQueryJob { Ecb = ecb, MainEntities = mainEntities, TargetEntities = targetEntities, RangeQueryResults = rangeResults }
                .ScheduleBatch(mainCount, mainCount / 32, jobHandle8);

            var jobHandle11 = JobHandle.CombineDependencies(jobHandle9, jobHandle10);

            Dependency = jobHandle11;

            _boidWithoutTargetQuery.AddDependency(Dependency);
            _boidQuery.AddDependency(Dependency);

            _boidWithoutTargetQuery.ResetFilter();
            _boidQuery.ResetFilter();
        }

        [BurstCompile]
        public struct CheckRangeQueryJob : IJobParallelForBatch
        {
            [ReadOnly] public NativeArray<RangeQueryResult> RangeQueryResults;

            [ReadOnly] public NativeArray<Entity> MainEntities;

            [ReadOnly] public NativeArray<Entity> TargetEntities;

            public EntityCommandBuffer.ParallelWriter Ecb;

            public void Execute(int startIndex, int count)
            {
                for ( int index = startIndex; index < startIndex + count; ++index )
                {
                    var rangeResult = RangeQueryResults[index];

                    if ( rangeResult.Length == 0 )
                    {
                        rangeResult.Dispose();

                        continue;
                    }

                    var targetEntityIndex = rangeResult[0];
                    var mainEntity        = MainEntities[index];
                    var targetEntity      = TargetEntities[targetEntityIndex];
                    var target            = new UnitTargetComponent { Target = targetEntity };
                    Ecb.AddComponent(startIndex, mainEntity, target);

                    rangeResult.Dispose();
                }
            }
        }

        [BurstCompile]
        public struct CheckTargetIsValidJob : IJobParallelForBatch
        {
            [ReadOnly] public NativeArray<Entity> Entities;

            [ReadOnly] public ComponentDataFromEntity<LocalToWorld> LocalToWorldFromEntity;

            [ReadOnly] public ComponentDataFromEntity<UnitAttackDetailsComponent> UnitAttackDetailsFromEntity;

            [ReadOnly] public ComponentDataFromEntity<UnitTargetComponent> UnitTargetComponentDataFromEntity;

            public EntityCommandBuffer.ParallelWriter Ecb;

            public void Execute(int startIndex, int count)
            {
                for ( int index = startIndex; index < startIndex + count; ++index )
                {
                    var entity         = Entities[index];
                    var position       = LocalToWorldFromEntity[entity].Position;
                    var range          = UnitAttackDetailsFromEntity[entity].Range;
                    var targetEntity   = UnitTargetComponentDataFromEntity[entity].Target;
                    var targetPosition = LocalToWorldFromEntity[targetEntity].Position;
                    var distance       = math.distance(position, targetPosition);

                    if ( distance > range )
                    {
                        Ecb.RemoveComponent<UnitTargetComponent>(startIndex, entity);
                    }
                }
            }
        }

        [BurstCompile]
        public struct FindTargetsInRangeJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Entity> Boids;

            [ReadOnly] public NativeArray<float3> TargetPositions;

            [ReadOnly] public ComponentDataFromEntity<UnitAttackDetailsComponent> UnitAttackDetailsFromEntity;

            [ReadOnly] public ComponentDataFromEntity<LocalToWorld> LocalToWorldFromEntity;

            public NativeArray<int> TargetPositionIndex;

            public void Execute(int index)
            {
                var boid     = Boids[index];
                var position = LocalToWorldFromEntity[boid].Position;
                var range    = UnitAttackDetailsFromEntity[boid].Range;

                NearestPosition(TargetPositions, position, out var targetIndex, out var targetDistance);
                TargetPositionIndex[index] = targetDistance > range ? -1 : targetIndex;
            }

            private void NearestPosition(NativeArray<float3> targets, float3 position, out int nearestPositionIndex, out float nearestDistance)
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