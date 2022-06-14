using System;

using Sandbox.ECS.Boids;

using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

using UnityEngine;

namespace Sandbox.ECS.CastleWars
{
    public struct WaypointDistanceToTargetComponent : IComponentData
    {
        public float Distance;

        public int2 GridIndex;
    }

    public struct WaypointComponent : ISharedComponentData
    {
        public TeamEnum TargetFor;
    }

    public class WaypointAuthoring : MonoBehaviour
    {
        public TeamEnum TargetFor;

        private void OnDrawGizmos()
        {
            Gizmos.DrawSphere(transform.position, 1);
        }
    }

    [UpdateInGroup(typeof(GameObjectConversionGroup))]
    [ConverterVersion("macton", 5)]
    public class TargetConversion : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((WaypointAuthoring targetAuthoring) =>
                             {
                                 var entity = GetPrimaryEntity(targetAuthoring);

                                 DstEntityManager.AddSharedComponentData(entity, new WaypointComponent { TargetFor = targetAuthoring.TargetFor, });

                                 // Remove default transform system components
                                 DstEntityManager.RemoveComponent<Translation>(entity);
                                 DstEntityManager.RemoveComponent<Rotation>(entity);
                             });
        }
    }
}