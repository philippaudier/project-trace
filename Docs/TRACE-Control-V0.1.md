# TRACE — Control V0.1 (ORIGIN Field Specialist, intégration visuelle et structurelle)

Le membre 2 de la squad devient **CONTROL**, spécialiste de terrain ORIGIN. Elle n'est pas une Tracewalker :
ses capacités sont de la technologie ORIGIN (analyse, stabilisation, générateurs de champ). Mécaniques inchangées :
même Gravity Field, mêmes attaques, même comportement de compagnon, même capsule de collision.

## Référence

Planche « CONTROL — ORIGIN Field Specialist (female) — Concept Design V2 » : silhouette élancée et verticale,
cheveux longs noirs avec mèches cyan pâle, manteau blanc cassé asymétrique en panneaux superposés, sous-tenue
noire, module de torse, gants noirs, staff modulaire long à tête annulaire cyan, marque ORIGIN sur l'épaule.
Palette : noir, anthracite, gris, gris clair, jaune ORIGIN, cyan pâle.

## Identité (`CharacterProfile`)

| Champ | Valeur |
| --- | --- |
| CharacterId | `origin_control_01` |
| DisplayName | `Control` |
| Designation | `Field Specialist — Control` |
| Affiliation | `ORIGIN` |
| Archetype | `Mid-Range Controller` |
| IsTracewalker | `false` |
| AccentColor | cyan froid (0,5 ; 0,85 ; 1) |

`CharacterProfile` gagne `Affiliation` et `IsTracewalker` ; le Tracewalker est marqué `IsTracewalker = true`, affiliation vide.

## Silhouette : comment elle se distingue du Tracewalker

| | Tracewalker | Control |
| --- | --- | --- |
| Stance | hanches à ±0,13 m, jambes 0,20 × 0,24 | hanches à ±0,10 m, jambes 0,16 × 0,20 |
| Torse | 0,46 × 0,56 × 0,28, épaules larges, capuche | 0,36 × 0,54 × 0,22, épaulettes plates, col cyan |
| Manteau | pans symétriques courts blanc cassé | **asymétrique** : long pan gauche blanc cassé jusqu'au tibia liseré cyan, revers avant blanc cassé, pan droit court anthracite, dos blanc cassé à ligne cyan |
| Tête | cheveux courts noirs | cheveux longs noirs tombant dans le dos, mèche cyan |
| Arme | lame courte vers l'avant | **staff** vertical de 1,9 m planté au repos, anneau de champ en tête |
| Module | Trace Module ambre sur le torse | Field Control Module cyan dans le dos, lentille de hanche, module de torse |
| Couleur dominante | ambre | cyan |
| Mouvement | nerveux, grandes amplitudes | posé, petites amplitudes, réponse plus lente |

De loin : une forme verticale blanche et noire avec une barre verticale (le staff) et des lignes cyan, contre une
forme plus massive aux pieds ambre et à la lame basse. Les deux restent dans la capsule 1,8 m / 0,35 du gameplay.

## Usage du cyan

- Technologie de contrôle : lentille du Field Control Module (émissive), lentille de hanche, module de torse,
  anneau et cœur du staff (émissifs à 0,5), anneaux du Gravity Field (`M_Control_Field`, unlit).
- Identité : col, manchettes, liserés du manteau et du revers, ligne du dos, mèche de cheveux, lignes de bottes (`M_Control_Cyan`).
- Le jaune ORIGIN n'apparaît que sur la marque d'épaule, une bande fine du module dorsal et une bague du staff
  (`M_Control_OriginAmber`). Testé : le nombre de pièces cyan dépasse le nombre de pièces ambre.

## Marque ORIGIN

Sur la plaque d'épaule droite (`Origin Mark`), face extérieure : plaque blanc cassé, barres noires, trait ambre.
Même géométrie que celle du Tracewalker, portée ici comme insigne institutionnel.

## Field Control Module

Unité dorsale légère (`Field Control Module`, 0,24 × 0,32 × 0,10 m, anthracite) avec un cœur cyan émissif
(`Module Core`) et une bande ambre ORIGIN. Le composant `FieldControlModule` lit `GravityFieldSkill.Field` :
émission 0,5 au repos, 1,8 tant que le Gravity Field est déployé, réponse 0,2 s. Aucune réaction au Tactical Focus.
Un relais sur la hanche (`Hip Lens`) et une lentille de torse partagent le même matériau.

## Gravity Staff

`Gravity Staff` sous l'épaule droite : hampe gris foncé légèrement métallique de 1,9 m, segment bas et segment haut
anthracite, bague de grip ambre, anneau de champ en tête (douze segments, huit cyan émissifs et quatre anthracite) autour d'un cœur cyan.
Tenu verticalement au repos ; il pivote avec le bras pendant la frappe. Purement visuel.

## Gravity Field

Mécanique intacte (`GravityField` : rayon 3 m, durée 3 s, ralentissement, attraction). Les trois anneaux passent
au cyan froid (`M_Control_Field`) et `FieldVisualPulse` (sur `Gravity Visual`) les fait contre-tourner
(cœur 40 °/s, intérieur −18 °/s, extérieur 9 °/s) avec une pulsation de largeur ±15 % à 1,5 Hz, en temps de jeu.
Pas de particules, pas de violet.

## Animation procédurale

`CharacterPuppet` (ex-`TracewalkerPuppet`, même GUID, généralisé) avec un réglage « posé » :

| Paramètre | Tracewalker | Control |
| --- | --- | --- |
| legSwing / armSwing | 32° / 22° | 22° / 10° |
| bobHeight | 3,5 cm | 1,8 cm |
| sprintLean / breathing | 8° / 1,2° | 5° / 0,5° |
| attaque armé / frappe | +60° / −75° | +30° / −60° |
| dodgeCrouch / dodgeLean | 28 cm / 12° | 20 cm / 8° |
| dashLean / hitFlinch | 22° / 14° | 12° / 9° |
| poseResponse / strikeResponse | 14 / 28 | 9 / 18 |
| focusGesture | oui (main au module) | non |

## Matériaux (`Assets/TRACE/Scenes/Materials/Control/`)

| Matériau | Base | Notes |
| --- | --- | --- |
| `M_Control_Charcoal` | (0,12 ; 0,13 ; 0,15) | sous-tenue, pan court, modules |
| `M_Control_Shadow` | (0,04 ; 0,04 ; 0,05) | bottes, gants, cheveux, barres ORIGIN |
| `M_Control_CoolGrey` | (0,50 ; 0,54 ; 0,60) | tête |
| `M_Control_OffWhite` | (0,84 ; 0,85 ; 0,84) | manteau, épaulettes, plaque de torse, plaque ORIGIN |
| `M_Control_Cyan` | (0,55 ; 0,82 ; 0,92) | liserés, col, manchettes, mèche |
| `M_Control_OriginAmber` | ambre ORIGIN | marque, bande du module, bague du staff |
| `M_Control_FieldModule` | (0,08 ; 0,12 ; 0,15) + émission cyan 0,5 | cœur du module, lentilles, anneau et cœur du staff |
| `M_Control_Staff` | (0,22 ; 0,24 ; 0,27), métallique 0,5 | hampe |
| `M_Control_Field` | unlit (0,45 ; 0,88 ; 1) | anneaux du Gravity Field |

## Fichiers

Créés : `Characters/FieldControlModule.cs`, `Skills/FieldVisualPulse.cs`, `Editor/SquadVisualKit.cs` (helpers partagés),
`Editor/ControlSetup.cs` (menu `TRACE/Apply Control V0.1`, idempotent), `Tests/PlayMode/ControlTests.cs`,
`Docs/Validation/ControlVisualCapture.cs`, neuf matériaux.
Modifiés : `Characters/CharacterProfile.cs` (affiliation, isTracewalker), `Characters/CharacterPuppet.cs` (renommé,
champs `breathing` et `focusGesture`), `Editor/TracewalkerSetup.cs` (utilise le kit), `Tests/PlayMode/TracewalkerTests.cs`,
`Tests/PlayMode/TracewalkerAnimationTests.cs`, les trois scènes `Prototype`, `PrototypeEncounter`, `FirstTrace`.

## Validation

| Étape | Résultat | Preuve |
| --- | --- | --- |
| Suite complète | 210/210 (204 précédents + 6 Control) | `Validation/TRACE-Control-PlayMode.xml` |
| Captures | 1/1, cinq vues 1280x720 | `Validation/TRACE-Control-Visual.xml`, `Validation/Control-*.png` |

Tests Control : profil, module, staff, marque et matériaux sur le membre 2 des trois scènes ; jamais Tracewalker,
cyan dominant, stance plus étroite, arme plus longue, marionnette plus calme sans geste de Focus ; Gravity Field cyan
qui tourne et allume le module dorsal, rayon inchangé ; Tactical Focus sans réaction spéciale quand elle est contrôlée ;
étiquette HUD `CONTROL` en cyan. Toutes les suites précédentes restent vertes.

## Vérification manuelle

1. Rouvrir les scènes dans l'Editor sans sauvegarder la version en mémoire (fichiers remplacés sur disque).
2. `Prototype`, touche 2 : la silhouette de Control se lit à la distance caméra, staff visible, cyan avant ambre.
3. E : le Gravity Field cyan tourne lentement, le module dorsal s'allume pendant 3 s puis redescend.
4. Marcher, frapper, esquiver avec Control : gestes plus posés que le Tracewalker. Amplitudes dans l'Inspector du
   `CharacterPuppet` de `Member 2 - Control`.
5. `FirstTrace` : les trois membres se distinguent dans le quai sombre (Control blanc et noir vertical, Tracewalker
   massif à pieds ambre, Support capsule bleue).
6. Hors périmètre : les locuteurs de dialogue de la slice restent `CONTROL` / `ASSAULT` / `SUPPORT` (contenu narratif).
