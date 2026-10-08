using System;
using System.Linq;
using TRACE.AI;
using TRACE.Characters;
using TRACE.Combat;
using TRACE.Encounter;
using TRACE.Narrative;
using TRACE.Skills;
using TRACE.Tactical;
using TRACE.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static TRACE.Editor.SquadVisualKit;

namespace TRACE.Editor
{
    // Builds the TRACE HUD V0.1 (ORIGIN field interface) in every playable scene: one screen-space canvas with
    // the active operator panel, squad status, mission, target, interaction prompt, reticle, threat view and the
    // Tactical Focus layer, all driven by HudRoot. Idempotent: re-running rebuilds the canvas.
    public static class HudSetup
    {
        private const string SpriteFolder = "Assets/TRACE/UI/Sprites/";
        private const string PortraitFolder = "Assets/TRACE/UI/Portraits/";
        private const string VolumeProfilePath = "Assets/TRACE/UI/HudFocusGrade.asset";
        private static readonly string[] Scenes =
        {
            "Assets/TRACE/Scenes/Prototype.unity",
            "Assets/TRACE/Scenes/PrototypeEncounter.unity",
            "Assets/TRACE/Scenes/FirstTrace.unity",
        };
        private static Sprite panelSprite, whiteSprite, reticleSprite;
        private static Font font;

        [MenuItem("TRACE/Apply HUD V0.1")]
        public static void Apply()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "HudPanel.png");
            whiteSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "HudWhite.png");
            reticleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + "HudReticle.png");
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (panelSprite == null || whiteSprite == null || reticleSprite == null)
                throw new InvalidOperationException("HUD sprites are missing under " + SpriteFolder);
            var profile = GradeProfile();
            int applied = 0;
            foreach (string path in Scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                var scene = EditorSceneManager.OpenScene(path);
                Build(scene, profile);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                applied++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"TRACE HUD V0.1 applied to {applied} scene(s).");
        }

        private static VolumeProfile GradeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            var adjustments = profile.Add<ColorAdjustments>(true);
            adjustments.saturation.Override(-42f);
            adjustments.colorFilter.Override(new Color(0.9f, 0.98f, 1f));
            adjustments.contrast.Override(6f);
            AssetDatabase.AddObjectToAsset(adjustments, profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static void Build(Scene scene, VolumeProfile gradeProfile)
        {
            var squad = UnityEngine.Object.FindFirstObjectByType<SquadController>();
            if (squad == null) throw new InvalidOperationException(scene.path + " has no squad.");
            var focus = squad.GetComponent<TacticalFocus>();
            var targeting = squad.GetComponent<TargetingSystem>();
            var encounter = UnityEngine.Object.FindFirstObjectByType<EncounterController>();
            var interaction = squad.GetComponent<InteractionController>();
            var story = UnityEngine.Object.FindFirstObjectByType<StoryDirector>();
            var threat = UnityEngine.Object.FindFirstObjectByType<ThreatIndicator>();
            var camera = UnityEngine.Camera.main;
            var enemies = UnityEngine.Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(e => e.name).ToArray();
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "HUD" || root.name == "HUD Focus Grade") UnityEngine.Object.DestroyImmediate(root);

            Portraits(squad);
            HandOverLegacyGui(squad, interaction, threat, encounter);
            RestyleTacticalOverlay(scene);
            var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData != null) cameraData.renderPostProcessing = true;
            var gradeObject = new GameObject("HUD Focus Grade");
            var volume = gradeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.weight = 0f;
            volume.sharedProfile = gradeProfile;

            var canvasObject = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            // Just past the near clip (0.1): a wall hugging the camera can no longer sit between the camera and the UI plane.
            canvas.planeDistance = 0.12f;
            canvas.sortingOrder = 90;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            var canvasRect = canvasObject.GetComponent<RectTransform>();
            var hud = canvasObject.AddComponent<HudRoot>();

            var panels = new System.Collections.Generic.List<HudPanel>
            {
                ActivePanel(canvasRect),
                SquadPanel(canvasRect),
                Mission(canvasRect, scene, encounter, story),
                Target(canvasRect),
                Reticle(canvasRect),
                FocusLayer(canvasRect, volume),
            };
            if (interaction != null) panels.Add(Prompt(canvasRect, interaction));
            if (threat != null) panels.Add(ThreatView(canvasRect, threat));

            var data = new SerializedObject(hud);
            data.FindProperty("squad").objectReferenceValue = squad;
            data.FindProperty("focus").objectReferenceValue = focus;
            data.FindProperty("targeting").objectReferenceValue = targeting;
            data.FindProperty("encounter").objectReferenceValue = encounter;
            Fill(data.FindProperty("enemies"), enemies);
            Fill(data.FindProperty("panels"), panels.ToArray());
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Portraits(SquadController squad)
        {
            var members = squad.Members;
            var roster = new SerializedObject(squad).FindProperty("members");
            var list = Enumerable.Range(0, roster.arraySize).Select(i => (SquadMember)roster.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
            string[] files = { "Portrait_Tracewalker.png", "Portrait_Control.png", "Portrait_Support.png" };
            for (int i = 0; i < list.Length && i < files.Length; i++)
            {
                var profile = list[i].GetComponent<CharacterProfile>();
                // Support has no design yet: a neutral placeholder profile keeps the slot functional.
                if (profile == null)
                    profile = Profile(list[i].gameObject, "support_placeholder", "Support", "Field Support — placeholder", "ORIGIN", "Support", false, new Color(0.45f, 0.7f, 1f));
                Reference(profile, "portrait", AssetDatabase.LoadAssetAtPath<Sprite>(PortraitFolder + files[i]));
            }
        }

        private static void HandOverLegacyGui(SquadController squad, InteractionController interaction, ThreatIndicator threat, EncounterController encounter)
        {
            var skillHud = squad.GetComponent<SkillHud>();
            if (skillHud != null) Flag(skillHud, "m_Enabled", false);
            if (interaction != null) Flag(interaction, "legacyPrompt", false);
            if (threat != null) Flag(threat, "legacyGui", false);
            var encounterHud = UnityEngine.Object.FindFirstObjectByType<EncounterHud>();
            if (encounterHud != null) Flag(encounterHud, "showWaveBox", false);
        }

        // The existing focus overlay keeps its enemy cards; its header moves to the top-left and its member
        // panels disappear (the HUD squad panel carries the cooldowns now). Colours join the HUD palette.
        private static void RestyleTacticalOverlay(Scene scene)
        {
            var overlay = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Tactical Overlay");
            if (overlay == null) return;
            var overlayCanvas = overlay.GetComponent<Canvas>();
            if (overlayCanvas != null) overlayCanvas.planeDistance = 0.12f;
            foreach (Transform child in overlay.transform)
            {
                var image = child.GetComponent<Image>();
                var rect = child.GetComponent<RectTransform>();
                if (child.name == "Focus Header")
                {
                    rect.anchoredPosition = new Vector2(194f, 678f); rect.sizeDelta = new Vector2(356f, 58f);
                    Style(image, HudPanel.Charcoal);
                    var label = child.GetComponentInChildren<Text>(); if (label != null) { label.fontSize = 17; label.text = $"<b>TACTICAL FOCUS</b>\n<color=#{ColorUtility.ToHtmlStringRGB(HudPanel.Cyan)}>ANALYSIS MODE   ·   TEMPS x0.15</color>"; }
                }
                else if (child.name == "Controls") Style(image, HudPanel.Charcoal);
                else if (child.name.StartsWith("Member ")) child.gameObject.SetActive(false);
                else if (child.name.StartsWith("Enemy ")) Style(image, new Color(0.1f, 0.075f, 0.06f, 0.9f));
            }
        }

        private static void Style(Image image, Color color)
        {
            if (image == null) return;
            image.sprite = panelSprite; image.type = Image.Type.Sliced; image.color = color;
        }

        // ---- Panels -------------------------------------------------------------------------------------------

        private static HudPanel ActivePanel(RectTransform canvas)
        {
            var panel = Panel("Active Operator Panel", canvas, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 16f), new Vector2(372f, 112f), out var accent);
            var frame = Picture("Portrait Frame", panel, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 12f), new Vector2(88f, 88f), whiteSprite, HudPanel.Amber);
            var portrait = Picture("Portrait", panel, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(12f, 14f), new Vector2(84f, 84f), null, Color.white);
            portrait.preserveAspect = true;
            var name = Label("Name", panel, new Vector2(108f, -10f), new Vector2(190f, 22f), "", 18, HudPanel.OffWhite, FontStyle.Bold);
            var role = Label("Role", panel, new Vector2(108f, -32f), new Vector2(190f, 16f), "", 11, HudPanel.Muted);
            var hpText = Label("HP Text", panel, new Vector2(-12f, -10f), new Vector2(80f, 22f), "", 14, HudPanel.OffWhite, FontStyle.Normal, TextAnchor.UpperRight, new Vector2(1f, 1f));
            var state = Label("State", panel, new Vector2(-12f, -32f), new Vector2(90f, 16f), "", 11, HudPanel.Muted, FontStyle.Bold, TextAnchor.UpperRight, new Vector2(1f, 1f));
            Picture("HP Back", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(108f, -50f), new Vector2(248f, 7f), whiteSprite, new Color(1f, 1f, 1f, 0.12f), new Vector2(0f, 1f));
            var hpFill = Bar("HP Fill", panel, new Vector2(108f, -50f), new Vector2(248f, 7f), HudPanel.OffWhite);
            var skill = Label("Skill", panel, new Vector2(108f, -62f), new Vector2(248f, 16f), "", 12, HudPanel.OffWhite);
            Picture("Skill Back", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(108f, -79f), new Vector2(248f, 4f), whiteSprite, new Color(1f, 1f, 1f, 0.12f), new Vector2(0f, 1f));
            var skillFill = Bar("Skill Fill", panel, new Vector2(108f, -79f), new Vector2(248f, 4f), HudPanel.Cyan);
            var ultimate = Label("Ultimate", panel, new Vector2(108f, -88f), new Vector2(120f, 14f), "", 10, HudPanel.Muted);
            var dodge = Label("Dodge", panel, new Vector2(236f, -88f), new Vector2(120f, 14f), "", 10, HudPanel.Muted);
            var highlight = Picture("Switch Highlight", panel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, panelSprite, new Color(1f, 1f, 1f, 0f));
            highlight.type = Image.Type.Sliced;
            var component = panel.gameObject.AddComponent<ActiveOperatorPanel>();
            var data = new SerializedObject(component);
            data.FindProperty("portrait").objectReferenceValue = portrait;
            data.FindProperty("accentBar").objectReferenceValue = accent;
            data.FindProperty("portraitFrame").objectReferenceValue = frame;
            data.FindProperty("nameText").objectReferenceValue = name;
            data.FindProperty("roleText").objectReferenceValue = role;
            data.FindProperty("hpText").objectReferenceValue = hpText;
            data.FindProperty("hpFill").objectReferenceValue = hpFill;
            data.FindProperty("skillText").objectReferenceValue = skill;
            data.FindProperty("skillFill").objectReferenceValue = skillFill;
            data.FindProperty("ultimateText").objectReferenceValue = ultimate;
            data.FindProperty("dodgeText").objectReferenceValue = dodge;
            data.FindProperty("stateText").objectReferenceValue = state;
            data.FindProperty("switchHighlight").objectReferenceValue = highlight;
            data.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        private static HudPanel SquadPanel(RectTransform canvas)
        {
            var panel = Panel("Squad Status Panel", canvas, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 16f), new Vector2(300f, 112f), out _);
            Label("Header", panel, new Vector2(12f, -6f), new Vector2(120f, 14f), "SQUAD", 10, HudPanel.Muted, FontStyle.Bold);
            var component = panel.gameObject.AddComponent<SquadStatusPanel>();
            var data = new SerializedObject(component);
            var cards = data.FindProperty("cards");
            cards.arraySize = 2;
            for (int i = 0; i < 2; i++)
            {
                var card = Rect("Card " + (i + 1), panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -22f - i * 44f), new Vector2(300f, 42f), new Vector2(0f, 1f));
                var accent = Picture("Accent", card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, -4f), new Vector2(3f, 34f), whiteSprite, HudPanel.Amber, new Vector2(0f, 1f));
                var portrait = Picture("Portrait", card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -4f), new Vector2(34f, 34f), null, Color.white, new Vector2(0f, 1f));
                portrait.preserveAspect = true;
                var name = Label("Name", card, new Vector2(54f, -4f), new Vector2(170f, 16f), "", 13, HudPanel.OffWhite, FontStyle.Bold);
                Picture("HP Back", card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(54f, -22f), new Vector2(150f, 5f), whiteSprite, new Color(1f, 1f, 1f, 0.12f), new Vector2(0f, 1f));
                var hpFill = Bar("HP Fill", card, new Vector2(54f, -22f), new Vector2(150f, 5f), HudPanel.OffWhite);
                var skill = Label("Skill", card, new Vector2(54f, -29f), new Vector2(200f, 13f), "", 10, HudPanel.Muted);
                var status = Label("Status", card, new Vector2(-10f, -4f), new Vector2(120f, 14f), "", 11, HudPanel.Muted, FontStyle.Normal, TextAnchor.UpperRight, new Vector2(1f, 1f));
                var combo = Picture("Combo Tag", card, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -24f), new Vector2(8f, 8f), whiteSprite, HudPanel.Amber, new Vector2(1f, 1f));
                combo.enabled = false;
                var entry = cards.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("root").objectReferenceValue = card;
                entry.FindPropertyRelative("portrait").objectReferenceValue = portrait;
                entry.FindPropertyRelative("accent").objectReferenceValue = accent;
                entry.FindPropertyRelative("nameText").objectReferenceValue = name;
                entry.FindPropertyRelative("hpFill").objectReferenceValue = hpFill;
                entry.FindPropertyRelative("skillText").objectReferenceValue = skill;
                entry.FindPropertyRelative("statusText").objectReferenceValue = status;
                entry.FindPropertyRelative("comboTag").objectReferenceValue = combo;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        private static HudPanel Mission(RectTransform canvas, Scene scene, EncounterController encounter, StoryDirector story)
        {
            var panel = Panel("Mission Panel", canvas, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(320f, 76f), out _);
            Label("Header", panel, new Vector2(12f, -6f), new Vector2(200f, 14f), "//  MAIN OBJECTIVE", 10, HudPanel.Amber, FontStyle.Bold);
            var objective = Label("Objective", panel, new Vector2(12f, -22f), new Vector2(296f, 20f), "", 15, HudPanel.OffWhite);
            var detail = Label("Detail", panel, new Vector2(12f, -43f), new Vector2(296f, 15f), "", 11, HudPanel.Cyan);
            var sector = Label("Sector", panel, new Vector2(12f, -59f), new Vector2(296f, 14f), "", 10, HudPanel.Muted);
            var component = panel.gameObject.AddComponent<MissionPanel>();
            var data = new SerializedObject(component);
            data.FindProperty("sectorText").objectReferenceValue = sector;
            data.FindProperty("objectiveText").objectReferenceValue = objective;
            data.FindProperty("detailText").objectReferenceValue = detail;
            data.FindProperty("encounter").objectReferenceValue = encounter;
            data.FindProperty("story").objectReferenceValue = story;
            bool slice = scene.path.EndsWith("FirstTrace.unity");
            bool arena = scene.path.EndsWith("PrototypeEncounter.unity");
            data.FindProperty("sector").stringValue = slice ? "First Trace" : arena ? "Arene de rencontre" : "Zone d'entrainement";
            data.FindProperty("objective").stringValue = slice ? "Explorer le site" : arena ? "Repousser les trois vagues" : "Tester les systemes de combat";
            data.FindProperty("detail").stringValue = arena ? "" : slice ? "" : "Squad au complet";
            if (slice)
            {
                Strings(data.FindProperty("storyObjectives"), "Rejoindre le terminal d'archives", "Lire l'archive, passer la porte 143",
                    "Traverser le hall", "Rester groupes, avancer", "Repousser les vagues", "Consulter le terminal B", "Fin de First Trace");
                Strings(data.FindProperty("storySectors"), "Quai de chargement", "Couloir / Terminal A", "Hall 143", "Couloir d'alerte", "Arene", "Terminal B", "First Trace");
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        private static HudPanel Target(RectTransform canvas)
        {
            var panel = Panel("Target Panel", canvas, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(360f, 62f), out var accent);
            accent.color = HudPanel.Danger;
            var frame = Picture("Frame", panel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, panelSprite, new Color(0.62f, 0.65f, 0.69f, 0.35f));
            frame.type = Image.Type.Sliced; frame.fillCenter = false; frame.pixelsPerUnitMultiplier = 6f;
            var tag = Label("Tag", panel, new Vector2(12f, -7f), new Vector2(60f, 14f), "", 10, HudPanel.Muted, FontStyle.Bold);
            var name = Label("Name", panel, new Vector2(60f, -5f), new Vector2(200f, 18f), "", 15, HudPanel.OffWhite, FontStyle.Bold);
            var hpText = Label("HP Text", panel, new Vector2(-12f, -6f), new Vector2(90f, 16f), "", 12, HudPanel.OffWhite, FontStyle.Normal, TextAnchor.UpperRight, new Vector2(1f, 1f));
            Picture("HP Back", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -28f), new Vector2(336f, 6f), whiteSprite, new Color(1f, 1f, 1f, 0.12f), new Vector2(0f, 1f));
            var hpFill = Bar("HP Fill", panel, new Vector2(12f, -28f), new Vector2(336f, 6f), HudPanel.Danger);
            var status = Label("Status", panel, new Vector2(12f, -40f), new Vector2(200f, 14f), "", 10, HudPanel.Muted);
            var combo = Label("Combo", panel, new Vector2(-12f, -40f), new Vector2(220f, 14f), "", 10, HudPanel.Amber, FontStyle.Bold, TextAnchor.UpperRight, new Vector2(1f, 1f));
            var component = panel.gameObject.AddComponent<TargetPanel>();
            var data = new SerializedObject(component);
            data.FindProperty("frame").objectReferenceValue = frame;
            data.FindProperty("nameText").objectReferenceValue = name;
            data.FindProperty("tagText").objectReferenceValue = tag;
            data.FindProperty("hpText").objectReferenceValue = hpText;
            data.FindProperty("hpFill").objectReferenceValue = hpFill;
            data.FindProperty("statusText").objectReferenceValue = status;
            data.FindProperty("comboText").objectReferenceValue = combo;
            data.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        private static HudPanel Prompt(RectTransform canvas, InteractionController interaction)
        {
            var panel = Panel("Interaction Prompt", canvas, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(260f, 44f), out _);
            var keyBox = Picture("Key Box", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -7f), new Vector2(30f, 30f), panelSprite, HudPanel.Amber, new Vector2(0f, 1f));
            keyBox.type = Image.Type.Sliced; keyBox.pixelsPerUnitMultiplier = 3f;
            var key = Label("Key", keyBox.rectTransform, Vector2.zero, new Vector2(30f, 30f), "F", 16, new Color(0.07f, 0.075f, 0.085f), FontStyle.Bold, TextAnchor.MiddleCenter);
            var prompt = Label("Prompt", panel, new Vector2(50f, -5f), new Vector2(200f, 36f), "", 12, HudPanel.OffWhite, FontStyle.Bold);
            var component = panel.gameObject.AddComponent<InteractionPrompt>();
            var data = new SerializedObject(component);
            data.FindProperty("interaction").objectReferenceValue = interaction;
            data.FindProperty("keyText").objectReferenceValue = key;
            data.FindProperty("promptText").objectReferenceValue = prompt;
            data.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        private static HudPanel Reticle(RectTransform canvas)
        {
            var rect = Rect("Reticle", canvas, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26f, 26f), new Vector2(0.5f, 0.5f));
            rect.gameObject.AddComponent<CanvasGroup>();
            var image = Picture("Ring", rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, reticleSprite, HudPanel.OffWhite);
            var component = rect.gameObject.AddComponent<ReticleController>();
            Reference(component, "reticle", image);
            return component;
        }

        private static HudPanel ThreatView(RectTransform canvas, ThreatIndicator threat)
        {
            var rect = Rect("Threat Indicators", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            rect.gameObject.AddComponent<CanvasGroup>();
            var markers = new RectTransform[4];
            var labels = new Text[4];
            for (int i = 0; i < 4; i++)
            {
                markers[i] = Rect("Threat " + (i + 1), rect, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(220f, 30f), new Vector2(0.5f, 0.5f));
                var back = Picture("Back", markers[i], Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, panelSprite, HudPanel.Charcoal);
                back.type = Image.Type.Sliced;
                Picture("Accent", markers[i], new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(3f, 0f), whiteSprite, HudPanel.Danger, new Vector2(0f, 0.5f));
                labels[i] = Label("Label", markers[i], Vector2.zero, new Vector2(220f, 30f), "", 13, HudPanel.Danger, FontStyle.Bold, TextAnchor.MiddleCenter);
                markers[i].gameObject.SetActive(false);
            }
            var component = rect.gameObject.AddComponent<ThreatIndicatorView>();
            var data = new SerializedObject(component);
            data.FindProperty("source").objectReferenceValue = threat;
            data.FindProperty("canvasRect").objectReferenceValue = canvas;
            Fill(data.FindProperty("markers"), markers);
            Fill(data.FindProperty("labels"), labels);
            data.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        private static HudPanel FocusLayer(RectTransform canvas, Volume volume)
        {
            var rect = Rect("Tactical Focus Layer", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            rect.gameObject.AddComponent<CanvasGroup>();
            var brackets = new RectTransform[4];
            Vector2[] corners = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            for (int i = 0; i < 4; i++)
            {
                Vector2 corner = corners[i];
                Vector2 inward = new Vector2(corner.x < 0.5f ? 1f : -1f, corner.y < 0.5f ? 1f : -1f);
                brackets[i] = Rect("Bracket " + (i + 1), rect, corner, corner, new Vector2(inward.x * 28f, inward.y * 28f), new Vector2(40f, 40f), corner);
                Picture("Horizontal", brackets[i], corner, corner, Vector2.zero, new Vector2(40f, 2f), whiteSprite, HudPanel.Cyan, corner);
                Picture("Vertical", brackets[i], corner, corner, Vector2.zero, new Vector2(2f, 40f), whiteSprite, HudPanel.Cyan, corner);
            }
            Picture("Top Line", rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(520f, 1f), whiteSprite, new Color(0.369f, 0.839f, 1f, 0.35f), new Vector2(0.5f, 1f));
            var analysisPanel = Panel("Analysis", rect, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 140f), new Vector2(372f, 96f), out var accent);
            accent.color = HudPanel.Cyan;
            UnityEngine.Object.DestroyImmediate(analysisPanel.GetComponent<CanvasGroup>());
            var analysis = Label("Analysis Text", analysisPanel, new Vector2(12f, -6f), new Vector2(352f, 86f), "", 11, HudPanel.OffWhite);
            analysis.lineSpacing = 1.15f;
            var component = rect.gameObject.AddComponent<TacticalFocusOverlay>();
            var data = new SerializedObject(component);
            data.FindProperty("gradeVolume").objectReferenceValue = volume;
            data.FindProperty("analysisText").objectReferenceValue = analysis;
            Fill(data.FindProperty("brackets"), brackets);
            data.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        // ---- UI primitives ------------------------------------------------------------------------------------

        private static RectTransform Rect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = pivot;
            if (anchorMin == anchorMax) { rect.sizeDelta = size; rect.anchoredPosition = position; }
            else { rect.offsetMin = new Vector2(position.x, position.y); rect.offsetMax = new Vector2(-position.x + size.x, -position.y + size.y); }
            return rect;
        }

        private static Image Picture(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Sprite sprite, Color color, Vector2? pivot = null)
        {
            var rect = Rect(name, parent, anchorMin, anchorMax, position, size, pivot ?? new Vector2(0f, 0f));
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite; image.color = color; image.raycastTarget = false;
            return image;
        }

        private static Image Bar(string name, RectTransform parent, Vector2 position, Vector2 size, Color color)
        {
            var image = Picture(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size, whiteSprite, color, new Vector2(0f, 1f));
            image.type = Image.Type.Filled; image.fillMethod = Image.FillMethod.Horizontal; image.fillOrigin = 0; image.fillAmount = 1f;
            return image;
        }

        private static Text Label(string name, RectTransform parent, Vector2 position, Vector2 size, string value, int fontSize, Color color,
            FontStyle style = FontStyle.Normal, TextAnchor alignment = TextAnchor.UpperLeft, Vector2? pivot = null)
        {
            Vector2 anchor = pivot ?? new Vector2(0f, 1f);
            var rect = Rect(name, parent, anchor, anchor, position, size, anchor);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = fontSize; text.text = value; text.color = color; text.fontStyle = style;
            text.alignment = alignment; text.supportRichText = true; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        // Charcoal sliced panel with a CanvasGroup and a left ORIGIN accent bar.
        private static RectTransform Panel(string name, RectTransform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, out Image accent)
        {
            var rect = Rect(name, parent, anchor, anchor, position, size, pivot);
            var back = rect.gameObject.AddComponent<Image>();
            back.sprite = panelSprite; back.type = Image.Type.Sliced; back.color = HudPanel.Charcoal; back.raycastTarget = false;
            rect.gameObject.AddComponent<CanvasGroup>();
            accent = Picture("Accent Bar", rect, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 6f), new Vector2(3f, -6f), whiteSprite, HudPanel.Amber, new Vector2(0f, 0.5f));
            return rect;
        }

        private static void Fill(SerializedProperty array, UnityEngine.Object[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void Strings(SerializedProperty array, params string[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).stringValue = values[i];
        }
    }
}
