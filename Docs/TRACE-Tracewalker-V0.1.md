# TRACE — Tracewalker V0.1 (intégration visuelle et structurelle)

Le membre 1 de la squad (ex-placeholder Assault) devient le **Tracewalker**, protagoniste masculin par défaut.
Mécaniques inchangées : même `DashStrike`, mêmes attaques, même capsule de collision, même caméra.

## Référence

Planche conceptuelle « TRACEWALKER — Player Character (male) — Concept Design V2 » : silhouette élancée,
techwear fonctionnel, noir / anthracite, gris froid, blanc cassé, accents jaune ambre, marque officielle `ORIGIN`,
Trace Module visible, lame courte technique.

## Ce qui a été fait

| Élément | Réalisation |
| --- | --- |
| Corps | capsule remplacée par un corps composé de primitives sous `Tracewalker Visual` : jambes et torse charbon, bottes et gants noirs à liseré ambre, pans de manteau blanc cassé (dos et côtés), épaulettes blanc cassé, capuche grise, tête grise, cheveux noirs. Gabarit inchangé (1,8 m, rayon 0,35). |
| Marque ORIGIN | brassard gauche (`Origin Mark`) : plaque blanc cassé, deux barres charbon inclinées, un trait ambre qui les traverse. Géométrie simple, une seule occurrence. |
| Trace Module | boîtier charbon en haut du torse droit, lentille ambre émissive (`Module Lens`). Composant `TraceModule` : émission 0,8 au repos, 2,6 avec pulse ±30 % à 2,5 Hz pendant Tactical Focus, réponse 0,12 s en temps réel. Lecture seule de `TacticalFocus`. |
| Arme | `Blade` dans la main droite : grip noir, garde ambre, lame gris foncé légèrement métallique avec un trait ambre, pointée vers l'avant et le bas (compatible attaques rapides et Dash Strike). Aucun système d'arme. |
| Identité | `CharacterProfile` sur le membre 1 : `tracewalker_player`, `Tracewalker`, `Tracewalker`, `Assault / Mobile Vanguard`, accent ambre. Objet renommé `Member 1 - Tracewalker`. |
| UI | `SkillHud` (IMGUI) et `TacticalOverlay` affichent `TRACEWALKER` dans la couleur d'accent du profil (ambre) quand un profil existe, sinon l'étiquette de rôle du prototype. Pas de refonte. |
| Scènes | `Prototype`, `PrototypeEncounter`, `FirstTrace` traitées par `TRACE/Apply Tracewalker V0.1` (idempotent). Rencontres, rythme, niveau, dialogues et narration inchangés. |
| Feedback existant | `CompanionFeedback` pointe sur le torse : flash de coup, teinte d'esquive, pose au sol conservés. Le `Facing Marker` du prototype est retiré (la lame et le module donnent l'orientation). |

## Matériaux (`Assets/TRACE/Scenes/Materials/Tracewalker/`)

| Matériau | Base | Notes |
| --- | --- | --- |
| `M_Tracewalker_Charcoal` | (0,13 ; 0,14 ; 0,15) | lissage 0,25 |
| `M_Tracewalker_Shadow` | (0,05 ; 0,05 ; 0,06) | bottes, gants, cheveux, barres ORIGIN |
| `M_Tracewalker_CoolGrey` | (0,55 ; 0,58 ; 0,62) | capuche, tête |
| `M_Tracewalker_OffWhite` | (0,86 ; 0,86 ; 0,83) | manteau, épaulettes, plaque ORIGIN |
| `M_Tracewalker_Amber` | (0,95 ; 0,70 ; 0,16) | liserés, garde, trait de lame, trait ORIGIN |
| `M_Tracewalker_Module` | (0,12 ; 0,11 ; 0,08) + émission ambre | lentille du module, émission pilotée par `TraceModule` |
| `M_Tracewalker_Blade` | (0,30 ; 0,32 ; 0,35) | lissage 0,55, métallique 0,6 |

Tous en URP Lit, sans chrome ni néon.

## Fichiers

Créés : `Characters/CharacterProfile.cs`, `Characters/TraceModule.cs`, `Editor/TracewalkerSetup.cs`,
`Tests/PlayMode/TracewalkerTests.cs`, les sept matériaux, `Docs/Validation/TracewalkerVisualCapture.cs`.
Modifiés : `Skills/SkillHud.cs`, `Tactical/TacticalOverlay.cs` (étiquette par profil), `Tests/PlayMode/TacticalFocusTests.cs`
(attend `TRACEWALKER`), `Scenes/Prototype.unity`, `Scenes/PrototypeEncounter.unity`, `Scenes/FirstTrace.unity`.

## Validation

| Étape | Résultat | Preuve |
| --- | --- | --- |
| Suite complète | 197/197 (193 précédents + 4 Tracewalker) | `Validation/TRACE-Tracewalker-PlayMode.xml` |
| Captures | 1/1, cinq vues 1280x720 | `Validation/TRACE-Tracewalker-Visual.xml`, `Validation/Tracewalker-*.png` |

Aucun warning ni erreur C#. Tests Tracewalker : profil, module, marque, lame et matériaux sur le membre 1 des trois scènes
(et absence sur les deux autres membres) ; émission du module qui monte sous Tactical Focus et redescend ;
étiquettes HUD ; membre 1 contrôlé au chargement de FirstTrace. Locomotion, attaque, dash, esquive, switch, focus,
lock, rencontre et slice sont couverts par les suites existantes, toutes vertes.

## Vérification manuelle

1. Rouvrir les scènes dans l'Editor (fichiers remplacés sur disque, ne pas sauvegarder l'ancienne version en mémoire).
2. `Prototype` : lisibilité de la silhouette à la distance caméra actuelle ; le module et la lame indiquent l'orientation.
3. Maintenir Tab : la lentille du module monte en intensité et pulse ; relâcher : retour en moins d'une demi-seconde.
4. Esquive et coups reçus : la teinte cyan et le flash blanc s'appliquent au torse.
5. `FirstTrace` : le Tracewalker reste distinct des deux capsules compagnons dans l'éclairage sombre du quai.
6. Hors périmètre, à décider plus tard : les locuteurs de dialogue de la slice restent `ASSAULT` (contenu narratif non modifié).
