# TRACE V0.1 — déplacement et caméra

Ouvrir `Assets/TRACE/Scenes/Prototype.unity`, attendre la fin de l’import puis Play.
La scène est déjà câblée et placée en tête des Build Settings ; aucun composant
à ajouter manuellement. Le template SampleScene reste disponible.

## Commandes

- WASD : déplacement relatif à la caméra (positions physiques ; ZQSD sur AZERTY).
- Maj gauche ou droite maintenue : sprint ; relâcher pour revenir à la marche.
- Souris : orbite horizontale et verticale.
- Échap : libérer le curseur et suspendre les entrées de jeu.
- Clic gauche dans Game : reprendre le contrôle. Après perte de focus, recliquer.

Le clavier propose marche et sprint ; aucune animation, aucun saut ni combat.
Les actions Attack, Skill, Dodge, Character1/2/3 et TacticalFocus existent sans
binding et sans consommation. Leur gameplay sera ajouté dans une prochaine V0.

## Structure et réglages

- `Input/TRACEInput.inputactions` : map Player et contrôles KeyboardMouse.
- `Input/TracePlayerInput.cs` : copie privée des actions, cycle enable/disable,
  lecture des entrées et capture du curseur.
- `Characters/ThirdPersonMotor.cs` : CharacterController, accélération, rotation,
  gravité, sonde au sol et adhérence aux pentes. Références input/caméra explicites.
- `Camera/ThirdPersonOrbit.cs` : cible indépendante de la rotation du personnage,
  sensibilité souris en degrés/pixel, limites de pitch et recentrage optionnel.
- Cinemachine Third Person Follow : placement, amortissement et collisions.
- `Editor/PrototypeSceneBuilder.cs` : auteur de la scène, menu TRACE ; refuse
  d’écraser une scène existante. Utile uniquement pour régénérer une scène absente.
- `Tests/PlayMode/PrototypeTests.cs` : tests d’intégration clavier/souris/physique.

Réglages Inspector :

| Objet / composant | Réglage initial |
| --- | --- |
| Player / Third Person Motor | Marche 3,2 m/s, sprint 6,2 m/s |
| Player / Third Person Motor | Accélération 18, décélération 24 m/s² |
| Player / Character Controller | Pente max 45°, marche max 0,30 m |
| Player Camera Rig / Third Person Orbit | Hauteur cible 1,45 m ; pitch −30° à 65° |
| Player Camera Rig / Third Person Orbit | Sensibilité 0,12°/pixel ; recentrage désactivé |
| Player Camera Rig / Cinemachine Third Person Follow | Distance 5,5 m ; décalage épaule 0,35 m |
| Player Camera Rig / Cinemachine Camera | Champ vertical 60°, near clip 0,10 m |
| Cinemachine Third Person Follow / Avoid Obstacles | Rayon 0,25 m ; entrée immédiate, retour amorti 0,4 s |

La géométrie est sur Default ; le player et ses visuels sur Ignore Raycast.
La sonde au sol et les collisions caméra utilisent Default, ignorent les triggers
et n’intersectent donc pas la capsule du joueur. Toute nouvelle géométrie bloquante
doit avoir un collider sur Default (ou ajuster explicitement les deux masques).
L’environnement fermé comprend une rampe de 20°, une plateforme de 2,05 m, des
marches de 20 cm, des cubes et un angle de murs hauts.

## Validation reproductible

Unity Test Runner > PlayMode > TRACE.Tests.PrototypeTests. Les tests chargent la
scène Prototype : elle doit figurer dans les Build Settings. Ils utilisent des
périphériques virtuels Input System et un pas simulé de 1/60 s, puis les retirent.

Commande (adapter les chemins) :

```sh
Unity -batchmode -nographics -projectPath /chemin/Project-TRACE \
  -runTests -testPlatform PlayMode -testFilter TRACE.Tests.PrototypeTests \
  -testResults /tmp/trace-playmode.xml -logFile /tmp/trace-playmode.log
```

Ne pas lancer un second éditeur sur un projet déjà ouvert ; utiliser une copie.

## Essai manuel

1. Marcher, changer de direction, sprinter puis relâcher : apprécier l’inertie.
2. Tourner autour de la capsule ; vérifier la direction de déplacement après orbite.
3. Monter/descendre la rampe, les marches, puis tomber du bord de la plateforme.
4. Longer les murs et faire tourner la caméra dans l’angle ; vérifier le retour
   progressif de la distance après une obstruction.
5. Vérifier les deux limites de pitch, Échap/clic et Alt-Tab/reprise.
6. Vérifier la Console durant l’essai. Le feeling reste un choix à valider à la main.

Aucun système d’escouade, combat, IA, ralenti tactique, singleton ou service global.

## Résultat de validation — 2026-10-07

Unity 6000.3.10f1, copie isolée `/tmp/trace-v01-validation`, baseline `a2c2fa6`.
Cinemachine 3.1.7 installé par le gestionnaire Unity ; dépendances résolues dans
`Packages/packages-lock.json`. Aucun autre framework ajouté.

- Compilation Unity réussie, aucun warning/erreur C#.
- **8 tests PlayMode réussis sur 8**, aucun échec ni test ignoré.
- Aucun log runtime inattendu dans les tests ; références et scripts de scène
  inspectés par l’éditeur, GUID et métadonnées contrôlés après copie.
- Rendus URP de la vue joueur et de l’ensemble du parcours inspectés visuellement.
- Les scripts livrés sont identiques à ceux exécutés dans la copie de validation.
- L’essai interactif clavier/souris, le ressenti et la Console de la session déjà
  ouverte restent à confirmer manuellement. Pas de build exécutable réalisé.

Rapport : `Docs/Validation/TRACE-V0.1-PlayMode.xml`.
Vues éditeur : `Docs/Validation/Prototype-player.png` et `Prototype-overview.png`.
Logs complets locaux : `/tmp/trace-v01-baseline.log`,
`/tmp/trace-v01-playmode3.log`, `/tmp/trace-v01-visual2.log`.
Les logs batch contiennent aussi des diagnostics de l’environnement Unity
(licence/cloud/debugger, déjà présents au baseline), distincts des logs gameplay.

| Critère | Évidence | État |
| --- | --- | --- |
| Ouvrir/lancer Prototype | Chargements PlayMode et inspection Editor | Validé automatiquement |
| Déplacement clavier | Marche, diagonale normalisée, rotation, arrêt, reprise | Validé automatiquement |
| Caméra souris | Orbite, déplacement relatif, deux limites de pitch | Validé automatiquement |
| Sprint | Vitesse accrue puis décélération | Validé automatiquement |
| Pentes et sol | Rampe montée/descente, marches, chute, refus pente 60° | Validé automatiquement |
| Collision caméra | Mur, rayon caméra, ligne de vue, retour après obstruction | Validé automatiquement |
| Console | Compilation et logs des tests sans warning C# ni erreur runtime | Validé dans la copie ; session interactive à confirmer |
| Base évolutive | 3 composants runtime, références explicites, tests et scènes sérialisés | Revue statique effectuée |

Le feeling, la sensibilité et le cadrage restent des paramètres de playtest,
non des propriétés prouvées par les tests automatisés.
