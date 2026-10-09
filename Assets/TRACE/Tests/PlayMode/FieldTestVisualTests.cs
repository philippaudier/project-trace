using System.Collections;
using System.Linq;
using NUnit.Framework;
using TRACE.Encounter;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TRACE.Tests
{
    // FieldTest Visual Pass B: layout, materials, wetness limits, decals, volumes under Tactical Focus, no colliders on
    // dressing, atmosphere budget, audio placeholders and landmarks.
    public sealed class FieldTestVisualTests
    {
        private Transform root;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("FieldTest");
            yield return null;
            root = GameObject.Find("FieldTest").transform;
        }

        [UnityTest] public IEnumerator TheHierarchyFollowsTheVisualPassLayout()
        {
            foreach (string path in new[] { "Environment/Terrain", "Environment/Architecture", "Environment/Props", "Environment/Vegetation",
                "Environment/Water", "Environment/Decals", "Lighting", "Volumes", "Gameplay", "Debug" })
                Assert.That(root.Find(path), Is.Not.Null, path);
            foreach (string path in new[] { "Environment/Props/Yard Gantry Crane", "Environment/Props/Door Frames/Hangar Door Frame",
                "Environment/Props/Machine Room Generator", "Environment/Props/Relay Antenna", "Environment/Water/Technical Basin" })
                Assert.That(root.Find(path), Is.Not.Null, "landmark " + path);
            yield return null;
        }

        [UnityTest] public IEnumerator EveryEnvironmentSurfaceUsesAFieldMaterial()
        {
            var left = new[] { "Environment", "Lighting", "Debug" }.SelectMany(p => root.Find(p).GetComponentsInChildren<Renderer>(true)).Where(r => !(r is ParticleSystemRenderer) && r.GetComponent<TextMesh>() == null)
                .Where(r => r.sharedMaterials.Any(m => m == null || !m.name.StartsWith("M_Field_"))).Select(r => r.name).ToArray();
            Assert.That(left, Is.Empty, "greybox materials left");
            yield return null;
        }

        [UnityTest] public IEnumerator WetSurfacesDarkenAndStayMatte()
        {
            var surfaces = root.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                .Where(m => m.shader.name == "TRACE/FieldSurface" || m.shader.name == "TRACE/FieldRock").Distinct().ToArray();
            Assert.That(surfaces.Length, Is.GreaterThan(10));
            foreach (var m in surfaces)
            {
                Assert.That(m.GetFloat("_SmoothnessMax"), Is.LessThanOrEqualTo(0.5f), m.name + " dry smoothness");
                Assert.That(m.GetFloat("_WetSmoothness"), Is.InRange(0.55f, 0.7f), m.name + " wet smoothness: damp, never a mirror");
                Assert.That(m.GetFloat("_WetDarken"), Is.InRange(0.8f, 0.9f), m.name + " wet darkening");
            }
            Assert.That(surfaces.Any(m => m.GetFloat("_Wetness") >= 0.5f), Is.True, "some surfaces are wet");
            yield return null;
        }

        [UnityTest] public IEnumerator DecalsAreFewAndProjected()
        {
            var projectors = root.GetComponentsInChildren<DecalProjector>(true);
            Assert.That(projectors.Length, Is.InRange(25, 60));
            Assert.That(projectors.All(p => p.material != null && p.material.shader.name == "Shader Graphs/Decal"), Is.True);
            Assert.That(projectors.All(p => p.size.z <= 1f), Is.True, "thin projection boxes (characters walk through them)");
            yield return null;
        }

        [UnityTest] public IEnumerator LocalVolumesStayBelowTacticalFocus()
        {
            var all = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
            var focus = GameObject.Find("Tactical Focus Presentation").GetComponentsInChildren<Volume>();
            var local = root.Find("Volumes").GetComponentsInChildren<Volume>().Where(v => !v.isGlobal).ToArray();
            Assert.That(focus.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(local.Length, Is.EqualTo(4), "interior (x2), damp low area, Trace");
            float floor = focus.Min(v => v.priority);
            Assert.That(all.Where(v => !focus.Contains(v)).All(v => v.priority < floor), Is.True, "Tactical Focus keeps the highest priority");
            foreach (var v in local)
            {
                var box = v.GetComponent<BoxCollider>();
                Assert.That(box != null && box.isTrigger, Is.True, v.name + " trigger shape");
                Assert.That(v.gameObject.layer, Is.EqualTo(2), v.name + " on Ignore Raycast");
            }
            var data = UnityEngine.Camera.main.GetComponent<UniversalAdditionalCameraData>();
            Assert.That((data.volumeLayerMask & (1 << 2)) != 0 && (data.volumeLayerMask & 1) != 0, Is.True, "the camera sees local and global volumes");
            yield return null;
        }

        [UnityTest] public IEnumerator DressingHasNoCollidersExceptTrunks()
        {
            foreach (string path in new[] { "Environment/Vegetation", "Environment/Decals", "Environment/Water/Puddles" })
            {
                var colliders = root.Find(path).GetComponentsInChildren<Collider>(true).Where(c => c.name != "Trunk").ToArray();
                Assert.That(colliders, Is.Empty, path);
            }
            Assert.That(root.Find("Environment/Water/Technical Basin/Water Surface").GetComponent<Collider>(), Is.Null);
            yield return null;
        }

        [UnityTest] public IEnumerator AtmosphereStaysLight()
        {
            Assert.That(RenderSettings.fog, Is.True);
            Assert.That(RenderSettings.fogMode, Is.EqualTo(FogMode.Exponential));
            Assert.That(RenderSettings.fogDensity, Is.LessThan(0.008f));
            Assert.That(RenderSettings.skybox, Is.Not.Null);
            var probes = root.GetComponentsInChildren<ReflectionProbe>();
            Assert.That(probes.Length, Is.InRange(2, 5));
            Assert.That(probes.All(p => p.mode == ReflectionProbeMode.Realtime && p.refreshMode == ReflectionProbeRefreshMode.OnAwake), Is.True, "captured once");
            var particles = root.GetComponentsInChildren<ParticleSystem>();
            Assert.That(particles.Length, Is.LessThanOrEqualTo(6));
            Assert.That(particles.Sum(p => p.main.maxParticles), Is.LessThan(400));
            yield return null;
        }

        [UnityTest] public IEnumerator AudioPlaceholdersAreMarked()
        {
            var markers = root.GetComponentsInChildren<FieldMarker>().Where(m => m.MarkerKind == FieldMarker.Kind.Audio).Select(m => m.name).ToArray();
            foreach (string kind in new[] { "Ventilation", "Water", "Machinery", "Wind Corridor", "Electrical Room" })
                Assert.That(markers.Any(m => m.Contains(kind)), Is.True, kind);
            yield return null;
        }
    }
}
