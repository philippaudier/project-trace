# Project TRACE — Unity context

Baseline: `a2c2fa6`, inspected 2026-10-07. Project root: `/home/philippe/Project-TRACE`.

Confirmed from ProjectSettings/ProjectVersion.txt and Packages/manifest.json:
Unity 6000.3.10f1, URP 17.3.0, Input System 1.18.0 (activeInputHandler=1),
Unity Test Framework 1.6.0. Initial project is the URP template, with SampleScene
as its only build scene and no first-party gameplay assemblies or tests.
No networking gameplay or Unity MCP bridge is configured. Editor executable is
installed and the project is open; validate in an isolated temporary copy.

V0.1 scope: keyboard/mouse third-person locomotion, sprint, orbit and camera
collision, a serialized prototype playground. No combat, squad, AI or tactical
slowdown. Add only the requested Cinemachine package and its dependencies.
Use concrete MonoBehaviours with explicit serialized references, TRACE namespaces,
private serialized camelCase fields, no global services or singleton.

Implementation lives in Assets/TRACE. Runtime assembly references Input System
and Cinemachine. Editor authoring is separate; PlayMode tests are test-only.
Prototype scene becomes the first build scene; template scene remains available.
Input owns a private copy of the action asset and its enable/disable lifetime.
Motor owns movement; orbit owns a separate camera target; Cinemachine owns camera
placement and obstacle avoidance. Configuration remains on components.

Sources: Packages/{manifest,packages-lock}.json, ProjectSettings/{ProjectVersion,
ProjectSettings,EditorBuildSettings,GraphicsSettings}.asset/txt, Assets tree.
Runtime feeling and visual quality require a human playtest; automated validation
and remaining limitations are recorded in Docs/TRACE-V0.1.md.

## V0.2 extension (2026-10-07)

V0.1 was manually validated by the user: feeling approved, all controls working,
Console clean. V0.2 adds Health, PlayerMeleeAttack and EnemyDummy in Combat,
a left-click Attack binding and three static dummies in the existing scene.
Health is independent; feedback subscribes to its damage/death events.
Damageable layer filters hits; Default blocks line of sight. No packages, AI,
combos, team system or generalized damage framework added. See Docs/TRACE-V0.2.md
for parameters and validation. Keep the user's pre-existing ShaderGraphSettings
change and all V0.1 working-tree files; they were not committed at task start.

## V0.3 extension (2026-10-07)

V0.1/V0.2 are user-validated; their 19 tests pass at V0.3 baseline.
BasicMeleeEnemy uses a local Idle/Chase/Attack/Dead state and the already-installed
AI Navigation package. Duel Area at (-11, 0, 11) owns a baked NavMeshSurface;
new colliders must be Physics.SyncTransforms-ed before editor-time baking.
Health stays unchanged. DamageReceiver gates incoming attacks during dodge
i-frames. ThirdPersonMotor owns both locomotion and dodge displacement, so only
one component calls CharacterController.Move. PlayerMeleeAttack cancels on dodge.
PlayerHealthFeedback handles hit/i-frame colors and disables controls on death.
EnemyDummy is reused as the moving enemy's visual damage/death feedback.
No new packages or generic AI/status framework. See Docs/TRACE-V0.3.md for current
parameters, manual playtest steps and exact validation results.

## V0.4 extension (2026-10-07)

Current workspace is Windows: C:/Users/Philippe/Documents/Programming/Unity/Projects/project-trace.
Editor: C:/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Unity.com.
No Unity MCP tool is available. Batch validation runs in Logs/v04-project;
direct batch launch on the original project exited with code 1 before import.
Baseline: f290696, 34/34 PlayMode tests passing. Pre-existing EditorBuildSettings,
ShaderGraphSettings and .vscode changes were preserved.

PrototypeSquad is the new first build scene, copied from Prototype, which stays
unchanged for V0.1–V0.3 regression tests. The new scene has a whole-terrain baked
SquadNavMesh, one player leader, green melee / blue ranged companions, three
BasicMeleeEnemy and a wider initial camera framing. No new packages or input.
SquadController owns explicit member references and formation. CompanionController
owns NavMesh navigation, target retention, attacks and prototype recovery.
Health/DamageReceiver are reused unchanged; CompanionFeedback keeps dead bodies
visible. BasicMeleeEnemy has an optional squad reference for multi-unit targeting;
its fixed-target V0.3 behavior is preserved when absent. No character switching.

See Docs/TRACE-V0.4.md for tuning and manual checks. Final validation: 53/53
production PlayMode tests (34 previous + 19 squad) and one additional graphical
capture scenario; no C# warnings/errors or unexpected gameplay logs. The user's
free-play comfort and current Editor Console remain manual checks.

## V0.5 extension (2026-10-07)

V0.4 is user-validated. PrototypeSwitch is the new first build scene; Prototype
and PrototypeSquad remain regression fixtures. SquadController optionally owns
three SquadMember references, ActiveMember and zero-based SwitchToMember(index).
One shared TracePlayerInput on Squad serves the enabled player motor/attack only.
SquadMember transfers movement authority between ThirdPersonMotor and NavMeshAgent;
the controlled actor's CompanionController/agent are disabled. All three members
share CharacterController, Health, DamageReceiver and ThirdPersonMotor.

TargetedAttack extracts V0.4 companion strikes and also serves PlayerRangedAttack.
PlayerMeleeAttack remains shared by members 1/2. Attack cooldowns survive transfers;
windups/dodges are cancelled. ThirdPersonOrbit retargets over 0.18s unscaled while
preserving angles; switch cooldown is 0.20s. Yellow ring indicates active member.
Death auto-selects the first survivor; three dead means IsDefeated and input stop.

The user's pre-existing Dodge binding is RIGHT MOUSE, preserved. Character1/2/3
now bind to top-row 1/2/3. Baseline 46/53 passed, with seven older tests still
simulating Space. DuelTests' input helper now also emits right mouse for dodge;
its assertions were retained. Final: 75/75 PlayMode tests (53 previous + 22 new)
and one graphical scenario passed in Logs/v05-project, without C# warnings/errors
or unexpected gameplay logs. See Docs/TRACE-V0.5.md for evidence and manual checks.
User modifications in Mobile_RPAsset, Prototype scene, ShaderGraph settings,
.vscode and SceneTemplateSettings were preserved. No V0.6 work.

## V0.6 extension (2026-10-07)

V0.5 is user-validated. Prototype now contains the switchable squad, three skills,
four BasicMeleeEnemy and a separate SkillsNavMesh. Original Prototype content was
snapshotted as PrototypeLegacy for unchanged V0.1–V0.3 test assertions; the existing
environment and main camera remain in the upgraded scene. PrototypeSquad and
PrototypeSwitch stay unchanged. The user's selection-pulse timing edit is retained.

CharacterSkill owns active-player gating and per-member cooldown timestamps;
concrete DashStrike / GravityFieldSkill / PulseShield own effects. Skill binds Q.
ThirdPersonMotor remains the sole player movement owner and integrates a 0.2s dash
through CharacterController.Move. Switch cancels an ongoing dash without refund.
GravityField is a scene-owned reusable zone; EnemyGravityResponse temporarily
changes agent.speed and attracts with NavMeshAgent.Move constrained by its raycast.
DamageReceiver now consults an optional Shield after i-frames and before unchanged
Health. Fields and applied shields survive transfer/caster death until expiration.
No autonomous AI skill use, generic buff framework, new packages or V0.7 systems.

Inspector defaults in the scene: dash 6m / 35 damage / 5s cooldown; field radius3m /
duration3s / slow40% / pull1.2m/s / cooldown8s; shield30HP / duration5s / cooldown10s.
SkillHud displays all cooldowns, HP and shield amount via prototype IMGUI.

Validation in Logs/v06-project: baseline75/75; final98/98 (23 new); graphical1/1.
No C# warnings/errors or unexpected gameplay logs in final runs. One V0.5 combat
test's setup now places the whole squad near enemies instead of only member1;
previous assertions are preserved. See Docs/TRACE-V0.6.md and Docs/Validation.
World-space effects were visually inspected; HUD readability, balance and user
Editor Console remain manual checks. No player build or profiling performed.

## V0.7 extension (2026-10-07)

V0.6 is user-validated. Prototype's existing four-enemy encounter now has optional
ComboOpportunity and ComboFeedback components on its seven actors. ComboPrompt on
Squad selects one world-space instruction to prevent overlapping labels; all ready
markers remain visible. No auto-switch, hit-stop, camera change or new input.

GravityField observes 0.6s continuous exposure then offers Grouped for 2.5s, once per
target per cast. Player Dash Strike consumes it at a valid hit for 1.5x damage (52.5)
and 17.5 splash within 2m, without chain consumption. Shield accumulates 20 absorbed
damage per application then offers Protected for 3s on that specific bearer. A player
melee/ranged hit or normal dash consumes it for 1.2x damage. Grouped takes precedence
over Protected on the same dash, preserving Protected rather than stacking bonuses.
Companion attacks consume neither; missed/cancelled/immune hits preserve windows.

Health stays unchanged. Small ComboDamage helper reuses DamageReceiver. Opportunity
consumption has 0.5s rearm delay; pause freezes timers, death/disable clears state.
Feedback is a 0.35s expanding luminous ring and a single billboard text instruction.
See Docs/TRACE-V0.7.md for Inspector fields, evidence and manual checks. Baseline 98/98;
final 123/123 (25 new) and graphical 1/1 passed without C# warnings/errors or unexpected
gameplay logs. Reports and inspected world captures live in Docs/Validation. Pre-existing
PrototypeSwitch camera edits and the user's selection pulse timing are preserved.
No V0.8 work, new package, player build or profiling.

## V0.8 extension (2026-10-07)

V0.7 is user-validated. Prototype now has TacticalFocus on Squad: Tab held (user rebound it from Left Alt),
factor 0.15, entry/exit 0.15 real seconds. Scene-local time ownership snapshots
timeScale/fixedDeltaTime and stores Unity's read-back values after writes (the
fixed timestep is internally quantized). Disable/unload/input loss/application
focus loss/active-member death restores owned values and requires key release.
External time writes have priority; no global TimeManager. V0.7 has no hit-stop.

Player movement/sprint/attack/dodge/skill input is gated while focus is active.
Entry cancels action movement and attack windups without refunding cooldowns.
Release immediately re-enables actions; blocked presses are not queued. Existing
fields/shields and autonomous AI continue in scaled time. Mouse orbit and
Cinemachine damping remain unscaled; the Prototype brain has IgnoreTimeScale set.
The 0.2s switch throttle uses real time during Focus and retains its prior scaled
clock outside Focus. Camera follows switches while Tab remains held.

TacticalOverlay uses the already-installed UGUI package, a screen-space camera
Canvas, subtle tint, three roster cards and enemy cards filtered by range (18m),
viewport and obstacle line of sight. TacticalTelegraph draws committed enemy
attack footprint/direction/victim during windup/active phases only in Focus.
Legacy SkillHud and detailed ComboPrompt labels hide during Focus; combo rings
remain visible. All gameplay cooldowns and combo/effect durations remain scaled.
Four existing enemies, NavMesh and previous scenes are retained. No new packages,
orders, action queue, skill wheel, audio processing or V0.9. See Docs/TRACE-V0.8.md
for evidence, file inventory, Inspector settings and manual comfort checks.

Validation in Logs/v08-project: baseline 123/123; final 146/146 (23 new); graphical 1/1
with three captures in Docs/Validation. No C# warnings/errors or exceptions. Batch
frames last ~1 ms, so transition tests sample real time, not frame counts. In batch
capture runs the window is unfocused: set Input System backgroundBehavior before
AddDevice, or the virtual device stays disabled. Mouse comfort and the user's Editor
Console remain manual checks. No player build or profiling performed.

## V0.9 extension (2026-10-07)

V0.8 is user-validated; the user rebound TacticalFocus to Tab (tests/docs follow).
PrototypeEncounter is the new first build scene, authored from Prototype by
PrototypeEncounterSetup; Prototype, PrototypeLegacy, PrototypeSquad and PrototypeSwitch
stay unchanged as fixtures. EnemyBrain is the abstract read-only base (archetype, target,
preparation, status label) of BasicMeleeEnemy (PURSUER / BULWARK by serialized archetype)
and MarksmanEnemy. CompanionController and TacticalOverlay target any EnemyBrain;
EnemyGravityResponse requires only NavMeshAgent and Health.

ComboDamage.Apply takes the attacker origin; FrontalGuard (Bulwark) multiplies frontal
hits (±60°) by 0.35 and shows BLOQUE / FLANC ! labels and a ground arc during Focus.
MarksmanEnemy aims 1.1s with a world line at the controlled member, then launches one
reusable straight Projectile (14 damage, 11 m/s, sphere-cast on layer 2 and Default).
EncounterController owns pre-placed inactive enemies in three serialized waves, spawn
rings (1s), 3s inter-wave delay, +20 HP heal, victory/defeat, R or button restart by
scene reload, and debug statistics. EncounterHud and ThreatIndicator are IMGUI; hints are
computed in LateUpdate so headless tests can assert them. No new package, player skill,
orders, boss, loot or XP. See Docs/TRACE-V0.9.md for tuning, positions and manual checks.

## Input standardization (2026-10-07, after V0.9)

TRACEInput.inputactions has Player and System maps and two control schemes (KeyboardMouse,
Gamepad); every binding carries exactly one group. Player intents: Move, Look, Attack, Dodge,
SkillPrimary (E / North; was Skill on Q), SkillSecondary (Q, prepared), TacticalFocus (Tab /
Left Trigger), Character1-3, SwitchPrevious/Next (LB/RB -> SquadController.SwitchRelative),
Sprint (Left Shift, Right Shift kept, Left Stick Press), Interact (F / South), Ultimate
(R / Right Trigger), TargetLock (Middle Mouse / Right Stick Press), TacticalMap (Select).
System: Pause (Escape / Start, releases the cursor), Restart (Backspace, encounter debug).
The System map is enabled in Awake and stays readable while the component is disabled.
TracePlayerInput.Look scales a gamepad stick by stickLookSpeed * unscaledDeltaTime so the
camera keeps one delta-based, unscaled code path. No PlayerInput component: both schemes are
active at once, no scheme switching. Space is unassigned. See Docs/TRACE-Input.md.

## V0.10 extension (2026-10-08)

V0.9 and the input standardization are user-validated. TargetingSystem on Squad (both
Prototype and PrototypeEncounter, authored once by PrototypeTargetLockSetup) owns the squad-wide
manual lock: MMB / R3 toggles, mouse wheel (TargetLeft/TargetRight) or a right-stick impulse
(threshold 0.6, re-arm below 60 %, 0.3 s real cooldown) switches to the screen-space neighbour.
Scoring: center-screen distance / 0.5 + 0.5 * distance / 22 m, hidden targets never acquired,
release on death, deactivation, > 24.2 m or > 1.25 s occlusion; no auto-transfer. Consumers find
the system through their serialized TracePlayerInput and call LockedWithin(actor, range,
obstructions), falling back to the untouched soft targeting: PlayerMeleeAttack (swing may turn
90 degrees), PlayerRangedAttack (bypasses the aim cone), DashStrike, GravityFieldSkill.
ThirdPersonMotor.FacingTarget keeps the active member facing the target with camera-relative
movement; ThirdPersonOrbit drifts yaw toward the target at <= 90 deg/s real time only after
0.25 s without look input, pitch toward 18 deg. TacticalOverlay tags the locked card [LOCK].
White billboard diamond marker (TargetLock.mat). No ADS, aim assist, auto-lock, skill or enemy.

## V1 First Trace (2026-10-08)

V0.10 is user-validated; the user installed ProBuilder 6.0.9 (and the legacy post-processing
package came along), neither is used: the slice is primitives. FirstTrace.unity is the first
build scene, authored once by FirstTraceSetup from PrototypeEncounter (squad, camera, encounter,
overlay, lock and the nine enemies kept; prototype environment removed). Level along +z: dock
(z -10..12), passage with badge alcove (12..31), terminal A and Door Clue (31), pillared hall with
Door Event (61), red tension corridor (61..73), arena (73..100) with gates A/B (z 100) and C
(x 16.5) opening on enemy pockets; terminal B at (-15, 78). NavMesh baked with doors disabled.
TRACE.Narrative: Interactable (prompt/range/one-shot/event), InteractionController on Squad
(Interact intent, hidden while a dialogue plays), DialogueRunner (real-time auto-advance, F skips),
GateDoor, FlickerLight, AmbienceController (code-generated placeholder clips, guarded for
headless runs), StoryDirector (phases Arrival/Clue/Hall/Tension/Combat/Conclusion/End, distance
zones, heals + ResetCooldown + encounter.Begin at the arena, gates opened per wave by proximity,
end panel with restart / prototype / stay). EncounterController has autoStart (false here) and
Begin(). Lighting: cold sun, linear fog, URP Volume (vignette, color adjustments, bloom). No
quest, save, loot, RPG progression, boss or new mechanic. See Docs/TRACE-V1-FirstTrace.md.

## Tracewalker V0.1 (2026-10-08)

Member 1 (former Assault placeholder) is the Tracewalker, dressed by TracewalkerSetup.Apply
(idempotent, menu TRACE/Apply Tracewalker V0.1) in Prototype, PrototypeEncounter and FirstTrace:
composed primitive body under "Tracewalker Visual" (charcoal, shadow, cool grey, off-white, amber),
ORIGIN mark on the left brassard (plate + two leaning bars + amber stroke), Trace Module on the
right chest whose amber lens is driven by TraceModule (reads TacticalFocus only: 0.8 idle, 2.6 with
a 2.5 Hz pulse while held, real-time response), short blade in the right hand. CharacterProfile
(characterId/displayName/designation/archetype/accentColor) on member 1 only; SkillHud and
TacticalOverlay label a profiled member by name in its accent colour, others keep the role labels.
CompanionFeedback targets the torso renderer; the prototype Facing Marker is removed. Materials in
Scenes/Materials/Tracewalker (M_Tracewalker_*). No gameplay, skill, stat or dialogue change (slice
speakers still say ASSAULT). See Docs/TRACE-Tracewalker-V0.1.md.
Procedural animation: TracewalkerPuppet (LateUpdate, order 50) rotates pivots Hip L/R, Upper Body,
Shoulder L/R (blade on the right, ORIGIN mark on the left) and Head from measured speed, melee /
companion attack, dodge, dash, Health.OnDamaged flinch and Tactical Focus (left hand to the module);
it stops on death so CompanionFeedback keeps the laid-down pose. No Animator or clips. 204/204 tests.

## Control V0.1 (2026-10-08)

Member 2 is CONTROL, ORIGIN Field Specialist (not a Tracewalker), dressed by ControlSetup.Apply
(menu TRACE/Apply Control V0.1, idempotent) in the three playable scenes: slim vertical primitive
body under "Control Visual", asymmetric layered coat (long off-white left panel with a cyan edge,
short charcoal right panel), long dark hair with a cyan strand, cyan collar/cuffs/lines, Field
Control Module on the back (FieldControlModule brightens its cyan core while GravityFieldSkill.Field
is deployed; no Tactical Focus reaction), hip and chest lenses, Gravity Staff (1.9 m, ring head)
under the right shoulder, ORIGIN mark on the right shoulder plate (amber only there, on a module
band and a grip ring). Gravity Field rings use M_Control_Field (cyan unlit) and FieldVisualPulse
(counter-rotation, width pulse, game time); mechanics untouched. CharacterProfile has
affiliation/isTracewalker; CharacterPuppet (renamed from TracewalkerPuppet, same GUID) gains
breathing and focusGesture, tuned calmer for Control. SquadVisualKit holds the shared editor
helpers (Part, Pivot, OriginMark, Mat, Unlit, Profile, Puppet). See Docs/TRACE-Control-V0.1.md.

## HUD V0.1 (2026-10-08)

UGUI HUD built per scene by HudSetup.Apply (menu TRACE/Apply HUD V0.1, idempotent) on a
screen-space-camera canvas (sorting 90, under the Tactical Overlay at 100). TRACE.UI: HudRoot
(derives Exploration/Combat/Focus from focus, encounter state and engaged enemies within 18 m with
2.5 s linger; picks the target: hard lock, then soft attack target, then nearest enemy engaging the
active member; ticks the panels), HudPanel base (CanvasGroup fade + slide, real time), panels
ActiveOperator, SquadStatus, Mission (story phase objectives + encounter waves), Target (LOCK vs
TARGET), InteractionPrompt, Reticle, ThreatIndicatorView (renders ThreatIndicator.Hints),
TacticalFocusOverlay (brackets, ANALYSIS block, global Volume HudFocusGrade weight). Palette:
charcoal panels, off-white, ORIGIN amber, analysis cyan, warning red. CharacterProfile.portrait
sprites in UI/Portraits (Support placeholder profile added). Legacy IMGUI handed over via flags
(SkillHud disabled, InteractionController.legacyPrompt, ThreatIndicator.legacyGui,
EncounterHud.showWaveBox). Cameras render post-processing for the focus grade. See
Docs/TRACE-HUD-V0.1.md.

## Support V0.1 (2026-10-09)

Member 3 is SUPPORT, ORIGIN Field Specialist (not a Tracewalker), dressed by SupportSetup.Apply
(menu TRACE/Apply Support V0.1, idempotent): stable stance (±0.12), enveloping off-white coat (two
long side panels, two front flaps with mint edges, hood, sleeves), long light-brown hair with mint
ribbons, mint collar/cuffs/lines, circular Support Module on the back (SupportModule brightens its
mint core while any member's Shield is active; no Tactical Focus reaction), stabilization staff held
close with a wide double emitter ring, small ORIGIN mark on the left shoulder. Pulse Shield halos on
all members use M_Support_Shield (mint unlit) with a slow FieldVisualPulse; mechanics untouched.
Profile origin_support_01 / Defensive Support / mint, portrait cropped from the board into
UI/Portraits/Portrait_Support.png (same GUID as the former placeholder). Calmest puppet tuning.
See Docs/TRACE-Support-V0.1.md.
