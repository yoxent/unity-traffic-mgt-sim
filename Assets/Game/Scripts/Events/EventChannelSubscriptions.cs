using System;
using System.Collections.Generic;

namespace TrafficSim.Events
{
    /// <summary>
    /// Tracks event-channel subscriptions and removes them on dispose.
    /// Use from MonoBehaviours that subscribe to SO event channels at runtime.
    /// </summary>
    public sealed class EventChannelSubscriptions : IDisposable
    {
        readonly List<Action> _unsubscribeActions = new();

        public void Track(Action unsubscribe) => _unsubscribeActions.Add(unsubscribe);

        public void Dispose()
        {
            for (var i = _unsubscribeActions.Count - 1; i >= 0; i--)
                _unsubscribeActions[i]?.Invoke();

            _unsubscribeActions.Clear();
        }
    }
}
