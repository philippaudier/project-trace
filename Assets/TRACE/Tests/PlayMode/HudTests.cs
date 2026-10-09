using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TRACE.AI;
using TRACE.Characters;
using TRACE.Encounter;
using TRACE.Input;
using TRACE.Narrative;
using TRACE.Skills;
using TRACE.Tactical;
using TRACE.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TRACE.Tests
{
    public sealed class HudTests
    {
        private SquadController squad;
        private TacticalFocus focus;
        private HudRoot hud;
        private SquadMember[] members;
        private EnemyBrain[] enemies;
        private Keyboard keyboard;
        private Mouse mouse;
        private float oldCapture, oldFixed;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldCapture = Time.captureDeltaTime; oldFixed = Time.fixedDeltaTime;
            Time.captureDeltaTime = 0f; Time.timeScale = 1f;
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            yield return Load("Prototype");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (focus != null) focus.enabled = false;
            Time.timeScale = 1f; Time.fixedDeltaTime = oldFixed; Time.captureDeltaTime = oldCapture;
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse);
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator ExplorationHudShowsOperatorSquadAndObjectiveWithoutTheLegacyGui()
        {
            Assert.That(hud.State, Is.EqualTo(HudState.Exploration));
            var active = Panel<ActiveOperatorPanel>();
            StringAssert.Contains("TRACEWALKER", active.NameLabel);
            StringAssert.Contains("ASSAULT", active.RoleLabel);
            Assert.That(active.PortraitSprite, Is.Not.Null);
            StringAssert.Contains("DASH STRIKE", active.SkillLabel); StringAssert.Contains("READY", active.SkillLabel);
            StringAssert.Contains("FIELD", active.StateLabel);
            Assert.That(active.Alpha, Is.GreaterThan(0.5f).And.LessThan(0.95f), "lighter in exploration");
            var squadPanel = Panel<SquadStatusPanel>();
            StringAssert.Contains("CONTROL", squadPanel.CardLabel(0));
            StringAssert.Contains("SUPPORT", squadPanel.CardLabel(1));
            Assert.That(squadPanel.CardSkill(0), Is.Empty, "no cooldown detail while exploring");
            var mission = Panel<MissionPanel>();
            Assert.That(mission.Objective, Is.Not.Empty);
            Assert.That(mission.Sector, Is.Not.Empty);
            Assert.That(Panel<TargetPanel>().IsShown, Is.False);
            Assert.That(Panel<ReticleController>().IsShown, Is.True);
            Assert.That(Panel<ReticleController>().Alpha, Is.LessThan(0.5f));
            Assert.That(squad.GetComponent<SkillHud>().enabled, Is.False, "legacy IMGUI handed over");
            yield return null;
        }

        [UnityTest] public IEnumerator PortraitsAreAssignedToEveryProfileAndSupportStaysAPlaceholder()
        {
            foreach (var member in members)
            {
                var profile = member.GetComponent<CharacterProfile>();
                Assert.That(profile, Is.Not.Null, member.name);
                Assert.That(profile.Portrait, Is.Not.Null, member.name);
            }
            var support = members[2].GetComponent<CharacterProfile>();
            Assert.That(support.IsTracewalker, Is.False);
            StringAssert.Contains("Support", support.Designation);
            var sprites = members.Select(m => m.GetComponent<CharacterProfile>().Portrait).Distinct().Count();
            Assert.That(sprites, Is.EqualTo(3), "three distinct portraits");
            yield return null;
        }

        [UnityTest] public IEnumerator PortraitsShareOneFormatWithASmallerFaceCropForTheSquadCards()
        {
            foreach (var member in members)
            {
                var profile = member.GetComponent<CharacterProfile>();
                Assert.That(profile.Portrait.rect.size, Is.EqualTo(new Vector2(256f, 256f)), profile.DisplayName);
                Assert.That(profile.MiniPortrait, Is.Not.EqualTo(profile.Portrait), profile.DisplayName);
                Assert.That(profile.MiniPortrait.rect.size, Is.EqualTo(new Vector2(72f, 72f)), profile.DisplayName);
                Assert.That(profile.RoleLabel, Is.EqualTo(profile.Archetype));
            }
            yield return null;
        }

        [UnityTest] public IEnumerator EveryMemberShowsItsPortraitAndAccentThroughRapidSwitches()
        {
            var active = Panel<ActiveOperatorPanel>();
            var squadPanel = Panel<SquadStatusPanel>();
            foreach (var key in new[] { Key.Digit2, Key.Digit3, Key.Digit1, Key.Digit3, Key.Digit2 })
            {
                Keys(key); yield return Wait(0.05f); Keys(); yield return Wait(0.05f);
            }
            yield return Wait(0.3f);
            for (int round = 0; round < 3; round++)
            {
                Keys(Key.Digit1 + round); yield return Wait(0.1f); Keys(); yield return Wait(0.2f);
                var profile = members[round].GetComponent<CharacterProfile>();
                Assert.That(squad.ActiveMember, Is.EqualTo(members[round]));
                Assert.That(active.PortraitSprite, Is.EqualTo(profile.Portrait), profile.DisplayName);
                StringAssert.Contains(profile.RoleLabel.ToUpperInvariant(), active.RoleLabel);
                Assert.That((Vector4)active.FrameColor, Is.EqualTo((Vector4)new Color(profile.AccentColor.r, profile.AccentColor.g, profile.AccentColor.b, 0.9f)).Using(ColorNear));
                for (int card = 0; card < 2; card++)
                    Assert.That(squadPanel.CardPortrait(card), Is.EqualTo(squadPanel.CardMember(card).GetComponent<CharacterProfile>().MiniPortrait));
            }
        }

        [UnityTest] public IEnumerator AMissingPortraitFallsBackToTheNeutralSilhouette()
        {
            var profile = members[1].GetComponent<CharacterProfile>();
            Set(profile, "portrait", null); Set(profile, "miniPortrait", null);
            var squadPanel = Panel<SquadStatusPanel>();
            var active = Panel<ActiveOperatorPanel>();
            // Rebind the cards so they pick the change up, then make Control active.
            Keys(Key.Digit3); yield return Wait(0.1f); Keys(); yield return Wait(0.2f);
            int card = squadPanel.CardMember(0) == members[1] ? 0 : 1;
            Assert.That(squadPanel.CardPortrait(card).name, Is.EqualTo("Portrait_Fallback"));
            Keys(Key.Digit2); yield return Wait(0.1f); Keys(); yield return Wait(0.15f);
            Assert.That(active.PortraitSprite.name, Is.EqualTo("Portrait_Fallback"));
            StringAssert.Contains("CONTROL", active.NameLabel);
        }

        [UnityTest] public IEnumerator TacticalFocusLiftsThePortraitAccentsAndKeepsThePortraits()
        {
            var active = Panel<ActiveOperatorPanel>();
            var squadPanel = Panel<SquadStatusPanel>();
            yield return Wait(0.1f);
            var calmFrame = active.FrameColor; var calmCard = squadPanel.CardFrame(0);
            var portrait = active.PortraitSprite; var mini = squadPanel.CardPortrait(0);
            Keys(Key.Tab); yield return Wait(0.6f);
            Assert.That(hud.State, Is.EqualTo(HudState.Focus));
            Assert.That(Brightness(active.FrameColor), Is.GreaterThan(Brightness(calmFrame)));
            Assert.That(Brightness(squadPanel.CardFrame(0)), Is.GreaterThan(Brightness(calmCard)));
            Assert.That(active.PortraitSprite, Is.EqualTo(portrait));
            Assert.That(squadPanel.CardPortrait(0), Is.EqualTo(mini));
            Keys(); yield return Wait(0.6f);
            Assert.That((Vector4)active.FrameColor, Is.EqualTo((Vector4)calmFrame).Using(ColorNear));
        }

        [UnityTest] public IEnumerator SwitchingMembersUpdatesTheOperatorPanelAndTheSquadCards()
        {
            var active = Panel<ActiveOperatorPanel>();
            var squadPanel = Panel<SquadStatusPanel>();
            var before = active.PortraitSprite;
            Keys(Key.Digit2); yield return Wait(0.15f); Keys(); yield return Wait(0.15f);
            Assert.That(squad.ActiveMember, Is.EqualTo(members[1]));
            StringAssert.Contains("CONTROL", active.NameLabel);
            StringAssert.Contains("CONTROLLER", active.RoleLabel);
            Assert.That(active.PortraitSprite, Is.Not.EqualTo(before));
            StringAssert.Contains("TRACEWALKER", squadPanel.CardLabel(0));
            StringAssert.Contains("SUPPORT", squadPanel.CardLabel(1));
            Assert.That(squadPanel.CardMember(0), Is.EqualTo(members[0]));
            Assert.That(Time.unscaledTime - hud.ActiveChangedAt, Is.LessThan(0.5f));
        }

        [UnityTest] public IEnumerator CombatAndLockDriveTheTargetPanelThenCalmReturnsToExploration()
        {
            var enemy = enemies[0];
            Place(enemy.transform, members[0].transform.position + members[0].transform.forward * 4f);
            enemy.gameObject.SetActive(true);
            yield return Wait(0.6f);
            Assert.That(hud.State, Is.EqualTo(HudState.Combat));
            var target = Panel<TargetPanel>();
            Assert.That(hud.TargetHealth, Is.Not.Null);
            Assert.That(target.IsShown, Is.True);
            StringAssert.Contains("TARGET", target.TagLabel);
            StringAssert.Contains("PURSUER", target.NameLabel);
            Assert.That(Panel<ReticleController>().Alpha, Is.GreaterThan(0.5f));
            StringAssert.Contains("ENGAGED", Panel<ActiveOperatorPanel>().StateLabel);
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Middle)); yield return Wait(0.1f);
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return Wait(0.2f);
            Assert.That(hud.IsLocked, Is.True);
            StringAssert.Contains("LOCK", target.TagLabel);
            Assert.That(Panel<ReticleController>().IsShown, Is.False, "the world marker replaces the reticle");
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Middle)); yield return Wait(0.1f);
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return Wait(0.2f);
            Assert.That(hud.IsLocked, Is.False);
            enemy.gameObject.SetActive(false);
            yield return Wait(3.2f);
            Assert.That(hud.State, Is.EqualTo(HudState.Exploration));
            Assert.That(target.IsShown, Is.False);
        }

        [UnityTest] public IEnumerator TacticalFocusEnrichesTheHudGradesTheImageAndLeavesCleanly()
        {
            var overlay = Panel<TacticalFocusOverlay>();
            var squadPanel = Panel<SquadStatusPanel>();
            var mission = Panel<MissionPanel>();
            Keys(Key.Tab); yield return Wait(0.6f);
            Assert.That(focus.IsActive, Is.True);
            Assert.That(hud.State, Is.EqualTo(HudState.Focus));
            Assert.That(overlay.IsShown, Is.True);
            Assert.That(overlay.GradeWeight, Is.GreaterThan(0.5f));
            StringAssert.Contains("ANALYSIS", overlay.Analysis);
            StringAssert.Contains("HOSTILE", overlay.Analysis);
            StringAssert.Contains("READY", squadPanel.CardSkill(0));
            StringAssert.Contains("ANALYSIS", Panel<ActiveOperatorPanel>().StateLabel);
            Assert.That(mission.Alpha, Is.LessThan(0.5f));
            Assert.That(Panel<ReticleController>().IsShown, Is.False);
            Keys(); yield return Wait(0.8f);
            Assert.That(hud.State, Is.EqualTo(HudState.Exploration));
            Assert.That(overlay.IsShown, Is.False);
            Assert.That(overlay.GradeWeight, Is.LessThan(0.1f));
            Assert.That(mission.Alpha, Is.GreaterThan(0.9f));
        }

        [UnityTest] public IEnumerator MissionPanelFollowsTheEncounterWaves()
        {
            yield return Load("PrototypeEncounter");
            var mission = Panel<MissionPanel>();
            var encounter = Object.FindFirstObjectByType<EncounterController>();
            Assert.That(encounter.HasBegun, Is.True);
            yield return Wait(3.2f);
            Assert.That(encounter.State, Is.Not.EqualTo(EncounterController.EncounterState.Pending));
            StringAssert.Contains("VAGUE 1 / 3", mission.Detail);
            Assert.That(hud.State, Is.EqualTo(HudState.Combat));
            Assert.That(Panel<ThreatIndicatorView>(), Is.Not.Null);
        }

        [UnityTest] public IEnumerator SquadCardsCarryEachMembersIdentityColour()
        {
            var squadPanel = Panel<SquadStatusPanel>();
            yield return Wait(0.1f);
            for (int card = 0; card < 2; card++)
            {
                var profile = squadPanel.CardMember(card).GetComponent<CharacterProfile>();
                Assert.That(squadPanel.CardAccent(card), Is.EqualTo(profile.AccentColor), profile.DisplayName);
                StringAssert.Contains(profile.AccentHex, squadPanel.CardLabel(card), "slot number tinted");
            }
            Assert.That(squadPanel.CardAccent(0), Is.Not.EqualTo(squadPanel.CardAccent(1)));
        }

        [UnityTest] public IEnumerator TargetPanelIsCompactForARegularEnemyAndHeavierForAnElite()
        {
            var enemy = enemies[0];
            Place(enemy.transform, members[0].transform.position + members[0].transform.forward * 4f);
            enemy.gameObject.SetActive(true);
            yield return Wait(0.6f);
            var target = Panel<TargetPanel>();
            Assert.That(target.IsShown, Is.True);
            Assert.That(target.IsEliteLayout, Is.False);
            var compact = target.Size;
            Assert.That(compact.y, Is.LessThan(50f), "thin strip for a regular enemy");
            SetBase(enemy, "elite", true);
            yield return Wait(0.2f);
            Assert.That(target.IsEliteLayout, Is.True);
            Assert.That(target.Size.x, Is.GreaterThan(compact.x));
            Assert.That(target.Size.y, Is.GreaterThan(compact.y));
            SetBase(enemy, "elite", false);
        }

        [UnityTest] public IEnumerator FocusAnalysisReadsAsShortTags()
        {
            Keys(Key.Tab); yield return Wait(0.6f);
            string analysis = Panel<TacticalFocusOverlay>().Analysis;
            foreach (var tag in new[] { "HOSTILES", "WIND-UP", "COMBO", "LOCK" }) StringAssert.Contains(tag, analysis);
            StringAssert.DoesNotContain("PREPARATION", analysis);
            Assert.That(analysis.Split('\n').Length, Is.EqualTo(2), "header and one row of tags");
            Keys(); yield return Wait(0.6f);
        }

        [UnityTest] public IEnumerator DialogueSpeakersResolveToSquadProfiles()
        {
            yield return Load("FirstTrace");
            var dialogue = Object.FindFirstObjectByType<DialogueRunner>();
            Assert.That(dialogue.Resolve("ASSAULT"), Is.EqualTo(members[0].GetComponent<CharacterProfile>()), "role word of the archetype");
            Assert.That(dialogue.Resolve("CONTROL"), Is.EqualTo(members[1].GetComponent<CharacterProfile>()));
            Assert.That(dialogue.Resolve("Support"), Is.EqualTo(members[2].GetComponent<CharacterProfile>()));
            Assert.That(dialogue.Resolve("TERMINAL"), Is.Null, "non-squad speakers stay neutral");
            float deadline = Time.unscaledTime + 7f;
            while (!dialogue.IsPlaying && Time.unscaledTime < deadline) yield return null;
            Assert.That(dialogue.IsPlaying, Is.True, "arrival exchange");
            Assert.That(dialogue.CurrentSpeaker, Is.Not.Null, dialogue.CurrentLine.speaker);
            Assert.That(dialogue.CurrentSpeaker, Is.EqualTo(dialogue.Resolve(dialogue.CurrentLine.speaker)));
            Assert.That(dialogue.CurrentPortrait, Is.EqualTo(dialogue.CurrentSpeaker.Portrait), "same portrait as the HUD");
        }

        [UnityTest] public IEnumerator FirstTracePromptAndObjectiveReadFromTheStory()
        {
            yield return Load("FirstTrace");
            var mission = Panel<MissionPanel>();
            var prompt = Panel<InteractionPrompt>();
            var story = Object.FindFirstObjectByType<StoryDirector>();
            Assert.That(story.Current, Is.EqualTo(StoryDirector.Phase.Arrival));
            StringAssert.Contains("terminal", mission.Objective);
            StringAssert.Contains("QUAI", mission.Sector);
            Assert.That(prompt.IsShown, Is.False);
            var interaction = squad.GetComponent<InteractionController>();
            Assert.That(Get<bool>(interaction, "legacyPrompt"), Is.False);
            var terminal = Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None).First(i => i.Prompt.Contains("terminal"));
            var dialogue = Object.FindFirstObjectByType<DialogueRunner>();
            float deadline = Time.unscaledTime + 8f;
            while (dialogue.IsPlaying && Time.unscaledTime < deadline) yield return null;
            Place(members[0].transform, terminal.transform.position + Vector3.left * 1.3f, members[0].GetComponent<CharacterController>());
            yield return Wait(0.3f);
            Assert.That(interaction.Current, Is.EqualTo(terminal));
            Assert.That(prompt.IsShown, Is.True);
            StringAssert.Contains("INTERACT", prompt.Label);
            StringAssert.Contains(terminal.Prompt, prompt.Label);
        }

        private T Panel<T>() where T : HudPanel => hud.GetComponentInChildren<T>(true);

        private IEnumerator Load(string scene)
        {
            yield return SceneManager.LoadSceneAsync(scene);
            squad = Object.FindFirstObjectByType<SquadController>();
            focus = squad.GetComponent<TacticalFocus>();
            hud = Object.FindFirstObjectByType<HudRoot>();
            Assert.That(hud, Is.Not.Null, scene + " has no HUD");
            Set(squad.GetComponent<TracePlayerInput>(), "captureCursor", false);
            members = squad.Members.ToArray();
            enemies = Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(e => e.name).ToArray();
            if (scene == "Prototype") foreach (var enemy in enemies) enemy.gameObject.SetActive(false);
            foreach (var other in members) if (!other.IsPlayerControlled) other.Companion.enabled = false;
            yield return Wait(0.3f);
        }

        private static void Place(Transform actor, Vector3 position, CharacterController body = null)
        {
            if (body != null) body.enabled = false;
            actor.position = position + Vector3.up * 0.08f;
            if (body != null) body.enabled = true;
            Physics.SyncTransforms();
        }

        private void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        private static IEnumerator Wait(float duration) { yield return new WaitForSecondsRealtime(duration); yield return null; }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private static float Brightness(Color color) => color.r + color.g + color.b;
        private static readonly System.Comparison<Vector4> ColorNearComparison = (a, b) => Vector4.Distance(a, b) < 0.02f ? 0 : 1;
        private static System.Collections.Generic.IComparer<Vector4> ColorNear => System.Collections.Generic.Comparer<Vector4>.Create(ColorNearComparison);
        private static void SetBase(EnemyBrain brain, string name, object value) => typeof(EnemyBrain).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(brain, value);
        private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    }
}
