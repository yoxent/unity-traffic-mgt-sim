using TrafficSim.Core;
using TrafficSim.Data;

namespace TrafficSim.UI
{
    public sealed class ModulePurchaseGate
    {
        public const int PaidUnlockIntervalDays = 15;

        int _lastPaidUnlockDay = -PaidUnlockIntervalDays;

        public int LastPaidUnlockDay => _lastPaidUnlockDay;

        public bool CanPurchase(ServiceModule module, RunState state, ServiceModuleDef def, out string blockReason)
        {
            blockReason = null;

            if (state.UnlockedModules.Contains(module))
            {
                blockReason = "Owned";
                return false;
            }

            if (state.UnlockedModules.Count == 0)
                return true;

            var nextUnlockDay = _lastPaidUnlockDay + PaidUnlockIntervalDays;
            if (state.DayIndex < nextUnlockDay)
            {
                blockReason = $"Unlock day {nextUnlockDay + 1}";
                return false;
            }

            if (def == null)
            {
                blockReason = "Missing def";
                return false;
            }

            if (state.Money < def.unlockCost)
            {
                blockReason = "Insufficient funds";
                return false;
            }

            return true;
        }

        public bool TryPurchase(ServiceModule module, RunState state, ServiceModuleDef def)
        {
            if (!CanPurchase(module, state, def, out var blockReason))
            {
                SimLog.ModuleInfo($"Purchase blocked {module}: {blockReason}");
                return false;
            }

            if (state.UnlockedModules.Count == 0)
            {
                TutorialSaveStub.SetFreeModuleChoice(module);
                state.UnlockedModules.Add(module);
                SimLog.ModuleInfo($"Free module chosen: {module}");
                return true;
            }

            state.Money -= def.unlockCost;
            _lastPaidUnlockDay = state.DayIndex;
            state.UnlockedModules.Add(module);
            SimLog.ModuleInfo(
                $"Purchased {module} cost={def.unlockCost:F0} money={state.Money:F0} day={state.DayIndex}");
            return true;
        }
    }
}
