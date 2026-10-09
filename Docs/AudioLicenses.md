# Third Party Audio

Every clip under `Assets/TRACE/Audio/` comes from a pack listed here. Kenney licences were read from the `License.txt`
bundled in each archive downloaded on 2026-10-09 from the official page; Hove Audio terms from its page (see below). Archives are kept out of the
repository.

## Kenney — Interface Sounds (1.0)

- Source: https://kenney.nl/assets/interface-sounds (archive `kenney_interface-sounds.zip`)
- Author: Kenney (www.kenney.nl), creation date 11-02-2020
- License: Creative Commons Zero, CC0 1.0 — http://creativecommons.org/publicdomain/zero/1.0/
- Attribution required: No ("Support us by crediting Kenney or www.kenney.nl (this is not mandatory)")
- Restrictions: none ("free to use in personal, educational and commercial projects")
- Bundled licence kept as `Assets/TRACE/Audio/License_Kenney_CC0.txt`

Files used (original → project):

| Original | Project clip |
| --- | --- |
| `Audio/toggle_002.ogg` | `UI/SFX_UI_Confirm_01.wav` |
| `Audio/select_002.ogg` | `UI/SFX_UI_Interact_01.wav` |
| `Audio/confirmation_004.ogg` | `UI/SFX_UI_ObjectiveUpdate_01.wav` |
| `Audio/select_001.ogg` | `UI/SFX_UI_CharacterSwitch_01.wav` |
| `Audio/glass_002.ogg` | `UI/SFX_UI_SkillReady_01.wav` |
| `Audio/confirmation_003.ogg` | `UI/SFX_UI_ComboReady_01.wav` |
| `Audio/tick_002.ogg` | `Gameplay/Targeting/SFX_UI_TargetChange_01.wav`, and the scan click layer of `Tactical/SFX_TacticalFocus_Enter.wav` |
| `Audio/error_008.ogg` | `Gameplay/Warnings/SFX_UI_ThreatWarning_01.wav` |

Processing: mono downmix, leading and trailing silence trimmed (−60 dB), 5 ms fade-out, peak normalised to about
−1 dBFS, 44.1 kHz 16-bit WAV. CC0 places no condition on modification.

## Kenney — UI Audio ("UI SFX Set")

- Source: https://kenney.nl/assets/ui-audio (archive `kenney_ui-audio.zip`)
- Author: Kenney Vleugels (Kenney.nl)
- License: Creative Commons Zero, CC0 1.0
- Attribution required: No ("Credit (Kenney or www.kenney.nl) would be nice but is not mandatory")
- Files used: `Audio/switch16.ogg` → `Ambience/AMB_Detail_Relay_01.wav` (ambience pass: distant relay click).

## Kenney — Sci-Fi Sounds (1.0)

- Source: https://kenney.nl/assets/sci-fi-sounds (archive `kenney_sci-fi-sounds.zip`)
- Author: Kenney (www.kenney.nl), creation date 11-10-2020
- License: Creative Commons Zero, CC0 1.0
- Attribution required: No (not mandatory)
- Files used:
  - `Audio/spaceEngineLow_002.ogg` → low layer (below 160 Hz, at −14 dB) of `Tactical/AMB_TacticalFocus_FLOAT_LOOP.ogg`
    (it first served as the Tactical Focus bed, since replaced).
  - `Audio/computerNoise_001.ogg` → `Ambience/AMB_Emitter_Terminal_LOOP.ogg` (powered terminals, 4.4 s loop).
  - `Audio/impactMetal_002.ogg`, `Audio/impactMetal_004.ogg` → `Ambience/AMB_Detail_MetalImpact_01.wav`, `_02.wav`.

## Hove Audio — Free Sci-Fi UI Sound Effects Pack

- Source: https://hoveaudio.itch.io/free-sci-fi-ui-sound-effects-pack (downloaded manually by the project owner;
  itch.io refused automated access with a Cloudflare challenge / HTTP 403)
- Author: Hove Audio (kade@hoveaudio.com)
- License: **no formal licence file**. The archive contains only WAV files. Terms are those stated on the page:
  - description: "Thanks for checking out my pack, feel free to use it in whatever project you like."
  - page tag: "Royalty Free"
  - author reply in the page comments, to "Can I edit and use for a commercial game? (it will be credit in the credits
    section)": "Of course, my sounds are royalty free :) Good luck on your game."
  - Screenshots of both, taken 2026-10-09: `Docs/AudioLicenses/HoveAudio_PageTerms.png`,
    `Docs/AudioLicenses/HoveAudio_AuthorReply.png`.
- Attribution required: not stated. Credited anyway (see below), as the commenter offered.
- Restrictions / caveat: commercial use and modification are explicitly allowed; redistribution of the raw files is
  not addressed. This repository is public, so only four processed one-shots are included, never the pack. For a
  commercial release, ask the author for a written confirmation (kade@hoveaudio.com).
- Decision: use approved by the project owner on 2026-10-09 on that basis.

Files used (original → project):

| Original | Project clip |
| --- | --- |
| `Rings/Reverse_Ring_2_Very_Low.wav` | swell layer of `Tactical/SFX_TacticalFocus_Enter.wav` |
| `Rings/Ring_Pitched_Down.wav` | ring layer of `Tactical/SFX_TacticalFocus_Exit.wav` (first 0.22 s) |
| `Click Combos/Click_Combo_2.wav` | `Gameplay/Targeting/SFX_UI_TargetLock_01.wav` |
| `Clicks/Click_3.wav` | `Gameplay/Targeting/SFX_UI_TargetUnlock_01.wav` |

Processing: mono downmix, silence trimmed, 8 ms fade-out, peak normalised to about −1.2 dBFS, 48 kHz 16-bit WAV.

## Fallus — Industrial Ambience (loops)

- Source: https://fallus-audio.itch.io/industrial-ambience (downloaded manually by the project owner on 2026-10-09;
  itch.io refuses automated access)
- Author: Fallus (Dobroant26@yandex.ru)
- License, from the bundled `ReadMe.txt`: "Free to use in commercial and non-commercial projects. Attribution required
  (CC BY 4.0). To credit me, please use the artist name "Fallus" and link to this page (https://fallus-audio.itch.io/)".
- **Attribution required: Yes** — see the credit line below. CC BY 4.0 also asks to indicate changes: the loops were
  shortened, mixed down to mono for the placed emitters and level-normalised (details below).

Files used (original → project):

| Original (`Atmosphere loops (Noise)/`) | Project clip | Changes |
| --- | --- | --- |
| `Noise - Sublevel Pressure (Loop).ogg` | `Ambience/AMB_FirstTrace_Base_LOOP.ogg` | 90 s excerpt from 20 s, 3 s equal-power loop crossfade, −24 LUFS, stereo |
| `Noise - Ventilation Shaft (Loop).ogg` | `Ambience/AMB_FirstTrace_Environment_LOOP.ogg` | 75 s from 15 s, 3 s crossfade, −25 LUFS, stereo |
| `Noise - Unseen Observer (Loop).ogg` | `Ambience/AMB_TracePresence_LOOP.ogg` | 60 s from 10 s, 3 s crossfade, −26 LUFS, stereo |
| `Noise - Pipeline (Loop).ogg` | `Ambience/AMB_Emitter_Pipeline_LOOP.ogg` | 40 s from 30 s, 2 s crossfade, mono, −22 LUFS |
| `Noise - Structural Resonance (Loop).ogg` | `Ambience/AMB_Emitter_Machinery_LOOP.ogg` | 40 s from 30 s, 2 s crossfade, mono, −22 LUFS |

## SRG774 — Dark Sci-Fi Audio Pack

- Source: https://opengameart.org/content/dark-sci-fi-audio-pack (MP3 files downloaded from the page on 2026-10-09)
- Author: SRG774
- License: **CC0 1.0** — page text: "released under the Creative Commons CC0 Public Domain Dedication. You can copy,
  modify, distribute, and perform the work, even for commercial purposes, all without asking permission or providing
  attribution. Though not required, mentioning SRG774 or linking back to this pack is always highly appreciated."
- Attribution required: No (credited anyway below).
- Files used: `airy.mp3` → main layer of `Tactical/AMB_TacticalFocus_FLOAT_LOOP.ogg` (58.5 s excerpt, 2 s
  equal-power loop crossfade, Kenney low layer mixed at −14 dB, +11 dB, stereo).

## Not used

- **SimonPeter.wav — Sounds From The Underground**: downloaded by the project owner, but the archive holds no licence
  (only a thank-you note) and its tracks are mostly musical (piano, strings, arpeggios). Not imported.
- **Nexus Core (sahjahmusic-hue)**: optional, not needed (Airy and Fallus covered every layer); not downloaded.

The Tactical Focus enter / exit cues also contain a short synthesised low pulse (sine sweep generated with ffmpeg,
no third-party material).

Credit lines for the game credits:
- "UI sound effects: Kenney (kenney.nl, CC0) and Hove Audio (hoveaudio.itch.io)"
- "Ambience: Fallus — Industrial Ambience (https://fallus-audio.itch.io/), licensed under CC BY 4.0, edited"
- "Tactical Focus texture: SRG774 — Dark Sci-Fi Audio Pack (OpenGameArt, CC0)"
