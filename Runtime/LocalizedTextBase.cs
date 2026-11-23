using System;
using AlicizaX.Localization.Runtime;
using Cysharp.Text;
using UnityEngine;

namespace AlicizaX.Localization.UI
{
    public abstract class LocalizedTextBase : MonoBehaviour
    {
        [SerializeField] private string _key;
        [SerializeField] private string[] _arguments = Array.Empty<string>();

        private object[] _runtimeArguments;
        private bool _subscribed;
        private string _lastText;

        public string Key => _key;

        protected abstract bool HasTarget { get; }

        protected void OnEnable()
        {
            if (!Application.isPlaying) return;
            LocalizationKit.Changed += OnLanguageChanged;
            _subscribed = true;
            Refresh();
        }

        protected void OnDisable()
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
            if (!HasTarget) return;

            string format = LocalizationKit.Get(_key);
            object[] arguments = _runtimeArguments ?? _arguments;
            if (arguments == null || arguments.Length == 0)
            {
                if (string.Equals(format, _lastText, StringComparison.Ordinal)) return;
                ApplyText(format);
                _lastText = format;
                return;
            }

            var builder = ZString.CreateStringBuilder();
            try
            {
                LocalizationFormatter.AppendFormat(ref builder, format, arguments);
                var text = builder.AsArraySegment();
                ApplyText(text.Array, text.Offset, text.Count);
                _lastText = null;
            }
            finally { builder.Dispose(); }
        }

        protected abstract void ApplyText(string text);

        // Consume or copy the borrowed buffer before returning.
        protected virtual void ApplyText(char[] buffer, int offset, int count)
            => ApplyText(new string(buffer, offset, count));
    }
}
