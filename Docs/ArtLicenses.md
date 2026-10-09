# TRACE — Licences des assets visuels

Aucune licence n'est supposée : chaque source est vérifiée et listée ici.

## ambientCG (CC0 1.0)

Licence vérifiée sur https://docs.ambientcg.com/license/ : « All ambientCG assets are provided under the Creative Commons
CC0 1.0 Universal License », fichiers bruts redistribuables dans un projet. Aucune attribution requise (faite quand même).

Téléchargés en 1K-JPG (`https://ambientcg.com/get?file=<ID>_1K-JPG.zip`), dans `Assets/TRACE/Art/FieldTest/Textures/`.
Conversion : `_Albedo` = Color ; `_Normal` = NormalGL réencodé en JPEG qualité 88 ; `_Mask` = R AO (ou 255),
G 1 − roughness, B metalness (ou 0). Foliage001 et LeafSet024 : `_Albedo.png` RGBA avec l'opacité en alpha.

| Asset | Page | Utilisation |
| --- | --- | --- |
| Concrete031 | https://ambientcg.com/view?id=Concrete031 | murs béton (sec / humide) |
| Concrete042A | https://ambientcg.com/view?id=Concrete042A | sols intérieurs, dalle de la cour |
| Metal027 | https://ambientcg.com/view?id=Metal027 | métal sombre (structures, grilles, toitures) |
| PaintedMetal012 | https://ambientcg.com/view?id=PaintedMetal012 | métal peint blanc industriel, bardage gris, peinture jaune ORIGIN |
| Metal022 | https://ambientcg.com/view?id=Metal022 | rouille (containers, tuyaux extérieurs) |
| Rock058 | https://ambientcg.com/view?id=Rock058 | roche (triplanar), écorce teintée |
| Ground036 | https://ambientcg.com/view?id=Ground036 | sol de la vallée |
| Ground037 | https://ambientcg.com/view?id=Ground037 | sol moussu, mousse sur les roches |
| Gravel043 | https://ambientcg.com/view?id=Gravel043 | gravier (pistes, couche du sol) |
| Ground106 | https://ambientcg.com/view?id=Ground106 | boue |
| Foliage001 | https://ambientcg.com/view?id=Foliage001 | cartes d'herbe |
| LeafSet024 | https://ambientcg.com/view?id=LeafSet024 | arbustes, feuillage des arbres |

PaintedMetal016 a été téléchargé puis retiré (texture à bandes, trop criarde) ; il n'est plus dans le projet.

## Générés pour TRACE (procéduraux, propriété du projet)

- `FX_MacroNoise`, `FX_WaterNormal`, `FX_PuddleMask`, `FX_EnvCube` et les décals `DEC_Leak`, `DEC_Rust`, `DEC_Dirt`,
  `DEC_Crack`, `DEC_WetEdge` : `Docs/Art/FieldTest/noise_textures.py` (Pillow, graines fixes, reproductible).
- `DEC_Hazard`, `DEC_ORIGIN_Marking`, `FX_Dust` : générés par un script Pillow ponctuel (non conservé). Le marquage ORIGIN
  utilise la police DejaVu Sans (licence libre Bitstream Vera / DejaVu, rendu en image seulement, la police n'est pas
  embarquée).
- Meshes de végétation (`Art/FieldTest/Meshes`) et prefabs (`Prefabs/FieldTest`) : générés par `FieldTestVisualPass.cs`.
