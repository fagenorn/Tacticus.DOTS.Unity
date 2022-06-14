using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Sandbox.ECS.CastleWars
{
    public partial class UnitSpawnerSystem : SystemBase
    {
        private EntityCommandBufferSystem _ecbSystem;

        protected override void OnCreate() { _ecbSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>(); }

        protected override void OnUpdate()
        {
            var cb   = _ecbSystem.CreateCommandBuffer().AsParallelWriter();
            var time = Time.ElapsedTime;

            Entities
                .WithoutBurst()
                .ForEach((int entityInQueryIndex, ref UnitSpawnerComponent unitSpawnerComponent, in Translation translation) =>
                         {
                             var canSpawn = false;
                             if ( time >= unitSpawnerComponent.NextTime )
                             {
                                 canSpawn                      =  true;
                                 unitSpawnerComponent.NextTime += 1 / unitSpawnerComponent.SpawnRate;
                             }

                             if ( !canSpawn )
                             {
                                 return;
                             }

                             var unitEntity    = cb.Instantiate(entityInQueryIndex, unitSpawnerComponent.PrefabUnit);
                             var spawnPosition = new LocalToWorld { Value = float4x4.TRS(new float3(translation.Value.x, 1, translation.Value.z), quaternion.LookRotationSafe(math.forward(), math.up()), new float3(1.0f, 1.0f, 1.0f)) };

                             cb.SetComponent(entityInQueryIndex, unitEntity, spawnPosition);
                         })
                .ScheduleParallel();

            _ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }
}