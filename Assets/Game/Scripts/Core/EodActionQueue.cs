using System;
using System.Collections.Generic;

namespace TrafficSim.Core
{
    public sealed class EodAction
    {
        public float Cost { get; set; }
        public Action<RunState> Apply { get; set; }
    }

    public sealed class EodActionQueue
    {
        readonly List<EodAction> _pending = new();

        public IReadOnlyList<EodAction> Pending => _pending;

        public void Enqueue(EodAction action)
        {
            if (action != null)
                _pending.Add(action);
        }

        public int ApplyAll(RunState state)
        {
            var applied = 0;

            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                var action = _pending[i];
                if (state.Money < action.Cost)
                    continue;

                state.Money -= action.Cost;
                action.Apply?.Invoke(state);
                _pending.RemoveAt(i);
                applied++;
            }

            return applied;
        }

        public void Clear() => _pending.Clear();
    }
}
