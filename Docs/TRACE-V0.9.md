# TRACE V0.9 — Première rencontre structurée

## Utilisation

Ouvrir `Assets/TRACE/Scenes/PrototypeEncounter.unity` (première scène de build), Play, cliquer dans la Game View.
La rencontre démarre seule après 2 s. Trois vagues s'enchaînent ; chaque vague est annoncée 1 s par des anneaux
orange au sol aux positions d'arrivée, puis les ennemis s'activent. Entre deux vagues : 3 s de pause, aucun ennemi,
et **+20 HP** pour chaque membre vivant (valeur `Inter Wave Heal` sur le composant **Encounter Controller**).

- Victoire : `ENCOUNTER COMPLETE` quand la vague 3 est éliminée. Défaite : `SQUAD DEFEATED` quand les trois membres tombent.
- **Retour arrière (Backspace)** ou le bouton `Recommencer` recharge la scène, à tout moment. `R` est réservé à l'action `Ultimate` (tâche de mapping qui suit la V0.9).
- Les commandes V0.1–V0.8 sont inchangées : clic gauche, clic droit (esquive), compétence (Q à la livraison V0.9, **E** après la standardisation des entrées), 1/2/3, **Tab** (Tactical Focus).
- En haut à droite : vague courante et ennemis restants. À la fin : durée, switches, activations de Tactical Focus,
  combos, dégâts subis, membres tombés (instrumentation debug, rien n'est sauvegardé).

`Prototype.unity` reste la scène V0.6–V0.8 inchangée, utilisée par les tests précédents.

## Trois archétypes

| | Pursuer | Marksman | Bulwark |
| --- | --- | --- | --- |
| Composant | `BasicMeleeEnemy` (`Archetype` PURSUER) | `MarksmanEnemy` | `BasicMeleeEnemy` (`Archetype` BULWARK) + `FrontalGuard` |
| Silhouette | capsule violette 0,7 × 0,8, rapide | capsule fine jaune-vert 0,55 × 1,0 avec canon | capsule large rouge sombre 1,35 × 1,0 avec plaque acier frontale |
| HP | 80 | 70 | 180 |
| Vitesse agent | 3,6 m/s, rotation 480°/s | 3,4 m/s, rotation 360°/s | 1,8 m/s, rotation 120°/s |
| Détection / perte | 30 m / 45 m | 20 m / 26 m | 30 m / 45 m |
| Attaque | mêlée 18, wind-up 0,45 s, zone 2 × 1,4 m | projectile 14 à 11 m/s, visée 1,1 s, récupération 0,5 s, cooldown 2,4 s | mêlée 30, wind-up 0,9 s, actif 0,2 s, récupération 1,1 s, zone 2,4 × 2,2 m |
| Distance | contact (1,65 m) | garde 5–12 m, préférée 8 m | contact (2 m) |
| Particularité | poursuite agressive | vise le membre contrôlé, recule de 4 m au plus toutes les 1,6 s si approché | dégâts × 0,35 depuis le cône frontal de ±60°, pleins sur les flancs et l'arrière |

Pursuer et Bulwark réutilisent l'IA V0.3 (poursuite, frappe télégraphiée, cue au sol, télégraphe tactique).
Le Bulwark tourne lentement et ne pivote pas pendant son wind-up/récupération : c'est la fenêtre de flanc.
La garde est appliquée dans `ComboDamage.Apply`, qui reçoit désormais l'origine de l'attaque (mêlée, tir, dash, splash, frappes des companions).
Un impact réduit affiche `BLOQUE` et fait flasher la plaque ; un impact de flanc affiche `FLANC !`.
Pendant Tactical Focus, la fiche du Bulwark indique `GARDE FRONTALE > FLANQUER` et un arc au sol dessine le cône protégé.

Le Marksman tient sa ligne de visée (jaune → rouge, s'épaississant) sur le membre contrôlé pendant 1,1 s puis fige la
direction du tir. Le projectile est rectiligne : sortir de la ligne ou esquiver (i-frames) l'évite ; un tir esquivé est consommé.
Les tirs traversent les autres ennemis et s'arrêtent sur le décor. Les champs de gravité ralentissent, attirent et préparent
GROUPED sur les trois archétypes ; les companions les ciblent tous via `EnemyBrain`.

## Rencontre

| Vague | Nom | Composition | Positions d'arrivée (monde) |
| --- | --- | --- | --- |
| 1 | APPRENTISSAGE | 2 Pursuers | (-13,5 ; 16), (-8,5 ; 16) |
| 2 | COMBINAISON | 2 Pursuers + 1 Marksman | (-16 ; 14), (-6 ; 15,5), Marksman (-11 ; 19) |
| 3 | TACTIQUE | 1 Bulwark + 1 Marksman + 2 Pursuers | Bulwark (-11 ; 15), Marksman (-17,5 ; 9,5) sur le flanc ouest, (-5 ; 13), (-7,5 ; 18) |

La squad démarre en (-11 ; 3) face au nord. Les ennemis sont pré-placés inactifs dans la scène et activés par vague
(`EncounterController.Awake` les désactive quoi qu'il arrive). Une vague n'avance que lorsque tous ses ennemis sont morts.
`EncounterController` suit `Pending → Spawning → Fighting → Cleared → … → Complete | Defeated`.

Indicateur de menace minimal : quand un ennemi prépare une attaque contre le membre contrôlé **hors champ**, un cartouche
`< MARKSMAN 0,8s <` apparaît au bord de l'écran dans sa direction. Rien n'est affiché pour les ennemis visibles.

Estimation de durée (non mesurée en jeu, à confirmer manuellement) : vague 1 ≈ 20–30 s, vague 2 ≈ 40–60 s,
vague 3 ≈ 60–120 s, soit **environ 3 à 4 minutes** avec les pauses, pour un joueur connaissant les contrôles.
Le budget de dégâts reste raisonnable : 18 / 14 / 30 par impact contre 100 HP, 30 de bouclier et +20 HP entre vagues.

## Fichiers V0.9

Créés :
- `Assets/TRACE/AI/EnemyBrain.cs` : surface commune (archétype, cible, préparation, libellé) pour companions, overlay et indicateur.
- `Assets/TRACE/AI/MarksmanEnemy.cs` : distance, visée télégraphiée, tir, repositionnement rationné.
- `Assets/TRACE/Combat/Projectile.cs` : tir rectiligne réutilisé par son tireur, sphere-cast contre membres et décor.
- `Assets/TRACE/Combat/FrontalGuard.cs` : cône frontal, feedback `BLOQUE` / `FLANC !`, arc pendant Focus.
- `Assets/TRACE/Encounter/EncounterController.cs`, `EncounterHud.cs`, `ThreatIndicator.cs`.
- `Assets/TRACE/Editor/PrototypeEncounterSetup.cs` : création unique de `PrototypeEncounter.unity` à partir de `Prototype.unity`.
- `Assets/TRACE/Tests/PlayMode/EncounterTests.cs` : tests V0.9.
- Scène `PrototypeEncounter.unity`, matériaux `EnemyMarksman`, `EnemyBulwark`, `BulwarkPlate`, `MarksmanAim`, `MarksmanShot`, `GuardArc`, `SpawnMarker`.
- `Docs/Validation/EncounterVisualCapture.cs` : scénario de capture graphique.

Modifiés :
- `BasicMeleeEnemy.cs` : hérite d'`EnemyBrain`, champ `Archetype`.
- `ComboDamage.cs` et ses appelants (`PlayerMeleeAttack`, `TargetedAttack`, `DashStrike`) : origine de l'attaque.
- `CompanionController.cs` : cible tout `EnemyBrain`. `EnemyGravityResponse.cs` : n'exige plus `BasicMeleeEnemy`.
- `TacticalOverlay.cs` : fiches par archétype, libellé d'état fourni par l'ennemi, rappel de la garde.
- `TacticalFocusTests.cs`, `TacticalVisualCapture.cs` et docs V0.8 : Tab au lieu d'Alt gauche (binding modifié par l'utilisateur).
- `ProjectSettings/EditorBuildSettings.asset` : `PrototypeEncounter` en première position.

Aucun nouveau package, skill joueur, boss, ressource, inventaire ni ordre tactique. Health, moteur, Input System et caméra sont inchangés.

## Validation

Exécutée en batch dans une copie isolée (`Logs/v09-project`, bibliothèque reprise de la copie V0.8), le projet principal restant ouvert dans l'Editor.

| Étape | Résultat | Preuve |
| --- | --- | --- |
| Baseline avant V0.9 | 146/146 tests V0.1–V0.8 (run final V0.8) | `Validation/TRACE-V0.9-Baseline.xml` |
| Suite finale | 166/166 (146 précédents + 20 Encounter) | `Validation/TRACE-V0.9-PlayMode.xml`, `Logs/v09-tests-final.log` |
| Scénario graphique | 1/1, trois captures 1280x720 | `Validation/TRACE-V0.9-Visual.xml`, `Validation/Encounter-*.png` |

Aucun warning ni erreur C#. Captures : `Encounter-spawn.png` (anneaux d'arrivée de la vague 1),
`Encounter-focus.png` (Focus avec fiches PURSUER / BULWARK / MARKSMAN, ligne de visée, arc de garde),
`Encounter-guard.png` (impact frontal `BLOQUE` sur le Bulwark). L'IMGUI (bandeau de vague, résultat, cartouche de menace)
n'apparaît pas dans un rendu caméra ; il est couvert par les tests et la vérification manuelle.

Les tests V0.9 couvrent : composition des vagues et première scène de build ; différenciation visuelle ; Pursuer inchangé ;
Marksman (cible = membre contrôlé, recul, visée télégraphiée, impact 14, esquive par pas de côté, i-frames, cooldown) ;
companions et Gravity Field sur le Marksman ; Bulwark (cône frontal, flanc, attaque lourde, mort) ; vagues (attente, soin,
délai, victoire, défaite, redémarrage, statistiques) ; indicateur de menace hors champ ; overlay tactique par archétype.

Trois ajustements pendant la validation : les companions ignorent un cerveau désactivé (comportement V0.7), le test
immobilise donc l'agent au lieu de désactiver le Marksman ; la première visée précède toute décision de repositionnement ;
et surtout **le projectile se détache de son tireur au lancement** : enfant du Marksman, il pivotait avec lui après le tir
et balayait un companion. Les fiches ennemies passent à 262 × 88 px pour les titres plus longs, et l'overlay lit la taille
réelle des panneaux pour éviter les chevauchements.

## Vérification manuelle

1. Vague 1 : observer les anneaux d'arrivée, reprendre attaque / esquive / switch. Les Pursuers doivent forcer le mouvement sans être dangereux.
2. Vague 2 : repérer le Marksman par sa silhouette et sa ligne de visée ; maintenir Tab pendant sa visée ; vérifier le cartouche de bord d'écran quand il vise hors champ ; sortir de la ligne ou esquiver.
3. Vague 3 : lire le cône de garde en Focus ; flanquer le Bulwark avec Assault (dash sur un GROUPED préparé par Control) ; vérifier `BLOQUE` de face et `FLANC !` sur le côté ; utiliser Pulse Shield quand Bulwark et Marksman menacent ensemble.
4. Perdre volontairement pour voir `SQUAD DEFEATED`, puis Retour arrière. Gagner pour lire les statistiques et la durée réelle.
5. Vérifier la Console, la lisibilité à la résolution de la Game View et l'équilibrage ressenti (durée visée 3 à 6 min).
