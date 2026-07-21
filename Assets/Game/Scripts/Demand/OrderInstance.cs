namespace TrafficSim.Demand
{
    /// <summary>
    /// Runtime order state. Expanded in Task 8 (patience, nodes, grace).
    /// </summary>
    public sealed class OrderInstance
    {
        public int Id { get; }

        public OrderInstance(int id) => Id = id;
    }
}
