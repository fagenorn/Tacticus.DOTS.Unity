using System.Collections.Generic;
using System.Linq;

using Unity.Mathematics;

using UnityEngine;

namespace Sandbox.ECS.FlowField
{
    public class GridDirection
    {
        public readonly int2 Vector;

        private GridDirection(int x, int y) { Vector = new int2(x, y); }

        public static implicit operator int2(GridDirection direction) { return direction.Vector; }

        public static GridDirection GetDirectionFromV2I(int2 vector) { return CardinalAndIntercardinalDirections.DefaultIfEmpty(None).FirstOrDefault(direction => Equals(direction, vector)); }

        public static readonly GridDirection None = new GridDirection(0, 0);

        public static readonly GridDirection North = new GridDirection(0, 1);

        public static readonly GridDirection South = new GridDirection(0, -1);

        public static readonly GridDirection East = new GridDirection(1, 0);

        public static readonly GridDirection West = new GridDirection(-1, 0);

        public static readonly GridDirection NorthEast = new GridDirection(1, 1);

        public static readonly GridDirection NorthWest = new GridDirection(-1, 1);

        public static readonly GridDirection SouthEast = new GridDirection(1, -1);

        public static readonly GridDirection SouthWest = new GridDirection(-1, -1);

        public static readonly List<GridDirection> CardinalDirections = new List<GridDirection> { North, East, South, West };

        public static readonly List<GridDirection> CardinalDirectionsAndNone = new List<GridDirection> { None, North, East, South, West };

        public static readonly List<GridDirection> CardinalAndIntercardinalDirections = new List<GridDirection> {
                                                                                                                    North,
                                                                                                                    NorthEast,
                                                                                                                    East,
                                                                                                                    SouthEast,
                                                                                                                    South,
                                                                                                                    SouthWest,
                                                                                                                    West,
                                                                                                                    NorthWest
                                                                                                                };

        public static readonly List<GridDirection> AllDirections = new List<GridDirection> {
                                                                                               None,
                                                                                               North,
                                                                                               NorthEast,
                                                                                               East,
                                                                                               SouthEast,
                                                                                               South,
                                                                                               SouthWest,
                                                                                               West,
                                                                                               NorthWest
                                                                                           };
    }

    public class GridDirectionOld
    {
        public readonly Vector2Int Vector;

        private GridDirectionOld(int x, int y) { Vector = new Vector2Int(x, y); }

        public static implicit operator Vector2Int(GridDirectionOld direction) { return direction.Vector; }

        public static GridDirectionOld GetDirectionFromV2I(Vector2Int vector) { return CardinalAndIntercardinalDirections.DefaultIfEmpty(None).FirstOrDefault(direction => direction == vector); }

        public static readonly GridDirectionOld None = new GridDirectionOld(0, 0);

        public static readonly GridDirectionOld North = new GridDirectionOld(0, 1);

        public static readonly GridDirectionOld South = new GridDirectionOld(0, -1);

        public static readonly GridDirectionOld East = new GridDirectionOld(1, 0);

        public static readonly GridDirectionOld West = new GridDirectionOld(-1, 0);

        public static readonly GridDirectionOld NorthEast = new GridDirectionOld(1, 1);

        public static readonly GridDirectionOld NorthWest = new GridDirectionOld(-1, 1);

        public static readonly GridDirectionOld SouthEast = new GridDirectionOld(1, -1);

        public static readonly GridDirectionOld SouthWest = new GridDirectionOld(-1, -1);

        public static readonly List<GridDirectionOld> CardinalDirections = new List<GridDirectionOld> { North, East, South, West };

        public static readonly List<GridDirectionOld> CardinalAndIntercardinalDirections = new List<GridDirectionOld> {
                                                                                                                          North,
                                                                                                                          NorthEast,
                                                                                                                          East,
                                                                                                                          SouthEast,
                                                                                                                          South,
                                                                                                                          SouthWest,
                                                                                                                          West,
                                                                                                                          NorthWest
                                                                                                                      };

        public static readonly List<GridDirectionOld> AllDirections = new List<GridDirectionOld> {
                                                                                                     None,
                                                                                                     North,
                                                                                                     NorthEast,
                                                                                                     East,
                                                                                                     SouthEast,
                                                                                                     South,
                                                                                                     SouthWest,
                                                                                                     West,
                                                                                                     NorthWest
                                                                                                 };
    }
}