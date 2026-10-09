# TRACE — Portraits HUD (temporaires)

Trois portraits temporaires cohérents pour le HUD combat, la squad, les dialogues et le Tactical Focus. Pas des
portraits finaux : une représentation propre, à remplacer sans toucher au HUD.

## Source et génération

Source unique : `Docs/Portraits/Source/Squad_Portraits.png` (2172 × 724), trois panneaux carrés peints comme un ensemble
(même cadrage, visages de 3/4, regard calme, fonds teintés à l'accent : ambre, bleu froid, sarcelle). Séparateurs
détectés aux colonnes 722-727 et 1445-1448.

`python3 Docs/Portraits/build_portraits.py` (Pillow) écrit dans `Assets/TRACE/UI/Portraits/` :

| Fichier | Taille | Usage |
| --- | --- | --- |
| `Portrait_<Nom>.png` | 256 × 256 | panneau opérateur actif (84 px de référence), dialogue (72 px) |
| `Portrait_<Nom>_Mini.png` | 72 × 72 | cartes squad (34 px de référence), recadré des yeux au menton |
| `Portrait_Fallback.png` | 256 × 256 | silhouette neutre si un profil n'a pas de portrait |

Traitement : découpe intérieure aux séparateurs (704 × 704), redimensionnement Lanczos, vignette légère vers le charbon
du HUD (30 %) ; miniature recadrée par personnage (Tracewalker a le visage à gauche du centre) et légèrement accentuée.
Import : Sprite simple, bilinéaire, sans mipmaps, sans compression, NPOT conservé.

## Assignation

`CharacterProfile` porte `portrait` et `miniPortrait`, et expose `Portrait`, `MiniPortrait` (retombe sur `Portrait`),
`AccentColor`, `DisplayName` et `RoleLabel` (= archétype). `TRACE/Apply HUD V0.1` (`HudSetup.Portraits`) assigne les
deux sprites aux trois profils et le `Portrait_Fallback` aux panneaux et au `DialogueRunner`.

| Consommateur | Sprite | Accent |
| --- | --- | --- |
| `ActiveOperatorPanel` | `Portrait` | cadre (α 0,9) et barre, éclaircis en Focus |
| `SquadStatusPanel` | `MiniPortrait` | contour, barre et numéro, éclaircis en Focus |
| `DialogueRunner` | `Portrait` (locuteur résolu) | contour, barre, nom |
| Tactical Focus | les mêmes, inchangés | `HudPanel.AccentFor` : accent mêlé à 25 % de blanc, opaque |

Fallback : `HudPanel.ShowPortrait` affiche la silhouette neutre si le sprite manque ; une image n'est jamais vide.

Lisibilité : en exploration, le panneau opérateur passe de 80 à 90 % et la squad de 60 à 75 %. En dessous, le décor
clair traversait les portraits et les miniatures devenaient délavées.

## Validation

| Étape | Résultat | Preuve |
| --- | --- | --- |
| Suite complète | 230/230 (226 + 4 portraits), aucun warning C# | `Validation/TRACE-Portraits-PlayMode.xml` |
| Captures | 1/1 | `Validation/TRACE-Portraits-Visual.xml`, `Validation/Hud-portraits-*.png`, `Validation/Hud-*.png` |

Résolutions vérifiées : 1280 × 720 (référence du `CanvasScaler`, trois membres actifs, combat, Focus, cible élite),
1600 × 900 et 1920 × 1080 (16:9, même composition mise à l'échelle). Tests : format commun 256 / 72 et miniature
distincte ; portrait, rôle et cadre corrects pour chaque membre après une série de switchs rapides ; miniatures des
cartes ; fallback quand un profil perd son portrait ; Focus qui éclaircit les accents sans changer les portraits ;
dialogue qui utilise le même portrait que le HUD.

## Limites

- Le dialogue est en IMGUI : les captures batch ne le rendent pas ; vérifier en jeu dans `FirstTrace`.
- Le grade du Focus désature tout l'écran, HUD compris (canvas Screen Space - Camera) : les portraits perdent un peu de
  couleur, compensé par les cadres éclaircis.
- Tailles de Game View non 16:9 non vérifiées (le scaler fait une moyenne largeur/hauteur à 0,5).
