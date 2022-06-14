using Unity.Entities;

namespace Sandbox.ECS.CastleWars
{
    [GenerateAuthoringComponent]
    public struct UnitAttackDetailsComponent : IComponentData
    {
        public float Range;

        public float RateOfFire;
        
        public float NextFireTime;
    }
}