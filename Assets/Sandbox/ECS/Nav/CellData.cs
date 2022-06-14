// using Unity.Entities;
// using Unity.Mathematics;
//
// namespace Sandbox.ECS.Nav
// {
//     public struct CellData : IComponentData
//     {
//         public float3 worldPos;
//
//         public int2 gridIndex;
//
//         public byte cost;
//
//         public ushort bestCost;
//     }
//
//     public struct CellsBestDirection : IComponentData
//     {
//         public int2 bestDirection;
//     }
//
//     public struct CellBestDirecitonBuff : IBufferElementData
//     {
//         public int2 bestDirection;
//
//         public static implicit operator int2(CellBestDirecitonBuff cellBestDirecitonBuff) { return cellBestDirecitonBuff.bestDirection; }
//
//         public static implicit operator CellBestDirecitonBuff(int2 e) { return new CellBestDirecitonBuff { bestDirection = e }; }
//     }
//
//     // I can have several layers on a single Flow Field each layer has a single BestDireciton which is a bufferelementdata so that I can have multiple.
//     public struct CellBDLayer : IComponentData
//     {
//         public int intVal;
//     }
//
//     public struct CellBDLayerToCalc : IComponentData
//     {
//         public int intVal;
//     }
//
//     public struct CellDestinationBuffer : IBufferElementData
//     {
//         public int2 Destination;
//
//         public static implicit operator int2(CellDestinationBuffer cellDestinationBuff) { return cellDestinationBuff.Destination; }
//
//         public static implicit operator CellDestinationBuffer(int2 e) { return new CellDestinationBuffer { Destination = e }; }
//     }
//
//     public struct DestinationChangedTag : IComponentData { }
//
//     // TODO: Check if this is really needed, might be waste of time and not that performant at all.
//     public struct CellDestinationChangedBuff : IBufferElementData
//     {
//         public bool HasItChanged;
//
//         public static implicit operator bool(CellDestinationChangedBuff cellDestinationChangedBuff) { return cellDestinationChangedBuff.HasItChanged; }
//
//         public static implicit operator CellDestinationChangedBuff(bool e) { return new CellDestinationChangedBuff { HasItChanged = e }; }
//     }
//
//     public struct AddLayerSystemEvent : IComponentData { }
//
//     public struct RemoveLayerSystemEvent : IComponentData { }
//
//     public struct AddLayerTempDestinationsValueBuff : IBufferElementData { }
//
//     public struct AddLayerTempBestDirectValsBuff : IBufferElementData { }
// }