using UnityEngine;

public class RayTracingMaster : MonoBehaviour {
    
    public ComputeShader RayTracingShader;

    private RenderTexture target;

    private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        InitRenderTexture();
    }

    private void InitRenderTexture() {
        if (target == null || target.width != Screen.width || target.height != Screen.height) {
            if (target != null) target.Release();

            target = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.enableRandomWrite = true;
            target.Create();
        }
    }
}
