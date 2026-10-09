# TRACE — Ambience V0.1 (ambiances environnementales et bed du Tactical Focus)

Première identité sonore des lieux : FirstTrace doit sonner vide, anciennement fonctionnel, encore partiellement
alimenté, froid, calme, légèrement inquiétant, sans jamais tourner à la maison hantée. Pas de musique ajoutée.
Menu : `TRACE/Apply Ambience V0.1` (idempotent). Licences et attributions : `Docs/AudioLicenses.md`.

## Packs

| Pack | Source | Licence vérifiée | Retenus |
| --- | --- | --- | --- |
| Fallus — Industrial Ambience | itch.io (téléchargé à la main) | **CC BY 4.0**, `ReadMe.txt` de l'archive, attribution « Fallus » + lien obligatoire | 5 / 22 |
| SRG774 — Dark Sci-Fi Audio Pack | OpenGameArt | **CC0**, texte de la page | 1 (Airy) / 8 |
| Kenney Sci-Fi Sounds, Kenney UI Audio | déjà téléchargés (passe UI) | CC0 | 4 |
| SimonPeter.wav — Sounds From The Underground | itch.io (téléchargé à la main) | **aucune licence dans l'archive** | 0 |
| Nexus Core | — | non téléchargé (optionnel, inutile) | 0 |

Sélection par analyse et non à l'écoute : sonie intégrée et plage (LUFS, LRA), centroïde spectral, spectrogrammes,
largeur stéréo (mid / side). Écartés :
- Fallus : les 5 « Ambient music loops » (de la musique) ; *Deep Shaft Resonance*, *Distant Sirens*, *Unmaintained
  Area* (pulsations ou cycles visibles) ; *Mine Tension* (trop mouvante) ; les autres, redondants.
- SRG774 : *Pulse* (rythmé, sections fortes, LRA 15,6), *Transmission* (tonal et musical), *Urgent*, *Sector*.
- SimonPeter : sans licence et surtout musical (piano, cordes, arpèges).

## Fichiers conservés (10)

| Clip | Origine | Format | Import |
| --- | --- | --- | --- |
| `Ambience/AMB_FirstTrace_Base_LOOP.ogg` | Fallus *Sublevel Pressure* | 90 s stéréo, −24 LUFS | Streaming, Vorbis |
| `Ambience/AMB_FirstTrace_Environment_LOOP.ogg` | Fallus *Ventilation Shaft* | 75 s stéréo, −25 LUFS | Streaming, Vorbis |
| `Ambience/AMB_TracePresence_LOOP.ogg` | Fallus *Unseen Observer* | 60 s stéréo, −26 LUFS | Streaming, Vorbis |
| `Ambience/AMB_Emitter_Pipeline_LOOP.ogg` | Fallus *Pipeline* | 40 s mono, −22 LUFS | Compressed in memory |
| `Ambience/AMB_Emitter_Machinery_LOOP.ogg` | Fallus *Structural Resonance* | 40 s mono, −22 LUFS | Compressed in memory |
| `Ambience/AMB_Emitter_Terminal_LOOP.ogg` | Kenney `computerNoise_001` | 4,4 s mono, −24 LUFS | Compressed in memory |
| `Ambience/AMB_Detail_MetalImpact_01.wav`, `_02.wav` | Kenney `impactMetal_002` / `_004` | ≈ 0,4 s mono | Decompress on load, ADPCM |
| `Ambience/AMB_Detail_Relay_01.wav` | Kenney UI `switch16` | 0,36 s mono | Decompress on load, ADPCM |
| `Tactical/AMB_TacticalFocus_FLOAT_LOOP.ogg` | SRG774 *Airy* + couche grave Kenney | 58,5 s **stéréo**, −24 LUFS | Streaming, Vorbis |

Retiré : `Tactical/SFX_TacticalFocus_Bed_LOOP.wav` (ancien bed mono, remplacé).

Boucles : extrait pris dans le corps du fichier, puis fondu croisé à puissance constante de sa fin vers son début (2 à
3 s). Test de jonction (boucle jouée deux fois, saut maximal autour du raccord comparé au 99,9e centile des sauts du
signal) : les dix fichiers passent. Le bed du Focus échouait d'abord (un filtre passe-bas appliqué après la répétition
de la couche grave créait un clic) ; refait avec un filtrage périodique, raccord propre.

## Architecture

`AmbienceController` (existant, API d'histoire conservée) passe à quatre couches, toutes en fondu sur le temps non
scalé :

```
Ambience (groupe de mixer Ambience : passe-bas 800 Hz et −14 dB pendant le Focus)
├── Base         boucle 2D continue, très basse
├── Environment  boucle 2D, autre bande spectrale (air, ventilation)
├── Detail       one-shots 3D rares, depuis des points plausibles
└── Anomaly      boucle 2D, seulement aux moments liés aux Traces
+ Combat Drone   (placeholder existant, groupe Music)
```

- Détails : tous les 8 à 30 s, 30 % des créneaux restent silencieux, jamais deux fois le même clip de suite, hauteur
  ±6 %, deux sources 3D en alternance (linéaire 3 → 22 m). Un tableau vide signifie aucun détail.
- `AmbienceEmitter` (nouveau) : boucle 3D placée, fondu d'entrée 1,5 s depuis un point aléatoire de la boucle,
  hauteur ±2 %, pour que deux émetteurs du même clip ne soient jamais en phase.
- Sans clip assigné, les générateurs du placeholder restent en repli (bourdonnement, vent, grincement).

Crochets d'histoire (`StoryDirector`) : événement du hall → `PlayCreak` + `SetAnomaly(true)` ; couloir de tension →
`SetTension(true)` (environnement ×1,35) + `SetAnomaly(false)` ; conclusion → `SetCombat(false)` +
`SetConclusion(true)` (couches à 45 %) + `SetAnomaly(true)`.

## Mapping par scène

### FirstTrace

| Couche | Clip | Volume | Fondu |
| --- | --- | --- | --- |
| Base | `AMB_FirstTrace_Base_LOOP` | 0,30 (≈ −34 LUFS effectifs) | 1,5 s |
| Environment | `AMB_FirstTrace_Environment_LOOP` | 0,22 (≈ −38 LUFS) ; 0,30 en tension | 1,5 s |
| Anomaly | `AMB_TracePresence_LOOP` | 0,30 (≈ −36 LUFS) | 2,5 s |
| Details | 2 impacts métalliques, 1 relais | 0,40 | — |
| Conclusion | Base et Environment ramenés à 45 % | | 1,5 s |

Émetteurs 3D (7, groupe Ambience, linéaire 1,5 m → portée) :

| Émetteur | Clip | Position | Volume | Portée |
| --- | --- | --- | --- | --- |
| Dock Machinery | Machinery | (−9 ; 2 ; 4) | 0,50 | 13 m |
| Corridor Pipe | Pipeline | (3,5 ; 3 ; 20) | 0,45 | 10 m |
| Terminal A | Terminal | (2,6 ; 1,2 ; 28,5) | 0,35 | 6 m |
| Hall Side Panel | Machinery | (−18,5 ; 2 ; 52) | 0,40 | 11 m |
| Tension Pipe | Pipeline | (−3,5 ; 3,5 ; 67) | 0,45 | 10 m |
| Terminal B | Terminal | (−14,3 ; 1,2 ; 78) | 0,35 | 6 m |
| Arena Machinery | Machinery | (12 ; 3 ; 92) | 0,45 | 14 m |

Points de détail (7) : haut du quai, plafond du couloir, murs lointains du hall, couloir de tension, bords de l'arène.
Le centre du hall et l'entrée du quai n'ont aucun émetteur : on y entend l'espace.

### Prototype

Base seule (`AMB_FirstTrace_Base_LOOP`, 0,22), pas d'environnement, pas de détail, pas d'anomalie, et un émetteur
lointain (Machinery, 0,35, 22 m), à 14 m sur le côté et 16 m devant le point de départ. Le reste de la scène de
test est inchangé.

## Tactical Focus : le bed flottant

`AMB_TacticalFocus_FLOAT_LOOP` : la texture *Airy* de SRG774, bruit large de 250 à 700 Hz, sans aucune ligne tonale
(pas de mélodie), stable (LRA 2,6), très large en stéréo (side ≈ mid). Un grave léger s'y ajoute : la nappe Kenney
sous 160 Hz, à −14 dB.

- Source `Bed Source` : 2D, stéréo conservée, groupe **Tactical** (hors du passe-bas du monde, donc plus claire que
  l'environnement filtré).
- Volume 0,32 (réglé à l'oreille le 2026-10-09, d'abord 0,14). Le monde reste présent mais nettement derrière :
  Ambience à −14 dB sous 800 Hz, World à −9 dB sous 1600 Hz pendant le Focus.
- Séquence : à l'entrée, son d'entrée, puis filtre du monde (0,16 s), puis bed après 80 ms en fondu de 0,3 s. Pendant
  le Focus, le bed est stable avec une dérive de hauteur ±1,5 % sur 17 s. À la sortie, le bed s'efface en 0,2 s, le
  monde revient (0,2 s), puis le son de sortie joue 0,1 s plus tard. Pas de relance tant que l'état ne change pas.

## Validation

| Étape | Résultat | Preuve |
| --- | --- | --- |
| Suite complète | 259/259 (252 + 7 ambiance), aucun test ignoré, aucun warning C# | `Validation/TRACE-Ambience-PlayMode.xml` |

Tests ambiance : couches sur clips réels avec fondu d'entrée, streaming stéréo, groupe Ambience ; quatre à huit
émetteurs 3D en boucle, portée ≤ 15 m, trois clips au plus ; détails rares (intervalle 8 à 30 s, silences) émis près
d'un point plausible ; couche Trace à l'événement du hall (fondu, pas de coupure) et retirée dans le couloir de tension ;
retrait à la conclusion ; bed du Focus stéréo 2D dans Tactical, retardé, faible, hauteur dans 0,98 à 1,02, lieu toujours
présent ; Prototype minimal.

## À vérifier à l'oreille (non automatisable)

Les choix et les niveaux viennent de l'analyse, pas d'une écoute. Faire une partie complète de FirstTrace au casque :
- arrivée (calme, espace, froid) ;
- exploration (bourdonnement et détails rares) ;
- indice ;
- événement étrange (la couche Trace doit se sentir sans s'imposer) ;
- tension ;
- combat (l'ambiance sous le combat) ;
- Tactical Focus (le bed ne doit pas évoquer une nouvelle musique) ;
- fin (place au silence).

Réglages : volumes et fondus sur l'objet « Ambience », émetteurs sous « Ambience Emitters », bed sur « Tactical
Focus Presentation ».

## Temporaire

- Le drone de combat reste le placeholder généré par le code (groupe Music).
- Le grincement de l'événement du hall reste généré par le code.
- Les positions des émetteurs et des points de détail suivent les coordonnées du niveau ; à ajuster si le level design
  bouge.
