using Unity.Entities;

namespace Sandbox.ECS.FlowField
{
    public struct EntityMovementData : IComponentData
    {
        public float moveSpeed;

        public float destinationMoveSpeed;

        public bool destinationReached;
    }
}