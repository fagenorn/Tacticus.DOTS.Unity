using Unity.Entities;
using Unity.Mathematics;

namespace Sandbox.ECS.FlowField
{
    public struct CellData : IComponentData
    {
        public float3 worldPos;

        public int2 gridIndex;

        public byte cost;

        public ushort bestCost;

        public int2 bestDirection;

        public int2 targetIndex;
    }
}