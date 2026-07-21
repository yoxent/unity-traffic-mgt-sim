using NUnit.Framework;
using TrafficSim.Core;
using TrafficSim.Data;
using UnityEngine;

namespace TrafficSim.Tests.Data
{
    public class VehicleEligibilityTests
    {
        [Test]
        public void Bicycle_ServesFoodSmallOnly()
        {            var def = ScriptableObject.CreateInstance<VehicleDef>();
            def.type = VehicleType.Bicycle;
            def.allowedModules = new[] { ServiceModule.Food, ServiceModule.Delivery };
            def.allowedSizeBands = new[] { JobSizeBand.Small };
            Assert.IsTrue(def.CanServe(ServiceModule.Food, JobSizeBand.Small));
            Assert.IsFalse(def.CanServe(ServiceModule.Car, JobSizeBand.OnePassenger));
        }
    }
}
