using System;
using System.Collections.Generic;
using System.Linq;
using TRACE.Encounter;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace TRACE.Editor
{
    // FieldTest Visual Pass B: art direction on top of the Pass A greybox, FieldTest only. A cold, damp, mineral ORIGIN
    // facility partly reclaimed by nature: textured world-space materials (ambientCG CC0), static wetness, a few URP
    // decals, light vegetation, still water and puddles, overcast lighting with warm and emergency accents, a light
    // global grade and three local volumes, dust and mist, landmarks per zone. Gameplay and navigation are unchanged:
    // the few new solid structures are built before the NavMesh bake and checked by the same landmark test.
    public static partial class FieldTestSetup
    {
        public const string ArtFolder = "Assets/TRACE/Art/FieldTest";
        private const string TextureFolder = ArtFolder + "/Textures";
        private const string DecalFolder = ArtFolder + "/Decals";
        public const string FieldMaterialFolder = ArtFolder + "/Materials";
        private const string MeshFolder = ArtFolder + "/Meshes";
        public const string PrefabFolder = "Assets/TRACE/Prefabs/FieldTest";
        private const string VolumeFolder = "Assets/TRACE/Scenes/Volumes";
        private static readonly string[] VisualRendererAssets = { "Assets/Settings/PC_Renderer.asset", "Assets/Settings/Mobile_Renderer.asset" };
        private static readonly Dictionary<string, Material> FieldMaterials = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Material> DecalMaterials = new Dictionary<string, Material>();
        private static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();

        // Static wetness (documented in FieldTest-V0.1.md): wet surfaces get darker and smoother, never mirror-like.
        public const float WetSmoothness = 0.62f;
        public const float WetDarken = 0.85f;
        public const float WetNormalFlatten = 0.4f;
        public const float MaxSurfaceSmoothness = 0.7f;

        // Solid art structures with colliders, before the NavMesh bake.
        public static readonly Vector3 BasinCenter = new Vector3(92f, 0f, 71f);
        public static readonly Vector2 BasinSize = new Vector2(12f, 10f);

        private static void BuildVisualStructures()
        {
            ConfigureTextures();
            BuildFieldMaterials();
            BuildPrefabs();
            BuildGantryCrane();
            BuildBasin();
            var frames = Child(props, "Door Frames");
            // The ORIGIN hangar door: the frame sits outside the 16 x 10 m opening, it does not narrow it.
            PlacePrefab("PF_DoorFrame", frames, "Hangar Door Frame", new Vector3(0f, 0f, 7.8f), Quaternion.identity, new Vector3(4f, 10f / 3f, 1.2f));
            PlacePrefab("PF_DoorFrame", frames, "Trace Hut Door Frame", new Vector3(60f, 0f, 99.8f), Quaternion.identity, new Vector3(0.5f, 2.6f / 3f, 0.6f));
            var pit = Child(props, "Service Pit");
            Part("Pit Generator", pit, new Vector3(-63f, -2.4f, 79f), new Vector3(3f, 2.2f, 2f), "Metal_Painted", collider: true);
            Part("Pit Generator Base", pit, new Vector3(-63f, -3.42f, 79f), new Vector3(3.4f, 0.16f, 2.4f), "Metal_Dark", collider: true);
            var posts = Child(props, "Lamp Posts");
            LampPost(posts, "Lamp Post Yard West", new Vector3(-34.6f, 0f, -58f), 90f);
            LampPost(posts, "Lamp Post Yard East", new Vector3(34.6f, 0f, -30f), -90f);
            LampPost(posts, "Lamp Post Open Exterior", new Vector3(48f, 0f, 18f), 180f);
            BuildTrees();
        }

        // After the bake: everything without colliders, lighting, volumes, then the material swap.
        private static void DressVisualPassB(UnityEngine.Camera camera)
        {
            EnsureDecalFeature();
            BuildGroundDressing();
            BuildLandmarkDetails();
            BuildLightFixtures();
            BuildAtmosphereLighting(camera);
            BuildVolumes();
            BuildParticles();
            BuildVegetation();
            BuildWater();
            BuildDecals();
            BuildAudioPlaceholders();
            ApplyFieldMaterials();
        }

        // ------------------------------------------------------------------ textures

        private static void ConfigureTextures()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder, DecalFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                string file = System.IO.Path.GetFileNameWithoutExtension(path);
                var settings = (type: TextureImporterType.Default, srgb: true, alpha: false, wrap: TextureWrapMode.Repeat, shape: TextureImporterShape.Texture2D, size: 1024);
                if (file.EndsWith("_Normal") || file == "FX_WaterNormal") settings.type = TextureImporterType.NormalMap;
                if (file.EndsWith("_Mask") || file == "FX_MacroNoise" || file == "FX_PuddleMask") settings.srgb = false;
                if (file.StartsWith("DEC_") || file == "FX_Dust" || file.EndsWith("_Albedo") && path.EndsWith(".png")) settings.alpha = true;
                if (file.StartsWith("DEC_") && file != "DEC_Hazard" || file == "FX_PuddleMask" || file == "FX_Dust") settings.wrap = TextureWrapMode.Clamp;
                if (file == "FX_EnvCube") { settings.shape = TextureImporterShape.TextureCube; settings.wrap = TextureWrapMode.Clamp; settings.size = 128; }
                if (file.StartsWith("FX_") && file != "FX_EnvCube") settings.size = 512;
                bool changed = importer.textureType != settings.type || importer.sRGBTexture != settings.srgb || importer.alphaIsTransparency != settings.alpha
                    || importer.wrapMode != settings.wrap || importer.textureShape != settings.shape || importer.maxTextureSize != settings.size;
                if (!changed) continue;
                importer.textureType = settings.type;
                importer.sRGBTexture = settings.srgb;
                importer.alphaIsTransparency = settings.alpha;
                importer.alphaSource = settings.alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                importer.wrapMode = settings.wrap;
                importer.textureShape = settings.shape;
                if (settings.shape == TextureImporterShape.TextureCube)
                {
                    var cube = new TextureImporterSettings();
                    importer.ReadTextureSettings(cube);
                    cube.cubemapConvolution = TextureImporterCubemapConvolution.Specular;
                    cube.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
                    importer.SetTextureSettings(cube);
                }
                // Foliage cards keep their coverage in the mip chain (alpha clip at 0.45).
                importer.mipMapsPreserveCoverage = file.EndsWith("_Albedo") && path.EndsWith(".png");
                importer.alphaTestReferenceValue = 0.45f;
                importer.maxTextureSize = settings.size;
                importer.SaveAndReimport();
            }
        }

        private static Texture Tex(string name)
        {
            foreach (string folder in new[] { TextureFolder, DecalFolder })
                foreach (string ext in new[] { ".jpg", ".png" })
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture>($"{folder}/{name}{ext}");
                    if (texture != null) return texture;
                }
            throw new InvalidOperationException("Missing FieldTest texture " + name);
        }

        // ------------------------------------------------------------------ materials

        private static void BuildFieldMaterials()
        {
            FieldMaterials.Clear();
            SquadVisualKit.EnsureFolder(FieldMaterialFolder);
            var surface = Find("TRACE/FieldSurface");
            var rock = Find("TRACE/FieldRock");
            // Concrete: dry walls darken in a band above the ground; wet in the pit, the ravine and the Trace hut.
            Surface("Concrete_Dry", surface, "Concrete031", new Color(0.95f, 0.95f, 0.93f), 0.4f, 0.06f, 0.26f, damp: 0.8f, patchiness: 0.6f);
            Surface("Concrete_Wet", surface, "Concrete031", new Color(0.9f, 0.92f, 0.92f), 0.4f, 0.06f, 0.26f, wetness: 0.7f, damp: 1.5f, patchiness: 0.45f);
            Surface("Concrete_Floor", surface, "Concrete042A", new Color(0.95f, 0.95f, 0.94f), 0.35f, 0.08f, 0.3f, wetness: 0.12f, patchiness: 0.9f);
            Surface("Concrete_Floor_Wet", surface, "Concrete042A", new Color(0.9f, 0.92f, 0.92f), 0.35f, 0.08f, 0.3f, wetness: 0.75f, patchiness: 0.4f);
            Surface("Yard", surface, "Concrete042A", new Color(0.93f, 0.93f, 0.92f), 0.3f, 0.06f, 0.26f, wetness: 0.2f, patchiness: 0.85f,
                layer: "Ground106", layerAmount: 0.3f, layerTint: new Color(0.85f, 0.8f, 0.75f), layerTiling: 0.35f);
            Surface("Metal_Dark", surface, "Metal027", new Color(0.85f, 0.87f, 0.9f), 0.5f, 0.22f, 0.48f, damp: 0.4f);
            Surface("Metal_Painted", surface, "PaintedMetal012", new Color(0.8f, 0.8f, 0.78f), 2f, 0.15f, 0.38f, damp: 0.6f);
            Surface("Metal_Panel", surface, "PaintedMetal012", new Color(0.42f, 0.45f, 0.48f), 2f, 0.15f, 0.38f, damp: 0.6f);
            Surface("Metal_Rust", surface, "Metal022", new Color(0.9f, 0.9f, 0.9f), 0.8f, 0.05f, 0.3f, damp: 0.6f);
            Surface("Hazard", surface, "PaintedMetal012", new Color(1.0f, 0.68f, 0.16f), 2f, 0.15f, 0.35f, damp: 0.4f);
            Surface("Rock", rock, "Rock058", new Color(0.95f, 0.97f, 1f), 0.2f, 0.05f, 0.3f, damp: 2f, macro: 0.25f, macroScale: 0.03f,
                layer: "Ground037", layerAmount: 0.35f, layerUp: 1f, layerTint: new Color(0.42f, 0.48f, 0.36f), layerTiling: 0.25f);
            Surface("Rock_Wet", rock, "Rock058", new Color(0.9f, 0.93f, 0.96f), 0.2f, 0.05f, 0.3f, wetness: 0.25f, damp: 4f, patchiness: 0.6f, macro: 0.25f, macroScale: 0.03f,
                layer: "Ground037", layerAmount: 0.45f, layerUp: 1f, layerTint: new Color(0.4f, 0.47f, 0.35f), layerTiling: 0.25f);
            Surface("Ground", surface, "Ground036", new Color(0.9f, 0.88f, 0.85f), 0.2f, 0.04f, 0.22f, wetness: 0.08f, patchiness: 0.9f, macro: 0.18f, macroScale: 0.025f,
                layer: "Gravel043", layerAmount: 0.35f, layerTiling: 0.3f);
            Surface("Ground_Moss", surface, "Ground037", new Color(0.5f, 0.55f, 0.43f), 0.2f, 0.04f, 0.22f, wetness: 0.2f, patchiness: 0.8f, macro: 0.18f,
                layer: "Ground106", layerAmount: 0.3f, layerTint: new Color(0.8f, 0.76f, 0.7f), layerTiling: 0.3f);
            Surface("Gravel", surface, "Gravel043", new Color(0.9f, 0.9f, 0.9f), 0.35f, 0.05f, 0.25f, wetness: 0.1f, patchiness: 0.9f);
            Surface("Mud", surface, "Ground106", new Color(0.8f, 0.75f, 0.7f), 0.3f, 0.05f, 0.25f, wetness: 0.55f, patchiness: 0.5f);
            Surface("Bark", rock, "Rock058", new Color(0.5f, 0.42f, 0.34f), 0.9f, 0.05f, 0.2f, macro: 0.1f);

            Lit("Glass", new Color(0.06f, 0.08f, 0.09f, 0.82f), 0.9f, 0f, Color.black, transparent: true);
            Lit("Emissive_ORIGIN", new Color(0.9f, 0.9f, 0.86f), 0.5f, 0f, new Color(1f, 0.9f, 0.72f) * 3f);
            Lit("Emissive_Off", new Color(0.32f, 0.33f, 0.34f), 0.5f, 0f, Color.black);
            Lit("Emergency", new Color(0.4f, 0.05f, 0.04f), 0.5f, 0f, new Color(1f, 0.12f, 0.08f) * 2.5f);
            Lit("Beacon", new Color(0.5f, 0.3f, 0.08f), 0.5f, 0f, new Color(1f, 0.55f, 0.12f) * 4f);
            Lit("Screen", new Color(0.05f, 0.12f, 0.12f), 0.6f, 0f, new Color(0.25f, 0.75f, 0.7f));
            // Cyan is for active technology and the Trace only.
            Lit("Tech", new Color(0.05f, 0.1f, 0.11f), 0.5f, 0f, new Color(0.3f, 0.85f, 0.9f) * 1.5f);
            Lit("Trace_Seam", new Color(0.02f, 0.04f, 0.05f), 0.5f, 0f, new Color(0.3f, 0.8f, 1f) * 0.6f);

            var vegetation = Find("TRACE/FieldVegetation");
            Vegetation("Vegetation_Grass", vegetation, "Foliage001_Albedo", new Color(0.72f, 0.78f, 0.62f), 0.45f, 0.07f, 1.3f);
            Vegetation("Vegetation_Leaves", vegetation, "LeafSet024_Albedo", new Color(0.62f, 0.7f, 0.55f), 0.4f, 0.05f, 1f);

            var water = Find("TRACE/FieldWater");
            var basin = LoadOrCreate("M_Field_Water_Basin", water);
            basin.SetTexture("_NormalMap", Tex("FX_WaterNormal"));
            basin.SetFloat("_NormalScale", 0.35f); basin.SetFloat("_NormalStrength", 0.3f); basin.SetFloat("_FlowSpeed", 0.3f);
            basin.SetColor("_ShallowColor", new Color(0.16f, 0.2f, 0.2f)); basin.SetColor("_DeepColor", new Color(0.03f, 0.06f, 0.07f));
            basin.SetFloat("_DepthMax", 1.2f); basin.SetFloat("_AlphaMin", 0.35f); basin.SetFloat("_AlphaDepth", 0.8f);
            basin.SetFloat("_Smoothness", 0.92f); basin.SetFloat("_ReflectionStrength", 0.7f); basin.SetFloat("_SpecularStrength", 0.6f);
            basin.SetTexture("_ShapeMap", null);
            Store("Water_Basin", basin);
            var puddle = LoadOrCreate("M_Field_Water_Puddle", water);
            puddle.SetTexture("_NormalMap", Tex("FX_WaterNormal"));
            puddle.SetFloat("_NormalScale", 0.5f); puddle.SetFloat("_NormalStrength", 0.12f); puddle.SetFloat("_FlowSpeed", 0.08f);
            puddle.SetColor("_ShallowColor", new Color(0.1f, 0.11f, 0.11f)); puddle.SetColor("_DeepColor", new Color(0.05f, 0.06f, 0.06f));
            puddle.SetFloat("_DepthMax", 0.3f); puddle.SetFloat("_AlphaMin", 0.55f); puddle.SetFloat("_AlphaDepth", 0.3f);
            puddle.SetFloat("_Smoothness", 0.9f); puddle.SetFloat("_ReflectionStrength", 0.6f); puddle.SetFloat("_SpecularStrength", 0.4f);
            puddle.SetTexture("_ShapeMap", Tex("FX_PuddleMask"));
            Store("Water_Puddle", puddle);

            var particles = LoadOrCreate("M_Field_Dust", Find("Universal Render Pipeline/Particles/Unlit"));
            particles.SetTexture("_BaseMap", Tex("FX_Dust"));
            particles.SetColor("_BaseColor", Color.white);
            Transparent(particles);
            particles.SetFloat("_SoftParticlesEnabled", 1f);
            particles.EnableKeyword("_SOFTPARTICLES_ON");
            particles.SetFloat("_SoftParticlesNearFadeDistance", 0f);
            particles.SetFloat("_SoftParticlesFarFadeDistance", 1.5f);
            particles.SetVector("_SoftParticleFadeParams", new Vector4(0f, 1f / 1.5f, 0f, 0f));
            Store("Dust", particles);

            DecalMaterials.Clear();
            var decal = Find("Shader Graphs/Decal");
            foreach (string name in new[] { "DEC_Leak", "DEC_Rust", "DEC_Dirt", "DEC_Crack", "DEC_ORIGIN_Marking", "DEC_Hazard", "DEC_WetEdge" })
            {
                var material = LoadOrCreate("M_" + name, decal);
                material.SetTexture("Base_Map", Tex(name));
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                DecalMaterials[name] = material;
            }
        }

        private static Shader Find(string name) => Shader.Find(name) ?? throw new InvalidOperationException("Missing shader " + name);

        private static Material LoadOrCreate(string file, Shader shader)
        {
            string path = $"{FieldMaterialFolder}/{file}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            return material;
        }

        private static void Store(string key, Material material)
        {
            EditorUtility.SetDirty(material);
            FieldMaterials[key] = material;
        }

        private static void Surface(string key, Shader shader, string set, Color tint, float tiling, float smoothMin, float smoothMax,
            float wetness = 0f, float damp = 0f, float patchiness = 0.5f, float macro = 0.12f, float macroScale = 0.04f,
            string layer = null, float layerAmount = 0f, float layerUp = 0f, Color? layerTint = null, float layerTiling = 0.3f)
        {
            var m = LoadOrCreate("M_Field_" + key, shader);
            m.SetTexture("_BaseMap", Tex(set + "_Albedo"));
            m.SetTexture("_NormalMap", Tex(set + "_Normal"));
            m.SetTexture("_MaskMap", Tex(set + "_Mask"));
            m.SetTexture("_MacroMap", Tex("FX_MacroNoise"));
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Tiling", tiling);
            m.SetFloat("_NormalStrength", 1f);
            m.SetFloat("_OcclusionStrength", 1f);
            m.SetFloat("_Metallic", 1f);
            m.SetFloat("_SmoothnessMin", smoothMin);
            m.SetFloat("_SmoothnessMax", smoothMax);
            m.SetFloat("_MacroScale", macroScale);
            m.SetFloat("_MacroStrength", macro);
            m.SetFloat("_Wetness", wetness);
            m.SetFloat("_WetDarken", WetDarken);
            m.SetFloat("_WetSmoothness", WetSmoothness);
            m.SetFloat("_WetNormalFlatten", WetNormalFlatten);
            m.SetFloat("_WetPatchiness", patchiness);
            m.SetFloat("_GroundWetHeight", damp);
            m.SetFloat("_GroundWetLevel", 0f);
            m.SetFloat("_GroundWetAmount", 0.6f);
            m.SetFloat("_LayerAmount", layer == null ? 0f : layerAmount);
            if (layer != null)
            {
                m.SetTexture("_LayerMap", Tex(layer + "_Albedo"));
                m.SetTexture("_LayerMaskMap", Tex(layer + "_Mask"));
            }
            m.SetColor("_LayerTint", layerTint ?? Color.white);
            m.SetFloat("_LayerTiling", layerTiling);
            m.SetFloat("_LayerContrast", 6f);
            m.SetFloat("_LayerUp", layerUp);
            m.SetFloat("_TriplanarSharpness", 6f);
            m.enableInstancing = true;
            Store(key, m);
        }

        private static void Lit(string key, Color color, float smoothness, float metallic, Color emission, bool transparent = false)
        {
            var m = LoadOrCreate("M_Field_" + key, Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (emission != Color.black)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
            }
            if (transparent) Transparent(m);
            m.enableInstancing = true;
            Store(key, m);
        }

        private static void Transparent(Material m)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void Vegetation(string key, Shader shader, string texture, Color tint, float cutoff, float wind, float speed)
        {
            var m = LoadOrCreate("M_Field_" + key, shader);
            m.SetTexture("_BaseMap", Tex(texture));
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Cutoff", cutoff);
            m.SetFloat("_Smoothness", 0.15f);
            m.SetFloat("_WindStrength", wind);
            m.SetFloat("_WindSpeed", speed);
            m.SetFloat("_Translucency", 0.35f);
            m.SetFloat("_NormalBend", 0.55f);
            m.enableInstancing = true;
            Store(key, m);
        }

        // Pass A keys to Field materials, by object and context. Only greybox materials are swapped.
        private static void ApplyFieldMaterials()
        {
            var root = environment.parent;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var material = renderer.sharedMaterial;
                if (material == null || !material.name.StartsWith("M_Proto_")) continue;
                string key = material.name.Substring("M_Proto_".Length);
                renderer.sharedMaterial = FieldMaterials[FieldKey(key, renderer.transform)];
            }
        }

        private static string FieldKey(string proto, Transform t)
        {
            string name = t.name;
            string path = string.Join("/", Ancestors(t).Select(a => a.name));
            bool trace = path.Contains("TraceTest"), pit = path.Contains("Service Pit"), ravine = path.Contains("Ravine");
            bool interiorRoom = path.Contains("/Interior/");
            switch (proto)
            {
                case "Rock":
                    return ravine || pit || name.StartsWith("Pit ") ? "Rock_Wet" : "Rock";
                case "Ground":
                    if (name.StartsWith("Ravine End")) return "Mud";
                    return name.StartsWith("Mound") ? "Ground_Moss" : "Ground";
                case "Concrete":
                    if (name == "Floor") return trace ? "Concrete_Floor_Wet" : "Concrete_Floor";
                    if (pit || trace || name.StartsWith("Trench") || name.StartsWith("Pit ")) return "Concrete_Wet";
                    return "Concrete_Dry";
                case "Metal":
                    if (name.Contains("Pipe")) return interiorRoom ? "Metal_Dark" : "Metal_Rust";
                    if (name.StartsWith("Container")) return name.Contains("3") ? "Metal_Painted" : "Metal_Rust";
                    if (name.EndsWith("Wall")) return "Metal_Panel";
                    if (name.Contains("Crate") || name.Contains("Bench")) return "Metal_Painted";
                    return "Metal_Dark";
                case "Dark":
                    if (name == "Pit Floor") return "Concrete_Floor_Wet";
                    if (trace && name.EndsWith("Wall")) return "Concrete_Wet";
                    return "Metal_Dark";
                case "ORIGIN": return "Metal_Painted";
                case "Hazard": return "Hazard";
                case "Screen": return "Screen";
                default: throw new ArgumentException(proto);
            }
        }

        private static IEnumerable<Transform> Ancestors(Transform t)
        {
            var list = new List<Transform>();
            for (var p = t; p != null; p = p.parent) list.Insert(0, p);
            return list;
        }

        // ------------------------------------------------------------------ prefabs

        private static void BuildPrefabs()
        {
            Prefabs.Clear();
            SquadVisualKit.EnsureFolder(PrefabFolder);

            // Ceiling light: dark housing, emissive panel, a point light under it. An "off" fixture swaps the panel.
            var fixture = new GameObject("PF_LightFixture");
            LocalPart("Housing", fixture.transform, new Vector3(0f, 0.07f, 0f), new Vector3(1.3f, 0.12f, 0.42f), "Metal_Dark");
            LocalPart("Panel", fixture.transform, new Vector3(0f, -0.005f, 0f), new Vector3(1.2f, 0.03f, 0.32f), "Emissive_ORIGIN");
            PointLight("Light", fixture.transform, new Vector3(0f, -0.35f, 0f), new Color(0.85f, 0.9f, 1f), 8f, 10f);
            SavePrefab(fixture);

            var emergency = new GameObject("PF_EmergencyLight");
            LocalPart("Box", emergency.transform, Vector3.zero, new Vector3(0.32f, 0.14f, 0.12f), "Metal_Dark");
            LocalPart("Lens", emergency.transform, new Vector3(0f, 0f, -0.065f), new Vector3(0.24f, 0.08f, 0.02f), "Emergency");
            PointLight("Light", emergency.transform, new Vector3(0f, -0.1f, -0.35f), new Color(1f, 0.2f, 0.12f), 1.4f, 4.5f);
            SavePrefab(emergency);

            // ORIGIN sign: painted panel with a hazard band; the marking is a decal on its front (-Z) face.
            var sign = new GameObject("PF_OriginSign");
            LocalPart("Panel", sign.transform, Vector3.zero, new Vector3(1.6f, 1.6f, 0.1f), "Metal_Painted");
            LocalPart("Band", sign.transform, new Vector3(0f, -0.86f, 0f), new Vector3(1.6f, 0.12f, 0.12f), "Hazard");
            var marking = new GameObject("Marking").AddComponent<DecalProjector>();
            marking.transform.SetParent(sign.transform, false);
            marking.transform.localPosition = new Vector3(0f, 0f, -0.3f);
            marking.material = DecalMaterials["DEC_ORIGIN_Marking"];
            marking.size = new Vector3(1.5f, 1.5f, 0.4f);
            marking.pivot = new Vector3(0f, 0f, 0.2f);
            marking.drawDistance = 80f;
            SavePrefab(sign);

            // Pipe module: a 4 m run along local X with flanges and two wall brackets (+Z is the wall).
            var pipe = new GameObject("PF_PipeModule");
            LocalPart("Pipe", pipe.transform, Vector3.zero, new Vector3(0.36f, 2f, 0.36f), "Metal_Rust", PrimitiveType.Cylinder, new Vector3(0f, 0f, 90f));
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -1.9f : 1.9f;
                LocalPart("Flange " + i, pipe.transform, new Vector3(x, 0f, 0f), new Vector3(0.52f, 0.04f, 0.52f), "Metal_Dark", PrimitiveType.Cylinder, new Vector3(0f, 0f, 90f));
                LocalPart("Bracket " + i, pipe.transform, new Vector3(x * 0.6f, 0f, 0.2f), new Vector3(0.08f, 0.1f, 0.4f), "Metal_Dark");
            }
            SavePrefab(pipe);

            // Door frame for a 4 x 3 m opening (origin at the bottom centre of the opening, on the wall's outer face);
            // it stands out of the wall along -Z. Scaled per door: world-space materials keep their texel density.
            var frame = new GameObject("PF_DoorFrame");
            LocalPart("Pilaster L", frame.transform, new Vector3(-2.2f, 1.75f, -0.25f), new Vector3(0.4f, 3.5f, 0.5f), "Metal_Painted", collider: true);
            LocalPart("Pilaster R", frame.transform, new Vector3(2.2f, 1.75f, -0.25f), new Vector3(0.4f, 3.5f, 0.5f), "Metal_Painted", collider: true);
            LocalPart("Header", frame.transform, new Vector3(0f, 3.25f, -0.25f), new Vector3(4.8f, 0.5f, 0.5f), "Metal_Painted", collider: true);
            LocalPart("Band L", frame.transform, new Vector3(-2.2f, 0.9f, -0.25f), new Vector3(0.42f, 0.16f, 0.52f), "Hazard");
            LocalPart("Band R", frame.transform, new Vector3(2.2f, 0.9f, -0.25f), new Vector3(0.42f, 0.16f, 0.52f), "Hazard");
            LocalPart("Header Band", frame.transform, new Vector3(0f, 3.05f, -0.51f), new Vector3(4.8f, 0.06f, 0.02f), "Hazard");
            SavePrefab(frame);
        }

        private static void SavePrefab(GameObject go)
        {
            string path = $"{PrefabFolder}/{go.name}.prefab";
            Prefabs[go.name] = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        private static GameObject PlacePrefab(string prefab, Transform parent, string name, Vector3 position, Quaternion rotation, Vector3? scale = null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(Prefabs[prefab], parent);
            go.name = name;
            go.transform.SetPositionAndRotation(position, rotation);
            if (scale.HasValue) go.transform.localScale = scale.Value;
            foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = go.GetComponentInChildren<UnityEngine.Light>() == null;
            return go;
        }

        // ------------------------------------------------------------------ geometry helpers (Field materials)

        private static GameObject Part(string name, Transform parent, Vector3 center, Vector3 size, string material, float yaw = 0f,
            bool collider = false, PrimitiveType type = PrimitiveType.Cube, Quaternion? rotation = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, rotation ?? Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = FieldMaterials[material];
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true;
            return go;
        }

        private static GameObject LocalPart(string name, Transform parent, Vector3 local, Vector3 size, string material,
            PrimitiveType type = PrimitiveType.Cube, Vector3 euler = default, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = FieldMaterials[material];
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject PipeRun(string name, Transform parent, Vector3 from, Vector3 to, float radius, string material)
        {
            var go = Part(name, parent, (from + to) * 0.5f, new Vector3(radius * 2f, Vector3.Distance(from, to) * 0.5f, radius * 2f), material,
                type: PrimitiveType.Cylinder, rotation: Quaternion.FromToRotation(Vector3.up, (to - from).normalized));
            return go;
        }

        private static UnityEngine.Light PointLight(string name, Transform parent, Vector3 local, Color color, float intensity, float range)
        {
            var light = new GameObject(name).AddComponent<UnityEngine.Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = local;
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            return light;
        }

        // ------------------------------------------------------------------ landmarks and solid structures

        // Zone A landmark: a gantry crane straddling the yard's centre, seen from the spawn overlook.
        private static void BuildGantryCrane()
        {
            var g = Child(props, "Yard Gantry Crane");
            foreach (float x in new[] { -11f, 11f })
                foreach (float z in new[] { -37f, -43f })
                {
                    Part("Leg", g, new Vector3(x, 5f, z), new Vector3(0.7f, 10f, 0.7f), "Metal_Painted", collider: true);
                    Part("Leg Foot", g, new Vector3(x, 0.3f, z), new Vector3(1.4f, 0.6f, 1.4f), "Concrete_Dry", collider: true);
                }
            foreach (float z in new[] { -37f, -43f }) Part("Girder", g, new Vector3(0f, 10.4f, z), new Vector3(23.2f, 0.9f, 0.7f), "Metal_Painted");
            foreach (float x in new[] { -11f, 11f }) Part("Cross Beam", g, new Vector3(x, 10.4f, -40f), new Vector3(0.7f, 0.9f, 6.7f), "Metal_Dark");
            Part("Trolley", g, new Vector3(3f, 11.2f, -40f), new Vector3(2.2f, 0.9f, 7f), "Hazard");
            Part("Hoist Cable", g, new Vector3(3f, 8.2f, -40f), new Vector3(0.06f, 2.6f, 0.06f), "Metal_Dark", type: PrimitiveType.Cylinder);
            Part("Hook Block", g, new Vector3(3f, 5.4f, -40f), new Vector3(0.6f, 0.7f, 0.4f), "Hazard");
            foreach (float x in new[] { -11f, 11f }) Part("Leg Band", g, new Vector3(x, 2.2f, -40f), new Vector3(0.2f, 0.25f, 6f), "Hazard");
        }

        // Open-exterior water feature: a raised technical basin (1 m curbs) fed by a pipe, next to the tower path.
        private static void BuildBasin()
        {
            var g = Child(water, "Technical Basin");
            float x0 = BasinCenter.x - BasinSize.x * 0.5f, x1 = BasinCenter.x + BasinSize.x * 0.5f;
            float z0 = BasinCenter.z - BasinSize.y * 0.5f, z1 = BasinCenter.z + BasinSize.y * 0.5f;
            const float h = 1f, t = 0.4f;
            Part("Curb S", g, new Vector3(BasinCenter.x, h * 0.5f, z0 + t * 0.5f), new Vector3(BasinSize.x, h, t), "Concrete_Wet", collider: true);
            Part("Curb N", g, new Vector3(BasinCenter.x, h * 0.5f, z1 - t * 0.5f), new Vector3(BasinSize.x, h, t), "Concrete_Wet", collider: true);
            Part("Curb W", g, new Vector3(x0 + t * 0.5f, h * 0.5f, BasinCenter.z), new Vector3(t, h, BasinSize.y - t * 2f), "Concrete_Wet", collider: true);
            Part("Curb E", g, new Vector3(x1 - t * 0.5f, h * 0.5f, BasinCenter.z), new Vector3(t, h, BasinSize.y - t * 2f), "Concrete_Wet", collider: true);
            Part("Basin Floor", g, new Vector3(BasinCenter.x, 0.02f, BasinCenter.z), new Vector3(BasinSize.x - t * 2f, 0.06f, BasinSize.y - t * 2f), "Concrete_Floor_Wet");
            PipeRun("Inlet Pipe", g, new Vector3(x1 + 3f, 1.6f, BasinCenter.z + 2f), new Vector3(x1 - 1f, 1.6f, BasinCenter.z + 2f), 0.22f, "Metal_Rust");
            Part("Inlet Support", g, new Vector3(x1 + 2f, 0.8f, BasinCenter.z + 2f), new Vector3(0.3f, 1.6f, 0.3f), "Metal_Dark");
        }

        private static void LampPost(Transform parent, string name, Vector3 foot, float yaw)
        {
            var g = Child(parent, name);
            g.SetPositionAndRotation(foot, Quaternion.Euler(0f, yaw, 0f));
            Part("Pole", g, foot + Vector3.up * 3f, new Vector3(0.22f, 3f, 0.22f), "Metal_Dark", collider: true, type: PrimitiveType.Cylinder);
            var arm = g.rotation * Vector3.forward;
            Part("Arm", g, foot + Vector3.up * 5.9f + arm * 0.6f, new Vector3(0.12f, 0.12f, 1.3f), "Metal_Dark", yaw);
            Part("Head", g, foot + Vector3.up * 5.8f + arm * 1.2f, new Vector3(0.5f, 0.16f, 0.32f), "Metal_Dark", yaw);
            Part("Lamp", g, foot + Vector3.up * 5.71f + arm * 1.2f, new Vector3(0.4f, 0.02f, 0.24f), "Emissive_ORIGIN", yaw);
            // Warm sodium accent against the cold overcast.
            var light = new GameObject("Light").AddComponent<UnityEngine.Light>();
            light.transform.SetParent(g, false);
            light.transform.SetPositionAndRotation(foot + Vector3.up * 5.5f + arm * 1.2f, Quaternion.Euler(90f, 0f, 0f));
            light.type = LightType.Spot; light.spotAngle = 110f; light.innerSpotAngle = 60f;
            light.color = new Color(1f, 0.72f, 0.42f); light.intensity = 9f; light.range = 14f; light.shadows = LightShadows.None;
        }

        // A few young trees along the valley edges, never on a route; trunks are solid, canopies are leaf cards.
        private static readonly Vector3[] TreeSpots =
        {
            new Vector3(-30f, 0f, -82f), new Vector3(-24f, 0f, -108f), new Vector3(36f, 0f, -78f), new Vector3(28f, 0f, -112f),
            new Vector3(58f, 0f, -38f), new Vector3(96f, 0f, -36f), new Vector3(98f, 0f, 8f), new Vector3(97f, 0f, 30f),
            new Vector3(70f, 0f, 92f), new Vector3(46f, 0f, 104f), new Vector3(98f, 0f, 84f), new Vector3(36f, 0f, 60f),
        };

        private static void BuildTrees()
        {
            var g = Child(vegetation, "Trees");
            var canopy = CardMesh("VEG_Canopy", 6, 3.2f, 2.4f, 25f, 7);
            var rnd = new Random(41);
            Physics.SyncTransforms();
            foreach (var spot in TreeSpots)
            {
                // Stay clear of anything solid (walls, props, routes along walls).
                if (Physics.CheckSphere(spot + Vector3.up * 1.6f, 1.4f, 1, QueryTriggerInteraction.Ignore)) continue;
                float height = 4.2f + (float)rnd.NextDouble() * 1.6f;
                var tree = Child(g, "Tree");
                tree.position = spot;
                var trunk = Part("Trunk", tree, spot + Vector3.up * height * 0.5f, new Vector3(0.3f, height * 0.5f, 0.3f), "Bark", collider: true, type: PrimitiveType.Cylinder,
                    rotation: Quaternion.Euler((float)rnd.NextDouble() * 4f - 2f, 0f, (float)rnd.NextDouble() * 4f - 2f));
                trunk.isStatic = true;
                for (int i = 0; i < 3; i++)
                {
                    var c = new GameObject("Canopy " + i);
                    c.transform.SetParent(tree, false);
                    c.transform.position = spot + new Vector3((float)rnd.NextDouble() - 0.5f, height - 1.4f + i * 0.7f, (float)rnd.NextDouble() - 0.5f);
                    c.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                    c.transform.localScale = Vector3.one * (1.2f - i * 0.25f);
                    c.AddComponent<MeshFilter>().sharedMesh = canopy;
                    var r = c.AddComponent<MeshRenderer>();
                    r.sharedMaterial = FieldMaterials["Vegetation_Leaves"];
                    c.isStatic = true;
                }
            }
        }

        // ------------------------------------------------------------------ ground dressing (no colliders)

        private static void BuildGroundDressing()
        {
            var g = Child(terrain, "Ground Dressing");
            // The yard is an industrial slab with mud in the low spots, laid 3 cm over the valley floor.
            Part("Yard Slab", g, new Vector3(0f, 0f, -30.5f), new Vector3(71f, 0.06f, 69f), "Yard");
            Part("Apron Slab", g, new Vector3(0f, 0f, 6f), new Vector3(22f, 0.06f, 4f), "Concrete_Floor");
            Part("Spawn Ramp Gravel", g, new Vector3(10f, 0f, -70f), new Vector3(8f, 0.06f, 6f), "Gravel");
            // Gravel track from the yard's east gate into the open exterior, and around the tower foot.
            Part("Track Gravel 1", g, new Vector3(42f, 0f, -8f), new Vector3(10f, 0.06f, 4.5f), "Gravel");
            Part("Track Gravel 2", g, new Vector3(52f, 0f, 2f), new Vector3(4.5f, 0.06f, 20f), "Gravel", 30f);
            Part("Tower Gravel", g, new Vector3(84f, 0f, 70f), new Vector3(12f, 0.06f, 16f), "Gravel");
            Part("Relay Gravel", g, new Vector3(70f, 0f, 50f), new Vector3(10f, 0.06f, 14f), "Gravel");
            // Mud where water collects: around the basin, the end of the ravine, under the trace hut.
            Part("Basin Mud", g, BasinCenter + new Vector3(-7.5f, 0f, -3f), new Vector3(4f, 0.06f, 7f), "Mud", 12f);
            Part("Ravine Mud", g, new Vector3(-88f, 0f, 40f), new Vector3(5f, 0.06f, 9f), "Mud", -26f);
            Part("Trace Mud", g, new Vector3(60f, 0f, 97f), new Vector3(5f, 0.06f, 4f), "Mud");
            // Glass: dark panes on the site office, the warehouse and the tower.
            var glass = Child(props, "Glass");
            for (int i = 0; i < 3; i++) Part("Site Office Window " + i, glass, new Vector3(21f + i * 4f, 3.6f, -27.03f), new Vector3(2.6f, 1.3f, 0.02f), "Glass");
            for (int i = 0; i < 4; i++) Part("Warehouse Window " + i, glass, new Vector3(-28f + i * 4f, 7.1f, -43.97f), new Vector3(2.4f, 0.9f, 0.02f), "Glass");
            for (int i = 0; i < 2; i++) Part("Tower Window " + i, glass, new Vector3(89f + i * 6f, 8f, 93.97f), new Vector3(3f, 1.6f, 0.02f), "Glass");
        }

        // Details on the zone landmarks (no colliders): generator, antenna, pit conduits, signs, pipes, Trace.
        private static void BuildLandmarkDetails()
        {
            // Zone C: the machine-room generator, the central interior landmark.
            var machine = Child(props, "Machine Room Generator");
            PipeRun("Exhaust Stack", machine, new Vector3(-47.5f, 4f, 89f), new Vector3(-47.5f, 9f, 89f), 0.45f, "Metal_Dark");
            Part("Cooling Unit", machine, new Vector3(-44f, 4.5f, 89f), new Vector3(2.4f, 1f, 2.4f), "Metal_Panel");
            Part("Fan", machine, new Vector3(-44f, 5.02f, 89f), new Vector3(1.8f, 0.04f, 1.8f), "Metal_Dark", type: PrimitiveType.Cylinder);
            Part("Status Strip", machine, new Vector3(-47.5f, 2.6f, 85.97f), new Vector3(2f, 0.08f, 0.02f), "Tech");
            Part("Gauge Panel", machine, new Vector3(-49.5f, 1.4f, 85.96f), new Vector3(1.2f, 0.7f, 0.04f), "Metal_Dark");
            Part("Ceiling Vent", machine, new Vector3(-30f, 8.9f, 82f), new Vector3(2f, 0.2f, 2f), "Metal_Dark");
            for (int i = 0; i < 3; i++) PlacePrefab("PF_PipeModule", machine, "Wall Pipe " + i, new Vector3(-52f + i * 4f, 4.5f, 95.55f), Quaternion.identity);

            // Relay platform antenna with an amber beacon, visible from the high route and the tower.
            var relay = Child(props, "Relay Antenna");
            foreach (float y in new[] { 8.5f, 11.5f }) Part("Cross Arm", relay, new Vector3(74f, y, 42f), new Vector3(2.4f, 0.12f, 0.12f), "Metal_Dark");
            Part("Dish", relay, new Vector3(74f, 10.5f, 41.4f), new Vector3(1.6f, 0.08f, 1.6f), "Metal_Painted", rotation: Quaternion.Euler(70f, 0f, 0f), type: PrimitiveType.Cylinder);
            Part("Beacon", relay, new Vector3(74f, 13.2f, 42f), new Vector3(0.35f, 0.35f, 0.35f), "Beacon", type: PrimitiveType.Sphere);
            var beacon = PointLight("Beacon Light", relay, Vector3.zero, new Color(1f, 0.55f, 0.15f), 2.5f, 7f);
            beacon.transform.position = new Vector3(74f, 13.2f, 42f);

            // Zone F: generator conduits climbing the pit wall, electrical cabinet lamp.
            var pit = Child(props, "Service Pit Conduits");
            for (int i = 0; i < 3; i++)
            {
                float z = 78.4f + i * 0.45f;
                PipeRun("Conduit Up " + i, pit, new Vector3(-61.4f, -2.2f, z), new Vector3(-58.2f, -2.2f, z), 0.07f, "Metal_Dark");
                PipeRun("Conduit Riser " + i, pit, new Vector3(-58.15f, -2.2f, z), new Vector3(-58.15f, -0.2f, z), 0.07f, "Metal_Dark");
            }
            Part("Generator Status", pit, new Vector3(-63f, -2f, 77.99f), new Vector3(1.2f, 0.06f, 0.02f), "Tech");
            PlacePrefab("PF_PipeModule", pit, "Pit Wall Pipe", new Vector3(-70f, -1.6f, 58.25f), Quaternion.Euler(0f, 180f, 0f));

            // Signs.
            var signs = Child(props, "Signs");
            PlacePrefab("PF_OriginSign", signs, "Sign Yard Entrance", new Vector3(16f, 1.6f, -66f), Quaternion.identity);
            Part("Sign Post", signs, new Vector3(16f, 0.4f, -65.92f), new Vector3(0.12f, 0.8f, 0.12f), "Metal_Dark");
            PlacePrefab("PF_OriginSign", signs, "Sign Junction", new Vector3(-36f, 2f, 63.75f), Quaternion.identity, new Vector3(0.6f, 0.6f, 1f));
            PlacePrefab("PF_OriginSign", signs, "Sign Pit", new Vector3(-74f, -1.8f, 58.05f), Quaternion.Euler(0f, 180f, 0f), new Vector3(0.7f, 0.7f, 1f));

            // Hangar wall pipes and a corridor run.
            var hangar = Child(props, "Hangar Pipes");
            for (int i = 0; i < 4; i++) PlacePrefab("PF_PipeModule", hangar, "Hangar Pipe " + i, new Vector3(-16f + i * 4f, 9f, 41.55f), Quaternion.identity);
            for (int i = 0; i < 3; i++) PlacePrefab("PF_PipeModule", hangar, "Corridor Pipe " + i, new Vector3(-30.45f, 2.7f, 50f + i * 4f), Quaternion.Euler(0f, 90f, 0f), new Vector3(1f, 0.6f, 0.6f));

            // Trace: subtle strangeness only. A faint seam in the chamber floor, panels slightly off true.
            var trace = Child(traceTest, "Trace Dressing");
            Part("Floor Seam", trace, new Vector3(60f, 0.035f, 115.5f), new Vector3(0.05f, 0.01f, 6f), "Trace_Seam");
            Part("Seam Cross", trace, new Vector3(60f, 0.035f, 116f), new Vector3(5f, 0.01f, 0.05f), "Trace_Seam");
            Part("Tilted Panel", trace, new Vector3(56.3f, 1.9f, 108f), new Vector3(0.06f, 1.4f, 2.2f), "Metal_Panel", rotation: Quaternion.Euler(0f, 0f, 2.5f));
            Part("Tilted Panel 2", trace, new Vector3(63.7f, 1.4f, 114f), new Vector3(0.06f, 1.2f, 1.8f), "Metal_Panel", rotation: Quaternion.Euler(1.5f, 0f, -3f));
        }

        // ------------------------------------------------------------------ light fixtures and accents

        private static readonly string[] FixtureLights =
        {
            "Hangar Light 1", "Hangar Light 2", "Hangar Balcony Light", "Lobby Light 1", "Lobby Light 2", "Office Light", "Corridor Light",
            "Junction Light", "Lab Light", "Service Light", "Machine Light 1", "Machine Light 2", "Pit Light 1", "Pit Light 2",
        };

        private static void BuildLightFixtures()
        {
            var fixtures = Child(lighting, "Fixtures");
            // Interior lights become fixtures (same colour, intensity and range); the light hangs under its housing.
            foreach (string name in FixtureLights)
            {
                var source = lighting.Find(name).GetComponent<UnityEngine.Light>();
                var go = PlacePrefab("PF_LightFixture", fixtures, name, source.transform.position + Vector3.up * 0.35f, Quaternion.identity);
                var light = go.GetComponentInChildren<UnityEngine.Light>();
                light.color = source.color; light.intensity = source.intensity; light.range = source.range;
                Object.DestroyImmediate(source.gameObject);
                Suspend(go.transform);
            }
            // Dead fixtures: the facility is half abandoned.
            (string name, Vector3 at)[] off = { ("Lobby Fixture (off)", new Vector3(-28f, 5.75f, 36f)), ("Hangar Fixture (off)", new Vector3(-10f, 12.35f, 32f)),
                ("Corridor Fixture (off)", new Vector3(-32f, 3.35f, 58f)), ("Machine Fixture (off)", new Vector3(-50f, 8.35f, 92f)) };
            foreach (var (name, at) in off)
            {
                var go = PlacePrefab("PF_LightFixture", fixtures, name, at, Quaternion.identity);
                go.transform.Find("Panel").GetComponent<Renderer>().sharedMaterial = FieldMaterials["Emissive_Off"];
                Object.DestroyImmediate(go.GetComponentInChildren<UnityEngine.Light>().gameObject);
                Suspend(go.transform);
            }
            // Emergency lights by the doors of the interior route and the pit.
            var emergency = Child(lighting, "Emergency");
            (string name, Vector3 at, float yaw)[] reds =
            {
                ("Emergency Junction", new Vector3(-40f, 2.7f, 63.74f), 0f), ("Emergency Service Corridor", new Vector3(-25.26f, 2.5f, 74f), 90f),
                ("Emergency Pit", new Vector3(-79.5f, -1.4f, 58.06f), 180f), ("Emergency Machine Room", new Vector3(-55.74f, 6.5f, 90f), -90f),
                ("Emergency Hangar Balcony", new Vector3(21.74f, 8f, 14f), 90f)
            };
            foreach (var (name, at, yaw) in reds) PlacePrefab("PF_EmergencyLight", emergency, name, at, Quaternion.Euler(0f, yaw, 0f));
            // Warm light at the hangar door, the interior / exterior threshold.
            var door = new GameObject("Hangar Door Warm Light").AddComponent<UnityEngine.Light>();
            door.transform.SetParent(lighting, false);
            door.transform.SetPositionAndRotation(new Vector3(0f, 9.4f, 6.2f), Quaternion.Euler(70f, 0f, 0f));
            door.type = LightType.Spot; door.spotAngle = 120f; door.innerSpotAngle = 70f;
            door.color = new Color(1f, 0.74f, 0.45f); door.intensity = 10f; door.range = 16f; door.shadows = LightShadows.None;
        }

        // A fixture far below its ceiling hangs from two rods.
        private static void Suspend(Transform fixture)
        {
            Physics.SyncTransforms();
            Vector3 top = fixture.position + Vector3.up * 0.13f;
            if (!Physics.Raycast(top, Vector3.up, out var hit, 12f, 1, QueryTriggerInteraction.Ignore) || hit.distance < 0.25f) return;
            foreach (float x in new[] { -0.5f, 0.5f })
                PipeRun("Rod", fixture, top + Vector3.right * x, top + Vector3.right * x + Vector3.up * hit.distance, 0.012f, "Metal_Dark");
        }

        // ------------------------------------------------------------------ sky, sun, fog, reflections

        private static void BuildAtmosphereLighting(UnityEngine.Camera camera)
        {
            // Overcast: cool, soft sun; fog and ambient from the same grey sky as the reflections.
            var sun = RenderSettings.sun;
            sun.color = new Color(0.84f, 0.88f, 0.95f);
            sun.intensity = 1.0f;
            sun.shadowStrength = 0.72f;
            sun.transform.rotation = Quaternion.Euler(52f, -32f, 0f);
            var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(TextureFolder + "/FX_EnvCube.png") ?? throw new InvalidOperationException("FX_EnvCube is not a cubemap.");
            var sky = LoadOrCreate("M_Field_Sky", Find("Skybox/Cubemap"));
            sky.SetTexture("_Tex", cube);
            sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));
            sky.SetFloat("_Exposure", 1.05f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.0042f;
            RenderSettings.fogColor = new Color(0.56f, 0.6f, 0.63f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.47f, 0.51f, 0.56f);
            RenderSettings.ambientEquatorColor = new Color(0.3f, 0.32f, 0.33f);
            RenderSettings.ambientGroundColor = new Color(0.11f, 0.11f, 0.105f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = cube;
            RenderSettings.reflectionIntensity = 0.8f;
            camera.clearFlags = CameraClearFlags.Skybox;
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            data.requiresDepthOption = CameraOverrideOption.On;
            data.volumeLayerMask |= 1 << 2;

            // Realtime probes captured once on load: the water and wet floors reflect their own surroundings.
            var probes = Child(lighting, "Reflection Probes");
            Probe(probes, "Probe Yard", new Vector3(0f, 5f, -30f), new Vector3(80f, 24f, 74f), 0, 64);
            Probe(probes, "Probe Hangar", new Vector3(0f, 6f, 25f), new Vector3(44f, 14f, 34f), 1, 64);
            Probe(probes, "Probe Service Pit", new Vector3(-69f, -1.5f, 70f), new Vector3(24f, 6f, 28f), 1, 64);
            Probe(probes, "Probe Basin", BasinCenter + new Vector3(0f, 3f, 0f), new Vector3(40f, 16f, 32f), 1, 128);
        }

        private static void Probe(Transform parent, string name, Vector3 center, Vector3 size, int importance, int resolution)
        {
            var probe = new GameObject(name).AddComponent<ReflectionProbe>();
            probe.transform.SetParent(parent, false);
            probe.transform.position = center;
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = size;
            probe.boxProjection = true;
            probe.importance = importance;
            probe.resolution = resolution;
            probe.hdr = true;
            probe.blendDistance = 2f;
        }

        // ------------------------------------------------------------------ post-process volumes

        public const string InteriorProfilePath = VolumeFolder + "/FieldTest_Interior.asset";
        public const string DampProfilePath = VolumeFolder + "/FieldTest_DampLowArea.asset";
        public const string TraceProfilePath = VolumeFolder + "/FieldTest_Trace.asset";

        private static void BuildVolumes()
        {
            // Global: moderate contrast, slightly low saturation, slightly cold, subtle bloom, very light vignette.
            var global = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            Override<Tonemapping>(global).mode.Override(TonemappingMode.ACES);
            var color = Override<ColorAdjustments>(global);
            color.saturation.Override(-12f); color.contrast.Override(8f); color.postExposure.Override(0f);
            Override<WhiteBalance>(global).temperature.Override(-8f);
            var bloom = Override<Bloom>(global);
            bloom.intensity.Override(0.3f); bloom.threshold.Override(1.1f); bloom.scatter.Override(0.55f);
            var vignette = Override<Vignette>(global);
            vignette.intensity.Override(0.15f); vignette.smoothness.Override(0.45f);
            EditorUtility.SetDirty(global);

            // Local volumes: priorities 1-3, below the Tactical Focus volumes (10 and 11), so the Focus look always wins.
            SquadVisualKit.EnsureFolder(VolumeFolder);
            var interior = Profile(InteriorProfilePath);
            var ic = Override<ColorAdjustments>(interior); ic.postExposure.Override(0.35f); ic.saturation.Override(-8f);
            Override<WhiteBalance>(interior).temperature.Override(3f);
            Override<Vignette>(interior).intensity.Override(0.2f);
            var damp = Profile(DampProfilePath);
            var dc = Override<ColorAdjustments>(damp); dc.postExposure.Override(0.2f); dc.saturation.Override(-20f); dc.contrast.Override(12f);
            dc.colorFilter.Override(new Color(0.92f, 1f, 0.97f));
            var trace = Profile(TraceProfilePath);
            var tc = Override<ColorAdjustments>(trace); tc.saturation.Override(-22f); tc.postExposure.Override(0.15f);
            Override<WhiteBalance>(trace).temperature.Override(-14f);
            Override<LensDistortion>(trace).intensity.Override(-0.12f);
            var tv = Override<Vignette>(trace); tv.intensity.Override(0.28f); tv.color.Override(new Color(0.02f, 0.06f, 0.08f));

            LocalVolume("Interior Hangar", interior, new Vector3(0f, 7f, 25f), new Vector3(44f, 14f, 34f), 1f, 4f);
            LocalVolume("Interior Main", interior, new Vector3(-39f, 4.5f, 63f), new Vector3(34f, 9f, 66f), 1f, 2f);
            LocalVolume("Damp Low Area (Service Pit)", damp, new Vector3(-69f, -1.5f, 70f), new Vector3(24f, 5f, 26f), 2f, 2f);
            LocalVolume("Trace", trace, new Vector3(60f, 2f, 110f), new Vector3(8f, 4f, 19f), 3f, 3f);
        }

        private static VolumeProfile Profile(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        private static T Override<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var component)) return component;
            component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            return component;
        }

        private static void LocalVolume(string name, VolumeProfile profile, Vector3 center, Vector3 size, float priority, float blend)
        {
            var go = new GameObject(name) { layer = 2 };
            go.transform.SetParent(volumes, false);
            go.transform.position = center;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = false;
            volume.priority = priority;
            volume.blendDistance = blend;
            volume.sharedProfile = profile;
        }

        // ------------------------------------------------------------------ dust and mist

        private static void BuildParticles()
        {
            var g = Child(lighting, "Atmosphere");
            // Dust drifting in the hangar and the machine room, suspended motes in the Trace corridor, low mist in the pit and ravine.
            Particles(g, "Hangar Dust", new Vector3(0f, 6f, 25f), new Vector3(40f, 10f, 30f), 90, 0.06f, new Color(0.9f, 0.9f, 0.85f, 0.35f), 12f, 0.12f);
            Particles(g, "Machine Room Dust", new Vector3(-39f, 4f, 87f), new Vector3(30f, 7f, 16f), 50, 0.05f, new Color(0.9f, 0.9f, 0.85f, 0.3f), 12f, 0.1f);
            Particles(g, "Trace Motes", new Vector3(60f, 1.6f, 112f), new Vector3(6f, 2.4f, 14f), 40, 0.04f, new Color(0.75f, 0.9f, 1f, 0.45f), 20f, 0.01f);
            Particles(g, "Pit Mist", new Vector3(-69f, -2.8f, 70f), new Vector3(20f, 0.8f, 22f), 14, 5f, new Color(0.75f, 0.8f, 0.82f, 0.05f), 18f, 0.08f);
            Particles(g, "Ravine Mist", new Vector3(-86f, 0.6f, 30f), new Vector3(10f, 0.8f, 50f), 16, 6f, new Color(0.75f, 0.8f, 0.82f, 0.045f), 20f, 0.1f);
        }

        private static void Particles(Transform parent, string name, Vector3 center, Vector3 size, int count, float particleSize, Color color, float lifetime, float speed)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.prewarm = true; main.playOnAwake = true;
            main.duration = lifetime;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = new ParticleSystem.MinMaxCurve(particleSize * 0.6f, particleSize * 1.4f);
            main.startColor = color;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission; emission.rateOverTime = count / lifetime;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = size;
            var fade = ps.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            var noise = ps.noise; noise.enabled = true; noise.strength = speed * 0.8f; noise.frequency = 0.2f; noise.quality = ParticleSystemNoiseQuality.Low;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = FieldMaterials["Dust"];
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.maxParticleSize = 2f;
        }

        // ------------------------------------------------------------------ vegetation

        private static void BuildVegetation()
        {
            SquadVisualKit.EnsureFolder(MeshFolder);
            var grass = CardMesh("VEG_GrassClump", 3, 0.9f, 0.55f, 12f, 3);
            var shrub = CardMesh("VEG_Shrub", 5, 1.5f, 1.2f, 22f, 5);
            Physics.SyncTransforms();
            var rnd = new Random(2207);
            // Clumps at the foot of walls, rocks and cliffs, in the open ground; never on a surface that is covered.
            var areas = new (string name, Vector3 a, Vector3 b, bool line, float spread, int grass, int shrubs)[]
            {
                ("Valley South", new Vector3(-44f, 0f, -120f), new Vector3(46f, 0f, -67f), false, 0f, 70, 18),
                ("Yard West Wall", new Vector3(-35f, 0f, -64f), new Vector3(-35f, 0f, -36f), true, 0.5f, 14, 3),
                ("Yard East Wall", new Vector3(35f, 0f, -64f), new Vector3(35f, 0f, -18f), true, 0.5f, 14, 3),
                ("Open Exterior", new Vector3(38f, 0f, -42f), new Vector3(99f, 0f, 92f), false, 0f, 150, 32),
                ("Ravine South", new Vector3(-40f, 0f, -29f), new Vector3(-80f, 0f, 0f), true, 3f, 22, 5),
                ("Ravine Mid", new Vector3(-80f, 0f, 0f), new Vector3(-86f, 0f, 40f), true, 3f, 22, 5),
                ("Ravine North", new Vector3(-86f, 0f, 40f), new Vector3(-97f, 0f, 66f), true, 2.5f, 18, 4),
                ("North Edge", new Vector3(30f, 0f, 90f), new Vector3(99f, 0f, 118f), false, 0f, 30, 8),
                ("Spawn Overlook", new Vector3(-1f, 0f, -104f), new Vector3(21f, 0f, -90f), false, 0f, 10, 2),
                ("Basin", BasinCenter - new Vector3(10f, 0f, 9f), BasinCenter + new Vector3(10f, 0f, 9f), false, 0f, 20, 3),
            };
            foreach (var area in areas)
            {
                var g = Child(vegetation, area.name);
                Clumps(g, area.name, "Grass", grass, "Vegetation_Grass", Scatter(rnd, area.a, area.b, area.line, area.spread, area.grass, 0.7f, 1.3f), false);
                Clumps(g, area.name, "Shrubs", shrub, "Vegetation_Leaves", Scatter(rnd, area.a, area.b, area.line, area.spread, area.shrubs, 0.7f, 1.3f), true);
            }
        }

        private static List<Matrix4x4> Scatter(Random rnd, Vector3 a, Vector3 b, bool line, float spread, int count, float minScale, float maxScale)
        {
            var result = new List<Matrix4x4>();
            Vector3 along = (b - a).normalized, side = Vector3.Cross(Vector3.up, along);
            for (int attempt = 0; attempt < count * 6 && result.Count < count; attempt++)
            {
                float u = (float)rnd.NextDouble(), v = (float)rnd.NextDouble();
                Vector3 p = line ? Vector3.Lerp(a, b, u) + side * (v * 2f - 1f) * spread : new Vector3(Mathf.Lerp(a.x, b.x, u), 0f, Mathf.Lerp(a.z, b.z, v));
                if (!Physics.Raycast(new Vector3(p.x, 40f, p.z), Vector3.down, out var hit, 80f, 1, QueryTriggerInteraction.Ignore)) continue;
                string surface = hit.collider.name;
                if (!(surface.StartsWith("Floor") || surface.StartsWith("Mound") || surface.StartsWith("Ravine End") || surface == "Overlook Rock")) continue;
                if (hit.normal.y < 0.85f) continue;
                if (Physics.Raycast(hit.point + Vector3.up * 0.3f, Vector3.up, 30f, 1, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.CheckSphere(hit.point + Vector3.up * 0.6f, 0.5f, 1, QueryTriggerInteraction.Ignore)) continue;
                if (new Rect(BasinCenter.x - BasinSize.x * 0.5f, BasinCenter.z - BasinSize.y * 0.5f, BasinSize.x, BasinSize.y).Contains(new Vector2(hit.point.x, hit.point.z))) continue;
                float scale = Mathf.Lerp(minScale, maxScale, (float)rnd.NextDouble());
                result.Add(Matrix4x4.TRS(hit.point - Vector3.up * 0.02f, Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f), Vector3.one * scale));
            }
            return result;
        }

        // Clumps of one area merged into one mesh (one draw per area and material), saved as an asset.
        private static void Clumps(Transform parent, string area, string kind, Mesh card, string material, List<Matrix4x4> placements, bool shadows)
        {
            if (placements.Count == 0) return;
            var combine = placements.Select(m => new CombineInstance { mesh = card, transform = m }).ToArray();
            string path = $"{MeshFolder}/VEG_{area.Replace(" ", "")}_{kind}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear();
            mesh.name = System.IO.Path.GetFileNameWithoutExtension(path);
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.CombineMeshes(combine, true, true);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            var go = new GameObject(kind);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = FieldMaterials[material];
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            go.isStatic = true;
        }

        // Crossed cards around the vertical axis, leaning outward; uv.y runs from the root (0) to the tip (1) for the wind.
        private static Mesh CardMesh(string name, int cards, float width, float height, float tilt, int seed)
        {
            SquadVisualKit.EnsureFolder(MeshFolder);
            string path = $"{MeshFolder}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear();
            mesh.name = name;
            var rnd = new Random(seed);
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uvs = new List<Vector2>(); var triangles = new List<int>();
            for (int i = 0; i < cards; i++)
            {
                float yaw = 180f / cards * i + (float)rnd.NextDouble() * 20f;
                var rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(tilt * (i % 2 == 0 ? 1f : -1f), 0f, 0f);
                Vector3 right = rotation * Vector3.right * width * 0.5f, up = rotation * Vector3.up * height, normal = rotation * Vector3.forward;
                int start = vertices.Count;
                vertices.AddRange(new[] { -right, right, -right + up, right + up });
                normals.AddRange(Enumerable.Repeat(normal, 4));
                uvs.AddRange(new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
                triangles.AddRange(new[] { start, start + 2, start + 1, start + 1, start + 2, start + 3 });
            }
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // ------------------------------------------------------------------ water

        private static void BuildWater()
        {
            var basin = water.Find("Technical Basin");
            var surface = Part("Water Surface", basin, BasinCenter + Vector3.up * 0.85f, new Vector3(BasinSize.x - 0.8f, BasinSize.y - 0.8f, 1f), "Water_Basin",
                type: PrimitiveType.Quad, rotation: Quaternion.Euler(90f, 0f, 0f));
            surface.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            // Puddles: where the ground or the floor is lowest; on the slabs they sit just above the 3 cm overlays.
            var puddles = Child(water, "Puddles");
            (Vector3 at, float size)[] spots =
            {
                (new Vector3(-8f, 0.045f, -20f), 3.5f), (new Vector3(12f, 0.045f, -57f), 2.6f), (new Vector3(-24f, 0.045f, -33f), 2.2f),
                (new Vector3(2f, 0.045f, -8f), 3f), (new Vector3(28f, 0.045f, -46f), 1.8f), (new Vector3(-6f, 0.045f, 18f), 2.4f),
                (new Vector3(-81f, 0.015f, 3f), 2.4f), (new Vector3(-88f, 0.015f, 24f), 3f), (new Vector3(-92f, 0.015f, 48f), 2.2f), (new Vector3(-96f, 0.015f, 62f), 2.6f),
                (new Vector3(-72f, -3.455f, 62f), 3f), (new Vector3(-64f, -3.455f, 76f), 2.2f), (new Vector3(60f, 0.045f, 108f), 1.6f),
                (BasinCenter + new Vector3(-7.6f, 0.045f, -2f), 2.4f), (new Vector3(52f, 0.045f, 0f), 2f),
            };
            var rnd = new Random(77);
            foreach (var (at, size) in spots)
            {
                var p = Part("Puddle", puddles, at, new Vector3(size, size * (0.6f + (float)rnd.NextDouble() * 0.35f), 1f), "Water_Puddle", type: PrimitiveType.Quad,
                    rotation: Quaternion.Euler(90f, (float)rnd.NextDouble() * 360f, 0f));
                p.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        // ------------------------------------------------------------------ decals

        private static void EnsureDecalFeature()
        {
            // Screen Space technique: decals are drawn after opaques from the depth buffer, so the Field shaders need no
            // DBuffer support; there is no extra cost in scenes without projectors.
            foreach (string path in VisualRendererAssets)
            {
                var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
                if (rendererData == null) continue;
                var feature = rendererData.rendererFeatures.OfType<DecalRendererFeature>().FirstOrDefault();
                if (feature == null)
                {
                    feature = ScriptableObject.CreateInstance<DecalRendererFeature>();
                    feature.name = "Decal";
                    AssetDatabase.AddObjectToAsset(feature, rendererData);
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
                    var dataObject = new SerializedObject(rendererData);
                    var list = dataObject.FindProperty("m_RendererFeatures");
                    list.arraySize++;
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = feature;
                    var map = dataObject.FindProperty("m_RendererFeatureMap");
                    map.arraySize++;
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                    dataObject.ApplyModifiedPropertiesWithoutUndo();
                }
                var settings = new SerializedObject(feature);
                settings.FindProperty("m_Settings.technique").enumValueIndex = 2;
                settings.FindProperty("m_Settings.maxDrawDistance").floatValue = 90f;
                settings.FindProperty("m_Settings.decalLayers").boolValue = false;
                settings.ApplyModifiedPropertiesWithoutUndo();
                feature.SetActive(true);
                rendererData.SetDirty();
                EditorUtility.SetDirty(rendererData);
                EditorUtility.SetDirty(feature);
            }
        }

        private static readonly Vector3 Down = Vector3.down;

        private static void BuildDecals()
        {
            var g = decals;
            Vector3 N = Vector3.forward, S = Vector3.back, E = Vector3.right, W = Vector3.left;
            // Yard (forward points into the surface).
            Decal(g, "Leak Warehouse 1", "DEC_Leak", new Vector3(-26f, 6.2f, -43.6f), S, 3f, 4.5f);
            Decal(g, "Leak Warehouse 2", "DEC_Leak", new Vector3(-17f, 6.4f, -43.6f), S, 2.4f, 4f, 0.8f);
            Decal(g, "Dirt Warehouse Base", "DEC_Dirt", new Vector3(-22f, 0.9f, -43.6f), S, 16f, 1.8f);
            Decal(g, "ORIGIN Warehouse Panel", "DEC_ORIGIN_Marking", new Vector3(-22f, 5.5f, -43.5f), S, 2f, 2f);
            Decal(g, "Dirt Yard West Wall", "DEC_Dirt", new Vector3(-35.1f, 0.8f, -50f), W, 28f, 1.6f, 0.85f);
            Decal(g, "Dirt Yard East Wall", "DEC_Dirt", new Vector3(35.1f, 0.8f, -40f), E, 40f, 1.6f, 0.85f);
            Decal(g, "Leak Yard West Wall", "DEC_Leak", new Vector3(-35.1f, 3f, -24f), W, 2f, 3f, 0.7f);
            Decal(g, "Rust Container 1", "DEC_Rust", new Vector3(-4.4f, 1.3f, -45f), W, 8f, 2.4f);
            Decal(g, "ORIGIN Container 2", "DEC_ORIGIN_Marking", new Vector3(5f, 1.4f, -31.6f), N, 2f, 2f);
            Decal(g, "Leak Site Office", "DEC_Leak", new Vector3(22f, 4.5f, -27.4f), N, 2.5f, 3.5f, 0.8f);
            Decal(g, "Crack Yard 1", "DEC_Crack", new Vector3(-4f, 0.3f, -20f), Down, 6f, 6f);
            Decal(g, "Crack Yard 2", "DEC_Crack", new Vector3(14f, 0.3f, -34f), Down, 5f, 5f, 0.8f, 70f);
            Decal(g, "Crack Yard 3", "DEC_Crack", new Vector3(-20f, 0.3f, -62f), Down, 5f, 5f, 0.8f, 160f);
            Decal(g, "Hazard Apron West", "DEC_Hazard", new Vector3(-9.5f, 0.3f, 5f), Down, 3f, 1f, 0.9f);
            Decal(g, "Hazard Apron East", "DEC_Hazard", new Vector3(9.5f, 0.3f, 5f), Down, 3f, 1f, 0.9f);
            // Hangar.
            Decal(g, "ORIGIN Hangar Facade", "DEC_ORIGIN_Marking", new Vector3(0f, 12.75f, 7.2f), N, 2.1f, 2.1f);
            Decal(g, "Leak Hangar Facade W", "DEC_Leak", new Vector3(-16f, 11.5f, 7.4f), N, 3f, 5f);
            Decal(g, "Leak Hangar Facade E", "DEC_Leak", new Vector3(15f, 11f, 7.4f), N, 2.5f, 4.5f, 0.8f);
            Decal(g, "Dirt Hangar Facade W", "DEC_Dirt", new Vector3(-15f, 0.9f, 7.4f), N, 13f, 1.8f);
            Decal(g, "Dirt Hangar Facade E", "DEC_Dirt", new Vector3(15f, 0.9f, 7.4f), N, 13f, 1.8f);
            Decal(g, "Hazard Hangar Machine", "DEC_Hazard", new Vector3(-12f, 0.3f, 31f), Down, 6.5f, 0.9f, 0.9f);
            Decal(g, "Crack Hangar", "DEC_Crack", new Vector3(4f, 0.3f, 28f), Down, 6f, 6f, 0.7f, 30f);
            Decal(g, "Leak Hangar West", "DEC_Leak", new Vector3(-21.4f, 10f, 18f), W, 3f, 6f);
            Decal(g, "Leak Hangar North", "DEC_Leak", new Vector3(-4f, 10f, 41.4f), N, 3f, 6f, 0.8f);
            // Interior.
            Decal(g, "Crack Lobby", "DEC_Crack", new Vector3(-34f, 0.3f, 42f), Down, 4f, 4f, 0.7f, 110f);
            Decal(g, "Leak Junction", "DEC_Leak", new Vector3(-44f, 2.4f, 63.4f), N, 2f, 2.6f, 0.8f);
            Decal(g, "Leak Machine Room", "DEC_Leak", new Vector3(-40f, 6f, 95.4f), N, 3f, 5f);
            Decal(g, "ORIGIN Generator", "DEC_ORIGIN_Marking", new Vector3(-44.4f, 1.6f, 85.6f), N, 1.5f, 1.5f);
            Decal(g, "Hazard Generator", "DEC_Hazard", new Vector3(-46f, 0.3f, 85.2f), Down, 7f, 0.8f, 0.9f);
            Decal(g, "Leak Service Corridor", "DEC_Leak", new Vector3(-25.6f, 2f, 70f), E, 1.5f, 2.2f, 0.8f);
            // Service pit.
            Decal(g, "Wet Edge Pit South", "DEC_WetEdge", new Vector3(-70f, -2.6f, 58.4f), S, 18f, 2.2f);
            Decal(g, "Wet Edge Pit North", "DEC_WetEdge", new Vector3(-70f, -2.6f, 81.6f), N, 18f, 2.2f);
            Decal(g, "Leak Pit East", "DEC_Leak", new Vector3(-58.4f, -1.8f, 62f), E, 3f, 3f);
            Decal(g, "Rust Pit Pipe", "DEC_Rust", new Vector3(-66f, -2f, 81.6f), N, 6f, 2f, 0.8f);
            Decal(g, "Hazard Pit Stairs", "DEC_Hazard", new Vector3(-71f, -3.2f, 71.5f), Down, 0.8f, 3f, 0.9f);
            Decal(g, "Crack Pit", "DEC_Crack", new Vector3(-66f, -3.2f, 64f), Down, 4f, 4f, 0.8f, 200f);
            // Open exterior, tower, basin, Trace, spawn.
            Decal(g, "ORIGIN Relay Platform", "DEC_ORIGIN_Marking", new Vector3(66f, 2.6f, 35.6f), N, 2.2f, 2.2f);
            Decal(g, "Dirt Relay Platform", "DEC_Dirt", new Vector3(70f, 0.9f, 35.6f), N, 12f, 1.8f);
            Decal(g, "ORIGIN Tower", "DEC_ORIGIN_Marking", new Vector3(92f, 4.2f, 93.6f), N, 3f, 3f);
            Decal(g, "Leak Tower", "DEC_Leak", new Vector3(97.2f, 9f, 93.6f), N, 2f, 5f, 0.8f);
            Decal(g, "Wet Edge Basin", "DEC_WetEdge", BasinCenter + new Vector3(0f, 0.6f, -5.4f), N, 12f, 1.2f);
            Decal(g, "Wet Edge Basin West", "DEC_WetEdge", BasinCenter + new Vector3(-6.4f, 0.6f, 0f), E, 10f, 1.2f);
            Decal(g, "Crack Trace Hut", "DEC_Crack", new Vector3(60f, 0.3f, 102f), Down, 3f, 3f, 0.7f, 40f);
            Decal(g, "Leak Trace Hut", "DEC_Leak", new Vector3(58.2f, 2.6f, 99.4f), N, 1.4f, 2.4f, 0.8f);
            Decal(g, "ORIGIN Spawn Sign", "DEC_ORIGIN_Marking", new Vector3(14f, 6.2f, -98.5f), S, 1.1f, 1.1f);
        }

        private static void Decal(Transform parent, string name, string material, Vector3 origin, Vector3 forward, float width, float height, float opacity = 1f, float yaw = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var up = forward == Vector3.down ? Quaternion.Euler(0f, yaw, 0f) * Vector3.forward : Vector3.up;
            go.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(forward, up));
            var projector = go.AddComponent<DecalProjector>();
            projector.material = DecalMaterials[material];
            float depth = forward == Vector3.down ? 0.8f : 0.8f;
            projector.size = new Vector3(width, height, depth);
            projector.pivot = new Vector3(0f, 0f, depth * 0.5f);
            projector.fadeFactor = opacity;
            projector.drawDistance = 80f;
            // Hazard stripes tile instead of stretching.
            if (material == "DEC_Hazard") projector.uvScale = width > height ? new Vector2(width / height, 1f) : new Vector2(1f, height / width);
        }

        // ------------------------------------------------------------------ audio placeholders

        private static void BuildAudioPlaceholders()
        {
            var g = Child(audio, "Placeholders");
            (string name, Vector3 at, Vector3 size)[] points =
            {
                ("Audio Ventilation (machine room vent)", new Vector3(-30f, 8.5f, 82f), new Vector3(2f, 1f, 2f)),
                ("Audio Water (basin inlet)", BasinCenter + new Vector3(5f, 1f, 2f), new Vector3(3f, 1f, 3f)),
                ("Audio Machinery (pit generator)", new Vector3(-63f, -2.4f, 79f), new Vector3(3f, 2.2f, 2f)),
                ("Audio Wind Corridor (ravine)", new Vector3(-84f, 3f, 30f), new Vector3(8f, 4f, 30f)),
                ("Audio Electrical Room (pit power cabinet)", new Vector3(-78f, -2.4f, 78f), new Vector3(2f, 2.2f, 4f)),
            };
            foreach (var (name, at, size) in points)
            {
                var go = new GameObject(name);
                go.transform.SetParent(g, false);
                go.transform.position = at;
                var marker = go.AddComponent<FieldMarker>();
                var data = new SerializedObject(marker);
                data.FindProperty("kind").enumValueIndex = (int)FieldMarker.Kind.Audio;
                data.FindProperty("size").vector3Value = size;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
