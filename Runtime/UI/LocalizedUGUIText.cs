using UnityEngine;

namespace AlicizaX.Localization.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Localization/Localized uGUI Text")]
    public sealed class LocalizedUGUIText : LocalizedTextBase
    {
        [SerializeField] private UnityEngine.UI.Text _target;

        protected override bool HasTarget => _target != null;

        private void Awake()
        {
            if (_target == null) _target = GetComponent<UnityEngine.UI.Text>();
        }

        protected override void ApplyText(string text) => _target.text = text;
    }
}
