using TrafficSim.Core;

namespace TrafficSim.Events
{
    public readonly struct OrderEventPayload
    {
        public readonly int OrderId;
        public readonly ServiceModule Module;

        public OrderEventPayload(int orderId, ServiceModule module)
        {
            OrderId = orderId;
            Module = module;
        }
    }
}
