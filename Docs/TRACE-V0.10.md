# TRACE V0.10 — Target Lock manuel et caméra de combat

## Utilisation

Dans `Prototype` ou `PrototypeEncounter` : **clic molette** (R3 à la manette) verrouille la cible la plus pertinente,
une seconde pression retire le lock. Sans lock, rien ne change : le soft targeting V0.2 décide. Avec lock :

- le personnage contrôlé fait face à la cible et peut avancer, reculer, strafer et tourner autour d'elle (déplacement
  toujours relatif à la caméra, pas de tank controls) ;
- l'esquive suit l'input (gauche, droite, avant) ; sans direction, elle recule, donc s'éloigne de la cible ;
- attaque de mêlée, tir de Support, Dash Strike et Gravity Field visent la cible verrouillée si elle est à portée et visible,
  sinon leur ciblage habituel reprend ;
- **molette haut / bas** (clavier) ou **impulsion du stick droit** droite / gauche (manette) passe à l'ennemi voisin
  du côté demandé, dans l'espace écran ;
- la caméra dérive doucement vers la cible quand le joueur ne touche pas à la vue ; souris et stick gardent toujours la main ;
- le lock survit au switch 1/2/3 (il appartient à la squad) et reste utilisable pendant Tactical Focus, où la fiche de
  la cible porte `[LOCK]`.

Un losange blanc billboard marque la cible verrouillée, distinct des anneaux jaune/cyan des Combo Opportunities.

## Paramètres (composant **Targeting System** sur Squad)

| Paramètre | Valeur | Rôle |
| --- | --- | --- |
| Max Lock Distance | 22 m | portée d'acquisition ; le lock est retiré au-delà de 110 % |
| Lost Sight Grace Period | 1,25 s | occlusion tolérée avant retrait |
| Screen Margin | 0,12 | candidats légèrement hors viewport encore acceptés |
| Center Weight / Distance Weight | 1 / 0,5 | poids du scoring |
| Stick Switch Threshold | 0,6 | seuil d'impulsion du stick droit ; réarmement sous 60 % du seuil |
| Switch Cooldown | 0,3 s réelles | anti-spam du changement de cible |
| Show Debug | off | gizmos : portée, lignes de visibilité, scores, cible |

Caméra (**Third Person Orbit**) : `Lock Assist Speed` 90°/s réels au maximum, `Lock Assist Delay` 0,25 s sans input
de vue avant reprise, `Lock Pitch` 18°.

## Scoring des cibles

À l'acquisition, les colliders `Damageable` dans 22 m autour du membre actif sont réduits à un `Health` vivant et actif
(membres de squad exclus). Un candidat doit être devant la caméra et dans le viewport élargi de la marge. Score, plus bas
est meilleur : `1,0 × (distance au centre écran / 0,5) + 0,5 × (distance / 22 m)`. Un candidat masqué (linecast depuis
les yeux du membre actif, couche Default) reçoit +10 et n'est jamais acquis. L'ennemi physiquement le plus proche ne gagne
donc que s'il est aussi proche du centre de l'écran.

Changement de cible : parmi les candidats visibles, ceux dont l'abscisse viewport est du côté demandé par rapport à la cible
courante ; on retient le coût minimal `|Δx| + 0,5 × |Δy|`. Pas de rotation en bout de liste.

Invalidation automatique : mort, désactivation, distance > 24,2 m, occlusion > 1,25 s, membre actif absent. Jamais de
transfert automatique vers une autre cible.

## Caméra

Ajustement limité à `ThirdPersonOrbit` : une dérive du yaw vers la direction membre → cible, vitesse proportionnelle à
l'écart angulaire (plancher 15 %), uniquement après 0,25 s sans input de vue ; le pitch tend vers 18° à 20 % de cette
vitesse. Tout input souris/stick s'applique intégralement et suspend la dérive. Aucun changement de distance, de
Cinemachine ni de collision caméra.

## Horloges

La dérive caméra, le réarmement et le cooldown du stick utilisent le temps réel (compatibles Tactical Focus).
La grace period d'occlusion utilise le temps de jeu, comme les cooldowns.

## Fichiers V0.10

Créés : `Assets/TRACE/Combat/TargetingSystem.cs`, `Assets/TRACE/Editor/PrototypeTargetLockSetup.cs`,
`Assets/TRACE/Tests/PlayMode/TargetLockTests.cs`, `Assets/TRACE/Scenes/Materials/TargetLock.mat`,
`Docs/Validation/TargetLockVisualCapture.cs`.

Modifiés : `TRACEInput.inputactions` et `TracePlayerInput.cs` (`TargetLeft` / `TargetRight` sur la molette,
`TargetSwitchDirection`, `StickLook`), `ThirdPersonMotor.cs` (`FacingTarget`), `ThirdPersonOrbit.cs` (assistance),
`PlayerMeleeAttack.cs`, `PlayerRangedAttack.cs`, `CharacterSkill.cs`, `DashStrike.cs`, `GravityFieldSkill.cs`
(priorité au lock via `LockedWithin`), `TacticalOverlay.cs` (`[LOCK]`), `InputBindingTests.cs`, scènes `Prototype`
et `PrototypeEncounter` (composant et marqueur), `Docs/TRACE-Input.md`.

Aucun ADS, aim assist avancé, auto-lock, nouveau skill ni nouvel ennemi.

## Validation

Copie isolée `Logs/v09-project` (réutilisée), projet principal ouvert dans l'Editor.

| Étape | Résultat | Preuve |
| --- | --- | --- |
| Baseline avant V0.10 | 173/173 (V0.1–V0.9 + bindings) | `Validation/TRACE-Input-PlayMode.xml` |
| Suite finale | 185/185 (173 précédents + 12 Target Lock) | `Validation/TRACE-V0.10-PlayMode.xml`, `Logs/v10-tests-final.log` |
| Scénario graphique | 1/1, trois captures 1280x720 | `Validation/TRACE-V0.10-Visual.xml`, `Validation/TargetLock-*.png` |

Aucun warning ni erreur C#. Captures : `TargetLock-locked.png` (losange sur l'ennemi au centre de l'écran, pas sur le plus
proche), `TargetLock-strafe.png` (cible changée à la molette, strafe à gauche, caméra ayant dérivé et relevé le pitch),
`TargetLock-focus.png` (Tactical Focus avec fiche `[LOCK]`).

Les tests V0.10 couvrent : acquisition centrée vs proximité, toggle et retour au soft targeting, mort / désactivation /
portée, cibles masquées et grace period, molette et impulsion stick (réarmement, cooldown, pas de rotation en bout de liste),
mêlée, Dash Strike, Gravity Field et tir de Support vers la cible verrouillée, lock conservé au switch avec transfert de
l'orientation, Tactical Focus (lock, changement, retrait, fiche), dérive caméra cédant à la souris, strafe face à la cible
et esquive selon l'input.

Pendant la validation, six échecs initiaux venaient de la géométrie des tests (ennemis placés dans « Obstacle Tall » ou
« Duel Obstacle West », téléportation hors arène, caméra de test traversant le mur d'angle) ; le code n'a pas changé,
hormis le décalage du marqueur vers la caméra pour qu'il ne soit pas masqué par le corps de la cible.

## Vérification manuelle

1. Verrouiller à la molette un ennemi au centre de l'écran, tourner autour avec A/D : le personnage reste face à lui.
2. Laisser la souris : la caméra dérive sans à-coup ; bouger la souris : elle obéit immédiatement.
3. Molette haut/bas et impulsion du stick droit : la cible voisine du bon côté est choisie.
4. Passer derrière un obstacle plus de 1,25 s : le lock tombe, le soft targeting reprend sans action supplémentaire.
5. Switch 1/2/3 sous lock, puis Tab : fiche `[LOCK]`, changement de cible et retrait possibles pendant le ralenti.
6. Support verrouillé sur un ennemi hors cône : le tir part vers lui. Assault : Dash Strike va à la cible verrouillée.
