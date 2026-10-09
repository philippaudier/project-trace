# TRACE — Support V0.2 (ORIGIN Field Specialist, nouvelle direction visuelle)

Intégration de la planche officielle « SUPPORT — Field Specialist » (gauntlet, support module, projection de barrière).
Le staff annulaire de la V0.1, trop proche de Control, disparaît. Mécaniques inchangées : même `PulseShield` (30 de
bouclier pour les trois membres pendant 5 s), même attaque à distance, même compagnon, même capsule, mêmes combos.
Menu : `TRACE/Apply Support V0.2` (idempotent, Prototype, PrototypeEncounter, FirstTrace).

## Identité (`CharacterProfile`)

| Champ | Valeur |
| --- | --- |
| CharacterId | `origin_support_01` |
| DisplayName | `Support` |
| Designation | `Field Specialist — Support` |
| Affiliation | `ORIGIN` |
| Archetype | `Defensive Support` |
| IsTracewalker | `false` |
| AccentColor | turquoise-menthe (0,36 ; 0,90 ; 0,74), vert dominant (Control : bleu dominant) |
| Portrait | `UI/Portraits/Portrait_Support.png`, recadrage 256 × 256 de l'illustration principale de la planche (même GUID) |

La planche indique aussi le rôle long « Defensive Support / Field Stabilization Specialist ». L'archétype garde la forme
courte pour que la ligne de rôle du panneau opérateur reste propre.

## Différenciation

| | Tracewalker | Control | Support |
| --- | --- | --- | --- |
| Signature | lame courte | staff 1,9 m, anneau | **gauntlet** sur l'avant-bras gauche, aucune arme longue |
| Silhouette | nerveuse | verticale (staff au-dessus de la tête) | **compacte** : rien ne dépasse la capuche, ≥ 0,2 m sous le haut de Control |
| Module | Trace Module ambre (torse) | boîtier cyan (dos) | **disque** blanc et charbon, anneau menthe tournant (dos) |
| Cheveux | courts noirs | longs noirs | **cendré clair**, frange, mèches latérales, queue haute, pince menthe |
| Accent | ambre | cyan froid | turquoise-menthe ; ambre ORIGIN limité à la marque d'épaule, au bandeau du gauntlet et à celui du module |
| Gestes | nerveux | posé | **gardé** : gauntlet tenu légèrement devant (16°), relevé en combat (34°), bras tendu (85°) au Pulse Shield |

## SupportGauntlet (élément signature)

Pivot `Support Gauntlet` sous `Shoulder L` (l'avant-bras suit le bras de la marionnette) : sous-couche charbon, coque
blanche (`M_Support_Gauntlet`), deux plaques déployables (externe et avant), bandeau ambre ORIGIN, protège-phalanges,
disque émetteur menthe tourné vers l'extérieur avec huit segments (lisible de dos), lentille avant. `Emitter Point`
juste après la main porte la `Barrier Projection` (hexagone extérieur, hexagone intérieur, trois rayons, lignes fines).

Composant `SupportGauntlet` (visuel seul) : écoute `CharacterSkill.Activated` du Pulse Shield et lit les `Shield` des
trois membres. Émission 0,6 au repos, éclat 3 à l'activation, 1,6 tant qu'un bouclier tient ; plaques ouvertes de
2,5 cm tant que le bouclier tient ; projection hexagonale 0,6 s (ouverture puis fondu) ; un lien menthe de 0,45 s de
l'émetteur vers chaque autre membre protégé. Temps de jeu : le Tactical Focus le ralentit.

## Support Module

Pivot `Support Module` dans le dos (≈ 0,3 m) : boîtier cylindrique charbon, quatre plaques de coque blanches (même
matériau que le gauntlet, ils se répondent), `Module Ring` de huit segments, cœur menthe, bandeau ambre.
`SupportModule` garde son rôle (émission 0,5 au repos, 1,7 tant qu'un bouclier est actif) et fait tourner l'anneau
jusqu'à 60 °/s pendant le bouclier. Le gauntlet projette, le module amplifie ; aucune réaction au Tactical Focus.

## Pulse Shield : lien visuel

Mécanique intacte. Séquence à l'activation : le bras gauche se tend, le gauntlet s'ouvre et s'illumine, la barrière
hexagonale s'ouvre devant la main, des liens partent de l'émetteur vers les autres membres, les halos menthe
apparaissent sur les trois (deux anneaux lents existants + trois arcs méridiens très fins `Dome Arc` qui ferment un
dôme), le module dorsal s'allume et son anneau tourne. Pas de particules, pas de violet, matériau unlit sans bloom ajouté.

## Animation procédurale (`CharacterPuppet`)

Ajouts génériques, inactifs par défaut (Tracewalker et Control inchangés) : `guardArmPitch` et `combatGuardPitch` (bras
gauche porté devant, balancement réduit à 40 %, garde haute quand le compagnon est en combat ou quand l'attaque à
distance du joueur a une cible), `skill` + `skillArmPitch` / `skillGestureTime` (extension du bras à l'activation).
Réglages du Support : amplitudes les plus faibles des trois, réponse 8 /s, garde 16° / 34°, extension 85° pendant 0,5 s.
`CharacterSkill` expose un événement `Activated` (présentation seulement).

## Matériaux (`Assets/TRACE/Scenes/Materials/Support/`)

| Matériau | Base | Usage |
| --- | --- | --- |
| `M_Support_Charcoal` | (0,11 ; 0,12 ; 0,13) | sous-tenue, boîtiers, sous-couche du gauntlet, liserés |
| `M_Support_Shadow` | (0,04 ; 0,04 ; 0,05) | bottes, gants, genouillères, harnais, barres ORIGIN |
| `M_Support_CoolGrey` | (0,62 ; 0,60 ; 0,58) | tête |
| `M_Support_OffWhite` | (0,88 ; 0,88 ; 0,86) | manteau, capuche, manches, épaulières, embouts de bottes |
| `M_Support_Hair` | (0,74 ; 0,71 ; 0,66) | cheveux cendré clair |
| `M_Support_Mint` | (0,40 ; 0,86 ; 0,74) | pince, manchette droite |
| `M_Support_OriginAmber` | ambre ORIGIN | marque d'épaule, bandeau du gauntlet, bandeau du module |
| `M_Support_Gauntlet` | (0,93 ; 0,93 ; 0,91), lisse 0,6, métal 0,15 | coque et plaques du gauntlet, coque du module, module de torse |
| `M_Support_Module` | (0,05 ; 0,12 ; 0,11) + émission menthe | émetteurs, lentilles, cœur et anneau du module |
| `M_Support_Shield` | unlit (0,42 ; 0,95 ; 0,80) | halos, arcs du dôme, barrière projetée, liens |

`M_Support_Staff` est supprimé. Le setup réaccorde les couleurs des matériaux existants à chaque exécution.

## Fichiers

Créés : `Characters/SupportGauntlet.cs`, `Docs/TRACE-Support-V0.2.md`, `Materials/Support/M_Support_Gauntlet.mat`.
Modifiés : `Editor/SupportSetup.cs` (V0.2), `Characters/SupportModule.cs` (anneau), `Characters/CharacterPuppet.cs`
(garde, geste de skill), `Skills/CharacterSkill.cs` (événement `Activated`), `UI/Portraits/Portrait_Support.png`,
`Tests/PlayMode/SupportTests.cs`, `Docs/Validation/SupportVisualCapture.cs` (vue `Support-cast`), les trois scènes,
les matériaux Support existants (couleurs). Supprimé : `M_Support_Staff.mat`.

## Validation

| Étape | Résultat | Preuve |
| --- | --- | --- |
| Suite complète | 226/226 (dont les 5 tests Support mis à jour) | `Validation/TRACE-Support-V0.2-PlayMode.xml` |
| Captures | 1/1, six vues 1280x720 | `Validation/TRACE-Support-V0.2-Visual.xml`, `Validation/Support-*.png` |

Tests Support : identité, gauntlet, module, matériaux (sans staff) dans les trois scènes ; silhouette compacte sous
Control, module entre 0,2 et 0,4 m ; Pulse Shield : geste du bras, projection, liens, éclat, plaques ouvertes, halos
et dôme menthe sur les trois, capacité inchangée, module allumé et anneau tournant ; Tactical Focus sans réaction
(garde habituelle) ; HUD (carte, portrait, panneau actif, accent dans l'overlay).

## Vérification manuelle

1. Rouvrir les scènes sans sauvegarder la version en mémoire (fichiers remplacés sur disque).
2. `Prototype`, touche 3 : vue de dos, le gauntlet et son émetteur à gauche, le disque dans le dos ; rien ne gêne la caméra.
3. E : bras tendu, barrière hexagonale, liens vers Tracewalker et Control, halos en dôme, module qui tourne.
4. Les trois côte à côte : lame / staff / gauntlet, trois accents, trois hauteurs de silhouette.
5. Réglages : garde et extension dans le `CharacterPuppet` du membre 3, durées et intensités dans `SupportGauntlet`.
