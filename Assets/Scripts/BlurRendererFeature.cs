using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class BlurRendererFeature : ScriptableRendererFeature
{
    class BlurPass : ScriptableRenderPass
    {
        public Material material;

        RTHandle source;
        RTHandle tempTexture;

        public void Setup(RTHandle source)
        {
            this.source = source;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;

            RenderingUtils.ReAllocateIfNeeded(
                ref tempTexture,
                desc,
                name: "_TempBlurTexture"
            );
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null) return;

            CommandBuffer cmd = CommandBufferPool.Get("Blur Pass");

            // source -> temp
            Blit(cmd, source, tempTexture, material);

            // temp -> source
            Blit(cmd, tempTexture, source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // RTHandle auto managed
        }
    }

    public Material blurMaterial;
    BlurPass blurPass;

    public override void Create()
    {
        blurPass = new BlurPass();

        // 🔥 مهم‌ترین خط
        blurPass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (blurMaterial == null) return;

        blurPass.material = blurMaterial;

        // 👇 نسخه جدید (به جای cameraColorTarget)
        blurPass.Setup(renderer.cameraColorTargetHandle);

        renderer.EnqueuePass(blurPass);
    }
}