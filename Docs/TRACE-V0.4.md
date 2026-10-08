# TRACE V0.4 — escouade de trois

## Jouer

Ouvrir `Assets/TRACE/Scenes/PrototypeSquad.unity`, puis Play. La scène est le
premier élément des Build Settings. Aucun câblage, package ou bake manuel requis.
`Prototype.unity` reste la scène V0.3 des tests précédents.

Le leader orange commence à (3, 0.08, 0), dans une zone dégagée. Companion A est
vert, avec un cube au-dessus de la tête ; Companion B est bleu, avec une sphère.
Commandes V0.3 conservées : déplacement, souris, sprint, clic gauche, esquive
avec Espace. Un seul personnage possède les composants d'input et de moteur.

Rejoindre la zone de combat à gauche de la rampe, autour de **X=-11, Z=11**.
Trois BasicMeleeEnemy violets attendent dans cet espace de 15 × 15 m. Le terrain
de 40 × 40 m et ses obstacles servent au suivi. Les mannequins V0.2 restent
présents, mais les compagnons ne ciblent que les BasicMeleeEnemy.

A frappe au corps à corps ; B garde davantage de recul et produit un trait de
tir instantané visible 0,12 s. Les coups respectent les obstacles et passent par
Health ou DamageReceiver lorsque présent. Aucun dégât joueur sur les compagnons.
Les trois membres ont 100 PV. À zéro, le compagnon reste grisé au sol sans
collision, attaque ni résurrection. La mort du leader arrête tous les combats.

## Réglages Inspector

| Paramètre | A : mêlée | B : distance |
| --- | --- | --- |
| Offset local (X,Y,Z), mètres | (-1,6 ; 0 ; -2,2) | (1,9 ; 0 ; -3) |
| Suivi / rattrapage | 4,2 / 7,2 m/s | identique |
| Rattrapage si distance au slot supérieure à | 4 m | identique |
| Arrêt / reprise du suivi | 0,45 / 0,85 m | identique |
| Rayon d'espacement souhaité | 1,15 m | identique |
| Réévaluation de destination / seuil de changement | 0,2 s / 0,4 m | identique |
| Détection / intervalle de recherche | 7 m / 0,25 s | identique |
| Portée / position d'approche | 1,65 / 1,25 m | 6 / 4,5 m |
| Recul lorsque l'ennemi est proche | — | moins de 3,5 m |
| Dégâts | 12 PV | 8 PV |
| Préparation / cooldown entre débuts | 0,25 / 1 s | 0,35 / 1,4 s |
| Distance maximale cible–leader | 10 m | identique |
| Rappel / reprise possible du combat | 9 / 4,5 m du leader | identique |
| Repositionnement si éloigné | plus de 18 m pendant 2 s | identique |
| Repositionnement si bloqué | moins de 0,25 m de progrès pendant 4 s | identique |

À l'acquisition, un ennemi engagé contre le leader est prioritaire ; sinon le
plus proche accessible et visible est retenu. Un compagnon conserve sa cible
jusqu'à sa mort, sa désactivation, une rupture de distance ou un repositionnement.
Les ennemis explicitement reliés à la squad peuvent attaquer ses trois membres :
cible visible la plus proche, choix conservé au moins 2 s et jamais changé en
pleine frappe. Sans référence squad, le comportement V0.3 reste inchangé.

La formation tourne progressivement avec le leader en déplacement (100°/s),
puis reste figée à l'arrêt. Les NavMeshAgent assurent navigation et évitement.
Les destinations ménagent de l'espace autour des autres membres, aussi en combat.
Les collisions internes sont ignorées sans modifier la matrice globale des layers.

La sécurité recherche le slot puis des points voisins sur le NavMesh : chemin
complet vers le leader, espace libre et hauteur proche. Sans emplacement sûr,
elle attend et réessaie au maximum une fois par seconde. Aucun compagnon mort
n'est repositionné. Le corps reste présent même après désactivation/réactivation.

Sur `Squad`, activer **Show Debug** et les Gizmos Unity pour afficher offsets,
rayons de rappel/détection/attaque, cibles et destinations de navigation.
Le cadrage V0.4 utilise une distance caméra de 7,5 m, une hauteur cible de 1,8 m
et une inclinaison initiale de 25°. Contrôles et évitement caméra sont conservés.
Des murs peuvent toujours masquer des personnages sous certains angles.

## Fichiers créés et modifiés

Les chemins de code suivants sont relatifs à `Assets/TRACE`.

- `AI/SquadController.cs` : références, formation, espacement, collisions et
  sélection des victimes par les ennemis.
- `AI/CompanionController.cs` : suivi NavMesh, rappel, deux rôles, attaques,
  récupération et mort.
- `Combat/CompanionFeedback.cs` : couleur, flash de dégât, corps persistant.
- `AI/BasicMeleeEnemy.cs` : ajout de la référence squad optionnelle.
- `Editor/PrototypeSquadSetup.cs` : création seulement si la scène est absente,
  câblage et bake ; refuse d'écraser une scène V0.4 existante.
- `Scenes/PrototypeSquad.unity`, `Scenes/Navigation/SquadNavMesh.asset`, quatre
  matériaux `Companion*` et toutes les métadonnées correspondantes.
- `Tests/PlayMode/SquadTests.cs` : 19 tests V0.4.
- `ProjectSettings/EditorBuildSettings.asset` : ajout de PrototypeSquad en tête,
  avec conservation des scènes et configurations précédentes.
- `Docs/AI/UnityProjectContext.md`, ce guide, rapports et captures de validation.

Health, DamageReceiver, PlayerMeleeAttack, input, moteur, scène et tests V0.1–V0.3
sont conservés. Aucun changement de personnage ni travail V0.5 ajouté.

## Validation du 7 octobre 2026

Unity **6000.3.10f1**, Windows, copie isolée `Logs/v04-project`. Le lancement
direct sur le projet principal quittait avec le code 1 avant import ; la copie
a permis compilation, bake et tests sans intervenir dans la session utilisateur.

- Baseline : **34/34** tests V0.1–V0.3 réussis avant modification.
- Suite livrée : **53/53**, dont **19 nouveaux** tests V0.4.
- Scénario graphique supplémentaire : **1/1**, soit **54/54** au rapport final.
- Aucun warning/erreur C# ni log gameplay inattendu pendant la validation finale.
  Les diagnostics licence/debugger/arrêt Mono sont présents dès la baseline et
  distincts des logs gameplay. La Console de la session utilisateur reste à vérifier.
- Quatre captures depuis la caméra de jeu inspectées visuellement : formation,
  tir, regroupement et compagnon au sol. Aucun build exécutable ni profiling.

Tests couverts : composition, convergence, arrêt stable, sprint/virage à 6,2 m/s,
séparation, contournement d'obstacle sans téléportation, réactivation, détection,
dégâts et cooldown des deux rôles, recul du tireur, stabilité de cible, mur apparu
pendant la préparation, rappel, récupération éloignement/blocage, mort par une
attaque ennemie réelle, annulation d'attaque à la mort, arrêt à la mort du leader,
combat à trois ennemis et retour en formation.

Le scénario graphique soigne le leader pendant le combat et rend les compagnons
invulnérables pour capturer leurs rôles durablement. Ces aides sont uniquement
dans le script de validation ; la scène livrée utilise les 100 PV normaux.
La mort d'un compagnon sous une attaque ennemie est testée sans invulnérabilité.

Rapports : `Docs/Validation/TRACE-V0.4-Baseline.xml` et `TRACE-V0.4-PlayMode.xml`.
Captures : `Squad-formation.png`, `Squad-combat.png`, `Squad-regroup.png`,
`Squad-incapacitated.png`. Script graphique conservé hors Assets dans
`Docs/Validation/SquadVisualCapture.cs` ; il n'ajoute aucun test à la suite normale.
Logs locaux : `Logs/v04-isolated-baseline.log` et `Logs/v04-delivery2.log`.

Commande de reproduction, projet fermé ou dans une copie isolée :

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.com' `
  -batchmode -nographics -projectPath 'CHEMIN_DU_PROJET' `
  -runTests -testPlatform PlayMode -testFilter TRACE.Tests `
  -testResults 'CHEMIN_DU_RAPPORT.xml' -logFile 'CHEMIN_DU_LOG.log'
```

| Critère | Preuve / statut |
| --- | --- |
| Trois membres, un seul contrôlé | Composition testée ; corps des morts persistants |
| Suivi et formation | Convergence, arrêt, sprint et obstacles testés |
| Lisibilité | Couleurs, marqueurs et captures inspectés ; appréciation en jeu encore manuelle |
| Absence de gêne | Collisions exclues et espace du leader testés ; caméra à éprouver librement |
| Participation et rôles | Dégâts, portée, recul, cooldown testés et tir capturé |
| Retour au leader | Mort cible, rappel, combat multi-unités et captures vérifiés |
| Combat multi-unités | Trois ennemis exécutés ; confort de jeu à confirmer manuellement |
| Console | Aucun warning/erreur C# ou gameplay dans la copie ; session utilisateur non inspectée |
| Non-régression | 34 tests précédents passent sans modification |

## Essai manuel restant

1. Ouvrir PrototypeSquad, marcher, sprinter, tourner et s'arrêter.
2. Traverser obstacles et rampe ; juger le confort de la caméra et le suivi.
3. Rejoindre les ennemis, attaquer/esquiver et observer mêlée, tirs et recul.
4. Quitter le combat puis éliminer les ennemis ; observer le regroupement.
5. Laisser un compagnon tomber : il reste au sol pendant que l'autre suit.
6. Vérifier la Console de la session Editor. Stop/Play réinitialise le prototype.

La partie automatisable est validée. Le jugement « agréable et lisible » en
déplacement libre reste à confirmer par le joueur avant approbation complète.
