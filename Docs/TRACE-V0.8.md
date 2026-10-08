# TRACE V0.8 — Tactical Focus

## Utilisation

Ouvrir `Assets/TRACE/Scenes/Prototype.unity`, Play, cliquer dans Game View pour capturer la souris.
Maintenir **Tab** (binding utilisateur, initialement Alt gauche) : temps à **0,15**, transitions d'entrée et de sortie de **0,15 s réelles**.
La souris reste disponible et **1 / 2 / 3** change le personnage contrôlé. Relâcher Tab pour agir.
Le Focus n'est ni une bascule ni une pause. Aucun ordre, ciblage manuel, lancement depuis l'interface ou stockage d'action.

Les réglages sont dans le composant **Tactical Focus**, sur **Squad** :
`Focus Scale`, `Enter Duration`, `Exit Duration`.
Le binding est dans `Assets/TRACE/Input/TRACEInput.inputactions`, map Player, action TacticalFocus.
Les bindings existants sont conservés, notamment Q et l'esquive au clic droit.

## Comportement retenu

- Le Focus bloque déplacement, sprint, attaque, esquive et compétences du joueur.
- Les attaques préparées et le dash/esquive en cours sont annulés, sans remboursement du cooldown. Les i-frames de l'esquive annulée sont retirées.
- Les champs et boucliers déjà lancés restent actifs. Les companions continuent leurs actions autonomes et les ennemis continuent à combattre au ralenti.
- Les actions se réactivent dès le relâchement de Tab, pendant la courte remontée du temps. Une touche Q/clic pressée pendant le Focus n'est pas mise en attente : refaire une pression après la sortie.
- Les trois membres sont affichés avec slot, rôle, PV, bouclier et READY/cooldown ; CONTRE indique une opportunité Protected.
- Les ennemis vivants, dans le champ de vision, à moins de 18 m du personnage actif et non masqués par le décor ont une fiche : PV, état/phase, cible par slot et GROUPED ou SLOWED. GROUPED affiche `1 + Q` pour rappeler Assault.
- La zone rectangulaire orange et la flèche indiquent le volume horizontal et la direction engagée d'une attaque ennemie. Le trait bleu désigne sa cible. Ces lignes apparaissent en wind-up et pendant la phase active.
- Une teinte froide légère signale le Focus. Pas de traitement audio ajouté.

Les quatre ennemis existants, les trois membres, le NavMesh, les obstacles et les compétences/combo V0.7 sont conservés. Aucune configuration manuelle de références n'est nécessaire.

## Horloges et restauration

| Système | Horloge |
| --- | --- |
| Locomotion, IA/NavMesh, attaques, dash/esquive, cooldowns de compétences | Temps de jeu ralenti |
| Gravity Field, Shield, dwell/expiration/réarmement des combos | Temps de jeu ralenti |
| Rotation/smoothing caméra, transition de suivi Cinemachine | Temps réel |
| Entrée/sortie du Focus | Temps réel |
| Rafraîchissement du texte tactique (20 Hz) et placement des fiches | Temps réel / chaque frame |
| Délai entre switches pendant Focus | 0,2 s réelles ; hors Focus, délai de jeu existant |

Le contrôleur appartient à la scène et mémorise les valeurs initiales. La cible est `valeur initiale × Focus Scale` ; pour la valeur normale 1, elle vaut 0,15. Le pas physique est proportionnel : 0,02 devient 0,003. Le suivi de propriété mémorise les valeurs relues après écriture, car Unity quantifie `fixedDeltaTime`.

Relâcher Tab restaure doucement les valeurs initiales, même après plusieurs réentrées rapides. Désactiver le composant, quitter/décharger la scène, perdre les entrées ou le focus application, ou perdre le membre contrôlé restaure immédiatement les valeurs encore détenues. Après une interruption, relâcher Tab avant de réactiver.

Si un autre système modifie `timeScale` ou `fixedDeltaTime`, il prend la priorité : le Focus s'arrête, conserve la valeur modifiée et restaure seulement l'autre valeur si elle lui appartient encore. Il ne peut pas démarrer pendant une pause à timeScale 0. Ce mécanisme détecte les changements de valeur ; il ne remplace pas un arbitrage central si un futur système doit composer plusieurs effets temporels simultanés.

**La V0.7 réelle ne contient pas de hit-stop** : son feedback de combo n'écrit pas dans le temps global. Aucun TimeManager ni hit-stop artificiel n'a été ajouté. La consommation de combo pendant le Focus est testée pour vérifier qu'elle ne restaure pas le temps à 1.

## Fichiers V0.8

Créés :
- `Assets/TRACE/Tactical/TacticalFocus.cs` : maintien, transitions et restitution des horloges.
- `Assets/TRACE/Tactical/TacticalOverlay.cs` : lecture des membres/ennemis et disposition des fiches.
- `Assets/TRACE/Tactical/TacticalTelegraph.cs` : dessin de l'attaque engagée.
- `Assets/TRACE/Editor/PrototypeTacticalSetup.cs` : migration de scène unique, refuse d'écraser un Focus déjà présent.
- `Assets/TRACE/Tests/PlayMode/TacticalFocusTests.cs` : tests V0.8.
- Deux matériaux `Tactical Attack` / `Tactical Target` et les métadonnées Unity.
- `Docs/Validation/TacticalVisualCapture.cs` : scénario de capture graphique reproductible dans une copie isolée.

Modifiés :
- `TracePlayerInput.cs`, `TRACEInput.inputactions` : binding, maintien et filtrage des entrées d'action.
- `ThirdPersonMotor.cs`, `CharacterSkill.cs`, `PlayerMeleeAttack.cs`, `PlayerRangedAttack.cs` : annulation/garde des actions.
- `SquadController.cs` : switch en temps réel pendant Focus.
- `BasicMeleeEnemy.cs` : accès en lecture aux paramètres d'attaque engagée.
- `SkillHud.cs`, `ComboPrompt.cs` : masque les anciennes informations détaillées pendant le Focus pour éviter les doublons ; les anneaux de combo restent visibles.
- Assemblies Runtime et Editor : référence à UGUI déjà installé, aucun package ajouté.
- `Prototype.unity` : références câblées, Canvas et fiches, télégraphes, Cinemachine Ignore Time Scale.

## Validation

Exécutée en batch dans une copie isolée du projet (`Logs/v08-project`), le projet principal restant ouvert dans l'Editor.

| Étape | Résultat | Preuve |
| --- | --- | --- |
| Baseline avant V0.8 | 123/123 tests V0.1–V0.7 | `Validation/TRACE-V0.8-Baseline.xml` |
| Suite finale | 146/146 (123 précédents + 23 Tactical Focus) | `Validation/TRACE-V0.8-PlayMode.xml`, `Logs/v08-tests-final.log` |
| Scénario graphique | 1/1, trois captures 1280x720 | `Validation/TRACE-V0.8-Visual.xml`, `Validation/Tactical-*.png` |

Aucun warning ni erreur C#, aucune exception dans les journaux finaux.

Captures : `Tactical-grouped.png` (Control actif, trois GROUPED lisibles avec rappel `1 + Q`, cooldown Gravity Field),
`Tactical-windup.png` (Assault actif après switch 1 pendant le Focus, CONTRE, deux télégraphes orange et WIND-UP),
`Tactical-released.png` (temps normal, overlay masqué, consigne de combo V0.7 de retour).

Deux corrections ont été apportées pendant la validation :
- `TransitionsUseRealTimeAndAreNotInstant` échantillonnait la transition après deux frames ; en batch une frame dure environ 1 ms
  et la courbe SmoothStep y est quasi plate (0,1501 lu). Le test lit désormais un tiers de la transition en temps réel (0,05 s).
- `TacticalVisualCapture.cs` ajoutait le clavier virtuel avant `backgroundBehavior = IgnoreFocus` ; la fenêtre batch n'ayant pas
  le focus, le périphérique était désactivé à sa création. Les réglages Input System sont maintenant appliqués avant `AddDevice`,
  et la capture laisse passer deux frames avec la render texture pour que le canvas écran et le placement des fiches utilisent la résolution de l'image.

## Vérification manuelle du confort

1. Approcher les ennemis. Maintenir Tab pendant leur préparation : observer le rectangle/flèche orange, le trait bleu et WIND-UP.
2. Faire tourner la caméra ; vérifier la sensibilité et la lisibilité dans la résolution de Game View utilisée.
3. Passer sur Control (2), lancer Q près du groupe, maintenir Tab lorsque GROUPED apparaît. Lire son délai et celui de Gravity Field.
4. Pendant le Focus, passer sur Assault (1), relâcher Tab puis Q : vérifier l'exploitation normale du combo. Ne pas maintenir Q avant la sortie.
5. Essayer clic gauche, clic droit, Q et déplacement pendant Focus : aucune nouvelle action du joueur. Les ennemis/companions doivent continuer lentement.
6. Tester plusieurs maintiens courts, Escape/perte du focus et la mort du membre actif. Le temps doit redevenir normal sans rester bloqué au ralenti.
7. Répéter avec Support (3), son bouclier et un CONTRE. Vérifier le confort du switch, puis Console sans erreur.

Le rendu et les assertions automatisées ne remplacent pas cette validation humaine du confort souris et de la lisibilité sur l'écran de jeu. Pas de V0.9, pas de build Player de livraison dans cette étape.
