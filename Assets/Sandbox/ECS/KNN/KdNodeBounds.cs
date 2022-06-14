using Unity.Mathematics;

namespace Sandbox.ECS.KNN
{
    public struct KdNodeBounds
    {
        public float3 Min;

        public float3 Max;

        public float3 Size => Max - Min;

        public float3 ClosestPoint(float3 point) { return math.clamp(point, Min, Max); }
    }
}