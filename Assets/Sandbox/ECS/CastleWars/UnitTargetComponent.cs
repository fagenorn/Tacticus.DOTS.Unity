using Unity.Entities;

namespace Sandbox.ECS.CastleWars
{
    public struct UnitTargetComponent : IComponentData
    {
        public Entity Target;
    }
}