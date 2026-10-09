using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TRACE.Rendering
{
    // URP renderer feature: redraws the renderers that carry a Tactical rendering layer with that tier's outline
    // material (override material, no extra renderer, no material changes on the enemies). One raster pass after
    // transparents, depth-tested against the camera depth. Skipped entirely while the global fade is zero.
    public sealed class TacticalOutlineFeature : ScriptableRendererFeature
    {
        [Serializable]
        public sealed class Tier
        {
            public string name = "";
            public Material material;
            [Range(0, 31)] public int renderingLayer;
        }

        public static readonly int FadeId = Shader.PropertyToID("_TacticalOutlineFade");
        public const int MaxTiers = 4;

        [SerializeField] private Tier[] tiers = Array.Empty<Tier>();
        [SerializeField] private RenderPassEvent passEvent = RenderPassEvent.AfterRenderingTransparents;
        private OutlinePass pass;

        public IReadOnlyList<Tier> Tiers => tiers;

        public override void Create()
        {
            pass = new OutlinePass(tiers) { renderPassEvent = passEvent };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (Shader.GetGlobalFloat(FadeId) <= 0.001f) return;
            var type = renderingData.cameraData.cameraType;
            if (type == CameraType.Preview || type == CameraType.Reflection) return;
            renderer.EnqueuePass(pass);
        }

        private sealed class PassData
        {
            public readonly RendererListHandle[] lists = new RendererListHandle[MaxTiers];
            public int count;
        }

        private sealed class OutlinePass : ScriptableRenderPass
        {
            private static readonly List<ShaderTagId> Tags = new List<ShaderTagId>
            {
                new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly"), new ShaderTagId("SRPDefaultUnlit"),
            };
            private readonly Tier[] tiers;

            public OutlinePass(Tier[] tiers) => this.tiers = tiers;

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var rendering = frameData.Get<UniversalRenderingData>();
                var camera = frameData.Get<UniversalCameraData>();
                var lights = frameData.Get<UniversalLightData>();
                using var builder = renderGraph.AddRasterRenderPass<PassData>("TRACE Tactical Outline", out var data);
                data.count = 0;
                foreach (var tier in tiers)
                {
                    if (tier == null || tier.material == null || data.count >= MaxTiers) continue;
                    var drawing = RenderingUtils.CreateDrawingSettings(Tags, rendering, camera, lights, SortingCriteria.CommonOpaque);
                    drawing.overrideMaterial = tier.material;
                    drawing.overrideMaterialPassIndex = 0;
                    var filtering = new FilteringSettings(RenderQueueRange.opaque, -1, 1u << tier.renderingLayer);
                    var list = renderGraph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, filtering));
                    data.lists[data.count++] = list;
                    builder.UseRendererList(list);
                }
                builder.SetRenderAttachment(resources.activeColorTexture, 0);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                builder.SetRenderFunc(static (PassData passData, RasterGraphContext context) =>
                {
                    for (int i = 0; i < passData.count; i++) context.cmd.DrawRendererList(passData.lists[i]);
                });
            }
        }
    }
}
