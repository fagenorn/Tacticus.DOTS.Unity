using Sandbox.ECS.Physics;

using Unity.Entities;
using Unity.Transforms;

namespace Sandbox.ECS.Boids
{
    [UpdateInGroup(typeof(GameObjectConversionGroup))]
    [ConverterVersion("macton", 5)]
    public class BoidConversion : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((BoidAuthoring boidAuthoring) =>
                             {
                                 var entity = GetPrimaryEntity(boidAuthoring);

                                 DstEntityManager.AddSharedComponentData(entity, new Boid {
                                                                                              CellRadius               = boidAuthoring.CellRadius,
                                                                                              SeparationWeight         = boidAuthoring.SeparationWeight,
                                                                                              AlignmentWeight          = boidAuthoring.AlignmentWeight,
                                                                                              TargetWeight             = boidAuthoring.TargetWeight,
                                                                                              ObstacleAversionDistance = boidAuthoring.ObstacleAversionDistance,
                                                                                              MoveSpeed                = boidAuthoring.MoveSpeed,
                                                                                              Team                     = boidAuthoring.Team,
                                                                                          });

                                 // Remove default transform system components
                                 DstEntityManager.RemoveComponent<Translation>(entity);
                                 DstEntityManager.RemoveComponent<Rotation>(entity);
                             });
        }
    }
}