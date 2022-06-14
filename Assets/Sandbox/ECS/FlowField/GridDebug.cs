using System.Collections.Generic;

using Sandbox.ECS.Boids;
using Sandbox.Helpers.Debug;

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

using UnityEditor;

using UnityEngine;

namespace Sandbox.ECS.FlowField
{
    public enum FlowFieldDisplayType
    {
        None,

        AllIcons,

        DestinationIcon,

        CostField,

        IntegrationField,

        CostHeatMap
    };

#if UNITY_EDITOR
    // [UpdateBefore(typeof(BoidSystem))]
    // [UpdateAfter(typeof(CalculateFlowFieldSystem))]
    // [DisableAutoCreation]
    public partial class GridDebugSystem : SystemBase
    {
        private EntityCommandBufferSystem _ecbSystem;

        private NativeArray<CellData> _gridCellData;

        private EntityQuery _addDebugQuery;

        private EntityQuery _removeDebugQuery;

        private EntityQuery _flowFieldQuery;

        protected override void OnCreate()
        {
            _ecbSystem        = World.GetOrCreateSystem<EntityCommandBufferSystem>();
            _gridCellData     = new NativeArray<CellData>(0, Allocator.Persistent);
            _addDebugQuery    = GetEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<CellData>(), ComponentType.ReadOnly<AddToDebugTag>() }, });
            _removeDebugQuery = GetEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<CellData>(), typeof(CompleteFlowFieldTag), }, None = new ComponentType[] { typeof(AddToDebugTag) } });
            _flowFieldQuery   = GetEntityQuery(typeof(FlowFieldData), typeof(CompleteFlowFieldTag));
        }

        protected override void OnStartRunning() { GizmoManager.OnDrawGizmos(DrawGizmos); }

        protected override void OnUpdate()
        {
            var ecb = _ecbSystem.CreateCommandBuffer();

            // ecb.RemoveComponentForEntityQuery<AddToDebugTag>(_addDebugQuery);

            if ( _flowFieldQuery.IsEmpty )
            {
                return;
            }

            Entities
                .WithoutBurst()
                .WithAll<CompleteFlowFieldTag>()
                .WithNone<AddToDebugTag>()
                .WithStructuralChanges()
                .ForEach((Entity entity, ref DynamicBuffer<EntityBufferElement> buffer, in FlowFieldData flowFieldData) =>
                         {
                             var gridEntities = buffer.Reinterpret<Entity>();

                             if ( _gridCellData.IsCreated )
                             {
                                 _gridCellData.Dispose();
                             }

                             _gridCellData = new NativeArray<CellData>(gridEntities.Length, Allocator.Persistent);

                             for ( var i = 0; i < gridEntities.Length; i++ )
                             {
                                 _gridCellData[i] = GetComponent<CellData>(gridEntities[i]);
                             }

                             ecb.AddComponent<AddToDebugTag>(entity);
                         })
                .Run();
        }

        protected override void OnDestroy()
        {
            if ( _gridCellData.IsCreated )
            {
                _gridCellData.Dispose();
            }
        }

        private void DrawGizmos()
        {
            if ( !_gridCellData.IsCreated )
            {
                return;
            }

            var gridCellData = _gridCellData;

            Entities.WithName("GizmoDrawGrid")
                    .ForEach((in FlowFieldControllerData flowFieldControllerData) =>
                             {
                                 var gridSize   = new Vector2Int { x = flowFieldControllerData.gridSize.x, y = flowFieldControllerData.gridSize.y };
                                 var cellRadius = flowFieldControllerData.cellRadius;

                                 DrawGrid(gridSize, (gridCellData.Length == 0) ? Color.yellow : Color.green, cellRadius);

                                 if ( gridCellData.Length == 0 )
                                 {
                                     return;
                                 }

                                 var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };

                                 foreach ( var curCell in gridCellData )
                                 {
                                     DisplayCell(curCell);

                                     // Handles.Label(curCell.worldPos, curCell.bestCost.ToString() + "\n" + curCell.targetIndex, style);

                                     // var costHeat = curCell.cost / 255f;
                                     // Gizmos.color = new Color(costHeat, costHeat, costHeat);
                                     // var center = new Vector3(cellRadius * 2 * curCell.gridIndex.x + cellRadius, 0, cellRadius * 2 * curCell.gridIndex.y + cellRadius);
                                     // var size   = Vector3.one * cellRadius * 2;
                                     // Gizmos.DrawCube(center, size);
                                 }
                             })
                    .WithoutBurst()
                    .Run();
        }

        private void DisplayCell(CellData cell)
        {
            cell.worldPos = new float3(cell.worldPos.x, cell.worldPos.y + 1, cell.worldPos.z);

            if ( cell.cost == 0 )
            {
                DrawArrow(cell.worldPos, new Vector3(1, 0, 0));
            }
            else if ( cell.cost == byte.MaxValue )
            {
                DrawCross(cell.worldPos);
            }
            else if ( cell.bestDirection.Equals(GridDirection.North) )
            {
                DrawArrow(cell.worldPos, new Vector3(0, 0, 1));
            }
            else if ( cell.bestDirection.Equals(GridDirection.South) )
            {
                DrawArrow(cell.worldPos, new Vector3(0, 0, -1));
            }
            else if ( cell.bestDirection.Equals(GridDirection.East) )
            {
                DrawArrow(cell.worldPos, new Vector3(1, 0, 0));
            }
            else if ( cell.bestDirection.Equals(GridDirection.West) )
            {
                DrawArrow(cell.worldPos, new Vector3(-1, 0, 0));
            }
            else if ( cell.bestDirection.Equals(GridDirection.NorthEast) )
            {
                DrawArrow(cell.worldPos, new Vector3(1, 0, 1));
            }
            else if ( cell.bestDirection.Equals(GridDirection.NorthWest) )
            {
                DrawArrow(cell.worldPos, new Vector3(-1, 0, 1));
            }
            else if ( cell.bestDirection.Equals(GridDirection.SouthEast) )
            {
                DrawArrow(cell.worldPos, new Vector3(1, 0, -1));
            }
            else if ( cell.bestDirection.Equals(GridDirection.SouthWest) )
            {
                DrawArrow(cell.worldPos, new Vector3(-1, 0, -1));
            }
        }

        public static void DrawArrow(Vector3 pos, Vector3 direction, Color? color = null, float length = 1f, float tipSize = 0.25f, float width = 0.5f)
        {
            Gizmos.color = color ?? Color.white;

            pos -= direction / 2;

            var sideLen     = length - length * tipSize;
            var widthOffset = Vector3.Cross(direction, Vector3.up) * width;

            var baseLeft         = pos + widthOffset * 0.3f;
            var baseRight        = pos - widthOffset * 0.3f;
            var tip              = pos + direction * length;
            var upCornerInRight  = pos - widthOffset * 0.3f + direction * sideLen;
            var upCornerInLeft   = pos + widthOffset * 0.3f + direction * sideLen;
            var upCornerOutRight = pos - widthOffset * 0.5f + direction * sideLen;
            var upCornerOutLeft  = pos + widthOffset * 0.5f + direction * sideLen;

            Debug.DrawLine(baseLeft, baseRight);
            Debug.DrawLine(baseRight, upCornerInRight);
            Debug.DrawLine(upCornerInRight, upCornerOutRight);
            Debug.DrawLine(upCornerOutRight, tip);
            Debug.DrawLine(tip, upCornerOutLeft);
            Debug.DrawLine(upCornerOutLeft, upCornerInLeft);
            Debug.DrawLine(upCornerInLeft, baseLeft);
        }

        private static void DrawCross(Vector3 pos, Color? color = null, float arrowHeadLength = 1f)
        {
            Gizmos.color = color ?? Color.red;

            Gizmos.DrawLine(pos + new Vector3(arrowHeadLength / 2, 0f, arrowHeadLength / 2), pos + new Vector3(-arrowHeadLength / 2, 0f, -arrowHeadLength / 2));
            Gizmos.DrawLine(pos + new Vector3(-arrowHeadLength / 2, 0f, arrowHeadLength / 2), pos + new Vector3(arrowHeadLength / 2, 0f, -arrowHeadLength / 2));
        }

        private static void DrawGrid(Vector2Int drawGridSize, Color drawColor, float drawCellRadius)
        {
            Gizmos.color = drawColor;
            for ( var x = 0; x < drawGridSize.x; x++ )
            {
                for ( var y = 0; y < drawGridSize.y; y++ )
                {
                    var center = new Vector3(drawCellRadius * 2 * x + drawCellRadius, 0, drawCellRadius * 2 * y + drawCellRadius);
                    var size   = Vector3.one * drawCellRadius * 2;
                    Gizmos.DrawWireCube(center, size);
                }
            }
        }
    }
#endif
}