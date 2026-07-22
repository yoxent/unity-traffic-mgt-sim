using System.Collections.Generic;
using UnityEngine;

namespace TrafficSim.Map
{
    public enum RoadTileKind
    {
        Straight,
        Corner,
        TJunction,
        Cross
    }

    public readonly struct RoadTilePlacement
    {
        public RoadTileKind Kind { get; }
        public float YRotation { get; }

        public RoadTilePlacement(RoadTileKind kind, float yRotation)
        {
            Kind = kind;
            YRotation = yRotation;
        }
    }

    /// <summary>
    /// Picks road prefab variants from open-side connectivity on the grid.
    /// Cardinal labels match prefab names: N=+Z, E=+X, S=-Z, W=-X.
    /// </summary>
    public static class RoadTileResolver
    {
        const int North = 1;
        const int East = 2;
        const int South = 4;
        const int West = 8;

        public static RoadTilePlacement Resolve(Vector2Int cell, HashSet<Vector2Int> roadCells)
        {
            var mask = BuildMask(cell, roadCells);
            var connectionCount = CountBits(mask);

            return connectionCount switch
            {
                4 => new RoadTilePlacement(RoadTileKind.Cross, 0f),
                3 => ResolveTJunction(mask),
                2 when IsStraight(mask) => ResolveStraight(mask),
                2 => ResolveCorner(mask),
                _ => ResolveDeadEnd(mask)
            };
        }

        static int BuildMask(Vector2Int cell, HashSet<Vector2Int> roadCells)
        {
            var mask = 0;
            if (roadCells.Contains(new Vector2Int(cell.x, cell.y + 1)))
                mask |= North;
            if (roadCells.Contains(new Vector2Int(cell.x + 1, cell.y)))
                mask |= East;
            if (roadCells.Contains(new Vector2Int(cell.x, cell.y - 1)))
                mask |= South;
            if (roadCells.Contains(new Vector2Int(cell.x - 1, cell.y)))
                mask |= West;
            return mask;
        }

        static RoadTilePlacement ResolveStraight(int mask) =>
            mask switch
            {
                North | South => new RoadTilePlacement(RoadTileKind.Straight, 0f),
                East | West => new RoadTilePlacement(RoadTileKind.Straight, 90f),
                _ => new RoadTilePlacement(RoadTileKind.Straight, 0f)
            };

        static RoadTilePlacement ResolveCorner(int mask) =>
            mask switch
            {
                East | South => new RoadTilePlacement(RoadTileKind.Corner, 0f),
                South | West => new RoadTilePlacement(RoadTileKind.Corner, 90f),
                West | North => new RoadTilePlacement(RoadTileKind.Corner, 180f),
                North | East => new RoadTilePlacement(RoadTileKind.Corner, 270f),
                _ => new RoadTilePlacement(RoadTileKind.Corner, 0f)
            };

        static RoadTilePlacement ResolveTJunction(int mask) =>
            mask switch
            {
                East | West | South => new RoadTilePlacement(RoadTileKind.TJunction, 0f),
                North | West | South => new RoadTilePlacement(RoadTileKind.TJunction, 90f),
                East | West | North => new RoadTilePlacement(RoadTileKind.TJunction, 180f),
                North | East | South => new RoadTilePlacement(RoadTileKind.TJunction, 270f),
                _ => new RoadTilePlacement(RoadTileKind.TJunction, 0f)
            };

        static RoadTilePlacement ResolveDeadEnd(int mask) =>
            mask switch
            {
                North | South => new RoadTilePlacement(RoadTileKind.Straight, 0f),
                East | West => new RoadTilePlacement(RoadTileKind.Straight, 90f),
                North or South => new RoadTilePlacement(RoadTileKind.Straight, 0f),
                _ => new RoadTilePlacement(RoadTileKind.Straight, 90f)
            };

        static bool IsStraight(int mask) =>
            mask == (North | South) || mask == (East | West);

        static int CountBits(int mask)
        {
            var count = 0;
            while (mask != 0)
            {
                count += mask & 1;
                mask >>= 1;
            }

            return count;
        }
    }
}
