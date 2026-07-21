using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace TrafficSim.UI
{
    [Serializable]
    public sealed class UiTextRef
    {
        static readonly Type TmpTextType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
        static readonly PropertyInfo TmpTextProperty = TmpTextType?.GetProperty("text");

        [SerializeField] Component _target;

        public void SetText(string value)
        {
            if (_target == null)
                return;

            if (TmpTextType != null && TmpTextProperty != null && TmpTextType.IsInstanceOfType(_target))
            {
                TmpTextProperty.SetValue(_target, value);
                return;
            }

            if (_target is Text legacy)
                legacy.text = value;
        }

        public void SetActive(bool active)
        {
            if (_target != null)
                _target.gameObject.SetActive(active);
        }
    }
}
