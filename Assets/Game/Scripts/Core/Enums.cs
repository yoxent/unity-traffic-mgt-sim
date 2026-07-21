namespace TrafficSim.Core
{
    public enum ServiceModule { Car, Food, Delivery }
    public enum VehicleType { Bicycle, Motorbike, FourSeater, SixSeater }
    public enum JobSizeBand { Small, OnePassenger, OneToFourPassengers, FourToSixPassengers, MediumDelivery, LargeDelivery }
    public enum VehicleState { Idle, EnRoute, Cooldown, Offline }
    public enum HubState { Active, Closing, Relocating }
    public enum RunPhase { Playing, EodIntervention, Failed, Won }
}
