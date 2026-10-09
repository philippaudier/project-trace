# TRACE — FieldTest V0.1 (Pass A, greybox)

Scène : `Assets/TRACE/Scenes/FieldTest.unity`. C'est une installation ORIGIN partiellement abandonnée dans une vallée
rocheuse, d'environ 240 × 240 m, pensée pour tester l'exploration et le combat dans des espaces variés. Ce n'est pas
un niveau narratif : il n'y a ni quête, ni progression. `Prototype` et `FirstTrace` ne sont pas modifiés.

Générée par `TRACE/Build FieldTest V0.1 (regenerates the scene)` (`Editor/FieldTestSetup.cs`), à partir de
`PrototypeEncounter` (squad, caméra, HUD, audio, Tactical Focus, outline et lock déjà câblés). L'environnement est
remplacé ; le NavMesh est baké puis **vérifié** (19 points clés atteignables depuis le spawn, sinon la génération
échoue). HUD, Audio et Tactical Focus sont réappliqués **à FieldTest seulement** (`ApplyTo`). La scène est du contenu
généré : la régénérer écrase les retouches manuelles.

## Plan

```
                         N (falaises)
 Ravin ┐    Fosse service ─ Lab ─ Salle des machines ─ Passage arrière ─┐    Hutte Trace
 étroit│    (−3,5 m)        │          │                                │    Tour d'observation (vue 3)
       │                Jonction ─ Couloir de service (plafond bas)      │    Plateforme relais (combat vertical)
       │                    │                                           │    Terrain ouvert
       │                  Hall ─ Bureaux                                │
       │                    │                                           │
       │                HANGAR (balcon) ─── pont ─── Passerelle haute (vue 2) ── escaliers
       │                    │                                           │
       └──── COUR EXTÉRIEURE (containers, camion, tuyaux, murets) ──────┘
                            │
                   Plateau de départ (vue 1, spawn)
                         S
```

## Zones et fonctions de test

| Zone | Où | Ce qu'on y teste |
| --- | --- | --- |
| Plateau de départ | sud, +5 m, rampe vers la cour | spawn calme avec vue sur la cour ; terminal de reset |
| A — Cour extérieure | ~70 × 60 m | combat ouvert, Marksman sur une pile de containers, mouvement latéral, lock longue distance, Focus en extérieur, formation de squad, lignes de vue multiples (entrepôt, bureau, containers, camion, rack de tuyaux, murets bas, petite plateforme) |
| D — Passerelle haute | est, +6 m, 72 m, escaliers aux deux bouts | verticalité, lock vertical, IA sur escaliers, vue sur la cour et le terrain ouvert (point d'observation) ; pont vers le balcon du hangar |
| E — Hangar | 44 × 34 × 14 m, porte de 16 m sur la cour | transition sombre / lumineux (exposition, post-process, ambiance), balcon à 6 m, piliers (caméra), combat mixte |
| C — Intérieur | hall à piliers, deux bureaux, couloir, jonction en T, labo, couloir de service (plafond 3 m), salle des machines (9 m) | caméra intérieure, lock en espace fermé, navigation de squad dans les portes (2,4 à 4 m), lignes de vue, changement de cible |
| B — Ravin | ouest, 140 m, largeur 4,5 à 9 m, virages, deux renfoncements | caméra proche, dodge, collisions, compagnons, Bulwark, combat sous contrainte |
| F — Fosse de service | −3,5 m, auvent bas, armoire électrique, tuyaux | espace bas, caméra sous plafond, éclairage artificiel, futurs sons 3D, combat compact |
| Terrain ouvert | est, buttes, rochers, pylône, pipeline | combat longue distance, Marksman en hauteur sur la plateforme relais (+5 m) |
| Tour d'observation | nord-est, +12 m, deux volées | point de vue sur toute la vallée, distances |
| Trace (prototype) | nord, hutte de 6 × 4 m adossée à la roche | l'intérieur est bien plus profond que l'extérieur (couloir de 8 m puis chambre faiblement éclairée) ; réservé aux futurs shaders Trace, à l'audio d'anomalie et aux réactions du Focus. Aucune mécanique. |

## Routes, boucles et points de vue

- Cour vers intérieur, **deux routes** : par le hangar (porte ouest vers le hall), ou par le ravin, puis la fosse de
  service, puis le labo (escalier vers la porte ouest du labo).
- Cour vers terrain ouvert, **deux routes** : au sol par l'angle nord-est de la cour, ou en hauteur par la passerelle.
- **Boucles** : la cour, la passerelle, le pont et le balcon du hangar ramènent dans le hangar par le haut. La salle des
  machines, le passage arrière (canyon étroit), le terrain ouvert, puis la passerelle ou le sol ramènent à la cour.
- **Points de vue** : le plateau de départ, le pont d'observation au nord de la passerelle et le sommet de la tour.

## Rencontres et outils de debug (développement uniquement)

Quatre compositions pré-placées, dormantes (ce sont des vagues de l'`EncounterController`, qui ne démarre jamais seul
ici) :

| Groupe | Lieu | Ennemis | Terminal | Touche |
| --- | --- | --- | --- | --- |
| OPEN COMBAT | cour | 2 Pursuers + Marksman sur la pile de containers | sud-est de la cour | F5 |
| TIGHT COMBAT | ravin | Pursuer + Bulwark | ouest de la cour, près de l'entrée du ravin | F6 |
| VERTICAL COMBAT | terrain ouvert | Marksman sur la plateforme relais + 2 Pursuers | terrain ouvert | F7 |
| MIXED COMBAT | hangar | Bulwark + Marksman au balcon + 2 Pursuers | devant le hangar | F8 |

- `EncounterDebugStations` (`Encounter/`) : un terminal (touche `F` / `A`) ou la touche de fonction réveille la
  composition là où elle est placée, une fois par chargement.
- **Reset** : terminal sur le plateau de départ, `F9` ou `Backspace` : recharge la scène (le chemin `Restart`
  existant, qui remet santé, recharges, positions et temps).
- Gizmos : `FieldMarker` (spawn, points de vue, routes, liens de navigation), zones et terminaux des rencontres.

## Hiérarchie

```
FieldTest
├── Environment
│   ├── Terrain (sol, falaises, masses rocheuses, Ground Dressing : dalle de la cour, gravier, boue)
│   ├── Architecture (Exterior, Interior, Vertical, TraceTest)
│   ├── Props (grue, encadrements de porte, générateurs, antenne, panneaux, tuyaux, vitres, lampadaires)
│   ├── Vegetation (touffes fusionnées par zone, arbres)
│   ├── Water (bassin technique, flaques)
│   └── Decals
├── Gameplay (PlayerSpawn, Encounters/<groupes>)
├── Debug (terminaux, stations)
├── Lighting (soleil, luminaires, secours, probes, Atmosphere)
├── Volumes (Post Volume global, volumes locaux)
├── Audio (Ambience, émetteurs, Placeholders)
└── Navigation (NavMeshSurface)
+ racines héritées de PrototypeEncounter : Squad, caméra, HUD, HUD Audio, Tactical Focus Presentation, Encounter…
```

## Matériaux et lumière (Pass A)

`Assets/TRACE/Scenes/Materials/FieldTest/` : `M_Proto_Concrete`, `M_Proto_Metal`, `M_Proto_Dark`, `M_Proto_Ground`,
`M_Proto_Rock`, `M_Proto_ORIGIN` (blanc industriel), `M_Proto_Hazard` (jaune ORIGIN, bandes et rambardes seulement),
`M_Proto_Screen`. Tous en URP/Lit, couleurs sobres.

Éclairage :
- soleil directionnel (1,15, ombres douces) ;
- ambiance trilight et brouillard linéaire de 70 à 260 m ;
- une vingtaine de lampes ponctuelles sans ombres, à la même échelle que FirstTrace ;
- un `Post Volume` léger (vignette 0,18, contraste +6, ACES).

Audio : base et couche d'air de FirstTrace à bas volume, quatre émetteurs 3D (hangar, salle des machines, fosse, labo)
et de rares détails en intérieur.

## Validation

| Étape | Résultat | Preuve |
| --- | --- | --- |
| Génération | 19 points clés atteignables depuis le spawn | journal du setup |
| Suite complète | 265/265 (259 + 6 FieldTest), aucun warning C# ; après le dernier réglage de lumière du hangar : tests FieldTest et capture 7/7 (`Validation/TRACE-FieldTest-Final.xml`) | `Validation/TRACE-FieldTest-PlayMode.xml` |
| Captures | 12 vues 1280x720 (vue aérienne, spawn, cour, hangar, porte du hangar, hall, salle des machines, ravin, fosse, passerelle, tour, hutte Trace) | `Validation/FieldTest-*.png` |

Tests FieldTest :
- structure et taille (200 à 300 m) ; aucun ennemi éveillé au départ ;
- toutes les zones atteignables ;
- les deux routes et les boucles, avec une longueur maximale par trajet ;
- les compagnons passent une porte et montent un escalier ;
- les stations (compositions, réveil unique, touches, reset, terminal) ;
- Focus et lock dans la cour.

## Problèmes trouvés pendant la construction

- Rambardes : celle du balcon du hangar bloquait l'arrivée de l'escalier, et la rambarde ouest de la passerelle
  bloquait le pont. Ouvertes aux points d'accès.
- Une butte de terrain était posée sous la première volée de la tour : déplacée.
- Ravin : dans les virages, le mur intérieur d'un tronçon traversait l'entrée du tronçon suivant (ravin bouché, chemin
  de 222 m par le hangar), et le premier correctif ouvrait des brèches vers l'arrière des murs. Les murs s'arrêtent
  maintenant à l'intersection exacte de leurs axes ; une masse rocheuse ferme la bande au nord du dernier virage.
- Un tuyau de la fosse, posé en travers de l'arrivée de la rampe à 1,85 m, était sous la hauteur d'agent et bloquait
  le NavMesh : déplacé le long du mur nord. Ravin vers fosse : 80 m de chemin direct.
- Le test « route directe » (longueur maximale de chemin entre deux zones) a trouvé ces deux blocages que la simple
  connectivité ne voyait pas, puisque le niveau restait connecté par un détour.
- Les lampes intérieures étaient cinq fois trop faibles (intérieurs illisibles) : remontées à l'échelle de FirstTrace.
- Les étiquettes des terminaux apparaissaient en miroir selon le côté d'approche : maintenant lisibles des deux côtés.
- Navigation, caméra et lock : aucun correctif de système n'a été nécessaire à cette étape ; voir les limites.

## Limites connues

- Les escaliers sont des rampes invisibles avec des marches visuelles : la locomotion monte en douceur, sans contact de
  marche.
- La hutte Trace n'utilise aucun portail : la profondeur « impossible » est un effet de construction (le couloir est
  caché dans la roche).
- Le Marksman de la pile de containers et celui du balcon sont sur des îlots de NavMesh : ils tirent et se replacent sur
  place, sans descendre.
- Collisions caméra : les plafonds bas (couloir de service à 3 m, auvent de la fosse) et les piliers sont là pour être
  testés à la main ; aucun réglage de caméra n'a été changé.
- Le hangar reste volontairement plus sombre que la cour (test de transition) ; deux lampes de travail rendent son sol
  lisible, mais l'exposition est à régler en Pass B.
- Une partie du terrain au sud-est de la cour est praticable sans contenu : c'est un raccourci au sol vers le terrain
  ouvert.

## Visual Pass B (direction artistique)

Installation ORIGIN froide, humide et minérale, en partie reprise par la nature. Palette anthracite, gris froid, béton,
blanc industriel sale, jaune ORIGIN discret, vert naturel ; le cyan est réservé à la technologie active et à la Trace.
Tout est généré par `Editor/FieldTestVisualPass.cs` (partie de `FieldTestSetup`, même menu) : les structures solides
(grue, bassin, encadrements, générateur de la fosse, lampadaires, troncs) sont posées **avant** le bake NavMesh et passent
le même contrôle des 19 points ; tout le reste (sans collider) est posé après.

### Shaders (`Assets/TRACE/Rendering/Field/`)

| Shader | Rôle |
| --- | --- |
| `TRACE/FieldSurface` | surface maître : albedo, normal, masque (R AO, G smoothness, B metal), tiling en monde (projection sur l'axe dominant, les primitives ne s'étirent plus), teinte, variation macro, humidité statique, couche secondaire projetée du dessus (gravier, boue) |
| `TRACE/FieldRock` | même entrée en triplanar (whiteout), macro plus forte, mousse sur les faces supérieures |
| `TRACE/FieldVegetation` | alpha clip, double face, vent léger sur le haut des cartes (temps de jeu : le Focus le ralentit), normales courbées vers le haut, translucidité |
| `TRACE/FieldWater` | eau calme et flaques : deux normales qui défilent, transparence et teinte selon la profondeur réelle (texture de profondeur), reflet Fresnel des reflection probes, reflet du soleil retenu ; masque de forme pour les flaques |

Passes ForwardLit (Forward+, ombres douces, SSAO, brouillard, instancing), ShadowCaster, DepthOnly et DepthNormals.

### Humidité statique

| Paramètre | Valeur |
| --- | --- |
| smoothness sèche | 0,05–0,3 (béton, sol), jusqu'à 0,48 pour le métal sombre |
| smoothness mouillée (`_WetSmoothness`) | 0,62 |
| assombrissement (`_WetDarken`) | × 0,85 |
| aplatissement de la normale | 0,4 |
| bande humide au pied des murs (`_GroundWetHeight`) | 0,4 à 4 m selon le matériau |
| taches (`_WetPatchiness`, bruit macro) | 0,4 à 0,9 |

Béton humide, sol de fosse et Trace : `_Wetness` 0,7 à 0,75. Aucun matériau ne dépasse 0,7 de smoothness : pas de
miroirs, le Focus reste lisible (testé).

### Matériaux (`Assets/TRACE/Art/FieldTest/Materials/`)

`M_Field_Concrete_Dry`, `_Wet`, `_Floor`, `_Floor_Wet`, `M_Field_Yard` (dalle avec boue), `M_Field_Metal_Dark`,
`_Painted`, `_Panel` (bardage gris froid), `_Rust`, `M_Field_Hazard` (peinture jaune unie), `M_Field_Rock`, `_Rock_Wet`,
`M_Field_Ground` (avec gravier), `_Ground_Moss`, `M_Field_Gravel`, `M_Field_Mud`, `M_Field_Bark`, `M_Field_Glass`,
`M_Field_Emissive_ORIGIN`, `_Emissive_Off`, `M_Field_Emergency`, `M_Field_Beacon`, `M_Field_Screen`, `M_Field_Tech`
(cyan), `M_Field_Trace_Seam`, `M_Field_Vegetation_Grass`, `_Leaves`, `M_Field_Water_Basin`, `_Water_Puddle`,
`M_Field_Dust`, `M_Field_Sky`. Les clés Pass A (`M_Proto_*`) sont remplacées selon l'objet et la zone
(`FieldKey`) ; les fichiers `M_Proto_*` restent sur disque.

### Décals

`DEC_Leak`, `DEC_Rust`, `DEC_Dirt`, `DEC_Crack`, `DEC_ORIGIN_Marking`, `DEC_Hazard`, `DEC_WetEdge` (matériaux
`M_DEC_*`, Shader Graphs/Decal). 45 URP Decal Projectors, boîtes de 0,8 m de profondeur. `DecalRendererFeature` en
**Screen Space** ajouté aux renderers PC et Mobile (aucun coût sans projecteur dans la scène).

### Sol, végétation, eau

- Dalle de béton boueuse sur la cour, pistes de gravier, plaques de boue, sols intérieurs en béton.
- Végétation : jusqu'à 370 touffes d'herbe et 80 arbustes, posés par lancer de rayon au pied des murs, des rochers et des
  falaises, jamais sous un toit ; ils sont fusionnés en un mesh par zone. Une douzaine d'arbres en bordure. Aucun
  collider, sauf les troncs.
- Eau : bassin technique surélevé (1 m) dans le terrain ouvert, alimenté par un tuyau ; 15 flaques (cour, ravin, fosse,
  hangar, Trace).

### Lumière

- Ciel couvert : cubemap `FX_EnvCube` utilisée à la fois pour le skybox et les reflets par défaut. Soleil froid (1,0,
  ombres douces à 0,72). Ambiance trilight froide. Brouillard exponentiel à 0,0042.
- Luminaires (`PF_LightFixture`) : toutes les lampes intérieures de la Pass A, avec tiges de suspension ; 4 luminaires
  éteints ; 5 lampes de secours rouges.
- Accents chauds : lampadaires au sodium (cour, terrain ouvert) et lumière chaude sur la porte du hangar.
- Émissifs machines : bande d'état du générateur, générateur de la fosse, balise ambre de l'antenne.
- 4 reflection probes temps réel, capturées une seule fois au chargement : cour, hangar, fosse, bassin.

### Post-process

- **Volume global** (`FieldTestVolume`) : ACES, contraste +8, saturation −12, température −8, bloom 0,3 (seuil 1,1),
  vignette 0,15.
- **Volumes locaux** (`Volumes/`, couche Ignore Raycast, priorités 1 à 3, sous le Focus à 10 et 11) :

| Volume | Profil | Effet |
| --- | --- | --- |
| Interior Hangar, Interior Main | `FieldTest_Interior` | exposition +0,35, saturation −8, légèrement plus chaud, vignette 0,2 (transition progressive sur 2 à 4 m) |
| Damp Low Area (Service Pit) | `FieldTest_DampLowArea` | exposition +0,2, saturation −20, contraste +12, filtre vert-gris |
| Trace | `FieldTest_Trace` | saturation −22, température −14, distorsion −0,12, vignette teintée ; pas d'aberration chromatique, qui reste propre au Focus |

### Atmosphère, repères, audio

- Particules : poussière dans le hangar et la salle des machines, particules en suspension presque immobiles dans la
  Trace, brume basse dans la fosse et le ravin (moins de 400 particules au total).
- **Repères visuels** : grue portique (cour, visible depuis le spawn), encadrement ORIGIN de la porte du hangar,
  générateur avec cheminée de la salle des machines, antenne relais avec balise, générateur et conduites de la fosse,
  bassin technique.
- **Trace** : une fine couture cyan au sol de la chambre, deux panneaux légèrement de travers, des poussières qui ne
  retombent pas, un étalonnage froid. Rien de plus.
- Points audio (`Audio/Placeholders`, `FieldMarker` de type Audio) : ventilation, eau, machinerie, couloir de vent
  (ravin), local électrique.
- Prefabs (`Assets/TRACE/Prefabs/FieldTest`) : `PF_LightFixture`, `PF_EmergencyLight`, `PF_OriginSign`,
  `PF_PipeModule`, `PF_DoorFrame`.

### Validation Pass B

- Suite complète : **274/274** (dont 8 tests `FieldTestVisualTests` et la capture), aucun warning C# ;
  `Validation/TRACE-FieldTest-VisualPassB.xml`.
- Les 19 points clés restent atteignables, et les trajets et boucles gardent leurs longueurs maximales.
- Captures 1280x720 : `Validation/FieldTest-*.png`. Trois vues ajoutées (bassin, chambre Trace, sol du ravin), plus le
  Focus inactif puis actif dans la cour (`FieldTest-focus-off` et `-on`) : le Focus reste nettement distinct.
- Corrigés pendant la passe : texture « hazard » à bandes trop criarde (remplacée par une peinture jaune unie), mousse
  trop saturée, écailles de peinture trop grosses (tiling ×2), lampe de secours rouge qui teintait la chambre Trace
  (retirée).

### Temporaire

- Les décals `DEC_*` et le marquage ORIGIN sont procéduraux, placeholders d'un futur passage graphique.
- Arbres : un cylindre et des cartes, sans mesh final.
- Reflection probes temps réel : à remplacer par des probes bakées le jour où l'éclairage sera baké.
- Rochers et architecture restent des primitives : la Pass B ne modifie que leur matériau.
- L'Unity ouvert sur le projet doit réimporter les textures, compter quelques minutes au premier lancement.

Licences : `Docs/ArtLicenses.md` (ambientCG, CC0).
