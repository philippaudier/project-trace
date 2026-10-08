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
        [SerializeField] private string archetype = "";
        [SerializeField] private Color accentColor = Color.white;
        public string CharacterId => characterId;
        public string DisplayName => displayName;
        public string Designation => designation;
        public string Archetype => archetype;
        public Color AccentColor => accentColor;
        public string AccentHex => ColorUtility.ToHtmlStringRGB(accentColor);
    }
}
