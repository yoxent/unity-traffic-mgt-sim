using TrafficSim.Core;
using UnityEngine;

namespace TrafficSim.Demand
{
    public sealed class OrderInstance
    {
        readonly float _graceTotal;

        public int Id { get; }
        public ServiceModule Module { get; }
        public JobSizeBand SizeBand { get; }
        public int PickupNode { get; }
        public int DropoffNode { get; }
        public float PatienceTotal { get; }
        public float PatienceRemaining { get; private set; }
        public float GraceRemaining { get; private set; }
        public OrderState State { get; private set; }

        public OrderInstance(int id) : this(id, ServiceModule.Food, JobSizeBand.Small, 0, 1, 120f, 30f)
        {
        }

        public OrderInstance(
            int id,
            ServiceModule module,
            JobSizeBand sizeBand,
            int pickupNode,
            int dropoffNode,
            float patienceTotal,
            float graceTotal)
        {
            Id = id;
            Module = module;
            SizeBand = sizeBand;
            PickupNode = pickupNode;
            DropoffNode = dropoffNode;
            PatienceTotal = patienceTotal;
            PatienceRemaining = patienceTotal;
            _graceTotal = graceTotal;
            GraceRemaining = graceTotal;
            State = OrderState.Pending;
        }

        public float RemainingFraction
        {
            get
            {
                if (PatienceTotal + _graceTotal <= 0f)
                    return 0f;

                if (GraceRemaining > 0f)
                    return 1f;

                if (PatienceTotal <= 0f)
                    return 0f;

                return Mathf.Clamp01(PatienceRemaining / PatienceTotal);
            }
        }

        public void TickPatience(float deltaTime)
        {
            if (State == OrderState.Completed || State == OrderState.Expired || deltaTime <= 0f)
                return;

            if (GraceRemaining > 0f)
            {
                GraceRemaining = Mathf.Max(0f, GraceRemaining - deltaTime);
                return;
            }

            PatienceRemaining = Mathf.Max(0f, PatienceRemaining - deltaTime);
            if (PatienceRemaining <= 0f)
                State = OrderState.Expired;
        }

        public void MarkAssigned() => State = OrderState.Assigned;

        public void MarkCompleted() => State = OrderState.Completed;
    }
}
