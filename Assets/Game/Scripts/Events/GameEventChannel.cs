using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrafficSim.Events
{
    [CreateAssetMenu(menuName = "TrafficSim/Events/Game Event")]
    public class GameEventChannel : ScriptableObject
    {
        readonly List<Action> _listeners = new();

        public void Register(Action listener) => _listeners.Add(listener);
        public void Unregister(Action listener) => _listeners.Remove(listener);

        public void Raise()
        {
            for (var i = _listeners.Count - 1; i >= 0; i--)
                _listeners[i]?.Invoke();
        }
    }
}
