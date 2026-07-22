using System.Collections.Generic;
using NUnit.Framework;
using TrafficSim.Map;
using UnityEngine;

namespace TrafficSim.Tests.Map
{
    public class MapGridSpecTests
    {
        [Test]
        public void CollectLineCells_Horizontal_IncludesEndpointsAndBetween()
        {
            var cells = new HashSet<Vector2Int>();
            MapGridSpec.CollectLineCells(new Vector3(-5f, 0f, 0f), new Vector3(5f, 0f, 0f), cells);

            CollectionAssert.AreEquivalent(
                new[] { new Vector2Int(-5, 0), new Vector2Int(-4, 0), new Vector2Int(-3, 0), new Vector2Int(-2, 0), new Vector2Int(-1, 0), new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0), new Vector2Int(4, 0), new Vector2Int(5, 0) },
                cells);
        }

        [Test]
        public void GetFootprintCellRange_ThreeByThree_CenteredOnOrigin()
        {
            MapGridSpec.GetFootprintCellRange(Vector3.zero, MapGridSpec.HubFootprint, out var minX, out var maxX, out var minZ, out var maxZ);

            Assert.AreEqual(-1, minX);
            Assert.AreEqual(1, maxX);
            Assert.AreEqual(-1, minZ);
            Assert.AreEqual(1, maxZ);
        }

        [Test]
        public void FootprintScale_Hub_MatchesThreeCells()
        {
            var scale = MapGridSpec.FootprintScale(MapGridSpec.HubFootprint, MapGridSpec.HubFill);
            Assert.AreEqual(2.7f, scale, 0.001f);
        }

        [Test]
        public void FootprintCenter_OneByTwo_LandsOnHalfCell()
        {
            var center = MapGridSpec.FootprintCenter(new Vector2Int(-3, 1), MapGridSpec.HouseFootprintVertical);
            Assert.AreEqual(new Vector3(-3f, 0f, 1.5f), center);
        }

        [Test]
        public void GetFootprintCellRange_FromOrigin_ExactlyTwoCells()
        {
            MapGridSpec.GetFootprintCellRange(
                new Vector2Int(7, -1),
                MapGridSpec.HouseFootprintHorizontal,
                out var minX,
                out var maxX,
                out var minZ,
                out var maxZ);

            Assert.AreEqual(7, minX);
            Assert.AreEqual(8, maxX);
            Assert.AreEqual(-1, minZ);
            Assert.AreEqual(-1, maxZ);
        }
    }
}
