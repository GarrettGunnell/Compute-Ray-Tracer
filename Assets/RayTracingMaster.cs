using UnityEngine;

public class RayTracingMaster : MonoBehaviour {
    
    public ComputeShader RayTracingShader;
    public Texture SkyboxTexture;

    private RenderTexture target;
    private uint currentSample = 0;
    private Material aliasing;

    private void Update() {
        if (transform.hasChanged) {
            currentSample = 0;
            transform.hasChanged = false;
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        InitShaders();

        Camera camera = GetComponent<Camera>();
        RayTracingShader.SetMatrix("_CameraToWorld", camera.cameraToWorldMatrix);
        RayTracingShader.SetMatrix("_CameraInverseProjection", camera.projectionMatrix.inverse);
        RayTracingShader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
        RayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));

        RayTracingShader.SetTexture(0, "Result", target);
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        aliasing.SetFloat("_Sample", currentSample);


        Graphics.Blit(target, destination, aliasing);
        currentSample++;
    }

    private void InitShaders() {
        if (target == null || target.width != Screen.width || target.height != Screen.height) {
            if (target != null) target.Release();

            target = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.enableRandomWrite = true;
            target.Create();
        }

        if (aliasing == null) aliasing = new Material(Shader.Find("Hidden/AntiAliasing"));
    }
}
