# TRACE — Tactical Focus, présentation V1

Le monde ne ralentit pas : le Tracewalker filtre sa perception. Cette passe ne touche qu'à la représentation du
Tactical Focus existant ; `TacticalFocus` (temps, entrée, sortie, switch) est inchangé. Menu :
`TRACE/Apply Tactical Focus Presentation V1` (idempotent, `Prototype`, `PrototypeEncounter`, `FirstTrace`).

## Architecture

| Élément | Rôle |
| --- | --- |
| `Tactical/TacticalFocusPresentationController` | lit `TacticalFocus.IsActive` et le représente : snapshot de mixer, sons d'entrée / sortie, nappe, grade, aberration, scan pulse, horloge de révélation (`Revealed(Stage)`). Tout en `Time.unscaledDeltaTime` cumulé. Aucune décision de jeu. |
| `Tactical/TacticalReadability` | marqueurs au sol des ennemis proches et Intention Ghost, après l'étape « markers » |
| `TacticalOverlay` (cartes ennemies), `TacticalFocusOverlay` (ANALYSIS) | suivent les étapes de révélation ; libellés courts |
| `Editor/TacticalFocusPresentationSetup` | mixer, profils, matériaux, objet « Tactical Focus Presentation », routage |

Objet de scène : `Grade Volume` (global, priorité 10), `Aberration Volume` (global, priorité 11), `Cue Source` et
`Bed Source` (2D, groupe Tactical), `Scan Ring` (LineRenderer 64 points). L'ancien « HUD Focus Grade » et
`HudFocusGrade.asset` (saturation −42) sont retirés ; `HudSetup` ne crée plus de grade.

## AudioMixer (`Assets/TRACE/Audio/TRACE_Mixer.mixer`)

Créé par le setup via l'API éditeur interne d'Unity (aucune API publique n'existe), mode de mise à jour **Unscaled
Time** : le Focus ralentit le temps, pas les transitions du mix.

```
Master
├── Music     ← drone de combat (AmbienceController)          Lowpass Simple
├── Ambience  ← vent, bourdonnement, gouttes, grincements      Lowpass Simple
├── World     ← (futurs SFX du monde)                          Lowpass Simple
├── Combat    ← (futurs impacts / armes)                       Lowpass Simple
├── UI        ← HUD Audio (menaces, combo, skill, lock, switch…)
└── Tactical  ← entrée, sortie, nappe du Focus
```

| Groupe | Normal | TacticalFocus |
| --- | --- | --- |
| World | 0 dB, coupure 22 kHz | −9 dB, coupure **1600 Hz** |
| Ambience | 0 dB, 22 kHz | −14 dB, 800 Hz |
| Music | 0 dB, 22 kHz | −8 dB, 2400 Hz |
| Combat | 0 dB, 22 kHz | −3 dB, 4000 Hz (plus léger : le combat reste lisible) |
| UI, Tactical | 0 dB, sans filtre | 0 dB, sans filtre |

Transitions : entrée 0,16 s, sortie 0,20 s (`AudioMixerSnapshot.TransitionTo`). Priorité perçue en Focus : menaces
(UI, volume 1,0), combo / skill (UI 0,6), retours tactiques et lock (UI / Tactical), nappe (0,32), monde filtré.

Réglage à l'oreille (2026-10-09) : les premiers niveaux (−5 dB à 2400 Hz, nappe 0,14) laissaient les boucles graves du lieu
passer presque intactes et la nappe sous l'ambiance. Le monde passe désormais nettement derrière la membrane et la nappe
prend la place, sans devenir une musique.

## Sons

| Son | Fichier | Construction | Volume |
| --- | --- | --- | --- |
| Entrée (0,28 s) | `SFX_TacticalFocus_Enter` | résonance inversée Hove (resserrement) + impulsion grave synthétisée 72 → 60 Hz à 150 ms + tick Kenney à 165 ms (scan) | 0,6 |
| Sortie (0,22 s) | `SFX_TacticalFocus_Exit` | relâchement grave 64 Hz + résonance descendante Hove (pas un simple renversement de l'entrée) | 0,5 |
| Nappe (boucle 4,5 s) | `SFX_TacticalFocus_Bed_LOOP` | Kenney `spaceEngineLow_002`, passe-bas 900 Hz, bouclage par fondu croisé ; fondu 0,25 s / 0,2 s | 0,12 |
| Artefacts rares | `SFX_UI_TargetChange_01` | tick très bas, pan aléatoire ±0,6, toutes les 2,5 à 6 s | 0,05 |

## Post-process (valeurs dans le contrôleur, appliquées à une copie runtime des profils)

| Paramètre | Valeur | Note |
| --- | --- | --- |
| Saturation | −20 | l'ancien grade était à −42 |
| Contraste | +8 | |
| Post Exposure | −0,1 | fait ressortir les surimpressions |
| Color Filter | (0,94 ; 0,985 ; 1) | tendance froide légère, pas de teinte bleue |
| Vignette | 0,18, douceur 0,45 | la vision périphérique reste utile |
| Poids du grade | 0 → 1 en 0,15 s, 1 → 0 en 0,2 s | |
| Aberration chromatique | 0 → **0,12** en 40 ms, puis **0,02** en 0,22 s ; sortie 0,02 → 0,06 → 0 | jamais maintenue forte |

Pas de flou, de motion blur ni de distorsion.

## Effets de lecture

- **Scan pulse** : un anneau unique part des pieds du membre actif, rayon 0 → 12 m (ease-out) en 0,45 s, largeur
  0,09 → 0,045, blanc-cyan (α 0,55) qui s'efface. Aucune particule.
- **Marqueurs ennemis** (≤ 18 m) : anneau au sol, différencié par la largeur, la taille et le mouvement, pas
  seulement par la couleur.

  | Type | Couleur | Largeur | Rayon | Mouvement |
  | --- | --- | --- | --- | --- |
  | Standard | blanc froid α 0,35 | 0,025 | 0,7 | fixe |
  | Verrouillé | cyan | 0,06 | 0,85 | fixe |
  | Menace (wind-up / visée) | orange-rouge | 0,07 | 0,9 | pulsation rapide ±12 % à 5 Hz |
  | Combo | ambre | 0,05 | 0,8 | respiration lente 1,6 Hz |

  Priorité : menace > verrouillé > combo > standard.
- **Intention Ghost : implémenté.** Pendant le wind-up d'un ennemi de mêlée (Pursuer, Bulwark), une copie de sa
  silhouette (même mesh, matériau transparent cyan, opacité 0,22 × 0,4 → 1 selon l'avancement) se penche jusqu'à
  0,7 m vers sa frappe. Uniquement en Focus. Le Marksman garde sa ligne de visée existante.
- **Micro-freeze : non retenu.** `TacticalFocus` considère toute modification externe de `Time.timeScale` comme une
  pause et abandonne le Focus. Un gel de 30 à 50 ms casserait donc la mécanique ou obligerait à modifier le
  gameplay. L'accent d'entrée est porté par le pic d'aberration et le clic de scan.

## Révélation du HUD (après l'entrée)

| Temps | Élément |
| --- | --- |
| 0 ms | grade, son, scan, en-tête TACTICAL FOCUS |
| 80 ms | marqueurs ennemis, ghosts |
| 140 ms | cartes ennemies (nom, PV, LOCKED) |
| 180 ms | statuts (WIND-UP, THREAT, SLOWED, GUARD > FLANK), panneau ANALYSIS |
| 220 ms | combos (GROUPED 1+E, PROTECTED) |

Libellés : `WIND-UP`, `THREAT` (visée du Marksman), `SLOWED`, `GROUPED 1+E`, `PROTECTED`, `COMBO`, `LOCKED`,
`HOSTILES n`. Les cartes ennemies gardent une bande libre de 110 px en haut, pour ne plus chevaucher le panneau cible.

## Fichiers

Créés : `Tactical/TacticalFocusPresentationController.cs`, `Tactical/TacticalReadability.cs`,
`Editor/TacticalFocusPresentationSetup.cs`, `Tests/PlayMode/TacticalFocusPresentationTests.cs`,
`Audio/TRACE_Mixer.mixer`, `Audio/Tactical/SFX_TacticalFocus_{Enter,Exit,Bed_LOOP}.wav`,
`Tactical/Presentation/{TacticalFocusGrade,TacticalFocusAberration}.asset`, `M_TacticalFocus_{Line,Ghost}.mat`,
`Docs/Validation/TacticalFocusVisualCapture.cs`.
Modifiés : `TacticalOverlay.cs`, `UI/TacticalFocusOverlay.cs` (le grade en sort), `UI/HudAudio.cs` (les sons du
Focus en sortent), `Narrative/AmbienceController.cs` (groupes de mixer), `Editor/HudSetup.cs`, `Editor/AudioSetup.cs`,
`Tests/PlayMode/{AudioTests,TacticalFocusTests}.cs`, les trois scènes.
Supprimés : `UI/HudFocusGrade.asset`, `Audio/Tactical/SFX_TacticalFocus_{Enter,Exit}_TMP.wav`.

## Validation

| Étape | Résultat | Preuve |
| --- | --- | --- |
| Suite complète | 245/245 (238 + 7 présentation), aucun warning C# | `Validation/TRACE-TacticalFocus-Presentation-PlayMode.xml` |
| Captures | 1/1, quatre vues 1280x720 (avant, entrée, actif, FirstTrace) | `Validation/Focus-*.png` |

Tests de présentation : mixer (groupes, snapshots, Unscaled Time, routage UI / Tactical) ; entrée (snapshot,
grade, pic d'aberration entre 0,08 et 0,15, valeur active ≤ 0,03, nappe faible, pas de rejeu) ; sortie (retour
complet, `timeScale` rendu) ; scan pulse unique ; cascade du HUD ; marqueurs standard / verrouillé / combo ;
menace et ghost pendant un wind-up, seulement en Focus ; `FirstTrace` (ambiance routée, switch pendant le Focus).

## Temporaire, et à vérifier à l'oreille et à l'œil

- Mix et sons choisis par analyse (spectres, niveaux), pas à l'écoute : coupure, atténuations, volume de la nappe et
  sons d'entrée et de sortie à valider au casque.
- World et Combat n'ont encore aucune source : les pas, les impacts et les armes devront y être routés.
- Le mixer est généré par l'API interne d'Unity ; s'il faut le retoucher, l'éditer directement dans la fenêtre
  Audio Mixer (le setup ne recrée pas un mixer existant, il réapplique seulement les valeurs ci-dessus).
