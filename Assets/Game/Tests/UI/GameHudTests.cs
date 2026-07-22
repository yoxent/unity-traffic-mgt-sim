using NUnit.Framework;
using TrafficSim.UI;

namespace TrafficSim.Tests.UI
{
    public sealed class GameHudTests
    {
        [Test]
        public void FormatClock_MapsDayFractionToSixAmStart()
        {
            Assert.AreEqual("06:00", GameHud.FormatClock(0f));
            Assert.AreEqual("12:00", GameHud.FormatClock(0.25f));
            Assert.AreEqual("18:00", GameHud.FormatClock(0.5f));
            Assert.AreEqual("00:00", GameHud.FormatClock(0.75f));
            Assert.AreEqual("05:58", GameHud.FormatClock(0.999f));
            Assert.AreEqual("06:00", GameHud.FormatClock(1f));
        }

        [Test]
        public void FormatTimeOfDay_UsesQuarterDayBands()
        {
            Assert.AreEqual("Morning", GameHud.FormatTimeOfDay(0f));
            Assert.AreEqual("Day", GameHud.FormatTimeOfDay(0.25f));
            Assert.AreEqual("Evening", GameHud.FormatTimeOfDay(0.5f));
            Assert.AreEqual("Night", GameHud.FormatTimeOfDay(0.75f));
        }
    }
}
