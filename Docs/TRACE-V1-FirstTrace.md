# TRACE V1 — First Trace (vertical slice)

## Jouer

Ouvrir `Assets/TRACE/Scenes/FirstTrace.unity` (première scène de build), Play, cliquer dans la Game View.
Commandes inchangées : WASD, souris, clic gauche, clic droit (esquive), E (compétence), 1/2/3, Tab (Tactical Focus),
clic molette / molette (Target Lock), **F** (interagir, continuer un dialogue), Backspace (recommencer).
`Prototype.unity` et `PrototypeEncounter.unity` restent des scènes de test technique.

## Flow complet

| # | Phase | Lieu (z) | Ce qui se passe |
| --- | --- | --- | --- |
| 1 | Arrivée | quai de chargement (−10 → 12) | Squad au complet, lampe chaude qui vacille, camion mort, flaques, vent et bourdonnement électrique. Après 4 s : échange CONTROL / ASSAULT. Aucun ennemi. |
| 2 | Passage | couloir (12 → 31) | Rétrécissement, câbles pendus, lampe froide. Alcôve optionnelle à gauche : chaise renversée et **badge** (interaction secondaire, deux lignes). |
| 3 | Indice | terminal A (z 28,5) | `F` : `ARCHIVE ENTRY // INCOMPLETE`, `Occupancy records: 143`, `Registered residents: 0`, puis l'échange « Ça ne colle pas » / « Trop grand pour les plans ». La porte 1 s'ouvre. |
| 4 | Hall | halle à piliers (31 → 61) | Espace ouvert, flaques, mousse, étagère effondrée, salle latérale avec six chaises en cercle, « 143 » peint sur le mur du fond près de la sortie (et au-dessus de la porte du terminal, côté couloir). Au centre : **événement étrange**, la porte du fond s'ouvre seule, une lumière froide s'allume au-dessus, un écran mural affiche `OCCUPANCY 143 / RESIDENTS 0` trois secondes, un grincement. Dialogue. |
| 5 | Tension | couloir (61 → 73) | Bandes d'alerte, trois lampes rouges qui montent, bourdonnement plus fort. « Restez groupés. Ils arrivent. » |
| 6 | Combat | arène (73 → 100) | Zone d'entrée = `EncounterStartPoint` : PV complets, compétences prêtes, drone de combat. Rencontre V0.9 en trois vagues ; chaque vague entre par une **porte qui s'ouvre** (A, B, C) après l'annonce d'arrivée. |
| 7 | Conclusion | terminal B (−15 ; 78) | 1,5 s de calme, drone coupé, le terminal éteint s'allume : `IDENTITY MATCH FOUND`, `STATUS: DECEASED`, `LAST ACCESS: 12 MINUTES AGO`, « ...Douze minutes. » / « On n'est pas seuls ici. » |
| 8 | Fin | | `END OF FIRST TRACE` avec les statistiques de la rencontre et trois choix : Recommencer, Scène Prototype, Rester ici. |

Composition des vagues : 2 Pursuers (porte A) ; 2 Pursuers (porte B) + 1 Marksman (porte C) ; 1 Bulwark (A) + 1 Marksman (B) + 2 Pursuers (C).
Réglages ennemis V0.9 inchangés ; délai d'annonce 2,5 s, pause 3 s, +20 HP entre vagues.

Durée estimée : 1 à 2 min d'exploration et de narration, 2 à 4 min de combat, 30 s de conclusion, soit **4 à 7 minutes**.
Non mesurée en jeu, à confirmer manuellement.

## Interactions narratives

- **Terminal A** (`Interactable`, portée 2,8 m, usage unique) : déclenche l'archive, l'échange de la squad et la porte 1.
- **Badge** (alcôve, usage unique, optionnel) : deux lignes ; n'influence pas la progression.
- **Événement du hall** (zone, rayon 5 m) : porte 2, lumière, écran, grincement, trois lignes.
- **Zone de tension** : lampes rouges, bourdonnement, deux lignes dont un rappel Tab / molette.
- **Zone d'arène** : soin, cooldowns, départ de la rencontre, portes des vagues.
- **Conclusion** : terminal B, cinq lignes, puis écran de fin.

Toutes les lignes sont des placeholders en français, dans l'Inspector du composant **Story Director** (objet `Story`).

## Systèmes ajoutés (légers, réutilisables)

| Script | Rôle |
| --- | --- |
| `Narrative/Interactable.cs` | prompt, portée, usage unique, callback `Interacted`, surbrillance émissive |
| `Narrative/InteractionController.cs` | sur Squad : plus proche interactable autour du membre contrôlé, prompt IMGUI, `Interact` |
| `Narrative/DialogueRunner.cs` | file de `DialogueLine` (locuteur, texte, durée), auto-avance en temps réel, `F` passe à la suite |
| `Narrative/StoryDirector.cs` | phases Arrival → Clue → Hall → Tension → Combat → Conclusion → End, zones par distance, portes, lumières, écran de fin |
| `Narrative/GateDoor.cs` | dalle coulissante `Open()` |
| `Narrative/FlickerLight.cs` | vacillement d'intensité en temps réel |
| `Narrative/AmbienceController.cs` | sons placeholder générés en code : vent, bourdonnement, gouttes, grincement, drone de combat |
| `Narrative/WallText.cs` + `Shaders/WallText.shader` | texte 3D mural testé en profondeur, suit l'atlas de police |
| `Editor/FirstTraceSetup.cs` | création unique de la scène à partir de `PrototypeEncounter` |

`EncounterController` gagne `Auto Start` (désactivé dans la slice) et `Begin()` ; `CharacterSkill` gagne `ResetCooldown()`.
Aucune nouvelle mécanique de combat, aucun ennemi, aucune progression, aucun système de sauvegarde.

## Éclairage et ambiance

Soleil froid faible (0,62), brouillard linéaire 18 → 90 m, ambiance trilight sombre, post-processing URP léger
(vignette 0,26, saturation −12, contraste +8, bloom 0,45 sur les écrans et lampes), lampes ponctuelles : chaude au quai (12)
et dans la salle des chaises (6), froides dans le couloir (8), le hall et l'arène (16, à 5,4 m), rouges dans le couloir de tension
(1,2 au repos, 12 en tension), teal sur les terminaux (2,5 et 8). Les valeurs sont volontairement hautes : l'atténuation URP des lampes
ponctuelles sur un sol à 5 m est forte, et c'est le premier réglage à ajuster en jouant.
Aucune lumière volumétrique, pas d'ombres sur les lampes locales. Le pipeline PC (`PC_RPAsset`, actif) passe les lumières
additionnelles de **per-vertex à per-pixel** (limite 8 par objet) : en per-vertex, les grandes primitives du niveau restaient noires.
Les textes muraux (« 143 », écran de l'événement) sont des `TextMesh` avec le shader `TRACE/WallText`, copie du shader de
police intégré avec un vrai test de profondeur ; le composant `WallText` les garde liés à l'atlas de la police dynamique.
Les glyphes sont rasterisés à 180 px puis réduits (`characterSize`), sinon le texte est flou de près.

## Fichiers

Créés : les huit scripts ci-dessus, `Scenes/FirstTrace.unity`, `Scenes/FirstTraceVolume.asset`,
`Scenes/Navigation/FirstTraceNavMesh.asset`, `Scenes/Materials/FirstTrace/FT_*.mat` (14 matériaux dont `FT_WallText`), `Shaders/WallText.shader`,
`Tests/PlayMode/FirstTraceTests.cs`, `Docs/Validation/FirstTraceVisualCapture.cs`.
Modifiés : `EncounterController.cs`, `CharacterSkill.cs`, `EncounterTests.cs` (la scène de rencontre n'est plus la première du build),
`TRACE.Editor.asmdef` (référence RP Core pour le Volume), `Assets/Settings/PC_RPAsset.asset` (lumières per-pixel),
`ProjectSettings/EditorBuildSettings.asset`, `Docs/AI/UnityProjectContext.md`.

## Validation

Copie isolée `Logs/v09-project` (packages synchronisés avec ProBuilder 6.0.9 et le post-processing legacy installés par l'utilisateur).

| Étape | Résultat | Preuve |
| --- | --- | --- |
| Baseline avant V1 | 185/185 (V0.1–V0.10 + bindings) | `Validation/TRACE-V0.10-PlayMode.xml` |
| Suite finale | 193/193 (185 précédents + 8 First Trace) | `Validation/TRACE-V1-PlayMode.xml`, `Logs/v1-tests-final.log` |
| Scénario graphique | 1/1, cinq captures 1280x720 | `Validation/TRACE-V1-Visual.xml`, `Validation/FirstTrace-*.png` |

Aucun warning ni erreur C#. Les tests V1 couvrent : première scène de build et ouverture calme sans démarrage automatique ;
interaction terminal (prompt, usage unique, archive, porte, F pour continuer) ; file de dialogue (F et auto-avance) ;
badge optionnel sans effet sur la progression ; événement du hall (porte, lumière, écran bref, grincement) ; tension puis
arène (soin, cooldowns, départ de la rencontre, porte A seule ouverte) ; rencontre complète jusqu'à la conclusion et la fin ;
redémarrage. L'ambiance et le rythme ne sont pas testés automatiquement.

Ajustements pendant la validation : les répliques s'enchaînent dans une file (une nouvelle scène ne coupe pas l'échange en
cours), les tests la vident donc entre deux temps forts ; une première passe d'éclairage était trop sombre sur les captures,
les lampes, le soleil et les albédos ont été relevés et le pipeline actif passe en lumières per-pixel ; les textes muraux
utilisent un shader de police testé en profondeur, car le shader intégré se voyait à travers les murs (un essai en canvas UGUI
était flou de près, un essai en chiffres à 7 segments a été écarté au profit du rendu texte).

## Vérification manuelle

1. Les 30 premières secondes : ambiance lisible (lampe, flaques, vent) sans ennemi ; comprendre où aller sans HUD de navigation.
2. Regarder l'alcôve et la salle des chaises : l'envie de regarder autour existe-t-elle ?
3. Le terminal donne-t-il envie d'en savoir plus ? Les dialogues coupent-ils le rythme ? (durées dans l'Inspector)
4. L'événement du hall : curiosité sans jumpscare.
5. Le combat : les portes rendent-elles les arrivées lisibles ? Tactical Focus et Target Lock restent-ils utiles ?
6. La conclusion crée-t-elle une question ? Durée totale chronométrée.
7. Console sans erreur ; bruit généré acceptable comme placeholder (volumes sur `Ambience`).
