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
