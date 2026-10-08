using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TRACE.AI;
using TRACE.Camera;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Input;
using TRACE.Skills;
using TRACE.Tactical;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TRACE.Tests
{
    // Keyboard/mouse and gamepad reach the same gameplay intents; no combat code knows a physical control.
    public sealed class InputBindingTests
    {
        private SquadController squad;
        private TracePlayerInput input;
        private TacticalFocus focus;
        private SquadMember[] members;
        private Keyboard keyboard;
        private Mouse mouse;
        private Gamepad gamepad;
        private float oldCapture;
        private InputSettings.BackgroundBehavior oldBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditor;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            Time.timeScale = 1f;
            oldBackground = InputSystem.settings.backgroundBehavior;
            oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            gamepad = InputSystem.AddDevice<Gamepad>();
            yield return SceneManager.LoadSceneAsync("Prototype");
            squad = Object.FindFirstObjectByType<SquadController>();
            input = squad.GetComponent<TracePlayerInput>();
            focus = squad.GetComponent<TacticalFocus>();
            members = squad.Members.ToArray();
            Set(input, "captureCursor", false);
            foreach (var enemy in Object.FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) enemy.gameObject.SetActive(false);
            yield return Frames(10);
            FreezeCompanions();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (focus != null) focus.enabled = false;
            Time.timeScale = 1f;
            Time.captureDeltaTime = oldCapture;
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            InputSystem.RemoveDevice(gamepad);
            InputSystem.settings.backgroundBehavior = oldBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AssetDeclaresIntentNamedActionsAndBothControlSchemes()
        {
            var asset = (InputActionAsset)typeof(TracePlayerInput).GetField("actions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(input);
            Assert.That(asset.controlSchemes.Select(s => s.name), Is.EquivalentTo(new[] { "KeyboardMouse", "Gamepad" }));
            var player = asset.FindActionMap("Player", true);
            var system = asset.FindActionMap("System", true);
            string[] expectedPlayer = { "Move", "Look", "Attack", "Dodge", "SkillPrimary", "SkillSecondary", "TacticalFocus", "Character1", "Character2",
                "Character3", "Sprint", "Interact", "Ultimate", "TargetLock", "SwitchPrevious", "SwitchNext", "TacticalMap", "TargetLeft", "TargetRight" };
            Assert.That(player.actions.Select(a => a.name), Is.EquivalentTo(expectedPlayer));
            Assert.That(system.actions.Select(a => a.name), Is.EquivalentTo(new[] { "Pause", "Restart" }));
            (string action, string path)[] required =
            {
                ("Move", "<Keyboard>/w"), ("Move", "<Gamepad>/leftStick"), ("Look", "<Mouse>/delta"), ("Look", "<Gamepad>/rightStick"),
                ("Attack", "<Mouse>/leftButton"), ("Attack", "<Gamepad>/buttonWest"), ("Dodge", "<Mouse>/rightButton"), ("Dodge", "<Gamepad>/buttonEast"),
                ("SkillPrimary", "<Keyboard>/e"), ("SkillPrimary", "<Gamepad>/buttonNorth"), ("SkillSecondary", "<Keyboard>/q"),
                ("TacticalFocus", "<Keyboard>/tab"), ("TacticalFocus", "<Gamepad>/leftTrigger"),
                ("Character1", "<Keyboard>/1"), ("Character2", "<Keyboard>/2"), ("Character3", "<Keyboard>/3"),
                ("Sprint", "<Keyboard>/leftShift"), ("Sprint", "<Gamepad>/leftStickPress"), ("Interact", "<Keyboard>/f"), ("Interact", "<Gamepad>/buttonSouth"),
                ("Ultimate", "<Keyboard>/r"), ("Ultimate", "<Gamepad>/rightTrigger"), ("TargetLock", "<Mouse>/middleButton"), ("TargetLock", "<Gamepad>/rightStickPress"),
                ("SwitchPrevious", "<Gamepad>/leftShoulder"), ("SwitchNext", "<Gamepad>/rightShoulder"), ("TacticalMap", "<Gamepad>/select"),
                ("TargetLeft", "<Mouse>/scroll/down"), ("TargetRight", "<Mouse>/scroll/up"),
            };
            foreach (var (action, path) in required)
                Assert.That(player.FindAction(action, true).bindings.Any(b => b.path == path), Is.True, action + " must bind " + path);
            Assert.That(system.FindAction("Pause", true).bindings.Select(b => b.path), Does.Contain("<Keyboard>/escape").And.Contain("<Gamepad>/start"));
            Assert.That(system.FindAction("Restart", true).bindings.Select(b => b.path), Does.Contain("<Keyboard>/backspace"));
            var all = player.bindings.Concat(system.bindings).ToList();
            Assert.That(all.Any(b => b.path == "<Keyboard>/space"), Is.False, "Space stays unassigned");
            Assert.That(all.Any(b => b.path == "<Keyboard>/leftAlt"), Is.False);
            // Every non-composite binding belongs to exactly one scheme, so a rebinding menu can filter per device.
            foreach (var binding in all.Where(b => !b.isComposite))
                Assert.That(binding.groups, Is.EqualTo("KeyboardMouse").Or.EqualTo("Gamepad"), binding.path);
            // One physical control never serves two Player intents.
            var duplicates = player.bindings.Where(b => !b.isComposite).GroupBy(b => b.path).Where(g => g.Select(b => b.action).Distinct().Count() > 1).Select(g => g.Key);
            Assert.That(duplicates, Is.Empty);
            yield return null;
        }

        [UnityTest]
        public IEnumerator KeyboardMouseAttackDodgeSkillSprintAndFocusReachGameplay()
        {
            var motor = members[0].GetComponent<ThirdPersonMotor>();
            var melee = members[0].GetComponent<PlayerMeleeAttack>();
            var skill = members[0].GetComponent<CharacterSkill>();
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return Frames(2);
            Assert.That(melee.IsAttacking, Is.True);
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return Wait(0.6f);
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
            yield return Frames(2);
            Assert.That(motor.IsDodging, Is.True);
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return Wait(1.2f);
            Assert.That(skill.IsReady, Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            yield return Frames(2);
            Assert.That(skill.IsReady, Is.True, "Q is the prepared secondary skill, not the primary one");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Frames(2);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return Frames(2);
            Assert.That(skill.IsReady, Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
            yield return Wait(0.4f);
            Assert.That(input.Sprint, Is.True);
            Assert.That(input.Move.y, Is.GreaterThan(0.9f));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
            yield return Frames(3);
            Assert.That(focus.IsActive, Is.True);
            Assert.That(input.Move, Is.EqualTo(Vector2.zero));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return RealTime(0.3f);
            Assert.That(focus.IsActive, Is.False);
        }

        [UnityTest]
        public IEnumerator GamepadAttackDodgeSkillSprintAndFocusReachTheSameGameplay()
        {
            var motor = members[0].GetComponent<ThirdPersonMotor>();
            var melee = members[0].GetComponent<PlayerMeleeAttack>();
            var skill = members[0].GetComponent<CharacterSkill>();
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.West));
            yield return Frames(2);
            Assert.That(melee.IsAttacking, Is.True);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return Wait(0.6f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.East));
            yield return Frames(2);
            Assert.That(motor.IsDodging, Is.True);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return Wait(1.2f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.North));
            yield return Frames(2);
            Assert.That(skill.IsReady, Is.False);
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = new Vector2(0f, 1f) }.WithButton(GamepadButton.LeftStick));
            yield return Wait(0.4f);
            Assert.That(input.Sprint, Is.True);
            Assert.That(input.Move.y, Is.GreaterThan(0.9f));
            Assert.That(motor.PlanarVelocity.magnitude, Is.GreaterThan(4f), "stick + stick press must sprint");
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftTrigger = 1f });
            yield return Frames(3);
            Assert.That(focus.IsActive, Is.True);
            Assert.That(Time.timeScale, Is.LessThan(1f));
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return RealTime(0.3f);
            Assert.That(focus.IsActive, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator DirectKeysAndBumpersSwitchTheSquad()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit2));
            yield return Frames(2);
            Assert.That(squad.ActiveMember, Is.EqualTo(members[1]));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Wait(0.3f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit1));
            yield return Frames(2);
            Assert.That(squad.ActiveMember, Is.EqualTo(members[0]));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return Wait(0.3f);
            foreach (int expected in new[] { 1, 2, 0 })
            {
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.RightShoulder));
                yield return Frames(2);
                Assert.That(squad.ActiveMember, Is.EqualTo(members[expected]), "next wraps around the roster");
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return Wait(0.3f);
            }
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.LeftShoulder));
            yield return Frames(2);
            Assert.That(squad.ActiveMember, Is.EqualTo(members[2]), "previous wraps backwards");
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return Wait(0.3f);
            members[0].Health.TakeDamage(9999f);
            yield return Frames(2);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.RightShoulder));
            yield return Frames(2);
            Assert.That(squad.ActiveMember, Is.EqualTo(members[1]), "next skips the fallen member");
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return Frames(2);
            Assert.That(squad.SwitchRelative(0), Is.False);
        }

        [UnityTest]
        public IEnumerator RightStickLookIsTimeBasedAndUnscaledLikeTheMouse()
        {
            var orbit = Object.FindFirstObjectByType<ThirdPersonOrbit>();
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = new Vector2(1f, 0f) });
            yield return Frames(2);
            // stickLookSpeed 1500 * mouseSensitivity 0.12 = 180 degrees per unscaled second at full deflection.
            float rate = 0f;
            yield return MeasureYawRate(orbit, 10, r => rate = r);
            Assert.That(rate, Is.EqualTo(180f).Within(25f));
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = new Vector2(1f, 0f), leftTrigger = 1f });
            yield return RealTime(0.3f);
            Assert.That(focus.IsActive, Is.True);
            Assert.That(Time.timeScale, Is.LessThan(0.2f));
            float slowed = 0f;
            yield return MeasureYawRate(orbit, 10, r => slowed = r);
            Assert.That(slowed, Is.EqualTo(rate).Within(25f), "stick look ignores the slowed time");
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return RealTime(0.3f);
            float start = Get<float>(orbit, "yaw");
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(100f, 0f) });
            yield return Frames(2);
            Assert.That(Get<float>(orbit, "yaw") - start, Is.EqualTo(12f).Within(0.5f), "mouse delta is not time scaled");
        }

        [UnityTest]
        public IEnumerator PreparedIntentsAreReadableWithoutGameplaySideEffects()
        {
            var skill = members[0].GetComponent<CharacterSkill>();
            var melee = members[0].GetComponent<PlayerMeleeAttack>();
            var motor = members[0].GetComponent<ThirdPersonMotor>();
            Vector3 position = members[0].transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R, Key.F));
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Middle));
            yield return null;
            Assert.That(input.UltimatePressed, Is.True);
            Assert.That(input.InteractPressed, Is.True);
            Assert.That(input.TargetLockPressed, Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return Frames(2);
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1f }.WithButton(GamepadButton.Select).WithButton(GamepadButton.Start));
            yield return null;
            Assert.That(input.UltimatePressed, Is.True);
            Assert.That(input.TacticalMapPressed, Is.True);
            Assert.That(input.PausePressed, Is.True);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return Frames(3);
            Assert.That(skill.IsReady, Is.True);
            Assert.That(melee.IsAttacking, Is.False);
            Assert.That(motor.IsDodging || motor.IsSkillDashing, Is.False);
            Assert.That(focus.IsActive, Is.False);
            Assert.That(squad.ActiveMember, Is.EqualTo(members[0]));
            Assert.That(Vector3.Distance(position, members[0].transform.position), Is.LessThan(0.05f));
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Prototype"));
        }

        [UnityTest]
        public IEnumerator SystemIntentsStayReadableWhileGameplayInputIsDisabled()
        {
            input.enabled = false;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape, Key.Backspace));
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return null;
            Assert.That(input.PausePressed, Is.True);
            Assert.That(input.RestartPressed, Is.True);
            Assert.That(input.AttackPressed, Is.False);
            Assert.That(input.GameplayInputEnabled, Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null;
            input.enabled = true;
            yield return null;
            Assert.That(input.GameplayInputEnabled, Is.True);
        }

        private void FreezeCompanions()
        {
            foreach (var member in members)
            {
                if (member.IsPlayerControlled || member.Health.IsDead) continue;
                member.Companion.enabled = false;
                member.Companion.Attack.Cancel();
                var agent = member.GetComponent<NavMeshAgent>();
                if (agent.enabled && agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = true; }
            }
        }
        private static IEnumerator MeasureYawRate(ThirdPersonOrbit orbit, int frames, System.Action<float> result)
        {
            float start = Get<float>(orbit, "yaw"), elapsed = 0f;
            for (int i = 0; i < frames; i++) { yield return null; elapsed += Time.unscaledDeltaTime; }
            result((Get<float>(orbit, "yaw") - start) / Mathf.Max(elapsed, 0.0001f));
        }
        private static IEnumerator RealTime(float seconds) { yield return new WaitForSecondsRealtime(seconds); yield return null; }
        private static IEnumerator Wait(float seconds) { float until = Time.time + seconds; while (Time.time < until) yield return null; }
        private static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
        private static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static T Get<T>(object target, string field) => (T)target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }
}
