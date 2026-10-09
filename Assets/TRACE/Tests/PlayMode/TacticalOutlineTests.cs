using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TRACE.AI;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Input;
using TRACE.Rendering;
using TRACE.Tactical;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TRACE.Tests
{
    public sealed class TacticalOutlineTests
    {
        private SquadController squad;
        private TacticalFocus focus;
        private TacticalReadability readability;
        private SquadMember[] members;
        private EnemyBrain[] enemies;
        private Keyboard keyboard;
        private float oldCapture, oldFixed;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;
        private static readonly int FadeId = Shader.PropertyToID("_TacticalOutlineFade");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldCapture = Time.captureDeltaTime; oldFixed = Time.fixedDeltaTime;
            Time.captureDeltaTime = 0f; Time.timeScale = 1f;
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            yield return Load("Prototype");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (focus != null) focus.enabled = false;
            Time.timeScale = 1f; Time.fixedDeltaTime = oldFixed; Time.captureDeltaTime = oldCapture;
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator TheOutlineFeatureRunsOnEveryUrpRenderer()
        {
            var asset = (QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            Assert.That(asset, Is.Not.Null);
            var list = (ScriptableRendererData[])typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(asset);
            foreach (var data in list)
            {
                var feature = data.rendererFeatures.OfType<TacticalOutlineFeature>().SingleOrDefault();
                Assert.That(feature, Is.Not.Null, data.name);
                Assert.That(feature.isActive, Is.True);
                Assert.That(feature.Tiers.Count, Is.EqualTo(4));
                Assert.That(feature.Tiers.Select(t => t.renderingLayer), Is.EqualTo(new[] { 8, 9, 10, 11 }));
                foreach (var tier in feature.Tiers)
                {
                    Assert.That(tier.material, Is.Not.Null, tier.name);
                    Assert.That(tier.material.shader.name, Is.EqualTo("TRACE/TacticalOutline"));
                    Assert.That(tier.material.GetFloat("_OutlineWidth"), Is.InRange(1f, 2f), "fine, about 1-2 px");
                }
            }
            yield return null;
        }

        [UnityTest] public IEnumerator NoOutlineOutsideFocusAndAShortFadeInAndOut()
        {
            var enemy = Place(enemies[0], 4f, 0f);
            yield return Wait(0.3f);
            Assert.That(readability.OutlineFade, Is.EqualTo(0f));
            Assert.That(Shader.GetGlobalFloat(FadeId), Is.EqualTo(0f));
            Assert.That(readability.OutlineOf(enemy), Is.EqualTo(TacticalReadability.Marker.None));
            AssertLayers(enemy, TacticalReadability.Marker.None);
            Keys(Key.Tab);
            yield return null; yield return null;
            Assert.That(readability.OutlineFade, Is.LessThan(1f), "never in one frame");
            yield return Wait(0.3f);
            Assert.That(readability.OutlineFade, Is.EqualTo(1f));
            Assert.That(readability.OutlineOf(enemy), Is.EqualTo(TacticalReadability.Marker.Standard));
            AssertLayers(enemy, TacticalReadability.Marker.Standard);
            Keys(); yield return Wait(0.3f);
            Assert.That(readability.OutlineFade, Is.EqualTo(0f));
            AssertLayers(enemy, TacticalReadability.Marker.None);
        }

        [UnityTest] public IEnumerator LockedThreatAndComboEachTakeTheirTier()
        {
            var a = Place(enemies[0], 4f, -1.2f);
            var b = Place(enemies[1], 5.5f, 1.6f);
            a.enabled = b.enabled = false;
            Keys(Key.Tab); yield return Wait(0.35f);
            var targeting = squad.GetComponent<TargetingSystem>();
            Assert.That(targeting.LockTarget(b.GetComponent<Health>()), Is.True);
            yield return Wait(0.05f);
            Assert.That(readability.OutlineOf(b), Is.EqualTo(TacticalReadability.Marker.Locked));
            AssertLayers(b, TacticalReadability.Marker.Locked);
            Assert.That(readability.OutlineOf(a), Is.EqualTo(TacticalReadability.Marker.Standard));
            Assert.That(targeting.LockTarget(a.GetComponent<Health>()), Is.True);
            yield return Wait(0.05f);
            Assert.That(readability.OutlineOf(a), Is.EqualTo(TacticalReadability.Marker.Locked), "the lock moves");
            Assert.That(readability.OutlineOf(b), Is.EqualTo(TacticalReadability.Marker.Standard));
            Assert.That(b.GetComponent<ComboOpportunity>().Offer(ComboOpportunityType.Grouped, 3f, squad), Is.True);
            yield return Wait(0.05f);
            Assert.That(readability.OutlineOf(b), Is.EqualTo(TacticalReadability.Marker.Combo));
            AssertLayers(b, TacticalReadability.Marker.Combo);
        }

        [UnityTest] public IEnumerator AWindUpTurnsTheOutlineIntoAThreat()
        {
            var pursuer = enemies.OfType<BasicMeleeEnemy>().First();
            Place(pursuer, 1.3f, 0f);
            float deadline = Time.unscaledTime + 5f;
            while (!pursuer.IsPreparingAttack && Time.unscaledTime < deadline) yield return null;
            Assume.That(pursuer.IsPreparingAttack, "the pursuer winds up next to the leader");
            Keys(Key.Tab); yield return Wait(0.2f);
            Assert.That(readability.OutlineOf(pursuer), Is.EqualTo(TacticalReadability.Marker.Threat));
            AssertLayers(pursuer, TacticalReadability.Marker.Threat);
        }

        [UnityTest] public IEnumerator RepeatedEntriesLeaveNothingBehind()
        {
            var enemy = Place(enemies[0], 4f, 0f);
            enemy.enabled = false;
            for (int i = 0; i < 3; i++)
            {
                Keys(Key.Tab); yield return Wait(0.25f);
                Assert.That(readability.OutlineOf(enemy), Is.EqualTo(TacticalReadability.Marker.Standard));
                Keys(); yield return Wait(0.3f);
                Assert.That(readability.OutlineFade, Is.EqualTo(0f));
                AssertLayers(enemy, TacticalReadability.Marker.None);
            }
        }

        [UnityTest] public IEnumerator EveryArchetypeIsOutlinedIncludingTheBulwarkPlateAndMarksmanBarrel()
        {
            yield return Load("PrototypeEncounter");
            foreach (var enemy in enemies)
                Assert.That(readability.OutlinedRenderers(enemy).Count, Is.GreaterThan(0), enemy.name);
            var bulwark = enemies.First(e => e.Archetype == "BULWARK");
            Assert.That(readability.OutlinedRenderers(bulwark).Any(r => r.name == "Guard Plate"), Is.True);
            var marksman = enemies.First(e => e.Archetype == "MARKSMAN");
            Assert.That(readability.OutlinedRenderers(marksman).Any(r => r.name == "Barrel"), Is.True);
            Assert.That(readability.OutlinedRenderers(marksman).All(r => !(r is LineRenderer)), Is.True, "aim lines stay out");
        }

        [UnityTest] public IEnumerator TheDevelopmentToggleShowsOutlinesWithoutFocus()
        {
            var enemy = Place(enemies[0], 4f, 0f);
            enemy.enabled = false;
            readability.ShowTacticalOutlines = true;
            yield return Wait(0.3f);
            Assert.That(focus.IsActive, Is.False);
            Assert.That(readability.OutlineOf(enemy), Is.EqualTo(TacticalReadability.Marker.Standard));
            readability.ShowTacticalOutlines = false;
            yield return Wait(0.3f);
            Assert.That(readability.OutlineOf(enemy), Is.EqualTo(TacticalReadability.Marker.None));
        }

        private void AssertLayers(EnemyBrain enemy, TacticalReadability.Marker tier)
        {
            uint all = 0;
            foreach (TacticalReadability.Marker t in System.Enum.GetValues(typeof(TacticalReadability.Marker))) all |= readability.OutlineLayerBit(t);
            uint expected = readability.OutlineLayerBit(tier);
            foreach (var renderer in readability.OutlinedRenderers(enemy))
                Assert.That(renderer.renderingLayerMask & all, Is.EqualTo(expected), renderer.name + " " + tier);
        }

        private EnemyBrain Place(EnemyBrain enemy, float ahead, float side)
        {
            var leader = members[0].transform;
            enemy.transform.position = leader.position + leader.forward * ahead + leader.right * side + Vector3.up * 0.08f;
            enemy.gameObject.SetActive(true);
            Physics.SyncTransforms();
            return enemy;
        }

        private IEnumerator Load(string scene)
        {
            yield return SceneManager.LoadSceneAsync(scene);
            squad = Object.FindFirstObjectByType<SquadController>();
            focus = squad.GetComponent<TacticalFocus>();
            readability = Object.FindFirstObjectByType<TacticalReadability>();
            Assert.That(readability, Is.Not.Null, scene);
            Set(squad.GetComponent<TracePlayerInput>(), "captureCursor", false);
            members = squad.Members.ToArray();
            enemies = Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(e => e.name).ToArray();
            if (scene == "Prototype") foreach (var enemy in enemies) enemy.gameObject.SetActive(false);
            foreach (var other in members) if (!other.IsPlayerControlled) other.Companion.enabled = false;
            yield return Wait(0.2f);
        }

        private void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        private static IEnumerator Wait(float duration) { yield return new WaitForSecondsRealtime(duration); yield return null; }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }
}
