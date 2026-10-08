# TRACE V0.5 — changement de personnage

## Lancer

Ouvrir `Assets/TRACE/Scenes/PrototypeSwitch.unity`, puis Play. Cette scène est
ajoutée en tête des Build Settings. Elle contient déjà les références, le NavMesh
V0.4 et l'indicateur : aucun bake, package ou câblage manuel supplémentaire.
Les scènes `Prototype` et `PrototypeSquad` restent disponibles pour les tests
V0.1–V0.4. Leurs assets n'ont pas été réécrits pour cette version.

| Commande | Résultat |
| --- | --- |
| 1 | Membre 1 orange, mêlée à 25 dégâts |
| 2 | Membre 2 vert, mêlée à 12 dégâts |
| 3 | Membre 3 bleu, tir instantané à 8 dégâts |
| Déplacement / souris / Maj | Locomotion, caméra, sprint existants |
| Clic gauche | Attaque du membre actif |
| Clic droit | Esquive ; binding personnalisé de l'utilisateur conservé |
| Échap | Libération du curseur, comme auparavant |

Les touches 1/2/3 désignent les touches physiques de la rangée supérieure
(positions &, é, " sur AZERTY, sans obligation d'appuyer sur Maj).
Le cercle jaune au sol identifie le membre actif et pulse brièvement au switch.
Les ennemis mobiles restent dans la zone autour de X=-11, Z=11, à gauche de la rampe.

## Transfert et valeurs

| Réglage | Valeur |
| --- | --- |
| Cooldown entre switches | 0,20 s, temps de jeu |
| Transition de cible caméra | 0,18 s, temps non mis à l'échelle |
| Pulse de l'indicateur | 0,12 s, agrandissement maximum de 25 % |
| Amortissement Cinemachine V0.5 (X,Y,Z) | (0,03 ; 0,05 ; 0,03) s |
| Membre 1 : portée / préparation / cooldown | 1,8 m / 0,08 s / 0,45 s |
| Membre 2 : portée / préparation / cooldown | 1,65 m / 0,25 s / 1 s |
| Membre 3 : portée / préparation / cooldown | 6 m / 0,35 s / 1,4 s |
| Esquive de chaque membre | 3 m en 0,30 s ; cooldown 0,70 s |
| Invulnérabilité d'esquive | délai 0,03 s, durée 0,20 s |

Les réglages de suivi, rappel et récupération restent ceux de la V0.4 : suivi
4,2 m/s, rattrapage 7,2 m/s, arrêt/reprise 0,45/0,85 m ; repositionnement de
sécurité après plus de 18 m pendant 2 s ou blocage pendant 4 s.

`SquadController.ActiveMember` expose le membre contrôlé. L'API
`SwitchToMember(index)` utilise les indices **0, 1, 2** et retourne true uniquement
si le transfert a eu lieu. Un membre mort/inactif, un indice invalide, le membre
déjà actif, une pause ou le cooldown donnent false. Ces refus ne consomment pas
un nouveau cooldown. Les actions Character1/2/3 existantes appellent cette API.

Un seul lecteur d'inputs existe sur `Squad`. Tous les moteurs le référencent,
mais un seul moteur et une seule attaque joueur sont activés. Sur le nouveau
membre contrôlé, CompanionController et NavMeshAgent sont désactivés ; sur l'ancien,
l'IA reprend. Le CharacterController sert aussi de collider aux trois membres.
Les exclusions de collision entre alliés sont réappliquées pendant le transfert.

Positions, rotations et PV sont identiques immédiatement avant/après l'appel.
L'ancien membre reprend ensuite un déplacement naturel. Si sa position est hors
NavMesh, il y reste jusqu'à ce que la sécurité V0.4 trouve un emplacement sûr ;
le switch ne déclenche pas de téléportation corrective immédiate.

Les attaques en préparation et les esquives en cours sont annulées proprement,
avec suppression des i-frames de l'ancien personnage. Les cooldowns d'attaque
et d'esquive sont conservés : un aller-retour ne permet pas de les réinitialiser.
Une cible transférée reste utilisable si vivante, visible et pertinente pour la
portée/angle ; sinon le ciblage normal reprend. Aucun lock-on ajouté.

La caméra garde ses angles et sa vitesse de lissage. Seule la position de sa
cible interpolée change, avec l'évitement d'obstacles existant. La formation
réattribue ses deux offsets aux membres restants, sans déplacer leurs transforms
lors du transfert. Les offsets gauche/droite se règlent sur SquadController.

À la mort du membre actif, le premier membre vivant dans l'ordre 1/2/3 est
sélectionné au tick suivant, même pendant le cooldown. Les corps restent au sol.
Quand personne ne vit, `IsDefeated` devient true, le cercle disparaît et les
inputs s'arrêtent. Stop/Play réinitialise le prototype ; aucun revive ni écran final.

## Fichiers créés

Les chemins de code ci-dessous sont relatifs à `Assets/TRACE`.

- `Characters/SquadMember.cs` : transfert exclusif moteur/agent, santé et arrêt à la mort.
- `Combat/TargetedAttack.cs` : préparation, dégâts, cooldown et visuel partagés
  entre attaques compagnons et tir contrôlé.
- `Combat/PlayerRangedAttack.cs` : clic joueur, sélection dans un cône de 35°,
  validation de la cible puis utilisation du même TargetedAttack que l'IA.
- `Editor/PrototypeSwitchSetup.cs` : auteur de la scène, des composants et du
  cercle ; refuse d'écraser une scène V0.5 existante.
- `Scenes/PrototypeSwitch.unity`, `Scenes/Materials/ActiveMember.mat` et métadonnées.
- `Tests/PlayMode/SwitchTests.cs` : 22 nouveaux tests.

## Fichiers modifiés

- `AI/SquadController.cs` : roster stable de trois, membre actif, API, formation,
  cooldown, sélection automatique, défaite et indicateur. Sans roster V0.5,
  les références leader/companions V0.4 conservent leur comportement.
- `AI/CompanionController.cs` : extraction de l'attaque, reprise après transfert,
  conservation de cible et récupération différée d'un ancien joueur hors NavMesh.
  Les champs sérialisés de réglage sont conservés ; les scènes V0.4 créent leur
  TargetedAttack en Awake, les instances V0.5 possèdent déjà ce composant.
- `Combat/PlayerMeleeAttack.cs` : accès au cooldown, délai minimum transférable
  et préférence de cible validée par le soft targeting existant.
- `Combat/CompanionFeedback.cs` : feedback d'esquive pour le membre contrôlé.
- `Camera/ThirdPersonOrbit.cs` : changement de cible avec interpolation, sans remise à zéro des angles.
- `Input/TracePlayerInput.cs` et `TRACEInput.inputactions` : lecture des actions
  Character1/2/3 et ajout de leurs trois bindings, avec conservation du clic droit.
- `Tests/PlayMode/DuelTests.cs` : adaptation du helper de simulation d'esquive au
  clic droit. Les scénarios et assertions de comportement restent identiques.
- `ProjectSettings/EditorBuildSettings.asset`, contexte projet et documentation.

Health, DamageReceiver, ThirdPersonMotor et BasicMeleeEnemy n'ont pas nécessité
de modification V0.5. Aucun combo, skill, Tactical Focus, ordre tactique, jauge,
progression, UI de portraits ou travail V0.6 ajouté.

## Validation — 7 octobre 2026

Unity 6000.3.10f1 sous Windows, copie isolée `Logs/v05-project`.

La baseline avant toute modification V0.5 a exécuté 53 tests : **46 réussis et
7 échecs**. Les sept échecs provenaient de tests simulant Espace alors que Dodge
avait été rebindé au clic droit. Ce binding utilisateur a été conservé ; seul
le helper des tests a été adapté, sans suppression d'assertion ni test ignoré.

Résultat final : **75/75 tests PlayMode réussis**, soit 53 précédents et 22 nouveaux,
plus **1/1 scénario graphique**. Aucun warning/erreur C# ou log gameplay inattendu.
Des diagnostics de licence, IPC, debugger et arrêt Mono apparaissent dans les
logs batch ; ils ne sont pas des erreurs des comportements de la scène.

| Critère | Preuve |
| --- | --- |
| Sélection 1→2→3→1 | Événements clavier Input System réels simulés |
| Aucun déplacement au switch | Comparaison immédiate des trois positions/rotations et PV |
| Ancien joueur devenu compagnon | États moteur/agent/IA exclusifs vérifiés |
| Nouveau joueur reçoit les inputs | Déplacement et sprint testés pour les trois, y compris input maintenu |
| Caméra et orientation | Cible, état intermédiaire, fin de transition et angles testés en temps réel |
| Formation | Réattribution, convergence et absence de récupération artificielle testées |
| Attaques et esquives | Trois dégâts/portées distincts ; clic droit et i-frames pour les trois |
| Switch en plein combat | 18 transferts successifs et scénario graphique |
| Annulation / cooldowns | Préparations IA/joueur annulées ; aller-retour sans reset testé |
| Ciblage et obstacles | Cible IA conservée, cible morte remplacée, tir bloqué par mur |
| Feedback actif | Position unique du cercle testée ; quatre captures inspectées |
| Mort | Membre mort exclu, sélection automatique, défaite totale, corps persistants |
| Hors NavMesh | Pas de téléportation au switch ; récupération après délai testée |
| Non-régression V0.1–V0.4 | 53 tests précédents réussis avec helper adapté au binding actuel |

Rapports dans `Docs/Validation` : `TRACE-V0.5-Baseline.xml`,
`TRACE-V0.5-PlayMode.xml`, `TRACE-V0.5-Visual.xml`. Captures :
`Switch-member1.png`, `Switch-member2.png`, `Switch-ranged-shot.png`,
`Switch-auto-replacement.png`. Le script graphique reproductible est conservé
hors Assets : `Docs/Validation/SwitchVisualCapture.cs`.
Logs locaux : `Logs/v05-baseline.log`, `Logs/v05-all2.log`, `Logs/v05-visual.log`.

Commande de tests, sur un projet fermé ou une copie isolée :

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.com' `
  -batchmode -nographics -projectPath 'CHEMIN_DU_PROJET' `
  -runTests -testPlatform PlayMode -testFilter TRACE.Tests `
  -testResults 'CHEMIN_DU_RAPPORT.xml' -logFile 'CHEMIN_DU_LOG.log'
```

Pour reproduire les captures, copier SwitchVisualCapture.cs dans les tests
PlayMode de la copie isolée, définir TRACE_CAPTURE_OUTPUT vers le dossier de sortie,
retirer `-nographics` et filtrer `TRACE.Tests.SwitchVisualCapture`.

## Essai manuel restant

1. Ouvrir PrototypeSwitch ; presser 1/2/3 à l'arrêt, puis en sprintant.
2. Tourner la caméra puis switcher : confirmer le confort et le maintien du cap.
3. Combattre, tirer avec le membre bleu et esquiver au clic droit avec chacun.
4. Switcher pendant les préparations et laisser un membre tomber.
5. Vérifier le cercle, la recomposition de formation et la Console de votre session.

Les tests et captures valident le fonctionnement automatisable. Le feeling du
switch en déplacement libre et la Console de la session utilisateur restent à
confirmer manuellement. Aucun build exécutable ou profiling de performances effectué.
