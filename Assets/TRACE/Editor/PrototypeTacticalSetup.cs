using System;
using System.Linq;
using TRACE.AI;
using TRACE.Input;
using TRACE.Tactical;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TRACE.Editor
{
    public static class PrototypeTacticalSetup
    {
        [MenuItem("TRACE/Upgrade Prototype to V0.8 Tactical Focus (once)")]
        public static void Create()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene("Assets/TRACE/Scenes/Prototype.unity");
            var squad = UnityEngine.Object.FindFirstObjectByType<SquadController>();
            if (squad.GetComponent<TacticalFocus>() != null)
                throw new InvalidOperationException("Tactical Focus already exists; refusing to overwrite tuning.");
            var focus = squad.gameObject.AddComponent<TacticalFocus>();
            References(focus, ("input", squad.GetComponent<TracePlayerInput>()), ("squad", squad));
            var camera = UnityEngine.Camera.main;
            camera.GetComponent<CinemachineBrain>().IgnoreTimeScale = true;

            var root = new GameObject("Tactical Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 0.5f;
            canvas.sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            var tint = Panel(root.transform, "Focus Tint", Vector2.zero, Vector2.zero, new Color(0.03f, 0.16f, 0.22f, 0.09f));
            tint.anchorMin = Vector2.zero; tint.anchorMax = Vector2.one; tint.offsetMin = tint.offsetMax = Vector2.zero;
            var header = Panel(root.transform, "Focus Header", new Vector2(640, 678), new Vector2(1248, 58), new Color(0.02f, 0.06f, 0.09f, 0.93f));
            Label(header, "TACTICAL FOCUS   /   TEMPS x0.15", 23);
            var hint = Panel(root.transform, "Controls", new Vector2(194, 604), new Vector2(356, 58), new Color(0.02f, 0.06f, 0.09f, 0.9f));
            Label(hint, "TAB maintenu : observer   |   1 / 2 / 3 : switch\nRelacher TAB pour agir", 16);
            var memberTexts = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                var panel = Panel(root.transform, "Member " + (i + 1), new Vector2(194, 516 - i * 108), new Vector2(356, 94), new Color(0.02f, 0.06f, 0.09f, 0.9f));
                memberTexts[i] = Label(panel, "", 18);
            }
            var enemies = UnityEngine.Object.FindObjectsByType<BasicMeleeEnemy>(FindObjectsSortMode.None).OrderBy(e => e.name).ToArray();
            var texts = new Text[enemies.Length];
            var panels = new RectTransform[enemies.Length];
            var gold = Material("Tactical Attack", new Color(1f, 0.55f, 0.08f));
            var cyan = Material("Tactical Target", new Color(0.25f, 0.85f, 1f));
            for (int i = 0; i < enemies.Length; i++)
            {
                panels[i] = Panel(root.transform, "Enemy " + (i + 1), Vector2.zero, new Vector2(210, 78), new Color(0.07f, 0.04f, 0.025f, 0.93f));
                texts[i] = Label(panels[i], "", 16);
                var telegraph = enemies[i].gameObject.AddComponent<TacticalTelegraph>();
                var visual = new GameObject("Tactical Telegraph");
                visual.transform.SetParent(enemies[i].transform, false);
                References(telegraph, ("focus", focus), ("visual", visual),
                    ("boundary", Line(visual.transform, "Hit Area", 4, true, gold, 0.055f)),
                    ("direction", Line(visual.transform, "Strike Direction", 5, false, gold, 0.075f)),
                    ("victimLine", Line(visual.transform, "Target", 2, false, cyan, 0.025f)));
                visual.SetActive(false);
            }
            var overlay = squad.gameObject.AddComponent<TacticalOverlay>();
            References(overlay, ("focus", focus), ("squad", squad), ("overlay", root), ("canvasRect", root.GetComponent<RectTransform>()));
            var data = new SerializedObject(overlay);
            Array(data, "memberTexts", memberTexts); Array(data, "enemies", enemies);
            Array(data, "enemyTexts", texts); Array(data, "enemyPanels", panels);
            data.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("TRACE V0.8: Tactical Focus authored with three member cards and four enemy telegraphs.");
        }

        private static void References(UnityEngine.Object target, params (string name, UnityEngine.Object value)[] values)
        {
            var data = new SerializedObject(target);
            foreach (var value in values) data.FindProperty(value.name).objectReferenceValue = value.value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Array(SerializedObject data, string name, UnityEngine.Object[] values)
        {
            var array = data.FindProperty(name); array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        private static RectTransform Panel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.sizeDelta = size; rect.anchoredPosition = position;
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            return rect;
        }
        private static Text Label(RectTransform parent, string value, int fontSize)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(10, 5); rect.offsetMax = new Vector2(-10, -5);
            var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize; text.text = value; text.color = new Color(0.9f, 0.96f, 1f);
            text.alignment = TextAnchor.MiddleLeft; text.supportRichText = true; text.raycastTarget = false;
            return text;
        }
        private static Material Material(string name, Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, "Assets/TRACE/Scenes/Materials/" + name + ".mat");
            return material;
        }
        private static LineRenderer Line(Transform parent, string name, int count, bool loop, Material material, float width)
        {
            var go = new GameObject(name); go.layer = 2; go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>(); line.useWorldSpace = true; line.loop = loop;
            line.positionCount = count; line.widthMultiplier = width; line.sharedMaterial = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
            return line;
        }
    }
}
