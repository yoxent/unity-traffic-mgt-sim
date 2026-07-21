using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrafficSim.Events
{
    [CreateAssetMenu(menuName = "TrafficSim/Events/Order Event")]
    public class OrderEventChannel : ScriptableObject
    {
        readonly List<Action<OrderEventPayload>> _listeners = new();

        public void Register(Action<OrderEventPayload> listener) => _listeners.Add(listener);
        public void Unregister(Action<OrderEventPayload> listener) => _listeners.Remove(listener);

        public void Raise(OrderEventPayload payload)
        {
            for (var i = _listeners.Count - 1; i >= 0; i--)
                _listeners[i]?.Invoke(payload);
        }
    }
}
