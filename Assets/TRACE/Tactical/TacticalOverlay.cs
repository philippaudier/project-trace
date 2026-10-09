using TRACE.AI;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Skills;
using UnityEngine;
using UnityEngine.UI;

namespace TRACE.Tactical
{
    public sealed class TacticalOverlay : MonoBehaviour
    {
        [SerializeField] private TacticalFocus focus;
        [SerializeField] private SquadController squad;
        [SerializeField] private GameObject overlay;
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private Text[] memberTexts;
        [SerializeField] private EnemyBrain[] enemies;
        [SerializeField] private Text[] enemyTexts;
        [SerializeField] private RectTransform[] enemyPanels;
        [SerializeField, Min(1f)] private float informationRange = 18f;
        [SerializeField, Tooltip("Reveal timing; without it everything shows at once.")] private TacticalFocusPresentationController presentation;
        [SerializeField, Min(0f), Tooltip("Band kept free at the top for the HUD target panel and the focus header.")] private float topMargin = 110f;
        private CharacterSkill[] skills;
        private Shield[] shields;
        private ComboOpportunity[] memberCombos;
        private CharacterProfile[] profiles;
        private Health[] enemyHealth;
        private ComboOpportunity[] enemyCombos;
        private EnemyGravityResponse[] gravity;
        private FrontalGuard[] guards;
        private TargetingSystem targeting;
        private UnityEngine.Camera view;
        private Rect[] placed;
        private int placedCount;
        private float nextTextRefresh;
        private readonly string[] roles = { "ASSAULT", "CONTROL", "SUPPORT" };
        public bool IsVisible => overlay.activeSelf;
        public int VisibleEnemyCount { get; private set; }
        public string MemberInfo(int index) => memberTexts[index].text;
        public string EnemyInfo(int index) => enemyTexts[index].text;

        private void Start()
        {
            view = UnityEngine.Camera.main;
            targeting = squad.GetComponent<TargetingSystem>();
            skills = new CharacterSkill[squad.Members.Count];
            shields = new Shield[skills.Length];
            memberCombos = new ComboOpportunity[skills.Length];
            profiles = new CharacterProfile[skills.Length];
            for (int i = 0; i < skills.Length; i++)
            {
                skills[i] = squad.Members[i].GetComponent<CharacterSkill>();
                shields[i] = squad.Members[i].GetComponent<Shield>();
                memberCombos[i] = squad.Members[i].GetComponent<ComboOpportunity>();
                profiles[i] = squad.Members[i].GetComponent<CharacterProfile>();
            }
            enemyHealth = new Health[enemies.Length];
            enemyCombos = new ComboOpportunity[enemies.Length];
            gravity = new EnemyGravityResponse[enemies.Length];
            guards = new FrontalGuard[enemies.Length];
            placed = new Rect[enemies.Length];
            for (int i = 0; i < enemies.Length; i++)
            {
                enemyHealth[i] = enemies[i].GetComponent<Health>();
                enemyCombos[i] = enemies[i].GetComponent<ComboOpportunity>();
                gravity[i] = enemies[i].GetComponent<EnemyGravityResponse>();
                guards[i] = enemies[i].GetComponent<FrontalGuard>();
            }
            overlay.SetActive(false);
        }

        private void LateUpdate()
        {
            if (skills == null) return;
            if (!focus.IsActive)
            {
                overlay.SetActive(false);
                VisibleEnemyCount = 0;
                return;
            }
            if (!overlay.activeSelf)
            {
                overlay.SetActive(true);
                Canvas.ForceUpdateCanvases();
                nextTextRefresh = 0f;
            }
            bool refresh = Time.unscaledTime >= nextTextRefresh;
            if (refresh)
            {
                nextTextRefresh = Time.unscaledTime + 0.05f;
                for (int i = 0; i < skills.Length; i++)
                {
                    var member = squad.Members[i];
                    string selection = member == squad.ActiveMember ? "<color=#72EBFF>ACTIF</color>" : "COMPANION";
                    string readiness = member.Health.IsDead ? "DEAD" : skills[i].IsReady ? "READY" : $"{skills[i].CooldownRemaining:0.0} s";
                    string counter = memberCombos[i].Type == ComboOpportunityType.Protected ? $"  <color=#72EBFF>CONTRE {memberCombos[i].RemainingDuration:0.0}s</color>" : "";
                    // A profiled member is named and tinted by its profile; the others keep the prototype role label.
                    string label = profiles[i] != null ? $"<color=#{profiles[i].AccentHex}>{profiles[i].DisplayName.ToUpperInvariant()}</color>" : roles[i];
                    memberTexts[i].text = $"<b>{i + 1}  {label}</b>  {selection}\nHP {member.Health.CurrentHealth:0}/{member.Health.MaxHealth:0}  SH {shields[i].CurrentAmount:0}\n{skills[i].SkillName} : <b>{readiness}</b>{counter}";
                }
            }
            placedCount = VisibleEnemyCount = 0;
            Vector2 size = canvasRect.rect.size;
            for (int i = 0; i < enemies.Length; i++)
            {
                Vector3 screen = view.WorldToViewportPoint(enemies[i].transform.position + Vector3.up * 2.2f);
                bool visible = Revealed(TacticalFocusPresentationController.Stage.Cards) && enemies[i].gameObject.activeInHierarchy && !enemyHealth[i].IsDead && squad.ActiveMember != null &&
                    Vector3.Distance(enemies[i].transform.position, squad.ActiveMember.transform.position) <= informationRange &&
                    screen.z > 0f && screen.x > 0f && screen.x < 1f && screen.y > 0f && screen.y < 1f &&
                    !Physics.Linecast(view.transform.position, enemies[i].transform.position + Vector3.up * 0.9f, 1, QueryTriggerInteraction.Ignore);
                enemyPanels[i].gameObject.SetActive(visible);
                if (!visible) continue;
                VisibleEnemyCount++;
                if (refresh)
                {
                    // Name line, then only the actionable tags, revealed in order: status first, combos last.
                    string victim = "";
                    for (int m = 0; m < squad.Members.Count; m++)
                        if (enemies[i].CurrentTarget == squad.Members[m].Receiver) victim = " > " + (m + 1);
                    string tags = "";
                    void add(string tag) => tags += (tags.Length > 0 ? "   " : "") + tag;
                    bool status = Revealed(TacticalFocusPresentationController.Stage.Status);
                    if (status && enemies[i].IsPreparingAttack)
                        add($"<color=#FF8A5C><b>{(enemies[i] is MarksmanEnemy ? "THREAT" : "WIND-UP")} {enemies[i].PreparationRemaining:0.0}{victim}</b></color>");
                    if (Revealed(TacticalFocusPresentationController.Stage.Combos) && enemyCombos[i].Type == ComboOpportunityType.Grouped) add("<color=#FFC629>GROUPED 1+E</color>");
                    if (status && gravity[i] != null && gravity[i].IsSlowed) add("<color=#5ED6FF>SLOWED</color>");
                    if (status && guards[i] != null) add("<color=#A9C4E6>GUARD > FLANK</color>");
                    string lockTag = targeting != null && targeting.LockedTarget == enemyHealth[i] ? "<color=#5ED6FF><b>LOCKED</b></color>  " : "";
                    enemyTexts[i].text = $"{lockTag}<b>{enemies[i].Archetype}</b>  <color=#9EA6B0>{enemyHealth[i].CurrentHealth:0}</color>" +
                        (tags.Length > 0 ? "\n" + tags : "");
                }
                // Greedy vertical placement prevents adjacent enemies from producing overlapping cards.
                Vector2 card = enemyPanels[i].sizeDelta;
                Vector2 anchor = new Vector2(Mathf.Clamp(screen.x * size.x, 470f, size.x - card.x * 0.5f - 5f),
                    Mathf.Clamp(screen.y * size.y + 42f, 50f, size.y - topMargin - card.y * 0.5f));
                Rect chosen = new Rect(anchor.x - card.x * 0.5f, anchor.y - card.y * 0.5f, card.x, card.y);
                for (int attempt = 0; attempt < 16; attempt++)
                {
                    float offset = attempt == 0 ? 0f : ((attempt + 1) / 2) * (card.y + 6f) * (attempt % 2 == 1 ? -1f : 1f);
                    var candidate = new Rect(chosen.x, Mathf.Clamp(anchor.y - card.y * 0.5f + offset, 12f, size.y - topMargin - card.y), card.x, card.y);
                    bool overlap = false;
                    for (int p = 0; p < placedCount; p++) if (candidate.Overlaps(placed[p])) overlap = true;
                    if (!overlap) { chosen = candidate; break; }
                }
                placed[placedCount++] = chosen;
                enemyPanels[i].anchoredPosition = chosen.center;
            }
        }

        private bool Revealed(TacticalFocusPresentationController.Stage stage) => presentation == null || presentation.Revealed(stage);

        private void OnDisable()
        {
            if (overlay != null) overlay.SetActive(false);
            VisibleEnemyCount = 0;
        }
    }
}
