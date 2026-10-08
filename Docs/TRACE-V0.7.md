# TRACE V0.7 — opportunités de combo

## Essayer dans Prototype

Ouvrir `Assets/TRACE/Scenes/Prototype.unity`, puis Play. La rencontre V0.6 de quatre
BasicMeleeEnemy est conservée, avec les nouveaux indicateurs sur les ennemis et les
trois membres. Aucun bake, package ou câblage supplémentaire n'est nécessaire.

1. Passer sur **2 / Control**, lancer **Q / Gravity Field** près du groupe et
   attendre environ 0,6 s. Les cibles préparées portent un anneau jaune.
2. La consigne **COMBO — 1 puis Q : DASH** invite à sélectionner Assault, puis à
   utiliser Dash Strike sur une cible préparée. Le choix reste entièrement manuel.
3. Passer sur **3 / Support**, lancer **Q / Pulse Shield**, puis laisser un ennemi
   frapper un membre protégé. Après 20 dégâts absorbés, ce membre porte un anneau cyan
   et une consigne **CONTRE — [numéro du membre] puis clic gauche**.
4. Contrôler ce membre et réussir une attaque de base ; Assault peut aussi exploiter
   Protected avec un dash normal. Une attaque ratée ne gaspille pas l'opportunité.

La présentation conserve un marqueur par cible mais n'affiche qu'une consigne
détaillée à la fois. Priorité : combo déclenché, Protected sur le membre actif,
Grouped, puis Protected sur un autre membre. Entre candidats de même priorité,
le plus proche du centre de la vue est choisi. Seuls les labels dans la vue sont
sélectionnés. Cette sélection d'affichage n'impose ni cible d'attaque ni switch.

## Valeurs et réglages Inspector

| Composant | Paramètre | Valeur |
| --- | --- | --- |
| Gravity Field / GravityField | Grouped Dwell Time | 0,6 s de présence continue |
| Gravity Field / GravityField | Grouped Duration | 2,5 s, sans rafraîchissement continu |
| Assault / DashStrike | Grouped Damage Multiplier | 1,5 : 52,5 dégâts au lieu de 35 |
| Assault / DashStrike | Combo Radius | 2 m autour de la cible touchée |
| Assault / DashStrike | Splash Damage Fraction | 0,5 du dash de base : 17,5 dégâts par voisin |
| Chaque membre / Shield | Protected Threshold | 20 dégâts réellement absorbés par ce shield |
| Chaque membre / Shield | Protected Duration | 3 s |
| Chaque membre / ComboOpportunity | Protected Damage Multiplier | 1,2 : bonus de 20 % |
| ComboOpportunity | Rearm Delay | 0,5 s après consommation ou expiration |
| ComboFeedback | Impact Duration | 0,35 s de flash/anneau expansif |

Les compétences V0.6 conservent leurs dégâts normaux, durées et cooldowns.
Protected donne par exemple 30 dégâts à l'attaque d'Assault (25 normalement),
14,4 à celle de Control (12), 9,6 au tir de Support (8), ou 42 au dash normal (35).

## Règles de consommation

`ComboOpportunityType` contient None, Grouped et Protected. Le composant expose le
type, le temps restant, la source, la cible Health et l'historique du dernier
déclenchement pour le feedback. Chaque acteur conserve son propre état.

Grouped appartient à l'ennemi. Le champ doit l'observer pendant 0,6 s consécutive ;
sortir avant ce délai remet la préparation à zéro. Un même lancement de Gravity Field
ne peut préparer une même cible qu'une fois, même après consommation, expiration ou
sortie/rentrée. Un lancement ultérieur peut la préparer à nouveau après verrouillage.
Les multiples colliders d'une cible ne multiplient pas le temps d'exposition.

Seul le Dash Strike contrôlé exploite Grouped, à l'arrivée, après validation de la
portée, de la visibilité et de la réception des dégâts. Un dash annulé au switch
laisse l'opportunité disponible. La cible principale reçoit 52,5 dégâts une seule
fois ; chaque voisin vivant dans les 2 m reçoit 17,5 dégâts au maximum, avec test
de visibilité. Les alliés ne sont pas touchés. L'AoE n'exploite pas les opportunités
des voisins et ne déclenche aucune réaction en chaîne. Le combo fonctionne même si
son coup principal tue la cible.

Protected appartient au **membre ayant absorbé les dégâts**, pas à toute la squad.
Le joueur peut le sélectionner pour utiliser l'opportunité ; les autres membres ne
la dépensent pas. Chaque application de shield produit au maximum une opportunité.
Les i-frames, les dégâts invalides et les pertes de HP directes ne contribuent pas
au seuil. Une absorption supplémentaire ne prolonge pas la fenêtre existante.

Protected est consommé au premier impact valide de la prochaine attaque de base
ou d'un dash normal contrôlé. Le multiplicateur de mêlée reste le même pour les
cibles de cette unique attaque ; il n'est pas réappliqué plusieurs fois sur la même
cible. Les tirs IA et les frappes des companions conservent leurs dégâts ordinaires
et ne consomment ni Protected ni Grouped. Miss, cible invulnérable ou préparation
annulée ne consomment pas Protected.

Si un dash rencontre Grouped alors qu'Assault possède Protected, **Grouped est
prioritaire et Protected reste disponible**. Les deux bonus ne se cumulent pas sur
le même dash : aucune synergie à trois personnages n'est introduite.

Les opportunités expirent en temps de jeu, se figent à la pause et disparaissent à
la mort/désactivation de leur porteur. Leur création n'interrompt aucune action.
Le feedback renforcé utilise un anneau lumineux expansif et un label COMBO ! ou
CONTRE ! ; il ne modifie pas Time.timeScale et n'ajoute aucune caméra spéciale.

## Fichiers

Créations dans `Assets/TRACE` :

- `Combat/ComboOpportunity.cs` : enum, fenêtre, source/cible, consommation et garde anti-spam.
- `Combat/ComboDamage.cs` : petit point commun de validation/application des hits et de consommation Protected par le joueur.
- `Combat/ComboFeedback.cs` : anneaux, labels au-dessus des acteurs et impact renforcé.
- `Combat/ComboPrompt.cs` : choix d'une seule consigne lisible, sans interférer avec le gameplay.
- `Editor/PrototypeComboSetup.cs` : ajout unique des composants et indicateurs ; refuse d'écraser une scène déjà configurée.
- `Tests/PlayMode/ComboTests.cs` : 25 tests d'intégration V0.7.
- `Scenes/Materials/Combo.mat` et toutes les métadonnées nécessaires.

Modifications :

- `Skills/GravityField.cs`, `EnemyGravityResponse.cs` : exposition des ennemis au champ et préparation Grouped.
- `Skills/DashStrike.cs` : bonus principal, AoE filtrée et priorité sur Protected.
- `Combat/Shield.cs` : comptage de l'absorption par application de shield.
- `Combat/PlayerMeleeAttack.cs`, `PlayerRangedAttack.cs`, `TargetedAttack.cs` : consommation réservée aux impacts contrôlés.
- `Scenes/Prototype.unity` : sept opportunités et feedbacks, un ComboPrompt.
- Contexte projet, présente documentation, rapports et script de capture dans `Docs/Validation`.

Health, SquadController, moteur, Input System et Build Settings n'ont pas été
modifiés pour V0.7. Les changements utilisateur de caméra dans PrototypeSwitch,
le pulse de sélection et les autres modifications préexistantes sont conservés.

## Validation

Unity 6000.3.10f1 sous Windows, projet isolé `Logs/v07-project`.

La baseline avant modification est de **98/98 tests réussis**. La validation finale
est de **123/123 tests PlayMode réussis**, sans test ignoré : 98 précédents et 25 V0.7.
Le rapport exact est `Docs/Validation/TRACE-V0.7-PlayMode.xml`, le log local
`Logs/v07-delivery.log`. Aucun warning/erreur C# ni log gameplay inattendu dans cette
exécution et dans le scénario graphique final. Les diagnostics de licence, debugger
et arrêt Mono présents dans le batch sont des messages d'environnement distincts.
Le scénario graphique `TRACE-V0.7-Visual.xml` a réussi (**1/1**) : préparation par le vrai
Gravity Field, switch manuel et Q, impact renforcé, vrai hit ennemi sur un shield,
sélection du porteur de Protected puis contre-attaque au clic.

La vérification visuelle a révélé des labels superposés sur les ennemis regroupés.
ComboPrompt corrige ce défaut en conservant les marqueurs tout en limitant le texte
détaillé à une seule cible. Les captures finales ont été inspectées à 1280×720 :
`Combo-grouped-ready.png`, `Combo-dash-impact.png`, `Combo-protected-ready.png`,
`Combo-counter-impact.png`.

Les tests couvrent préparation continue, sortie/rentrée, expiration, source/cible,
consommation unique, nouveau lancement, dash normal/renforcé, AoE et colliders
multiples, mur, mort sur le hit principal, switch interrompant le dash, seuil Shield,
durée Protected, attaque ratée, mêlée, tir, dash, non-cumul, IA, porteur au switch,
pause, mort, invulnérabilité, i-frames et priorité de la consigne unique.

La première exécution ciblée a identifié un défaut de préparation d'un test : le
companion ignorait à juste titre un ennemi dont le cerveau était désactivé. Le
scénario active désormais ce cerveau, avec vitesse nulle, pour vérifier une vraie
frappe automatique. Aucun comportement de ciblage IA ni assertion de dégâts n'a
été assoupli. Les 98 tests préexistants n'ont pas été modifiés pour V0.7.

Les deux tests de visibilité utilisent une caméra explicite pour que le cadrage
soit reproductible en mode sans rendu, où il ne faut pas dépendre d'une frame
graphique Cinemachine. La capture visuelle utilise, elle, la caméra du jeu.
L'échec initial de ces tests de visibilité est conservé dans `Logs/v07-ui-first.xml`.

Pour reproduire les tests, utiliser la commande batch des versions précédentes
avec `-testFilter TRACE.Tests`. Pour les captures, copier
`Docs/Validation/ComboVisualCapture.cs` dans les tests PlayMode de la copie isolée,
retirer `-nographics`, définir `TRACE_CAPTURE_OUTPUT` et filtrer
`TRACE.Tests.ComboVisualCapture`. Retirer cette copie temporaire avant la suite normale.

Restent à confirmer manuellement : confort du switch en combat libre, compréhension
des signaux à la résolution de la Game View, équilibrage et Console de la session
utilisateur. Aucun build exécutable ni profiling de performances n'a été effectué.
Aucun travail V0.8 n'a été commencé.
