using UnityEngine;

namespace TRACE.Characters
{
    // Identity metadata of a squad member, read by the HUD and the scene labels. No stats, no progression.
    [DisallowMultipleComponent]
    public sealed class CharacterProfile : MonoBehaviour
    {
        [SerializeField] private string characterId = "";
        [SerializeField] private string displayName = "";
        [SerializeField] private string designation = "";
        [SerializeField] private string affiliation = "";
        [SerializeField] private string archetype = "";
        [SerializeField] private bool isTracewalker;
        [SerializeField] private Color accentColor = Color.white;
        [SerializeField, Tooltip("HUD portrait (active operator, dialogue); replaceable without touching the HUD.")] private Sprite portrait;
        [SerializeField, Tooltip("Tighter face crop for small sizes (squad cards). Falls back to the portrait.")] private Sprite miniPortrait;
        public string CharacterId => characterId;
        public string DisplayName => displayName;
        public string Designation => designation;
        public string Affiliation => affiliation;
        public string Archetype => archetype;
        public bool IsTracewalker => isTracewalker;
        public Color AccentColor => accentColor;
        public Sprite Portrait => portrait;
        public Sprite MiniPortrait => miniPortrait != null ? miniPortrait : portrait;
        // Role line shown under the name in the HUD.
        public string RoleLabel => archetype;
        public string AccentHex => ColorUtility.ToHtmlStringRGB(accentColor);
    }
}
