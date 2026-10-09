# TRACE — Voice System V0.1 (vocalisations de combat)

Couche gameplay seulement : chaque membre de la squad peut émettre de courtes vocalisations contextuelles, sans spam,
avec ou sans vrais enregistrements. Pas de doublage narratif, pas de dialogue, pas de sous-titres.
Menu : `TRACE/Apply Voice V0.1` (idempotent) sur `Prototype`, `PrototypeEncounter`, `FirstTrace`, `FieldTest`.

## Architecture (`Assets/TRACE/Voice/`)

| Composant | Rôle |
| --- | --- |
| `CharacterVoiceSet` (ScriptableObject) | les lignes d'un personnage : une par catégorie, avec ses variantes de clips, volume, probabilité, cooldown ; `characterId`, préfixe de fichiers, jitter de hauteur (≤ ±1 %) |
| `VoiceCategory` / `VoicePolicy` | catégories, priorité fixe par catégorie, cooldowns et probabilités par défaut |
| `CharacterVoice` (sur chaque membre) | possède le `VoiceAudioSource`, applique la politique (priorité, cooldown, probabilité, pas de répétition immédiate, jitter), écoute ses propres événements : attaque, esquive, coups, vie basse, skill principal |
| `SquadVoiceDirector` (sur Squad) | événements d'équipe : switch (in / out), début de combat une fois par engagement, une seule réaction quand un membre tombe, souffle discret au Tactical Focus |
| `VoiceDebugPanel` (sur Squad) | **F9** : panneau IMGUI, un bouton par catégorie pour le membre actif, probabilité forcée, cooldown ignoré |
| `Editor/VoiceSetup` | imports, mixer (groupe Voice), sets remplis depuis la convention de nommage, sources et composants dans les scènes |

Aucun singleton ; aucune écriture d'état de jeu. Le runtime ne lit jamais les noms de fichiers : seuls les clips assignés
dans le set comptent.

## Sets et personnages

| Membre | Set | Préfixe | Identité vocale visée |
| --- | --- | --- | --- |
| Tracewalker | `Audio/Voice/Tracewalker/VoiceSet_Tracewalker.asset` | `TW` | masculin, calme, concentré, peu bavard, effort sec |
| Control | `Audio/Voice/Control/VoiceSet_Control.asset` | `Control` | féminine, analytique, précise, froide sans être robotique |
| Support | `Audio/Voice/Support/VoiceSet_Support.asset` | `Support` | féminine, calme, rassurante, douce mais professionnelle |

## Catégories implémentées

| Catégorie | Déclencheur | Priorité | Cooldown | Probabilité | Clips V0.1 |
| --- | --- | --- | --- | --- | --- |
| AttackLight | front montant de `PlayerMeleeAttack.IsAttacking` | Low | 0,35 s | 55 % | 3 |
| AttackHeavy | aucun (réservé) | Low | 0,5 s | 80 % | 0 |
| Dodge | front montant de `ThirdPersonMotor.IsDodging` | Low | 0,8 s | 50 % | 2 |
| HurtLight | `Health.OnDamaged` sous 20 % des PV max | Low | 0,6 s | 85 % | 3 |
| HurtHeavy | `Health.OnDamaged` à partir de 20 % des PV max | Critical | 1,2 s | 100 % | 2 |
| LowHealth | passage sous 30 % des PV, réarmé au-dessus de 35 % | High | 12 s | 100 % | 1 |
| SkillPrimary | `CharacterSkill.Activated` | High | 1,5 s | 80 % | 2 |
| SwitchIn | nouveau membre actif, hors rafale (1,5 s) | Medium | 1,5 s | 55 % | 2 |
| SwitchOut | membre quitté | Medium | 1,5 s | 20 % | 0 |
| CombatStart | HUD Exploration → Combat, une fois par engagement | Medium | 6 s | 40 % | 1 |
| AllyDown | mort d'un membre : une réaction (membre actif, sinon premier survivant) | Critical | 2 s | 100 % | 1 |
| ComboReady | fenêtre de combo (ennemis groupés → membre actif ; membre protégé → lui-même) | Medium | 4 s | 50 % | 2 |
| TacticalFocusEnter / Exit | entrée / sortie du Focus, membre actif | Low | 6 s | 25 % / 15 % | 2 / 0 |
| Contextual | aucun (réservé) | Low | 3 s | 50 % | 0 |

Règles : une catégorie vide est du silence, jamais une erreur. Une ligne plus prioritaire interrompt une ligne en cours ;
une ligne de priorité égale ou inférieure attend ou se tait. Un tirage de probabilité refusé compte comme un passage
(pas de nouveau tirage avant le cooldown). Avec plusieurs variantes, le clip précédent de la catégorie n'est jamais rejoué
immédiatement. Jitter de hauteur ±1 % au plus. Un seul son de douleur par coup (le lourd gagne). Les hurt ne jouent pas sur
un membre mort ; sa source est coupée à la mort.

## Audio

- Groupe de mixer **Voice** sous Master (entre UI et Tactical). Snapshot Tactical Focus : −2 dB, passe-bas à 6000 Hz
  seulement, les voix restent intelligibles sous la membrane.
- `VoiceAudioSource` par membre à 1,5 m de hauteur : 3D à 60 %, atténuation linéaire 3 → 28 m, pas de Doppler,
  spread 30°, priorité 20, reverb zone mix 0,3. Le membre contrôlé est à portée de caméra : toujours lisible.
- Imports (`VoiceSetup.ConfigureImports`, dossier `Audio/Voice`) : mono forcé, Decompress On Load, ADPCM, préchargé,
  fréquence conservée, jamais en streaming.

## Convention de nommage et remplacement des placeholders

```
Assets/TRACE/Audio/Voice/
├── Tracewalker/   VO_TW_<Categorie>_NN.wav          (+ VoiceSet_Tracewalker.asset)
├── Control/       VO_Control_<Categorie>_NN.wav     (+ VoiceSet_Control.asset)
├── Support/       VO_Support_<Categorie>_NN.wav     (+ VoiceSet_Support.asset)
└── Temp/          TMP_VO_<Prefixe>_<Categorie>_NN.wav  (placeholders synthétiques)
```

`<Categorie>` est le nom de l'énumération (`AttackLight`, `Dodge`, `HurtLight`, `HurtHeavy`, `SkillPrimary`, `SwitchIn`,
`LowHealth`, `AllyDown`, `CombatStart`, …). Alias acceptés : `Skill_<Nom>` (ex. `VO_TW_Skill_DashStrike_01`) → SkillPrimary,
`Attack` → AttackLight, `Hurt` → HurtLight, `Focus` / `FocusEnter` / `FocusExit`.

Workflow : enregistrer `VO_TW_AttackLight_01.wav` (mono, 44,1 kHz, court) dans `Tracewalker/`, puis relancer
`TRACE/Apply Voice V0.1`. Pour chaque catégorie, les vrais fichiers du dossier du personnage remplacent entièrement les
placeholders de `Temp` ; les catégories sans enregistrement gardent leurs placeholders. Volume, probabilité et cooldown déjà
réglés dans le set sont conservés. On peut aussi glisser les clips à la main dans l'Inspector du set : rien dans le code
ne dépend des noms.

## Placeholders

`Docs/Audio/voice_placeholders.py` synthétise 51 bursts vocaux artificiels (train d'impulsions glottiques à travers trois
formants, souffle, enveloppe par catégorie, hauteur de base 115 / 205 / 235 Hz pour TW / Control / Support) :
3 AttackLight, 2 Dodge, 3 HurtLight, 2 HurtHeavy, 2 SkillPrimary, 2 SwitchIn, 1 LowHealth, 1 AllyDown, 1 CombatStart
par personnage. Ils sont volontairement reconnaissables comme provisoires. GUID des `.meta` dérivés du nom de fichier
(stables entre regénérations et copies du projet).

Clips encore manquants (aucun fichier, catégorie silencieuse) : AttackHeavy, SwitchOut, TacticalFocusExit, Contextual, pour
les trois personnages. Les déclencheurs existent déjà pour SwitchOut et la sortie du Focus.

## Imported Generated Voices (2026-10-09)

42 lignes générées (mp3, voix de synthèse, anglais) déposées dans `Assets/TRACE/Audio`, déplacées avec leurs `.meta`
(GUID conservés) et renommées `VO_<Character>_<Event>_<NN>`. Elles remplacent les placeholders de leurs catégories ;
AttackLight, Dodge, HurtLight et HurtHeavy gardent les placeholders synthétiques `TMP_` faute d'enregistrement.
Import : mono forcé, Decompress On Load, ADPCM, préchargé. Routage : groupe `Voice`. Statut : **TEMP** (voix de
synthèse de pré-production, à remplacer par des prises définitives le moment venu).

| Fichier final | Ancien nom | Personnage | Catégorie | Phrase |
| --- | --- | --- | --- | --- |
| `VO_Tracewalker_SwitchIn_01` | `player_male_I’m on it` | Tracewalker | SwitchIn | “I’m on it.” |
| `VO_Tracewalker_SwitchIn_02` | `player_male_My turn` | Tracewalker | SwitchIn | “My turn.” |
| `VO_Tracewalker_CombatStart_01` | `player_male_Contact` | Tracewalker | CombatStart | “Contact.” |
| `VO_Tracewalker_CombatStart_02` | `player_male_Stay sharp` | Tracewalker | CombatStart | “Stay sharp.” |
| `VO_Tracewalker_DashStrike_01` | `player_male_Moving` | Tracewalker | SkillPrimary | “Moving.” |
| `VO_Tracewalker_DashStrike_02` | `player_male_Opening` | Tracewalker | SkillPrimary | “Opening.” |
| `VO_Tracewalker_LowHealth_01` | `player_male_Still good` | Tracewalker | LowHealth | “Still good.” |
| `VO_Tracewalker_LowHealth_02` | `player_male_Keep moving` | Tracewalker | LowHealth | “Keep moving.” |
| `VO_Tracewalker_AllyDown_01` | `player_male_Operator down` | Tracewalker | AllyDown | “Operator down.” |
| `VO_Tracewalker_AllyDown_02` | `player_male_I’ve got them` | Tracewalker | AllyDown | “I’ve got them.” |
| `VO_Tracewalker_ComboReady_01` | `player_male_Now` | Tracewalker | ComboReady | “Now.” |
| `VO_Tracewalker_ComboReady_02` | `player_male_Window open` | Tracewalker | ComboReady | “Window open.” |
| `VO_Tracewalker_TacticalFocusEnter_01` | `player_male_Focus` | Tracewalker | TacticalFocusEnter | “Focus.” |
| `VO_Tracewalker_TacticalFocusEnter_02` | `player_male_Reading the field` | Tracewalker | TacticalFocusEnter | “Reading the field.” |
| `VO_Control_SwitchIn_01` | `control_Taking control` | Control | SwitchIn | “Taking control.” |
| `VO_Control_SwitchIn_02` | `control_I have the field` | Control | SwitchIn | “I have the field.” |
| `VO_Control_CombatStart_01` | `control_Targets mapped` | Control | CombatStart | “Targets mapped.” |
| `VO_Control_CombatStart_02` | `control_Assessing` | Control | CombatStart | “Assessing.” |
| `VO_Control_GravityField_01` | `control_Field deployed` | Control | SkillPrimary | “Field deployed.” |
| `VO_Control_GravityField_02` | `control_Containment active` | Control | SkillPrimary | “Containment active.” |
| `VO_Control_LowHealth_01` | `control_Systems stable` | Control | LowHealth | “Systems stable.” |
| `VO_Control_LowHealth_02` | `control_I’m functional` | Control | LowHealth | “I’m functional.” |
| `VO_Control_AllyDown_01` | `control_Operator compromised` | Control | AllyDown | “Operator compromised.” |
| `VO_Control_AllyDown_02` | `control_Cover them` | Control | AllyDown | “Cover them.” |
| `VO_Control_ComboReady_01` | `control_Target grouped` | Control | ComboReady | “Target grouped.” |
| `VO_Control_ComboReady_02` | `control_Exploit the opening` | Control | ComboReady | “Exploit the opening.” |
| `VO_Control_TacticalFocusEnter_01` | `control_Telemetry clear` | Control | TacticalFocusEnter | “Telemetry clear.” |
| `VO_Control_TacticalFocusEnter_02` | `control_Patterns visible` | Control | TacticalFocusEnter | “Patterns visible.” |
| `VO_Support_SwitchIn_01` | `support_im_here` | Support | SwitchIn | “I’m here.” |
| `VO_Support_SwitchIn_02` | `support_I’ve got you` | Support | SwitchIn | “I’ve got you.” |
| `VO_Support_CombatStart_01` | `support_Stay within reach` | Support | CombatStart | “Stay within reach.” |
| `VO_Support_CombatStart_02` | `support_I’ll cover you` | Support | CombatStart | “I’ll cover you.” |
| `VO_Support_PulseShield_01` | `support_Shield up` | Support | SkillPrimary | “Shield up.” |
| `VO_Support_PulseShield_02` | `support_Barrier deployed` | Support | SkillPrimary | “Barrier deployed.” |
| `VO_Support_LowHealth_01` | `support_I can continue` | Support | LowHealth | “I can continue.” |
| `VO_Support_LowHealth_02` | `support_Still with you` | Support | LowHealth | “Still with you.” |
| `VO_Support_AllyDown_01` | `support_They’re down` | Support | AllyDown | “They’re down!” |
| `VO_Support_AllyDown_02` | `support_I’m on them` | Support | AllyDown | “I’m on them.” |
| `VO_Support_ComboReady_01` | `support_You’re protected` | Support | ComboReady | “You’re protected.” |
| `VO_Support_ComboReady_02` | `support_Go, now` | Support | ComboReady | “Go, now.” |
| `VO_Support_TacticalFocusEnter_01` | `support_Field is stable` | Support | TacticalFocusEnter | “Field is stable.” |
| `VO_Support_TacticalFocusEnter_02` | `support_I’m with you` | Support | TacticalFocusEnter | “I’m with you.” |

Aucun fichier ambigu, aucun fichier laissé non assigné. Le setup accepte désormais le nom complet du personnage comme
préfixe (`VO_Tracewalker_` en plus de `VO_TW_`) et les alias de skill `DashStrike`, `GravityField`, `PulseShield`,
ainsi que `TacticalFocus`. ComboReady, jusque-là sans déclencheur, est branché dans `SquadVoiceDirector` : ennemis
groupés par le Gravity Field → le membre actif annonce la fenêtre ; membre protégé (contre) → ce membre parle lui-même.
Une fois par ouverture de fenêtre, cooldown 4 s, probabilité 50 %.

## Validation

| Étape | Résultat | Preuve |
| --- | --- | --- |
| Suites audio (Voice, Audio, Ambience, Présentation Focus) | 10/10 voix, 31/32 puis 32/32 après correction du setup (références de sets perdues entre scènes) | `Logs/voice-tests2.xml` |
| Suite complète | 285/285 (273 précédents + 12 voix, dont mapping des lignes importées et fenêtres de combo) | `Validation/TRACE-Voice-PlayMode.xml` |

Tests voix : sets et sources sur les trois membres (catégories minimales, mono, non streamé, court, groupe Voice, 3D légère) ;
attaques limitées en cadence sans répétition de variante ; hurt léger / lourd exclusifs, lourd prioritaire, cadence ;
vie basse une fois puis réarmement après remontée ; switch hors rafale ; skill et une seule réaction AllyDown ;
début de combat une fois par engagement et Focus discret ; clips manquants ou set absent = silence sans erreur ;
groupe Voice dans le mixer ; voix présentes dans FirstTrace et FieldTest.

## Vérification manuelle

1. Rouvrir les scènes sans sauvegarder la version en mémoire (fichiers remplacés sur disque).
2. `FieldTest` : combat ouvert puis intérieur, switch 1 / 2 / 3, skills, coups reçus, laisser tomber un membre.
   Les voix doivent rester lisibles sous l'ambiance, les SFX de combat et le Tactical Focus.
3. **F9** pour auditionner chaque catégorie du membre actif.
4. `FirstTrace` : seules les vocalisations de combat jouent, aucune ligne narrative ajoutée.
5. Réglages : volumes, probabilités et cooldowns dans les trois `VoiceSet_*.asset` ; seuils de coup lourd et de vie basse
   sur chaque `CharacterVoice` ; fenêtre de rafale de switch sur le `SquadVoiceDirector`.
