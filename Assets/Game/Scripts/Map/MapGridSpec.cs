using System.Collections.Generic;
using UnityEngine;

namespace TrafficSim.Map
{
    /// <summary>
    /// Grid conventions: 1 world unit = 1 cell. Integer X/Z coordinates are cell centers.
    /// Cell bounds span [center - 0.5, center + 0.5] on each axis.
    /// </summary>
    public static class MapGridSpec
    {
        public const float CellSize = 1f;

        public static readonly Vector2Int HubFootprint = new(3, 3);
        public static readonly Vector2Int VehicleFootprint = new(1, 1);
        public static readonly Vector2Int RoadFootprint = new(1, 1);
        public static readonly Vector2Int HouseFootprintVertical = new(1, 2);
        public static readonly Vector2Int HouseFootprintHorizontal = new(2, 1);

        public const float RoadFill = 0.92f;
        public const float HubFill = 0.9f;
        public const float HubSlotFill = 0.88f;
        public const float HouseFill = 1f;
        public const float VehicleSpriteFill = 0.62f;

        public static float TileScale(float fill) => CellSize * fill;

        public static float FootprintScale(Vector2Int footprint, float fill) =>
            MaxExtent(footprint) * CellSize * fill;

        public static Vector2 FootprintScaleXY(Vector2Int footprint, float fill) =>
            new(footprint.x * CellSize * fill, footprint.y * CellSize * fill);

        public static bool IsValidHouseFootprint(Vector2Int footprint) =>
            footprint == HouseFootprintVertical || footprint == HouseFootprintHorizontal;

        public static int MaxExtent(Vector2Int footprint) => Mathf.Max(footprint.x, footprint.y);

        public static Vector3 CellCenter(int cellX, int cellZ) => new(cellX, 0f, cellZ);

        public static Vector3 CellCenter(Vector2Int cell) => CellCenter(cell.x, cell.y);

        /// <summary>
        /// World center of a footprint whose min cell is <paramref name="origin"/>.
        /// Even extents land on half-cells (e.g. 2×1 at origin (7,-1) → center (7.5, -1)).
        /// </summary>
        public static Vector3 FootprintCenter(Vector2Int origin, Vector2Int footprint) =>
            new(
                origin.x + (footprint.x - 1) * 0.5f * CellSize,
                0f,
                origin.y + (footprint.y - 1) * 0.5f * CellSize);

        public static Vector2Int WorldToCell(Vector3 world) => new(
            Mathf.RoundToInt(world.x),
            Mathf.RoundToInt(world.z));

        public static void GetFootprintCellRange(
            Vector2Int origin,
            Vector2Int footprint,
            out int minX,
            out int maxX,
            out int minZ,
            out int maxZ)
        {
            minX = origin.x;
            maxX = origin.x + footprint.x - 1;
            minZ = origin.y;
            maxZ = origin.y + footprint.y - 1;
        }

        public static void GetFootprintCellRange(
            Vector3 center,
            Vector2Int footprint,
            out int minX,
            out int maxX,
            out int minZ,
            out int maxZ)
        {
            // Odd extents: center is an integer cell. Even extents: center is between cells.
            var halfX = (footprint.x - 1) * 0.5f;
            var halfZ = (footprint.y - 1) * 0.5f;
            minX = Mathf.RoundToInt(center.x - halfX);
            maxX = Mathf.RoundToInt(center.x + halfX);
            minZ = Mathf.RoundToInt(center.z - halfZ);
            maxZ = Mathf.RoundToInt(center.z + halfZ);
        }

        public static void CollectLineCells(Vector3 from, Vector3 to, HashSet<Vector2Int> cells)
        {
            var start = WorldToCell(from);
            var end = WorldToCell(to);
            CollectLineCells(start, end, cells);
        }

        public static void CollectLineCells(Vector2Int start, Vector2Int end, HashSet<Vector2Int> cells)
        {
            var x0 = start.x;
            var z0 = start.y;
            var x1 = end.x;
            var z1 = end.y;

            var dx = Mathf.Abs(x1 - x0);
            var dz = Mathf.Abs(z1 - z0);
            var sx = x0 < x1 ? 1 : -1;
            var sz = z0 < z1 ? 1 : -1;
            var err = dx - dz;

            while (true)
            {
                cells.Add(new Vector2Int(x0, z0));
                if (x0 == x1 && z0 == z1)
                    break;

                var e2 = err * 2;
                if (e2 > -dz)
                {
                    err -= dz;
                    x0 += sx;
                }

                if (e2 < dx)
                {
                    err += dx;
                    z0 += sz;
                }
            }
        }
    }
}
