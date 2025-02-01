using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


// My edit below, for original script please go to https://github.com/sebastianhein/urp_kawase_blur 
public class KawaseBlur : ScriptableRendererFeature
{
    [System.Serializable]
    public class KawaseBlurSettings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        public Material blurMaterial = null;

        [Range(2, 15)]
        public int blurPasses = 1;

        [Range(1, 4)]
        public int downsample = 1;
        public bool copyToFramebuffer;
        public string targetName = "_blurTexture";
    }

    public KawaseBlurSettings settings = new KawaseBlurSettings();

    class CustomRenderPass : ScriptableRenderPass
    {
        public Material blurMaterial;
        public int passes;
        public int downsample;
        public bool copyToFramebuffer;
        public string targetName;
        private string profilerTag;

        private RTHandle tmpRT1;
        private RTHandle tmpRT2;
        private RTHandle source;

        public CustomRenderPass(string profilerTag)
        {
            this.profilerTag = profilerTag;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            var width = cameraTextureDescriptor.width / downsample;
            var height = cameraTextureDescriptor.height / downsample;

            tmpRT1 = RTHandles.Alloc(width, height, depthBufferBits: DepthBits.None, colorFormat: GraphicsFormat.R8G8B8A8_UNorm, filterMode: FilterMode.Bilinear);
            tmpRT2 = RTHandles.Alloc(width, height, depthBufferBits: DepthBits.None, colorFormat: GraphicsFormat.R8G8B8A8_UNorm, filterMode: FilterMode.Bilinear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(profilerTag);

            if (source == null)
            {
                source = renderingData.cameraData.renderer.cameraColorTargetHandle; // Use cameraColorTargetHandle
            }

            cmd.SetGlobalFloat("_offset", 1.5f);
            cmd.Blit(source, tmpRT1, blurMaterial);

            for (var i = 1; i < passes - 1; i++)
            {
                cmd.SetGlobalFloat("_offset", 0.5f + i);
                cmd.Blit(tmpRT1, tmpRT2, blurMaterial);

                // Ping-pong
                (tmpRT1, tmpRT2) = (tmpRT2, tmpRT1);
            }

            cmd.SetGlobalFloat("_offset", 0.5f + passes - 1f);
            if (copyToFramebuffer)
            {
                cmd.Blit(tmpRT1, source, blurMaterial);
            }
            else
            {
                cmd.Blit(tmpRT1, tmpRT2, blurMaterial);
                cmd.SetGlobalTexture(targetName, tmpRT2);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            CommandBufferPool.Release(cmd);
        }


        public override void FrameCleanup(CommandBuffer cmd)
        {
            tmpRT1?.Release();
            tmpRT2?.Release();
        }
    }

    CustomRenderPass scriptablePass;

    public override void Create()
    {
        scriptablePass = new CustomRenderPass("KawaseBlur");
        scriptablePass.blurMaterial = settings.blurMaterial;
        scriptablePass.passes = settings.blurPasses;
        scriptablePass.downsample = settings.downsample;
        scriptablePass.copyToFramebuffer = settings.copyToFramebuffer;
        scriptablePass.targetName = settings.targetName;

        scriptablePass.renderPassEvent = settings.renderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.blurMaterial == null)
        {
            Debug.LogWarning("Missing blur material. KawaseBlur pass will not execute.");
            return;
        }

        renderer.EnqueuePass(scriptablePass);
    }
}
