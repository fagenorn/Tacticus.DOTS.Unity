using Unity.Entities;

namespace Sandbox.ECS.FlowField
{
    [InternalBufferCapacity(250)]
    public struct EntityBufferElement : IBufferElementData
    {
        public Entity entity;

        public static implicit operator Entity(EntityBufferElement entityBufferElement) { return entityBufferElement.entity; }

        public static implicit operator EntityBufferElement(Entity e) { return new EntityBufferElement { entity = e }; }
    }
}