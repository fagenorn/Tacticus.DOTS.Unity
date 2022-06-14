using Sandbox.ECS.CastleWars;

using Unity.Entities;
using Unity.Mathematics;

namespace Sandbox.ECS.FlowField
{
    public struct NewFlowFieldData : IComponentData
    {
        public bool isExistingFlowField;
    }
}