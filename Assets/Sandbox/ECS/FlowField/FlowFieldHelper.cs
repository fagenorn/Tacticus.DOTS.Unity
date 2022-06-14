using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Unity.Collections;
using Unity.Mathematics;

using UnityEngine;

namespace Sandbox.ECS.FlowField
{
    public static class FlowFieldHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetNeighborIndices(int2 originIndex, IEnumerable<GridDirection> directions, int2 gridSize, ref NativeList<int2> results)
        {
            foreach ( int2 curDirection in directions )
            {
                var neighborIndex = GetIndexAtRelativePosition(originIndex, curDirection, gridSize);

                if ( neighborIndex.x >= 0 )
                {
                    results.Add(neighborIndex);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int2 GetIndexAtRelativePosition(int2 originPos, int2 relativePos, int2 gridSize)
        {
            var finalPos = originPos + relativePos;
            if ( finalPos.x < 0 || finalPos.x >= gridSize.x || finalPos.y < 0 || finalPos.y >= gridSize.y )
            {
                return new int2(-1, -1);
            }
            else
            {
                return finalPos;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToFlatIndex(int2 index2D, int height) { return height * index2D.x + index2D.y; }

        public static int2 FromFlatIndex(int index, int height)
        {
            var y = index % height;
            var x = (index - y) / height;

            return new int2(x, y);
        }

        public static int2 GetCellIndexFromWorldPos(float3 worldPos, int2 gridSize, float cellDiameter)
        {
            var percentX = worldPos.x / (gridSize.x * cellDiameter);
            var percentY = worldPos.z / (gridSize.y * cellDiameter);

            percentX = math.clamp(percentX, 0f, 1f);
            percentY = math.clamp(percentY, 0f, 1f);

            var cellIndex = new int2 { x = math.clamp((int)math.floor((gridSize.x) * percentX), 0, gridSize.x - 1), y = math.clamp((int)math.floor((gridSize.y) * percentY), 0, gridSize.y - 1) };

            return cellIndex;
        }
    }
}