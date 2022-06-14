using Sandbox.ECS.CastleWars;

using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace Sandbox.ECS.FlowField
{
    [UpdateAfter(typeof(LocateTargetSystem))]
    public partial class ShootTargetSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var vfxSystem          = World.GetOrCreateSystem<VFXManagerSystem>();
            var localToWorldHandle = GetComponentDataFromEntity<LocalToWorld>();
            var time               = Time.ElapsedTime;
            var requestsParallel   = vfxSystem.BatchVFXRequests.AsParallelWriter();

            Entities
                .WithNone<VFXPlayRequest>()
                .WithReadOnly(localToWorldHandle)
                .ForEach((Entity entity, ref UnitAttackDetailsComponent attckDetails, in UnitTargetComponent target, in LocalToWorld localToWorld) =>
                         {
                             var canShoot = false;
                             if ( time >= attckDetails.NextFireTime )
                             {
                                 canShoot                  =  true;
                                 attckDetails.NextFireTime += 1 / attckDetails.RateOfFire;
                             }

                             if ( !canShoot )
                             {
                                 return;
                             }

                             var targetLocalWorld = localToWorldHandle[target.Target];

                             var vfxRequest = new BatchVFXRequest {
                                                                      Distance  = 10,
                                                                      Intensity = 10,
                                                                      Position  = localToWorld.Position,
                                                                      Rotation  = localToWorld.Rotation,
                                                                      VFXId     = 0,
                                                                      Target    = targetLocalWorld.Position
                                                                  };

                             requestsParallel.Enqueue(vfxRequest);
                         }).ScheduleParallel();

            vfxSystem.AddDependencyToComplete(Dependency);
        }
    }
}