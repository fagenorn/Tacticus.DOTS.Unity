using Unity.Entities;

using UnityEngine;

namespace Sandbox.ECS.CastleWars
{
    [GenerateAuthoringComponent]
    public struct UnitSpawnerComponent : IComponentData
    {
        public Entity PrefabUnit;

        public float SpawnRate;

        public float NextTime;
    }
}