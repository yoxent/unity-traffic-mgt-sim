using System;
using TMPro;
using UnityEngine;

namespace TrafficSim.UI
{
    [Serializable]
    public sealed class UiTextRef
    {
        [SerializeField] TMP_Text _target;

        public void SetText(string value)
        {
            if (_target != null)
                _target.text = value;
        }

        public void SetActive(bool active)
        {
            if (_target != null)
                _target.gameObject.SetActive(active);
        }
    }
}
