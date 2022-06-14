using Unity.Entities;
using Unity.Mathematics;

namespace Sandbox.ECS.FlowField
{
    public struct FlowFieldData : IComponentData
    {
        public int2 gridSize;

        public float cellRadius;
        
        public int2 TargetGridIndex;
    }
}