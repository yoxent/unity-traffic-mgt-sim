using System;
using TrafficSim.Core;
using TrafficSim.Data;
using UnityEngine;
using UnityEngine.UI;

namespace TrafficSim.UI
{
    public sealed class ModulePurchasePanel : MonoBehaviour
    {
        [Serializable]
        public sealed class ModuleOffer
        {
            public ServiceModuleDef def;
            public Button buyButton;
            public UiTextRef label;
        }

        [SerializeField] ModuleOffer[] _offers;
        [SerializeField] UiTextRef _statusText;

        RunState _state;
        readonly ModulePurchaseGate _gate = new();

        public ModulePurchaseGate Gate => _gate;

        public void Bind(RunState state)
        {
            _state = state;
            Refresh();
        }

        void Awake()
        {
            if (_offers == null)
                return;

            for (var i = 0; i < _offers.Length; i++)
            {
                var offer = _offers[i];
                if (offer?.buyButton == null || offer.def == null)
                    continue;

                var capturedDef = offer.def;
                offer.buyButton.onClick.AddListener(() => TryPurchase(capturedDef));
            }
        }

        void OnDestroy()
        {
            if (_offers == null)
                return;

            for (var i = 0; i < _offers.Length; i++)
            {
                var offer = _offers[i];
                if (offer?.buyButton == null || offer.def == null)
                    continue;

                offer.buyButton.onClick.RemoveAllListeners();
            }
        }

        void LateUpdate()
        {
            if (_state == null)
                return;

            Refresh();
        }

        void TryPurchase(ServiceModuleDef def)
        {
            if (_state == null || def == null)
                return;

            _gate.TryPurchase(def.module, _state, def);
            Refresh();
        }

        void Refresh()
        {
            if (_state == null || _offers == null)
                return;

            ServiceModuleDef lastBlocked = null;
            string lastReason = null;

            for (var i = 0; i < _offers.Length; i++)
            {
                var offer = _offers[i];
                if (offer?.def == null)
                    continue;

                var def = offer.def;
                var canPurchase = _gate.CanPurchase(def.module, _state, def, out var blockReason);
                var owned = _state.UnlockedModules.Contains(def.module);
                var isFree = _state.UnlockedModules.Count == 0 && !owned;
                var label = GetDisplayName(def);

                if (owned)
                    label += " (owned)";
                else if (isFree)
                    label += " (free)";
                else
                    label += $" (${Mathf.FloorToInt(def.unlockCost)})";

                offer.label?.SetText(label);

                if (offer.buyButton != null)
                    offer.buyButton.interactable = canPurchase;

                if (!canPurchase && !owned)
                {
                    lastBlocked = def;
                    lastReason = blockReason;
                }
            }

            if (_statusText == null)
                return;

            if (lastBlocked != null && !string.IsNullOrEmpty(lastReason))
                _statusText.SetText($"{GetDisplayName(lastBlocked)}: {lastReason}");
            else if (_state.UnlockedModules.Count == 0)
                _statusText.SetText("Choose your first free module.");
            else
                _statusText.SetText(string.Empty);
        }

        static string GetDisplayName(ServiceModuleDef def) =>
            string.IsNullOrWhiteSpace(def.displayName) ? def.module.ToString() : def.displayName;
    }
}
