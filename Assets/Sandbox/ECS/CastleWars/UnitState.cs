using Unity.Entities;

namespace Sandbox.ECS.CastleWars
{
    public enum UnitStateEnum
    {
        Moving = 0,

        Attacking = 1
    }

    public struct UnitState : IComponentData
    {
        public UnitStateEnum State;
    }
}