namespace TrafficSim.Core.Contracts
{
    public interface IDispatchService
    {
        void Tick();
        void TickPathAgents(float deltaTime);
    }
}
