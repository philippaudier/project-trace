# TRACE — Audio V0.1 (sound design fonctionnel de l'interface)

Première identité sonore de l'interface et des retours de jeu principaux : douze one-shots 2D courts (huit Kenney CC0,
quatre Hove Audio « royalty free »), déclenchés aux transitions d'état que le HUD calcule déjà. Pas d'impacts, de pas, de voix, de musique ni
d'ambiance (l'ambiance procédurale de `AmbienceController` reste telle quelle). Licences : `Docs/AudioLicenses.md`.

## Packs

| Pack | Licence vérifiée | Retenus |
| --- | --- | --- |
| Kenney Interface Sounds | CC0 (fichier `License.txt` de l'archive) | 8 / 100 |
| Kenney UI Audio | CC0 | 0 / 52 |
| Kenney Sci-Fi Sounds | CC0 | 0 / 73 |
| Hove Audio Free Sci-Fi UI | pas de licence formelle : « use it in whatever project you like », « Royalty Free », usage commercial et modification confirmés par l'auteur (captures dans `Docs/AudioLicenses/`) ; redistribution des fichiers bruts non précisée | 4 / 480 |

Sélection faite par analyse et non à l'écoute (à confirmer à l'oreille) : durée, crête et RMS, centroïde spectral (brillance) et spectrogrammes
(contour de hauteur, enveloppe). Critères : court, mat (centroïde bas à moyen), mono propre, contour lisible (montant,
descendant ou plat), pas d'effet rétro / casino / glitch.

## Mapping événement → son

| Événement | Clip | Source | Vol. | Pitch | Anti-spam |
| --- | --- | --- | --- | --- | --- |
| Menace hors champ (nouvelle) | `SFX_UI_ThreatWarning_01` (0,14 s, pulsations medium) | Kenney `error_008` | 1,0 | — | 2,5 s global ; un ennemi ne réalerte qu'après 6 s hors des indices |
| Opportunité de combo apparue | `SFX_UI_ComboReady_01` (0,32 s, trois paliers montants) | `confirmation_003` | 0,6 | — | 0,4 s |
| Skill prêt (recharge → prêt) | `SFX_UI_SkillReady_01` (0,12 s, ping mat) | `glass_002` | 0,6 | ±2 % | 0,35 s |
| Target Lock | `SFX_UI_TargetLock_01` (0,30 s, double clic mécanique) | Hove `Click_Combo_2` | 0,6 | ±2 % | 0,05 s |
| Target Unlock | `SFX_UI_TargetUnlock_01` (0,20 s, clic simple de la même famille) | Hove `Click_3` | 0,5 | ±2 % | silencieux si la cible est morte |
| Changement de cible | `SFX_UI_TargetChange_01` (0,02 s, tick) | `tick_002` | 0,35 | ±3 % | 0,04 s |
| Changement de personnage | `SFX_UI_CharacterSwitch_01` (0,04 s) | `select_001` | 0,4 | ±3 % | 0,04 s |
| Nouvel objectif | `SFX_UI_ObjectiveUpdate_01` (0,48 s, motif alterné grave) | `confirmation_004` | 0,4 | — | 1 s ; jamais au chargement |
| Interaction déclenchée | `SFX_UI_Interact_01` (0,04 s) | `select_002` | 0,35 | ±2 % | 0,1 s |
| Ligne de dialogue avancée par le joueur | `SFX_UI_Confirm_01` (0,10 s, deux crans montants) | `toggle_002` | 0,25 | ±2 % | 0,08 s |

Volume final = volume du cue × `uiVolume` (0,8, groupe UI du mixer). Hiérarchie : danger, puis combo / skill, puis
ciblage, puis switch, puis UI secondaire. Les clips sont normalisés en crête ; les pulsations espacées de la menace
ont un RMS bas, d'où son volume maximal ; les clips Hove, secs et au RMS plus bas, reçoivent des volumes plus hauts. `UI_Cancel` n'est pas importé : aucun événement actuel ne l'utilise.

## Écartés

Kenney Interface Sounds : 92 fichiers (`back_*`, `bong_*`, `click_*`, `close_*`, `drop_*`, `error_001-007`, `glass_001/003-006`,
`glitch_*`, `maximize_*`, `minimize_*`, `open_*`, `pluck_*`, `question_*`, `scratch_*`, `scroll_*`, `select_003-008`,
`switch_00x` (opposition de phase, quasi muets en mono), `tick_001/004`, `toggle_001/003/004`, `confirmation_001/002`),
dont `toggle_001`, `toggle_004`, `maximize_006`, `minimize_006` essayés en premier puis remplacés par Hove.
Kenney UI Audio : les 52 (clics et rollovers plus « bureau », peu techniques). Kenney Sci-Fi Sounds : les 73
(boucles de moteurs de 5 s, lasers, explosions : hors périmètre UI). Hove : 476 sur 480 (`Glitches/*` trop cyberpunk,
`Tone1-3/*` bips purs plus arcade, `Impacts/*` 4-5 s, `FX Sounds/*` presque silencieux, autres `Rings` et `Clicks`
redondants).

## Architecture

`HudAudio` (`Assets/TRACE/UI/`) sur un objet racine « HUD Audio » avec un `AudioSource` 2D (spatial blend 0, priorité
32). Il lit ce que `HudRoot` dérive déjà (membre actif, Focus, lock, cible) et les sources simples
(`CharacterSkill.IsReady`, `ComboOpportunity.Type`, `InteractionController.InteractionCount`, `MissionPanel.Objective`,
`ThreatIndicator.Hints`, `DialogueRunner.ManualAdvanceCount`), et joue un one-shot sur chaque front. Premier frame :
photographie de l'état sans son. Aucun singleton, aucun bus d'événements, aucun middleware. Ajout côté jeu :
`DialogueRunner.ManualAdvanceCount` (compteur).

`TRACE/Apply Audio V0.1` (`Editor/AudioSetup.cs`, idempotent) règle l'import et pose l'objet dans `Prototype`,
`PrototypeEncounter` et `FirstTrace` avec le mix ci-dessus. `Apply HUD V0.1` le reconnecte au nouveau HUD.

Import des clips : mono forcé, Decompress On Load, ADPCM, préchargés, fréquence conservée (44,1 kHz Kenney, 48 kHz
Hove ; latence minimale, ~3,5:1 sans le coût de décodage Vorbis). 12 fichiers WAV mono 16 bits, environ 200 Ko.

## Fichiers

`Assets/TRACE/Audio/` : `UI/` (6 clips), `Gameplay/Targeting/` (3), `Gameplay/Warnings/` (1), `Tactical/` (3, voir la passe Tactical Focus),
`License_Kenney_CC0.txt`. Code : `UI/HudAudio.cs`, `Editor/AudioSetup.cs`, `Tests/PlayMode/AudioTests.cs` ; modifiés :
`Narrative/DialogueRunner.cs`, `Editor/HudSetup.cs`, les trois scènes.

Tactical Focus (entrée, sortie, nappe) a quitté `HudAudio` : voir `Docs/TRACE-TacticalFocus-Presentation-V1.md`
(groupe de mixer Tactical, sons composites définitifs pour cette étape).

## Temporaire

- Tout le mix est un premier jet à régler à l'oreille (champs `cues` de `HUD Audio`).

## Validation

| Étape | Résultat | Preuve |
| --- | --- | --- |
| Suite complète | 238/238 (230 + 8 audio), aucun warning C# | `Validation/TRACE-Audio-PlayMode.xml` |

Tests audio : chargement silencieux, clips mono courts sur source 2D, hiérarchie du mix, combo distinct du skill ;
switch une fois par changement même rapide ; skill prêt uniquement au front ; combo une fois par apparition ;
lock / changement / unlock ; anti-spam des menaces ; `FirstTrace` sans ambiance
(passe « silence ») : interaction, nouvel objectif, avance de dialogue.

## Vérification à l'oreille (non automatisable)

1. `FirstTrace` complet casque sur les oreilles : terminal, objectifs, dialogues, combat, switchs, skills, combos,
   lock, Focus, menaces, victoire. Le HUD doit s'entendre sans devenir permanent.
2. Passe silence : désactiver l'objet « Ambience » ; chaque événement clé doit rester compréhensible.
3. Réactiver l'ambiance (drone de combat compris) : vérifier que le skill prêt et le changement de cible ne disparaissent pas.
4. Ajuster `uiVolume` et les volumes par cue sur l'objet « HUD Audio ».
