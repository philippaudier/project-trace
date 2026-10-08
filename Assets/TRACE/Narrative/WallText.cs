using UnityEngine;

namespace TRACE.Narrative
{
    // 3D text on a wall with the depth-tested TRACE/WallText material. Dynamic fonts rebuild their atlas when other
    // labels request new glyphs; this keeps the renderer pointed at the font's current texture.
    [RequireComponent(typeof(TextMesh), typeof(MeshRenderer))]
    [DisallowMultipleComponent]
    public sealed class WallText : MonoBehaviour
    {
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private TextMesh label;
        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock properties;

        private void Awake()
        {
            label = GetComponent<TextMesh>();
            meshRenderer = GetComponent<MeshRenderer>();
            properties = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            Font.textureRebuilt += OnTextureRebuilt;
            Apply();
        }

        private void OnDisable() => Font.textureRebuilt -= OnTextureRebuilt;

        private void OnTextureRebuilt(Font font)
        {
            if (font == label.font) Apply();
        }

        private void Apply()
        {
            if (label.font == null || label.font.material == null) return;
            meshRenderer.GetPropertyBlock(properties);
            properties.SetTexture(MainTex, label.font.material.mainTexture);
            meshRenderer.SetPropertyBlock(properties);
        }
    }
}
