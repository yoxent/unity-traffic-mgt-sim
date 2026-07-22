using System.Collections.Generic;
using NUnit.Framework;
using TrafficSim.Map;
using UnityEngine;

namespace TrafficSim.Tests.Map
{
    public class RoadTileResolverTests
    {
        [Test]
        public void Resolve_FourWayIntersection_UsesCrossPrefab()
        {
            var roads = new HashSet<Vector2Int>
            {
                new(0, 0),
                new(0, 1),
                new(0, -1),
                new(1, 0),
                new(-1, 0)
            };

            var placement = RoadTileResolver.Resolve(new Vector2Int(0, 0), roads);

            Assert.AreEqual(RoadTileKind.Cross, placement.Kind);
            Assert.AreEqual(0f, placement.YRotation);
        }

        [Test]
        public void Resolve_HorizontalStraight_UsesStraightRotated90()
        {
            var roads = new HashSet<Vector2Int>
            {
                new(0, 0),
                new(-1, 0),
                new(1, 0)
            };

            var placement = RoadTileResolver.Resolve(new Vector2Int(0, 0), roads);

            Assert.AreEqual(RoadTileKind.Straight, placement.Kind);
            Assert.AreEqual(90f, placement.YRotation);
        }

        [Test]
        public void Resolve_EastSouthCorner_UsesCornerPrefab()
        {
            var roads = new HashSet<Vector2Int>
            {
                new(0, 0),
                new(1, 0),
                new(0, -1)
            };

            var placement = RoadTileResolver.Resolve(new Vector2Int(0, 0), roads);

            Assert.AreEqual(RoadTileKind.Corner, placement.Kind);
            Assert.AreEqual(0f, placement.YRotation);
        }

        [Test]
        public void Resolve_WestNorthCorner_RotatesCorner180()
        {
            var roads = new HashSet<Vector2Int>
            {
                new(0, 0),
                new(-1, 0),
                new(0, 1)
            };

            var placement = RoadTileResolver.Resolve(new Vector2Int(0, 0), roads);

            Assert.AreEqual(RoadTileKind.Corner, placement.Kind);
            Assert.AreEqual(180f, placement.YRotation);
        }

        [Test]
        public void Resolve_EastWestSouthTJunction_UsesTJunctionPrefab()
        {
            var roads = new HashSet<Vector2Int>
            {
                new(0, 0),
                new(1, 0),
                new(-1, 0),
                new(0, -1)
            };

            var placement = RoadTileResolver.Resolve(new Vector2Int(0, 0), roads);

            Assert.AreEqual(RoadTileKind.TJunction, placement.Kind);
            Assert.AreEqual(0f, placement.YRotation);
        }

        [Test]
        public void Resolve_NorthEastSouthTJunction_RotatesTJunction270()
        {
            var roads = new HashSet<Vector2Int>
            {
                new(0, 0),
                new(0, 1),
                new(1, 0),
                new(0, -1)
            };

            var placement = RoadTileResolver.Resolve(new Vector2Int(0, 0), roads);

            Assert.AreEqual(RoadTileKind.TJunction, placement.Kind);
            Assert.AreEqual(270f, placement.YRotation);
        }
    }
}
