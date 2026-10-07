# TRACE V0.3 — duel et esquive

## Jouer

Quitter Play, attendre l’import puis rouvrir `Assets/TRACE/Scenes/Prototype.unity`.
Le NavMesh est déjà cuit et référencé : aucun bake ou câblage manuel nécessaire.
La zone de duel bleutée se trouve à **X=-11, Z=11**, à gauche de la rampe.
La capsule violette est l’ennemi mobile. Les trois mannequins V0.2 restent présents.

- WASD (ZQSD physique en AZERTY), souris, Maj et clic gauche : contrôles existants.
- **Espace** : esquive dans la direction de déplacement relative à la caméra.
  Sans direction, esquive vers l’arrière du personnage. Disponible au sol.
- L’esquive annule un coup joueur en cours et empêche d’attaquer jusqu’à sa fin.
- Le rectangle jaune au sol annonce la direction et l’étendue du coup ennemi.
  Il devient rouge pendant la frappe, puis disparaît pendant la récupération.
- Le joueur devient cyan durant l’invulnérabilité et flashe rouge s’il est touché.
- La direction ennemie est fixée au début de la préparation : une esquive latérale
  peut sortir de la zone ; une esquive bien synchronisée peut absorber le coup.
- À zéro PV joueur, les contrôles sont coupés et l’ennemi cesse d’attaquer.
  Stop/Play réinitialise le duel. Aucun système de respawn ou écran final ajouté.
- Échap libère le curseur, mais **ne met pas le monde en pause**.

## Réglages

| Composant | Paramètre | Valeur initiale |
| --- | --- | --- |
| Health joueur / ennemi | Max Health | 100 / 100 PV |
| Basic Melee Enemy | Detection / Lose Target Range | 7 / 10 m |
| Basic Melee Enemy | Attack Range / Hit Range | 1,65 / 2 m |
| Basic Melee Enemy | Hit Half Width | 0,70 m |
| Basic Melee Enemy | Damage | 20 PV |
| Basic Melee Enemy | Windup / Active / Recovery | 0,45 / 0,15 / 0,65 s |
| NavMesh Agent ennemi | Speed / Stopping Distance | 2,8 m/s / 1,45 m |
| Third Person Motor joueur | Dodge Distance / Duration | 3 m / 0,30 s |
| Third Person Motor joueur | Dodge Cooldown | 0,70 s entre débuts |
| Third Person Motor joueur | Invulnerability Delay / Duration | 0,03 / 0,20 s |
| Player Health Feedback | Flash Duration | 0,18 s |

Le déplacement de dodge s’arrête sur les obstacles. La distance est intégrée sur
une courbe de vitesse à départ rapide et arrêt progressif ; gravité, sol, pentes
et collisions restent traités par le seul CharacterController du moteur existant.
Les timers utilisent le temps de jeu. Le délai et la durée d’invulnérabilité sont
configurables sur le moteur ; leur fin est bornée par la durée d’esquive.

## Architecture

- `AI/BasicMeleeEnemy.cs` : référence sérialisée au DamageReceiver et au collider
  joueur ; quatre états locaux Idle/Chase/Attack/Dead. NavMeshAgent poursuit avec
  mise à jour de destination toutes les 0,2 s. L’attaque a trois phases locales,
  une direction engagée, un unique impact et une vérification d’obstacle.
- `Combat/DamageReceiver.cs` : porte d’entrée de dégâts avec une simple fenêtre
  temporelle d’invulnérabilité. Les attaques passent par TryTakeDamage. Health
  reste inchangé et gère uniquement les PV ; l’appeler directement contourne
  volontairement cette porte d’entrée (outil de test ou logique de santé brute).
- `Characters/ThirdPersonMotor.cs` : ajoute le mouvement d’esquive et déclenche la
  fenêtre du DamageReceiver. Une seule autorité déplace le CharacterController.
- `Combat/PlayerHealthFeedback.cs` : flash rouge, indication cyan, arrêt des
  contrôles à la mort. Aucun recul physique joueur ajouté.
- `Combat/PlayerMeleeAttack.cs` : annule l’attaque pendant le dodge et utilise un
  DamageReceiver lorsqu’une cible en possède un, sinon Health comme en V0.2.
- `Combat/EnemyDummy.cs` est réutilisé comme feedback de dégâts/mort sur l’ennemi
  mobile. Il ne pilote que le visuel ; BasicMeleeEnemy arrête l’agent à la mort.
- `Input/TracePlayerInput.cs` et `TRACEInput.inputactions` : action Dodge sur Espace.
- `Editor/PrototypeDuelSetup.cs` : auteur de la zone, des références et du NavMesh.
  Refuse un second ajout. La scène livrée contient déjà son résultat.

L’ennemi ne traverse pas les murs avec ses coups. Une frappe qui rencontre les
invulnérabilités est consommée : elle ne retente pas de dégâts à leur expiration
pendant la même fenêtre active. Un joueur hors du NavMesh n’entraîne aucune sortie
de la surface par l’agent. La perte de cible arrête la poursuite, sans retour à une
position de garde ni comportement supplémentaire.

## Navigation

AI Navigation 2.0.10 était déjà installé. Seule l’assembly Editor référence
`Unity.AI.Navigation` pour cuire la surface ; aucun package supplémentaire.
`Duel Area / NavMeshSurface` utilise un volume de 16 × 6 × 16 m, les colliders
Default et une taille de voxel de 0,1 m. Les personnages Damageable/Ignore Raycast
et les marquages sans collider sont exclus du bake.
Données persistées dans `Assets/TRACE/Scenes/Navigation/DuelNavMesh.asset`.
Si la géométrie de la zone change plus tard, refaire Bake sur cette surface.

## Essai manuel

1. Approcher depuis l’extérieur de la zone : l’ennemi attend, détecte puis poursuit.
2. Contourner les blocs : il doit les contourner pour atteindre le joueur.
3. Observer la préparation jaune puis le coup rouge ; sans esquive, perdre 20 PV.
4. Esquiver latéralement, puis tester volontairement trop tôt et trop tard.
5. Contre-attaquer pendant la récupération, puis tuer l’ennemi en quatre coups.
6. Tester la mort joueur : contrôles arrêtés, pas d’agression persistante.
7. Vérifier la Console et refaire un parcours rapide V0.1/V0.2.

Les PV et l’état ennemi sont inspectables en Play ; les Gizmos sélectionnés
montrent les portées, hitboxes et direction d’esquive. Aucun HUD final.
Aucun combo, stamina, parry, loot, XP, skill, squad ou Tactical Focus ajouté.

## Validation exécutée — 2026-10-07

Unity 6000.3.10f1, copie isolée `/tmp/trace-v01-validation` ; aucun contrôle direct
ou écrasement de la session Editor ouverte. Baseline source conservée dans
`/tmp/trace-v03-baseline` pour distinguer les modifications préexistantes.

- Les **19 tests V0.1/V0.2** passent avant les changements.
- Après intégration : **34/34 tests PlayMode réussis**, soit 19 tests existants et
  15 tests V0.3 ; aucun test ignoré.
- Compilation sans warning/erreur C#, aucun log runtime inattendu dans les tests.
- NavMesh chargé à l’ouverture de scène ; chemin complet contournant un obstacle
  vérifié, puis déplacement effectif de l’agent jusqu’à portée d’attaque.
- Préparation immobile, dégât unique de 20 PV, récupération, perte de cible,
  reprise de poursuite, esquive directionnelle/arrière, collisions et cooldown
  vérifiés. Limites de la fenêtre invulnérable, esquives tôt/tard et absorption
  sans dommage différé testées avec l’attaque ennemie réelle.
- Mort ennemie testée avec quatre clics joueur ; arrêt agent/collider puis
  désactivation. Mort joueur et arrêt des contrôles testés.
- Un scénario graphique supplémentaire a exécuté la préparation, une esquive
  absorbant le coup (100 PV conservés) et une contre-attaque (ennemi à 75 PV).
  Captures réalisées depuis la caméra de jeu puis inspectées visuellement :
  `Docs/Validation/Duel-windup.png`, `Duel-dodge.png`, `Duel-counter.png`.
- Aucun objet V0.1/V0.2 supprimé. Health, EnemyDummy, packages et réglages projet
  préexistants préservés. Les nouvelles données de navigation sont sérialisées.

Rapport conservé : `Docs/Validation/TRACE-V0.3-PlayMode.xml`.
Logs locaux : `/tmp/trace-v03-baseline.log`, `/tmp/trace-v03-playmode2.log`,
`/tmp/trace-v03-visual2.log`. Les messages d’environnement batch Unity
(licence/cloud/debugger) déjà présents sont distincts des logs gameplay.

```sh
Unity -batchmode -nographics -projectPath /chemin/Project-TRACE \
  -runTests -testPlatform PlayMode -testFilter TRACE.Tests \
  -testResults /tmp/trace-v03.xml -logFile /tmp/trace-v03.log
```

Utiliser une copie ou fermer l’autre instance du même projet avant le mode batch.

| Critère | Résultat / limite |
| --- | --- |
| Détection et poursuite | Testées, y compris contour d’obstacle |
| Arrêt à portée | Position immobile durant préparation vérifiée |
| Attaque lisible | Jaune puis rouge, inspectés avec caméra de jeu |
| Dégâts ennemis | 20 PV, une fois par coup, cooldown vérifié |
| Esquive | Input Espace, directions et collision validés |
| Invulnérabilité | Début/fin/annulation et attaque réelle validés |
| Timing | Réussite pendant la fenêtre ; échec tôt/tard vérifié |
| Mort ennemie | Quatre coups, agent arrêté, collider coupé, disparition |
| Boucle de duel | Séquence esquive → contre-attaque exécutée et capturée |
| Console | Pas de warning C# ni erreur runtime pendant validation isolée |
| Tests V0.1/V0.2/V0.3 | 34/34 réussis |

Le feeling, la lisibilité en mouvement libre et la Console de la session utilisateur
restent à confirmer manuellement après rechargement. Aucun build exécutable produit.
