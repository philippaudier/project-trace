# TRACE V0.2 — premier combat

## Jouer

Laisser Unity importer les scripts, quitter Play si nécessaire puis rouvrir
`Assets/TRACE/Scenes/Prototype.unity`. Aucun câblage Inspector nécessaire.
La zone grise de combat se trouve à droite et derrière le point de départ,
autour de **X=10, Z=-5** ; les trois capsules rouges sont espacées de 4 m.

Approcher un mannequin, orienter le personnage vers lui et cliquer gauche.
Chaque pression déclenche un coup ; maintenir le bouton ne répète pas l’attaque.
Le clic servant à reprendre le curseur après Échap ne déclenche pas de coup.
La marche, le sprint et la caméra restent utilisables pendant les attaques.

Le petit trait/volume jaune matérialise brièvement le coup, même à vide. Un impact
fait flasher le mannequin en jaune et reculer son visuel. Au quatrième coup,
il grise, rétrécit et se désactive. Stop/Play réinitialise les trois mannequins.
Les PV courants sont visibles sur leur composant Health pendant Play.

## Composants

- `Combat/Health.cs` : santé indépendante du joueur et des ennemis ; MaxHealth,
  CurrentHealth, IsDead, TakeDamage et Heal. `OnDamaged(float)` fournit les dégâts
  effectivement appliqués ; `OnDeath()` est émis une seule fois. Les montants
  négatifs, nuls ou non finis sont ignorés. Pas de soin après mort ni résurrection.
  Awake initialise les PV ; désactiver/réactiver le composant ne restaure pas les PV.
- `Combat/PlayerMeleeAttack.cs` : lecture d’Attack, préparation, fenêtre active,
  cooldown, sélection discrète et hitbox. Un HashSet garantit un seul dégât par
  Health et par attaque, même avec plusieurs colliders ou plusieurs frames actives.
  Plusieurs ennemis présents dans la hitbox peuvent chacun recevoir un coup.
- `Combat/EnemyDummy.cs` : abonné aux événements Health, flash via
  MaterialPropertyBlock (matériau partagé intact), recul visuel et mort différée.
  Aucun déplacement du collider, aucune IA ni navigation.
- `Input/TracePlayerInput.cs` et `TRACEInput.inputactions` : Attack sur clic gauche,
  accès à la pression et inhibition des coups au clic de recapture du curseur.
- `Editor/PrototypeCombatSetup.cs` : ajout ciblé à la scène existante ; refuse
  un deuxième ajout. La scène livrée contient déjà le résultat de cette opération.
- `Tests/PlayMode/CombatTests.cs` : santé, événements, hitbox, cooldown, détection,
  ciblage, recul/couleur, mort et interruptions.

Aucun package ajouté. Les composants de déplacement et caméra restent inchangés.
`ProjectSettings/TagManager.asset` ajoute la layer **Damageable** pour isoler les
colliders attaquables. La scène conserve tous ses objets V0.1 et leurs références.

## Réglages Inspector

| Composant | Paramètre | Valeur |
| --- | --- | --- |
| Health | Max Health | 100 PV |
| Player Melee Attack | Damage | 25 PV |
| Player Melee Attack | Range | 1,8 m (distance au bord de la hitbox) |
| Player Melee Attack | Half Width / Half Height | 0,5 / 0,6 m |
| Player Melee Attack | Attack Height | 0,9 m |
| Player Melee Attack | Windup / Active Duration | 0,08 / 0,12 s |
| Player Melee Attack | Cooldown | 0,45 s entre débuts de coups |
| Player Melee Attack | Target Mask | Damageable |
| Player Melee Attack | Obstruction Mask | Default |
| Player Melee Attack | Targeting Half Angle | 35° autour de l’avant du personnage |
| Player Melee Attack | Maximum Aim Correction | 15° maximum sur la direction du coup |
| Enemy Dummy | Hit Duration / Recoil Distance | 0,18 s / 0,18 m |
| Enemy Dummy | Death Delay | 0,55 s |

Le ciblage favorise le plus petit angle avec l’avant du **personnage**, puis la
plus petite distance en cas d’égalité. Il ne suit pas une cible persistante, ne
modifie ni la rotation du personnage ni celle de la caméra. La direction est
choisie au début du coup ; seule la hitbox détermine les dégâts. Les murs Default
interrompent la ligne de vue : pas de dégâts à travers les obstacles.

Les timers suivent le temps de jeu. Désactiver l’attaque ou l’entrée de jeu annule
la fenêtre en cours. Il n’y a ni combo, ni buffer d’entrée, ni auto-attaque.
Les futurs types de dégâts et compétences pourront appeler Health ; aucune de
ces fonctionnalités, ni loot/XP/escouade/IA/ralenti, n’est implémentée.

## Vérification manuelle

1. Toucher chaque mannequin et observer les PV passer de 100 à 75, 50, 25 puis 0.
2. Cliquer rapidement puis maintenir : pas de répétition ni double dégât.
3. Tester un léger décalage angulaire, puis un ennemi derrière soi ou trop loin.
4. Observer le flash, le recul puis la disparition ; aucun contact après la mort.
5. Vérifier Échap/clic, le déplacement pendant l’attaque et la Console.

Le ressenti du coup et du feedback restent à valider en jouant, même après les
contrôles automatisés et l’inspection visuelle.

## Validation exécutée — 2026-10-07

- Baseline : les 8 tests V0.1 passent avant modification ; validation utilisateur
  V0.1 déjà confirmée (feeling et Console).
- Unity 6000.3.10f1 : compilation réussie, aucun warning/erreur C#.
- **19/19 tests PlayMode réussis** : 8 V0.1 et 11 V0.2, sans log runtime inattendu.
- Un scénario PlayMode supplémentaire avec rendu URP a capturé un coup réel,
  quatre attaques, le feedback de mort puis la désactivation ; assertions réussies.
- Captures d’impact et mort inspectées visuellement :
  `Docs/Validation/Combat-impact.png` et `Combat-death.png`.
- Aucun objet V0.1 supprimé ; les seules références existantes modifiées dans la
  scène sont l’ajout du composant/cue au joueur et la liste des objets racines.
- Scripts exécutés identiques aux scripts livrés ; nouveaux `.meta` générés par
  Unity ; aucune dépendance de package ajoutée.

Rapport conservé : `Docs/Validation/TRACE-V0.2-PlayMode.xml`.
Exécution dans `/tmp/trace-v01-validation` (copie temporaire réutilisée).
Logs locaux : `/tmp/trace-v02-baseline.log`, `/tmp/trace-v02-playmode2.log`,
`/tmp/trace-v02-visual.log`. Les diagnostics d’environnement Unity batch
(licence/cloud/debugger) sont distincts des messages gameplay, comme en V0.1.

Commande de reproduction (fermer l’autre instance du même projet ou copier le projet) :

```sh
Unity -batchmode -nographics -projectPath /chemin/Project-TRACE \
  -runTests -testPlatform PlayMode -testFilter TRACE.Tests \
  -testResults /tmp/trace-v02.xml -logFile /tmp/trace-v02.log
```

| Critère | Résultat |
| --- | --- |
| Input Attack | Clic via périphérique Input System simulé : validé |
| Détection proche | Hitbox, couche, angle, portée et obstruction : validés |
| Application de dégâts / diminution des PV | 25 PV par coup, une fois par Health : validé |
| Feedback visible | Couleur/recul testés et rendu PlayMode inspecté |
| Mort après plusieurs coups | Quatre attaques, OnDeath une seule fois : validé |
| Fin des interactions / disparition | Collider coupé immédiatement puis objet inactif : validé |
| Console | Pas d’erreur runtime ou de warning C# durant la validation isolée |
| Périmètre | Revue effectuée ; aucune fonctionnalité V0.3 |

À confirmer par le joueur : ressenti à la souris, lisibilité en mouvement et
Console de la session interactive après rechargement. Aucun build exécutable
n’a été demandé ni produit.
