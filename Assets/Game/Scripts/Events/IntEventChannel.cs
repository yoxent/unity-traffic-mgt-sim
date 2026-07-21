using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrafficSim.Events
{
    [CreateAssetMenu(menuName = "TrafficSim/Events/Int Event")]
    public class IntEventChannel : ScriptableObject
    {
        readonly List<Action<int>> _listeners = new();

        public void Register(Action<int> listener) => _listeners.Add(listener);
        public void Unregister(Action<int> listener) => _listeners.Remove(listener);

        public void Raise(int value)
        {
            for (var i = _listeners.Count - 1; i >= 0; i--)
                _listeners[i]?.Invoke(value);
        }
    }
}
