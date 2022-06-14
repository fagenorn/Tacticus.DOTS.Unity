using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

using UnityEngine;

namespace Sandbox.ECS.CastleWars
{
    public struct MainTargetComponent : ISharedComponentData
    {
        public TeamEnum TargetFor;
    }

    public class MainTargetAuthoring : MonoBehaviour
    {
        public TeamEnum TargetFor;
    }

    [UpdateInGroup(typeof(GameObjectConversionGroup))]
    [ConverterVersion("macton", 5)]
    public class MainTargetConversion : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((MainTargetAuthoring targetAuthoring) =>
                             {
                                 var entity = GetPrimaryEntity(targetAuthoring);

                                 DstEntityManager.AddSharedComponentData(entity, new MainTargetComponent { TargetFor = targetAuthoring.TargetFor, });

                                 // Remove default transform system components
                                 DstEntityManager.RemoveComponent<Translation>(entity);
                                 DstEntityManager.RemoveComponent<Rotation>(entity);
                             });
        }
    }
}