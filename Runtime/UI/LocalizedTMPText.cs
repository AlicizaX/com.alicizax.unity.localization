#if TEXTMESHPRO_SUPPORT
using UnityEngine;

namespace AlicizaX.Localization.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Localization/Localized TMP Text")]
    public sealed class LocalizedTMPText : LocalizedTextBase
    {
        [SerializeField] private TMPro.TMP_Text _target;

        protected override bool HasTarget => _target != null;

        private void Awake()
        {
            if (_target == null) _target = GetComponent<TMPro.TMP_Text>();
        }

        protected override void ApplyText(string text) => _target.SetText(text);

        protected override void ApplyText(char[] buffer, int offset, int count)
            => _target.SetCharArray(buffer, offset, count);
    }
}
#endif
