using System;
using UnityEngine;

namespace TRACE.Narrative
{
    // Short-range, prompt-driven interaction point. Callbacks only; no quest or inventory framework.
    [DisallowMultipleComponent]
    public sealed class Interactable : MonoBehaviour
    {
        [SerializeField] private string prompt = "Interagir";
        [SerializeField, Min(0.5f)] private float range = 2.6f;
        [SerializeField] private bool oneShot = true;
        [SerializeField] private Renderer highlight;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock properties;
        private Color baseEmission;
        public string Prompt => prompt;
        public float Range => range;
        public int UseCount { get; private set; }
        public bool CanInteract => isActiveAndEnabled && (!oneShot || UseCount == 0);
        public bool IsFocused { get; private set; }
        public event Action Interacted;

        private void Awake()
        {
            properties = new MaterialPropertyBlock();
            if (highlight != null && highlight.sharedMaterial.HasProperty(EmissionColor))
                baseEmission = highlight.sharedMaterial.GetColor(EmissionColor);
        }

        public void SetFocused(bool focused)
        {
            if (focused == IsFocused) return;
            IsFocused = focused;
            if (highlight == null) return;
            highlight.GetPropertyBlock(properties);
            properties.SetColor(EmissionColor, focused ? baseEmission * 2.2f + new Color(0.15f, 0.15f, 0.15f) : baseEmission);
            highlight.SetPropertyBlock(properties);
        }

        public bool Interact()
        {
            if (!CanInteract) return false;
            UseCount++;
            Interacted?.Invoke();
            return true;
        }

        private void OnDisable() => SetFocused(false);
    }
}
