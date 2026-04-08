#if ENABLE_RENDER_PIPELINE_UNIVERSAL
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Moments
{
    public class RecorderURPFeature : ScriptableRendererFeature
    {
        class RecorderURPPass : ScriptableRenderPass
        {
            const string ProfilerTag = "Moments Recorder Capture";
            Recorder m_Recorder;

            private class UnsafePassData
            {
                internal TextureHandle source;
            }

            public RecorderURPPass()
            {
                base.profilingSampler = new ProfilingSampler(ProfilerTag);
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            }

            public void Setup(Recorder recorder)
            {
                m_Recorder = recorder;
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_Recorder == null || m_Recorder.State != RecorderState.Recording)
                    return;

                RenderTargetIdentifier source = new RenderTargetIdentifier(renderingData.cameraData.renderer.cameraColorTargetHandle);

                CommandBuffer cmd = CommandBufferPool.Get(ProfilerTag);
                m_Recorder.CaptureFrame(cmd, source);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (m_Recorder == null || m_Recorder.State != RecorderState.Recording)
                    return;

                string passName = "Moments Recorder Capture";

                using (var builder = renderGraph.AddUnsafePass<UnsafePassData>(passName, out var passData, profilingSampler))
                {
                    UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                    passData.source = resourceData.activeColorTexture;

                    builder.UseTexture(passData.source);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((UnsafePassData data, UnsafeGraphContext unsafeContext) =>
                    {
                        CommandBuffer unsafeCmd = CommandBufferHelpers.GetNativeCommandBuffer(unsafeContext.cmd);
                        RenderTargetIdentifier source = data.source;
                        m_Recorder.CaptureFrame(unsafeCmd, source);
                    });
                }
            }
        }

        RecorderURPPass m_ScriptablePass;

        public override void Create()
        {
            m_ScriptablePass = new RecorderURPPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!renderingData.cameraData.camera.TryGetComponent<Recorder>(out var recorder))
                return;

            m_ScriptablePass.Setup(recorder);
            renderer.EnqueuePass(m_ScriptablePass);
        }
    }
}
#endif
