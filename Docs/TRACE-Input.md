# TRACE — Mapping des entrées (clavier/souris + manette)

Asset : `Assets/TRACE/Input/TRACEInput.inputactions`. Deux control schemes, `KeyboardMouse` (`<Keyboard>` + `<Mouse>`)
et `Gamepad` (`<Gamepad>`, layout Xbox générique). Chaque binding appartient à exactement un scheme, ce qui prépare un menu
de rebinding filtré par périphérique. Les actions sont nommées par intention ; aucun script gameplay ne lit une touche physique.

## Map `Player`

| Action | Clavier / souris | Manette | Consommateur |
| --- | --- | --- | --- |
| Move | WASD (composite 2D) | Left Stick | `ThirdPersonMotor` |
| Look | Mouse Delta | Right Stick | `ThirdPersonOrbit` via `TracePlayerInput.Look` |
| Attack | clic gauche | X / West | `PlayerMeleeAttack`, `PlayerRangedAttack` |
| Dodge | clic droit | B / East | `ThirdPersonMotor` |
| SkillPrimary | **E** (anciennement Q) | Y / North | `CharacterSkill` (`SkillPressed`) |
| SkillSecondary | Q | — | préparée, `SkillSecondaryPressed`, aucun effet |
| TacticalFocus | Tab maintenu | Left Trigger maintenu | `TacticalFocus` |
| Character1 / 2 / 3 | 1 / 2 / 3 | — | `SquadController.SwitchToMember` |
| SwitchPrevious / SwitchNext | — | LB / RB | `SquadController.SwitchRelative` (cyclique, membres vivants) |
| Sprint | Left Shift (Right Shift conservé) | Left Stick Press | `ThirdPersonMotor` |
| Interact | F | A / South | préparée, `InteractPressed` |
| Ultimate | R | Right Trigger | préparée, `UltimatePressed` |
| TargetLock | clic molette | Right Stick Press | V0.10 : `TargetingSystem` (toggle lock) |
| TargetLeft / TargetRight | molette bas / haut | impulsion stick droit (lue dans `StickLook`) | V0.10 : changement de cible verrouillée |
| TacticalMap | — | Select / View | préparée, `TacticalMapPressed` |

`Space` reste libre. `Left Alt` n'est plus utilisé.

## Map `System`

Activée dès `Awake` et indépendante de l'état du composant : elle reste lisible quand le gameplay est coupé (défaite, menus).

| Action | Clavier | Manette | Effet actuel |
| --- | --- | --- | --- |
| Pause | Escape | Start / Menu | libère le curseur (comportement existant, auparavant lu directement sur `Keyboard.current`) |
| Restart | Backspace | — | `EncounterController.Restart` (debug V0.9) |

## Conflits trouvés et décisions

- **Q → E.** La compétence était sur Q ; le mapping demandé met `SkillPrimary` sur E et réserve Q à `SkillSecondary`.
  Les textes d'aide (`E : Skill`, `1 puis E : DASH`, `1 + E`), les tests V0.6–V0.9 et les scripts de capture pressent désormais E.
- **R.** `R` est réservé à `Ultimate` ; le redémarrage V0.9 (`R` dans sa spécification) passe sur Backspace via l'action `Restart`.
- **Escape.** La libération du curseur passe par l'action `Pause` ; le clic de recapture passe par l'action `Attack` (plus de `Mouse.current`).
- **Stick droit.** La caméra consomme des deltas souris. `TracePlayerInput.Look` convertit un stick (taux) en delta par seconde
  non scalé (`Stick Look Speed` = 1500 unités/s, soit 3°/frame à 60 Hz à plein débattement) : la caméra reste identique et
  non ralentie pendant Tactical Focus. Aucune modification de `ThirdPersonOrbit`.
- **Right Shift.** Binding préexistant de Sprint, conservé car sans conflit.
- **Changement automatique de scheme.** Le projet n'utilise pas `PlayerInput` : les bindings des deux schemes sont actifs
  simultanément sur l'instance privée de l'asset ; rien ne bascule, rien à vérifier. Un futur `PlayerInput` ou menu de
  rebinding pourra s'appuyer sur les groupes de bindings déjà posés.
- Aucun contrôle physique ne sert deux intentions du map `Player` (vérifié par test).

## Tests

`Assets/TRACE/Tests/PlayMode/InputBindingTests.cs` (scène `Prototype`) : structure de l'asset et des schemes, Space libre,
unicité des contrôles ; attaque / esquive / compétence / sprint / Tactical Focus au clavier-souris et à la manette ;
switch direct 1/2/3 et cyclique LB/RB avec membre tombé ; look stick temporisé et non ralenti, look souris inchangé ;
intentions préparées lisibles sans effet gameplay ; map `System` lisible gameplay coupé.

## Validation

Suite PlayMode complète dans `Logs/v09-project` après la standardisation : **173/173** (166 V0.1–V0.9 + 7 bindings),
sans warning ni erreur C# (`Docs/Validation/TRACE-Input-PlayMode.xml`, `Logs/input-tests-final.log`).
Deux hypothèses de test ont été corrigées pendant la validation : en batch, `Time.unscaledDeltaTime` suit le temps réel même
quand `captureDeltaTime` est fixé, donc la sortie de Focus et le débit du stick se mesurent en temps réel. Aucune capture
graphique n'est nécessaire pour cette tâche. Restent manuels : le ressenti de la sensibilité du stick (`Stick Look Speed`
sur `TracePlayerInput`) et la vérification avec une vraie manette Xbox.
