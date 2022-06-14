// using Unity.Entities;
// using Unity.Mathematics;
//
// namespace Sandbox.ECS.Nav
// {
//     public struct FlowFieldData : IComponentData
//     {
//         public int2 gridSize;
//
//         public float cellRadius;
//
//         public float3 destination;
//
//         public int2 desttinationCell;
//
//         public CurrentFlowField currentFlowField;
//
//         public int maxFlowLayers;
//     }
//
//     public struct FlowFieldMemberOf : IComponentData
//     {
//         public CurrentFlowField flowField;
//     }
//
//     public enum CurrentFlowField
//     {
//         FlowField1,
//
//         FlowField2,
//
//         FlowFIled3,
//
//         FlowField4,
//
//         FlowField5,
//
//         FLowField6,
//
//         FlowField7,
//
//         FlowField8,
//
//         FlowField9,
//
//         FlowField10
//     }
//
//     public struct CellDataBuffer : IBufferElementData
//     {
//         public CellData celldata;
//
//         public static implicit operator CellData(CellDataBuffer cellBufferElement) { return cellBufferElement.celldata; }
//
//         public static implicit operator CellDataBuffer(CellData e) { return new CellDataBuffer { celldata = e }; }
//     }
//
//     public struct FlowfieldVertPointsBuff : IBufferElementData
//     {
//         public float3 Float3points;
//
//         public static implicit operator float3(FlowfieldVertPointsBuff flowvertelem) { return flowvertelem.Float3points; }
//
//         public static implicit operator FlowfieldVertPointsBuff(float3 e) { return new FlowfieldVertPointsBuff { Float3points = e }; }
//     }
//
//     public struct NewFlowFieldEvent : IComponentData { }
//
//     public struct DestinationCellData : IComponentData
//     {
//         public int2 int2Value;
//     }
//
//     public struct CalculateCellCostEventTag : IComponentData { }
// }