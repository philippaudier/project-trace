# TRACE — Support V0.1 (ORIGIN Field Specialist, intégration visuelle et structurelle)

Le membre 3 de la squad devient **SUPPORT**, spécialiste de terrain ORIGIN (protection, stabilisation, sauvegarde de
l'escouade). Elle n'est pas une Tracewalker : son Pulse Shield est une technologie de protection ORIGIN. Mécaniques
inchangées : même `PulseShield` (30 de bouclier pour tous pendant 5 s), même attaque à distance, même compagnon, même capsule.

## Référence

Planche « SUPPORT — ORIGIN Field Specialist (female) — Concept Design V2 » : cheveux longs châtain clair à rubans
menthe, manteau blanc cassé enveloppant et superposé, capuche, sous-tenue noire, module de torse, module dorsal
circulaire, staff à large anneau émetteur, marque ORIGIN discrète sur l'épaule. Palette : noir, anthracite, gris,
gris clair, menthe, gris-menthe pâle, jaune ORIGIN.

## Identité (`CharacterProfile`)

| Champ | Valeur |
| --- | --- |
| CharacterId | `origin_support_01` |
| DisplayName | `Support` |
| Designation | `Field Specialist — Support` |
| Affiliation | `ORIGIN` |
| Archetype | `Defensive Support` |
| IsTracewalker | `false` |
| AccentColor | menthe (0,45 ; 0,90 ; 0,78) |
| Portrait | `UI/Portraits/Portrait_Support.png`, recadrage 256 × 256 de la planche (remplace le placeholder, même GUID) |

## Différenciation des trois membres

| | Tracewalker | Control | Support |
| --- | --- | --- | --- |
| Stance | ±0,13 m | ±0,10 m | ±0,12 m |
| Manteau | pans courts symétriques | **asymétrique** long à gauche | **enveloppant** : deux pans longs, deux rabats avant à liseré menthe, dos long, **capuche** blanc cassé, manches blanc cassé |
| Cheveux | courts noirs | longs noirs, mèche cyan | longs **châtain clair** (`M_Support_Hair`), deux rubans menthe |
| Arme | lame courte | staff 1,9 m, anneau ⌀ 0,26 | staff 1,8 m tenu près du corps, **double anneau émetteur ⌀ 0,36** avec cœur menthe |
| Module | Trace Module ambre (torse) | Field Control Module cyan (dos, boîtier) | **Support Module** : anneau circulaire de douze segments et cœur menthe dans le dos |
| Accent | ambre | cyan froid | menthe |
| Mouvement | nerveux | posé | **calme** : amplitudes encore réduites, réponse 8 /s, frappe mesurée, pas de geste de Focus |

Tests : accent menthe (vert dominant) contre cyan (bleu dominant) et ambre (rouge dominant) ; pièces menthe plus
nombreuses que les pièces ambre ; cheveux d'une couleur propre ; deux pans de manteau ; anneau plus large que celui de
Control ; stance plus large que Control ; marionnette plus calme que Control.

## Support Module

Pivot `Support Module` dans le dos : boîtier anthracite, cœur sphérique menthe émissif (`Module Core`), anneau de
douze segments (neuf menthe émissifs, trois anthracite), bande ambre ORIGIN sous l'anneau. Le composant `SupportModule`
lit les `Shield` des trois membres : émission 0,5 au repos, 1,7 tant qu'au moins un bouclier est actif, réponse 0,25 s.
Aucune réaction au Tactical Focus. Lentille de torse et lentille de sacoche partagent le matériau émissif.

## Pulse Shield

Mécanique intacte. Les halos (deux anneaux par membre) passent au menthe (`M_Support_Shield`, unlit) et reçoivent un
`FieldVisualPulse` lent (±12 °/s en contre-rotation, pulsation ±10 % à 0,8 Hz) : une protection posée, distincte des
anneaux cyan rapides du Gravity Field.

## HUD

Rien à rebrancher : la carte squad et le panneau opérateur lisent `CharacterProfile` (nom SUPPORT, archétype DEFENSIVE
SUPPORT, accent menthe sur la barre et dans l'overlay tactique, portrait, état et recharge de Pulse Shield). Le profil
provisoire créé par `HudSetup` est remplacé par le profil définitif de `SupportSetup`.

## Matériaux (`Assets/TRACE/Scenes/Materials/Support/`)

| Matériau | Base | Notes |
| --- | --- | --- |
| `M_Support_Charcoal` | (0,12 ; 0,13 ; 0,14) | sous-tenue, boîtiers |
| `M_Support_Shadow` | (0,04 ; 0,04 ; 0,05) | bottes, gants, genouillères, barres ORIGIN |
| `M_Support_CoolGrey` | (0,52 ; 0,55 ; 0,58) | tête |
| `M_Support_OffWhite` | (0,88 ; 0,88 ; 0,86) | manteau, capuche, manches, plaques |
| `M_Support_Hair` | (0,52 ; 0,45 ; 0,37) | cheveux |
| `M_Support_Mint` | (0,50 ; 0,86 ; 0,76) | col, manchettes, liserés, rubans, lignes de bottes |
| `M_Support_OriginAmber` | ambre ORIGIN | marque, bande du module, bague du staff |
| `M_Support_Module` | (0,06 ; 0,13 ; 0,12) + émission menthe 0,5 | cœur et segments du module, lentilles, anneau du staff |
| `M_Support_Staff` | (0,20 ; 0,22 ; 0,24), métallique 0,5 | hampe, anneau intérieur |
| `M_Support_Shield` | unlit (0,50 ; 0,95 ; 0,82) | halos du Pulse Shield |

## Fichiers

Créés : `Characters/SupportModule.cs`, `Editor/SupportSetup.cs` (menu `TRACE/Apply Support V0.1`, idempotent),
`Tests/PlayMode/SupportTests.cs`, `Docs/Validation/SupportVisualCapture.cs`, dix matériaux.
Modifiés : `UI/Portraits/Portrait_Support.png` (recadrage de la planche), `Tests/PlayMode/HudTests.cs` et `Tests/PlayMode/ControlTests.cs` (profil et corps réels),
les trois scènes `Prototype`, `PrototypeEncounter`, `FirstTrace`.

## Validation

| Étape | Résultat | Preuve |
| --- | --- | --- |
| Suite complète | 222/222 (217 précédents + 5 Support) | `Validation/TRACE-Support-PlayMode.xml` |
| Captures | 1/1, cinq vues 1280x720 | `Validation/TRACE-Support-Visual.xml`, `Validation/Support-*.png` |

Tests Support : identité, module, staff, marque et matériaux dans les trois scènes ; différenciation palette,
silhouette et mouvement ; Pulse Shield menthe sur les trois membres avec capacité inchangée et module allumé ;
Tactical Focus sans réaction ; HUD (carte, portrait, panneau actif, accent dans l'overlay).

## Vérification manuelle

1. Rouvrir les scènes sans sauvegarder la version en mémoire (fichiers remplacés sur disque).
2. `Prototype`, touche 3 : silhouette et portrait de Support, E pour le Pulse Shield (halos menthe sur les trois, module dorsal allumé 5 s).
3. Les trois membres côte à côte : trois accents (ambre, cyan, menthe), trois armes, trois manteaux lisibles à distance.
4. Marcher et attaquer avec elle : gestes les plus calmes des trois (réglages dans le `CharacterPuppet` du membre 3).
5. Hors périmètre : les locuteurs de dialogue de la slice restent `SUPPORT` / `CONTROL` / `ASSAULT`.
