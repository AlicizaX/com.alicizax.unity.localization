#if TEXTMESHPRO_SUPPORT
using System;
using AlicizaX.Localization.Runtime;
using Cysharp.Text;
using TMPro;
using UnityEngine;

namespace AlicizaX.Localization.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Localization/Localized Text")]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private TMP_Text _target;
        [SerializeField] private string _key;
        [SerializeField] private string[] _arguments = Array.Empty<string>();

        private object[] _runtimeArguments;
        private bool _subscribed;
        private string _lastText;

        public string Key => _key;

        private void Awake()
        {
            if (_target == null) _target = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            LocalizationKit.Changed += OnLanguageChanged;
            _subscribed = true;
            Refresh();
        }

        private void OnDisable()
        {
            if (!_subscribed) return;
            LocalizationKit.Changed -= OnLanguageChanged;
            _subscribed = false;
        }

        public void SetKey(string key)
        {
            _key = key ?? string.Empty;
            Refresh();
        }

        public void SetArguments(params object[] arguments)
        {
            _runtimeArguments = arguments;
            Refresh();
        }

        public void SetContent(string key, params object[] arguments)
        {
            _key = key ?? string.Empty;
            _runtimeArguments = arguments;
            Refresh();
        }

        private void OnLanguageChanged(string language)
        {
            Refresh();
        }

        public void Refresh()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || string.IsNullOrEmpty(_key)) return;
            if (_target == null) return;

            string format = LocalizationKit.Get(_key);
            object[] arguments = _runtimeArguments ?? _arguments;
            if (arguments == null || arguments.Length == 0)
            {
                if (string.Equals(format, _lastText, StringComparison.Ordinal)) return;
                _target.SetText(format);
                _lastText = format;
                return;
            }

            var builder = ZString.CreateStringBuilder();
            try
            {
                LocalizationFormatter.AppendFormat(ref builder, format, arguments);
                var text = builder.AsArraySegment();
                _target.SetCharArray(text.Array, text.Offset, text.Count);
                _lastText = null;
            }
            finally { builder.Dispose(); }
        }
    }
}
#endif
