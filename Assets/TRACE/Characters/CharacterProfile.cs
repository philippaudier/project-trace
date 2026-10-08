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
        [SerializeField, Tooltip("HUD portrait; replaceable without touching the HUD.")] private Sprite portrait;
        public string CharacterId => characterId;
        public string DisplayName => displayName;
        public string Designation => designation;
        public string Affiliation => affiliation;
        public string Archetype => archetype;
        public bool IsTracewalker => isTracewalker;
        public Color AccentColor => accentColor;
        public Sprite Portrait => portrait;
        public string AccentHex => ColorUtility.ToHtmlStringRGB(accentColor);
    }
}
