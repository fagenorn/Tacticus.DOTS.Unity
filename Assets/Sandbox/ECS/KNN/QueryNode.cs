using Unity.Mathematics;

namespace Sandbox.ECS.KNN
{
    public struct QueryNode
    {
        public int NodeIndex;

        public float3 TempClosestPoint;

        public float Distance;
    }
}