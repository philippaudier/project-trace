# TRACE — HUD V0.1 (ORIGIN Field Interface)

Socle du HUD in-game, construit en UGUI sur un canvas screen-space par scène (`TRACE/Apply HUD V0.1`, idempotent),
dans `Prototype`, `PrototypeEncounter` et `FirstTrace`. Référence : planche « TRACE HUD V0.1 — ORIGIN Field Interface ».

## Palette et style

Charbon translucide (0,07 ; 0,075 ; 0,085 ; α 0,82) pour les panneaux, blanc cassé pour le texte, gris froid pour le
secondaire, **ambre ORIGIN #FFC629** pour les barres d'accent et l'en-tête d'objectif, **cyan #5ED6FF** pour tout ce
qui est analytique (READY, LOCK, Focus, module), rouge-orange seulement pour la vie ennemie, DOWN et les menaces.
Panneaux à coins coupés (sprite 9-slice `HudPanel.png`), barre d'accent de 3 px à gauche, lignes fines, police LegacyRuntime
en trois tailles (18 titres, 13–15 corps, 10–11 labels), uppercase pour les titres.

## Architecture (`Assets/TRACE/UI/`)

| Composant | Rôle |
| --- | --- |
| `HudRoot` | pilote scène-local : lit squad, Tactical Focus, Target Lock, rencontre et ennemis ; calcule l'état (Exploration / Combat / Focus) et la cible courante ; tique tous les panneaux. Aucune logique de jeu. |
| `HudPanel` | base : `CanvasGroup` fondu + glissement en temps réel vers la visibilité décidée par le panneau ; rafraîchissement texte à 20 Hz ; palette partagée |
| `ActiveOperatorPanel` | bas gauche : portrait, nom, archétype, HP (barre + chiffres), slot E (nom du skill, barre de recharge, READY / s), slots R ULTIMATE (placeholder) et RMB DODGE, état (FIELD / ENGAGED / ANALYSIS / EVADE / DOWN), flash au switch |
| `SquadStatusPanel` | bas droite : deux cartes (mini portrait, numéro + nom, barre HP, skill READY / s hors exploration, statut DOWN / HP / COMBO CONTRE, pastille ambre) |
| `MissionPanel` | haut droite : `// MAIN OBJECTIVE`, objectif, ligne de détail cyan, secteur. Sources : `StoryDirector` (objectif et secteur par phase) et `EncounterController` (vague, hostiles, contact, zone sécurisée, défaite) |
| `TargetPanel` | haut centre : tag LOCK (cyan) ou TARGET (gris), archétype, HP rouge, statut (`StatusLabel`, GARDE FRONTALE, SLOWED), ligne COMBO ambre. Cadre cyan si verrouillé |
| `InteractionPrompt` | bas centre : touche `F` sur fond ambre, `INTERACT` + libellé de l'interactable (`InteractionController.Current`) |
| `ReticleController` | centre : anneau fin, 25 % en exploration, 80 % en combat, cyan si une cible molle existe, caché sous lock (le marqueur monde prend le relais) et en Focus |
| `ThreatIndicatorView` | bords : jusqu'à quatre étiquettes rouges positionnées depuis `ThreatIndicator.Hints` (la détection reste dans l'indicateur) |
| `TacticalFocusOverlay` | couche Focus : quatre équerres cyan qui respirent, ligne technique, bloc ANALYSIS (hostiles détectés, attaques en préparation, combos, verrouillage), et un `Volume` global (`HudFocusGrade.asset` : saturation −42, filtre légèrement cyan, contraste +6) dont le poids suit le Focus en temps réel |

Canvas en Screen Space - Camera avec un plan à 0,12 m (near clip 0,1) : l'UI est testée en profondeur contre la scène,
et un plan plus loin (0,6 m à l'origine) se faisait masquer par les murs quand la caméra de collision s'y collait.
L'overlay tactique reçoit la même distance.

Séparation : les données restent dans les composants de jeu, les panneaux ne font que lire et présenter, `HudRoot` est
le seul binding. Aucun singleton.

## Les trois états

| État | Condition (`HudRoot`) | Comportement |
| --- | --- | --- |
| Exploration | ni Focus, ni combat depuis 2,5 s | opérateur à 90 %, squad à 75 % sans détail de skill, objectif, reticle discret, prompt d'interaction, pas de panneau cible |
| Combat | rencontre en Spawning / Fighting / Cleared, ou un ennemi engagé / en préparation d'attaque à moins de 18 m du membre actif | panneaux pleins, cooldowns, panneau cible (lock ou cible molle), reticle visible, menaces aux bords |
| Focus | `TacticalFocus.IsActive` | couche analytique, grade désaturé, ANALYSIS, cooldowns des trois membres et combos sur les cartes, objectif atténué à 35 %, reticle caché ; l'overlay existant garde ses cartes ennemies (en-tête déplacé en haut à gauche, panneaux membres retirés) |

Les transitions sont des fondus de 0,15 s environ (vitesse 7 /s) avec un glissement de 10 px, en temps non scalé.

## Target Lock et combos

Lock dur : tag LOCK cyan, cadre cyan, barre cyan de ligne, reticle masqué ; le marqueur diamant monde V0.10 reste.
Cible molle (attaque ou ennemi qui vise le membre actif) : tag TARGET gris, cadre atténué, reticle cyan.
Combos : `COMBO GROUPED … 1 + E` sur le panneau cible, `COMBO CONTRE` et pastille ambre sur la carte du membre protégé,
ligne dédiée dans ANALYSIS en Focus.

## Portraits

`CharacterProfile` porte un `Sprite portrait`. Trois sprites dans `Assets/TRACE/UI/Portraits/` : recadrages des planches
Tracewalker et Control (256 × 256) et un placeholder neutre pour Support, qui reçoit un profil provisoire
(`support_placeholder`, « Field Support — placeholder », ORIGIN, bleu). Remplacer un portrait = remplacer le sprite
dans le profil, rien à toucher dans le HUD.

## Reprise des IMGUI du prototype

`SkillHud` désactivé ; `InteractionController.legacyPrompt`, `ThreatIndicator.legacyGui` et `EncounterHud.showWaveBox`
à faux (le HUD rend ces informations). `EncounterHud` garde les bannières de vague et les panneaux de résultat,
`DialogueRunner` et `StoryDirector` gardent leurs IMGUI narratifs. Le post-processing est activé sur la caméra des
scènes prototype pour le grade du Focus (aucun autre volume ne s'y applique).

## Ajustements V0.1.1 (lisibilité)

Retours de la première revue visuelle, sans nouveau système :

| Point | Changement |
| --- | --- |
| Tactical Focus trop verbeux | ANALYSIS tient sur une ligne de tags courts (`HOSTILES n`, `WIND-UP n`, `COMBO 1+E` / `COMBO CONTRE`, `LOCK`) allumés quand ils sont actionnables, grisés sinon ; panneau réduit à 46 px. Les cartes ennemies passent à deux lignes : `LOCK  ARCHETYPE  PV`, puis seulement les tags utiles (`WIND-UP 0.3 > 2`, `COMBO 1+E`, `SLOWED`, `GUARD > FLANK`) ; l'état de déplacement (CHASE, REPOSITION…) n'est plus affiché. Cartes 240 × 48, corps 14. |
| Panneau cible trop présent | Bandeau compact par défaut (280 × 40, nom 13, barre 4 px). `EnemyBrain.elite` (case à cocher) passe le panneau en disposition élite / boss (420 × 64, nom 17, barre 8 px). Tailles réglables dans `TargetPanel` (`compact`, `elite`). |
| Identité couleur du squad | Chaque carte reprend la couleur du `CharacterProfile` (Tracewalker ambre, Control cyan, Support menthe) : barre verticale, contour du portrait et numéro de slot. La barre du panneau devient neutre pour ne pas concurrencer. |
| Dialogues | `DialogueRunner.speakers` (câblé par `Apply HUD V0.1`) résout le locuteur vers un profil par nom affiché ou premier mot de l'archétype (`ASSAULT` → Tracewalker) : mini-portrait 72 px avec contour, nom affiché du profil dans sa couleur, barre d'accent. Les autres locuteurs (`TERMINAL`) restent neutres, sans portrait. |

## Fichiers

Créés : les dix scripts `UI/`, `Editor/HudSetup.cs`, `Tests/PlayMode/HudTests.cs`, `Docs/Validation/HudVisualCapture.cs`,
`UI/Sprites/Hud{Panel,White,Reticle,Bracket}.png`, `UI/Portraits/Portrait_{Tracewalker,Control,Support}.png`,
`UI/HudFocusGrade.asset`.
Modifiés : `CharacterProfile.cs` (portrait), `InteractionController.cs`, `ThreatIndicator.cs`, `EncounterHud.cs` (drapeaux),
`TRACE.Runtime.asmdef` et `TRACE.Editor.asmdef` (références UGUI / URP), les trois scènes.

## Validation

| Étape | Résultat | Preuve |
| --- | --- | --- |
| Suite complète | 217/217 (210 précédents + 7 HUD) | `Validation/TRACE-HUD-PlayMode.xml` |
| Captures | 1/1, cinq vues 1280x720 | `Validation/TRACE-HUD-Visual.xml`, `Validation/Hud-*.png` |
| V0.1.1 : suite complète | 226/226 (222 précédents + 4 ajustements) | `Validation/TRACE-HUD-V0.1.1-PlayMode.xml` |
| V0.1.1 : captures | 1/1, six vues (+ `Hud-target-elite`) | `Validation/TRACE-HUD-V0.1.1-Visual.xml` |

Les captures batch ne rendent pas l'IMGUI : le dialogue (portrait, couleur) se vérifie en jeu, sa résolution de
locuteur est couverte par `DialogueSpeakersResolveToSquadProfiles`.

Tests HUD : exploration (opérateur, squad, objectif, pas de cible, reticle discret, IMGUI héritée coupée) ; portraits
sur les trois profils ; switch (panneau actif et cartes) ; combat puis lock puis retour au calme ; Focus (overlay,
grade, ANALYSIS, cooldowns, sortie propre) ; vagues de la rencontre dans l'objectif ; prompt et objectif de First Trace.

## Vérification manuelle

1. Rouvrir les scènes sans sauvegarder la version en mémoire (fichiers remplacés sur disque).
2. `Prototype` : marcher vers les ennemis, vérifier la bascule Exploration → Combat → retour, le panneau cible, le lock
   (molette), Tab pour le Focus et son grade. Lisibilité des tailles de police à ta résolution.
3. `FirstTrace` : prompt au terminal, objectif qui suit les phases, vagues dans l'objectif, menaces aux bords.
4. Ajuster si besoin : alphas d'exploration (champs des panneaux), poids du grade (`TacticalFocusOverlay`), seuils de
   combat (`HudRoot`).
5. Hors périmètre : compas / distance d'objectif, niveau ennemi, ultimate réelle, Support final.
