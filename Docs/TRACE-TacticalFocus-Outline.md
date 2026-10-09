# TRACE — Tactical Focus, outline ennemi

Pendant le Tactical Focus seulement, un contour fin et discret dit où sont les ennemis, lequel est verrouillé, lequel
attaque et lequel offre un combo. Pas d'outline hors Focus, pas de vision à travers les murs, aucun effet de jeu.
Appliqué par `TRACE/Apply Tactical Focus Presentation V1` (idempotent).

## Technique

**`ScriptableRendererFeature` URP (RenderGraph) + calques de rendu + matériau d'override.**

- `Rendering/TacticalOutlineFeature` : une passe raster après les transparents, qui redessine les renderers portant un
  des quatre calques de rendu Tactical avec le matériau de ce niveau (`DrawRendererList`, override material). Aucun
  renderer ajouté, aucun mesh dupliqué, aucun matériau ennemi modifié. La passe est sautée tant que le fondu global est
  nul (aucun coût hors Focus). Elle est installée sur `PC_Renderer` et `Mobile_Renderer`.
- `Rendering/TacticalOutline.shader` : coque inversée (faces arrière poussées le long de la normale **en espace écran**),
  donc une largeur constante en pixels quelle que soit la distance. `ZTest LEqual` contre la profondeur caméra : un
  ennemi caché n'a aucun contour (pas de wallhack, pas de rayons X).
- `TacticalReadability` : recense une fois tous les `MeshRenderer` / `SkinnedMeshRenderer` des ennemis (corps,
  plaque du Bulwark, canon du Marksman ; les lignes de visée et les télégraphes restent hors du contour). À chaque
  changement de niveau il change seulement le bit de calque, puis pilote quatre globales : fondu, horloge non scalée,
  révélation par le scan, plage de distance. Ni `renderer.material`, ni `MaterialPropertyBlock`, ni allocation par frame.

Les ennemis futurs sont inclus automatiquement dès qu'ils figurent dans la liste de la scène (le setup la remplit avec
tous les `EnemyBrain`), sans câblage par prefab.

## Calques de rendu

| Index | Nom | Niveau |
| --- | --- | --- |
| 8 | `TacticalReadable` | standard |
| 9 | `TacticalLocked` | cible verrouillée |
| 10 | `TacticalThreat` | wind-up / visée en cours |
| 11 | `TacticalCombo` | opportunité de combo |

Un seul bit à la fois par ennemi. Priorité : menace > verrouillé > combo > standard (la même que les marqueurs au sol).

## Paramètres (matériaux `Rendering/M_TacticalOutline_*`)

| Niveau | Couleur de base | Accent | Largeur | Mouvement |
| --- | --- | --- | --- | --- |
| Standard | cyan désaturé (0,62 ; 0,82 ; 0,90), α 0,5 | — | 1,3 px | aucun, pas de clignotement |
| Verrouillé | cyan plus lumineux (0,60 ; 0,93 ; 1), α 0,92 | blanc bleuté, 25 % | 1,8 px | respiration très lente 0,5 Hz, ±15 % |
| Menace | cyan clair (0,75 ; 0,90 ; 0,95), α 0,95 | orange / blanc chaud (1 ; 0,7 ; 0,45), 85 % au pic | 1,8 px | pulse court 2,2 Hz, ±30 % |
| Combo | cyan (0,62 ; 0,86 ; 0,95), α 0,75 | ambre-blanc (1 ; 0,86 ; 0,55), 60 % au pic | 1,5 px | pulse 1,2 Hz, ±20 % |

Globales (dans `TacticalReadability`) :
- fondu d'entrée 0,14 s, de sortie 0,11 s, en `unscaledDeltaTime` ;
- distance caméra : contour plein jusqu'à 15 m, décroissance douce jusqu'à 25 m, absent au-delà ;
- révélation : le contour apparaît 0,5 m derrière le front du scan pulse, au plus tard 0,18 s après l'entrée ;
- interrupteur de développement « Show Tactical Outlines » (`showTacticalOutlines`) : contours sur tous les ennemis,
  Focus ou non.

## Comment les états agissent

- **Verrouillé** : plus net (1,8 px au lieu de 1,3), plus opaque et plus lumineux, avec une respiration lente. Il
  s'ajoute au marqueur au sol cyan épais, à la carte `LOCKED` et au losange du lock déjà existants.
- **Menace** (`EnemyBrain.IsPreparingAttack`, l'état déjà utilisé par les wind-ups et les `ThreatWarning`) : le
  contour se réchauffe vers l'orange en pulses de 2,2 Hz. Il s'ajoute à l'anneau orange qui pulse, au télégraphe,
  au ghost et au tag `WIND-UP` / `THREAT`.
- **Combo** : la base reste cyan, un accent ambre-blanc pulse. Il s'ajoute à l'anneau ambre qui respire et au tag
  `GROUPED 1+E`, après l'étape « combos » de la cascade.

Aucun état ne dépend de la seule couleur : largeur, opacité, mouvement et labels HUD différent aussi.

## Ajustements liés

- Intention Ghost : opacité 0,22 → **0,14**, pour passer sous le contour et le télégraphe.
- Anneau au sol standard : α 0,35 → **0,22**, puisque le contour porte désormais la présence.

## Fichiers

Créés : `Rendering/TacticalOutline.shader`, `Rendering/TacticalOutlineFeature.cs`,
`Rendering/M_TacticalOutline_{Standard,Locked,Threat,Combo}.mat`, `Tests/PlayMode/TacticalOutlineTests.cs`,
`Docs/TRACE-TacticalFocus-Outline.md`.
Modifiés : `Tactical/TacticalReadability.cs`, `Tactical/TacticalFocusPresentationController.cs` (`ScanOrigin`),
`Editor/TacticalFocusPresentationSetup.cs`, `Tests/PlayMode/TRACE.PlayModeTests.asmdef` (références URP),
`Assets/Settings/{PC,Mobile}_Renderer.asset` (feature), `ProjectSettings/TagManager.asset` (noms des calques),
`Docs/Validation/TacticalFocusVisualCapture.cs` (vue d'occlusion), les trois scènes.

## Validation

| Étape | Résultat | Preuve |
| --- | --- | --- |
| Suite complète | 252/252 (245 + 7 outline), aucun warning C# | `Validation/TRACE-TacticalFocus-Outline-PlayMode.xml` |
| Captures | 1/1 : actif (contours fins sur les ennemis à distance) et occlusion (mur temporaire : seule la partie visible garde son contour) | `Validation/Focus-active.png`, `Validation/Focus-occlusion.png` |

Tests outline : feature présente et active sur chaque renderer URP, avec quatre niveaux et une largeur de 1 à 2 px ;
rien hors Focus, fondu jamais instantané, retour à zéro et calques nettoyés à la sortie ; verrouillé, changement de
cible, combo ; wind-up vers menace ; entrées et sorties répétées ; tous les archétypes contourés (plaque du Bulwark,
canon du Marksman, lignes exclues) ; interrupteur de développement.

## Limites

- Coque inversée : sur les cubes à arêtes vives (plaque du Bulwark, canon), le contour peut s'ouvrir légèrement aux
  coins (normales séparées). C'est invisible sur les capsules actuelles ; un modèle final avec normales lissées réglera
  ce point.
- Les pulses utilisent un sinus : un pic correspond à un battement doux, pas à un flash.
- Le grade du Focus (saturation −20) s'applique aussi au contour, dessiné avant le post-process ; les niveaux restent
  distincts par la luminosité et le mouvement.
- Les performances n'ont pas été profilées en build : une passe, quatre listes de rendu, au plus une quinzaine
  d'ennemis, rien d'alloué par frame côté C#.
