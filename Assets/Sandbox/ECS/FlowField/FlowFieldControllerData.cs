using Unity.Entities;
using Unity.Mathematics;

namespace Sandbox.ECS.FlowField
{
    [GenerateAuthoringComponent]
    public struct FlowFieldControllerData : IComponentData
    {
        public int2 gridSize;

        public float cellRadius;
    }
}