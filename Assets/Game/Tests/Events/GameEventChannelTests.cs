using NUnit.Framework;
using TrafficSim.Events;

namespace TrafficSim.Tests.Events
{
    public class GameEventChannelTests
    {
        [Test]
        public void Raise_InvokesRegisteredListener()
        {
            var channel = UnityEngine.ScriptableObject.CreateInstance<GameEventChannel>();
            var count = 0;
            channel.Register(() => count++);
            channel.Raise();
            Assert.AreEqual(1, count);
        }
    }
}
