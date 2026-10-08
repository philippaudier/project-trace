# TRACE V0.6 — trois compétences de rôle

## Lancer et tester

Ouvrir `Assets/TRACE/Scenes/Prototype.unity`, puis Play. La scène est de nouveau
la première des Build Settings. Elle contient trois membres, quatre
BasicMeleeEnemy groupés dans l'arène à X≈-11, Z≈10–14 et un NavMesh déjà calculé.
La squad démarre à l'entrée de cette rencontre. Aucun câblage, package ou bake
manuel n'est nécessaire. Les obstacles et mannequins du terrain sont conservés.

- **1** : Assault orange, **2** : Control vert, **3** : Support bleu.
- **Q** : compétence du membre actif (action Input System `Player/Skill`).
- Clic gauche : attaque normale ; clic droit : esquive, binding utilisateur conservé.
- Le panneau en haut à gauche indique READY ou le cooldown restant, les HP et
  les points de shield (SH) de chaque membre. Le nom du skill actif est affiché dessous.

Les compétences ne sont jamais déclenchées par l'IA. Q maintenu ne les répète pas.
Si un switch et Q arrivent dans la même frame, le nouveau membre utilise son skill.

## Réglages Inspector

| Personnage / composant | Réglages livrés |
| --- | --- |
| Member 1 - Assault / DashStrike | Cooldown 5 s ; Target Range 6 m ; cône ±45° ; Damage 35 ; Dash Duration 0,20 s ; Untargeted Distance 3 m ; Arrival Range 1,5 m |
| Member 2 - Control / GravityFieldSkill | Cooldown 8 s ; Cast Range 6 m ; Forward Distance 4 m |
| Gravity Field / GravityField | Radius 3 m ; Duration 3 s ; Slow Fraction 0,4 ; Attraction Speed 1,2 m/s |
| Member 3 - Support / PulseShield | Cooldown 10 s ; Capacity 30 HP ; Duration 5 s |

Les cooldowns sont les champs hérités de CharacterSkill sur chaque composant.
Le bouclier de chaque membre est géré par son composant Shield. Les réglages de
capacité/durée du lancement collectif se trouvent sur PulseShield.

Dash Strike choisit une cible vivante, visible, devant le personnage. Il avance
sur 0,20 s, en s'arrêtant idéalement à 0,9 m de la position initiale de la cible.
Sans cible, il suit l'input de déplacement relatif à la caméra, ou l'avant.
Les dégâts ne sont appliqués qu'à l'arrivée, si la cible reste assez proche et
visible. Le CharacterController conserve collisions, sol et gravité. Le recul
visuel existant d'EnemyDummy sert de léger stagger ; aucun déplacement physique
de l'agent ennemi n'est ajouté au hit. Le dash ne donne pas d'i-frames.

Gravity Field vise une cible visible ou le sol devant le joueur. Les cercles
violets matérialisent le rayon. Le placement cherche le NavMesh ; un placement
impossible ne consomme pas le cooldown. Un mur bloque l'application du champ.
EnemyGravityResponse mémorise la vitesse de l'agent, applique 60 % de celle-ci,
puis la restaure à l'expiration, à la mort ou à la désactivation. Quitter la zone
retire le ralentissement au plus tard 0,1 s après le dernier contact.
L'attraction utilise NavMeshAgent.Move et son raycast de navigation, sans forces
Rigidbody ni écriture directe du Transform. Une seule zone par Control est
réutilisée ; aucun framework de debuffs ni cumul de champs n'est introduit.

Pulse Shield donne 30 points à chaque membre vivant, où qu'il soit dans la squad.
Le pipeline est `attaque → DamageReceiver / i-frames → Shield → Health`.
Un impact entièrement absorbé est considéré comme reçu, sans perte de HP.
Les i-frames empêchent aussi la consommation du shield. Un nouveau shield
remplace le précédent, sans cumul. Les points restants expirent après 5 s ;
les deux halos cyan disparaissent à l'épuisement, à l'expiration ou à la mort.
Health reste inchangé : les appels directs à Health.TakeDamage, utilisés par
certains tests pour provoquer une mort, contournent volontairement DamageReceiver.

## Switch, mort et temps

Chaque skill conserve son propre timestamp de disponibilité sur son personnage.
Un aller-retour entre membres ne le réinitialise pas. Cooldowns, dash, zone et
shield utilisent le temps de jeu et se figent pendant une pause.

Un switch ou une mort pendant Dash Strike **annule le déplacement et le hit
restant**, en conservant le cooldown engagé. Attendre les 0,20 s du dash avant
le switch permet donc d'infliger ses dégâts. Les attaques normales en préparation
sont annulées pendant le dash. Esquive et dash ne peuvent pas se superposer ;
une demande d'esquive simultanée à Q a priorité.

Gravity Field et les shields déjà appliqués continuent jusqu'à leur expiration,
même après le switch ou la mort de leur lanceur. Aucun personnage mort ne peut
activer une compétence ou recevoir un nouveau shield.

## Fichiers de cette version

Créations dans `Assets/TRACE` :

- `Skills/CharacterSkill.cs` : activation, autorité joueur et cooldown individuel.
- `Skills/DashStrike.cs`, `GravityFieldSkill.cs`, `PulseShield.cs` : les trois skills.
- `Skills/SkillTargeting.cs` : sélection partagée de cible visible dans un cône.
- `Skills/GravityField.cs`, `EnemyGravityResponse.cs` : zone et réponse NavMesh locale.
- `Skills/SkillHud.cs` : panneau prototype READY/cooldown, HP et SH.
- `Combat/Shield.cs` : absorption, durée et halos indépendants de Health.
- `Editor/PrototypeSkillsSetup.cs` : migration unique de la scène, protégée contre
  une réexécution qui écraserait les réglages ; déjà exécutée pour la livraison.
- `Tests/PlayMode/SkillTests.cs` : 23 tests V0.6.
- `Scenes/PrototypeLegacy.unity` : copie exacte de l'ancienne scène avant migration,
  avec son propre GUID ; fixture conservée pour V0.1–V0.3.
- `Scenes/Navigation/SkillsNavMesh.asset`, trois matériaux `Skill*` et leurs métadonnées.

Modifications :

- `Characters/ThirdPersonMotor.cs` : dash intégré à l'unique propriétaire du mouvement.
- `Combat/DamageReceiver.cs` : absorption optionnelle par Shield avant les HP.
- `Combat/PlayerMeleeAttack.cs`, `PlayerRangedAttack.cs` : exclusion pendant le dash.
- `Input/TracePlayerInput.cs`, `TRACEInput.inputactions` : lecture de Skill et binding Q.
- `Scenes/Prototype.unity`, `ProjectSettings/EditorBuildSettings.asset` : rencontre V0.6.
- `Tests/PlayMode/{PrototypeTests,CombatTests,DuelTests}.cs` : seule la scène chargée
  devient PrototypeLegacy ; les assertions précédentes sont conservées.
- `Tests/PlayMode/SwitchTests.cs` : placement des deux compagnons dans la rencontre
  du test de switches répétés, au lieu de les laisser au spawn loin du premier membre.
- Contexte projet et présente documentation.

SquadController n'a pas reçu de logique de compétences. Le changement utilisateur
du timing du pulse de sélection présent avant V0.6 est conservé. Les modifications
préexistantes URP, ShaderGraph, VS Code et SceneTemplateSettings sont conservées.
PrototypeSquad et PrototypeSwitch, leurs matériaux et leur navigation sont inchangés.

## Validation du 7 octobre 2026

Unity 6000.3.10f1 sous Windows, copie isolée `Logs/v06-project`.

- Baseline avant modification : **75/75** tests précédents réussis.
- Final : **98/98** tests PlayMode réussis, sans test ignoré (75 précédents + 23 V0.6).
- Graphique : **1/1** scénario réussi, capturant dash, champ, shields puis un combat
  réel avec ennemis et companions activés. Les quatre captures ont été inspectées.
- Pas d'erreur/warning C# ni de log gameplay inattendu dans les exécutions finales.
  Les diagnostics batch de licence, debugger et arrêt Mono restent des messages
  d'environnement ; ils sont distingués de la Console gameplay.

Couverture V0.6 : Q et autorité exclusive, sélection simultanée, dash progressif,
dégâts uniques à l'arrivée, dash sans cible et selon le déplacement, mur bloquant,
annulation au switch, cooldowns 5/8/10 s, rotation complète des trois personnages,
ralentissement de 40 %, attraction NavMesh, sortie de zone et restitution de vitesse,
durée du champ après mort du lanceur, shields collectifs, absorption et excédent,
expiration, i-frames, dommages invalides, membres morts et pause.

La première suite complète a révélé un test V0.5 intermittent : il téléportait
uniquement le premier membre dans l'arène puis switchait vers les compagnons
encore au spawn, favorisant un rappel plutôt qu'un combat. Il passait isolément.
Sa préparation a été stabilisée avec les trois positions dans la rencontre ;
ses assertions d'autorité, de survie et de dégâts restent inchangées.
L'échec est conservé dans `Logs/v06-all-first.xml` et `Logs/v06-all.log`.

Rapports : `Docs/Validation/TRACE-V0.6-{Baseline,PlayMode,Visual}.xml`.
Captures : `Skills-dash.png`, `Skills-gravity.png`, `Skills-shields.png`,
`Skills-live-combat.png`. Le script reproductible est `Docs/Validation/SkillsVisualCapture.cs`.
Les captures proviennent de Camera.Render : elles couvrent les effets dans le monde,
pas le panneau IMGUI. Son confort de lecture reste à confirmer dans la Game View.

Commande de tests sur la copie isolée :

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.com' `
  -batchmode -nographics -projectPath 'CHEMIN_DU_PROJET' `
  -runTests -testPlatform PlayMode -testFilter TRACE.Tests `
  -testResults 'CHEMIN_DU_RAPPORT.xml' -logFile 'CHEMIN_DU_LOG.log'
```

Pour reproduire les captures : copier SkillsVisualCapture.cs dans les tests PlayMode
de la copie isolée, retirer `-nographics`, filtrer `TRACE.Tests.SkillsVisualCapture`
et définir `TRACE_CAPTURE_OUTPUT` vers le dossier de sortie. Retirer ensuite cette
copie temporaire du script avant d'exécuter la suite normale.

## Essai manuel restant

1. Ouvrir Prototype, avancer vers le groupe, faire Assault Q et attendre l'arrivée.
2. Passer sur Control, lancer Q dans le groupe puis passer sur Support et lancer Q.
3. Revenir sur Assault et vérifier le cooldown, les halos, le ralentissement et les HP.
4. Comparer le combat avec/sans cette rotation pour juger sa valeur tactique.
5. Vérifier la lisibilité du panneau à la résolution de la Game View et la Console
   de la session utilisateur.

Le ressenti et l'équilibrage restent à valider par le joueur. Aucun build exécutable,
test de performance ou travail V0.7 n'a été réalisé.
