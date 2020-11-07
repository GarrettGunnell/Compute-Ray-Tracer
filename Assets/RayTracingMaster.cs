using System.Runtime;
using System.Collections.Generic;
using UnityEngine;

public class RayTracingMaster : MonoBehaviour {
    
    public ComputeShader RayTracingShader;
    public ComputeShader RandomSpheresShader;
    public Texture SkyboxTexture;
    public Light DirectionalLight;

    public Vector2 sphereRadius = new Vector2(3.0f, 8.0f);
    public uint maxSpheres = 100;
    public float spherePlacementRadius = 100.0f;
    public enum LuminanceMode {Alpha, Green, Calculate }
    public LuminanceMode luminanceSource;
    [Range(0.0312f, 0.0833f)]
    public float contrastThreshold = 0.0312f;
    [Range(0.063f, 0.333f)]
    public float relativeThreshold = 0.063f;
    [Range(0f, 1f)]
    public float subpixelBlending = 1f;

    private ComputeBuffer _SphereBuffer;
    private RenderTexture target;
    private Material aliasing;

    const int LUMINANCE_PASS = 0;
    const int FXAA_PASS = 1;

    struct Sphere {
        public Vector3 position;
        public float radius;
        public Vector3 albedo;
        public Vector3 specular;
    }

    private void OnEnable() {
        SetUpSceneWithCompute();
    }

    private void SetUpSceneWithCompute() {
        Sphere[] sphereData = new Sphere[maxSpheres];

        _SphereBuffer = new ComputeBuffer(sphereData.Length, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Sphere)));
        int kernel = RandomSpheresShader.FindKernel("InitSpheres");
        RandomSpheresShader.SetVector("_SphereRadius", sphereRadius);
        RandomSpheresShader.SetFloat("_PlacementRadius", spherePlacementRadius);
        RandomSpheresShader.SetInt("_Seed", Random.Range(2, 1000));
        RandomSpheresShader.SetBuffer(kernel, "_SphereBuffer", _SphereBuffer);
        RandomSpheresShader.Dispatch(kernel, 8, 1, 1);

        kernel = RandomSpheresShader.FindKernel("DiscardSpheres");
        RandomSpheresShader.SetBuffer(kernel, "_SphereBuffer", _SphereBuffer);
        RandomSpheresShader.Dispatch(kernel, 8, 1, 1);
    }

    private void OnDisable() {
        if (_SphereBuffer != null) {
            _SphereBuffer.Release();
        }
    }

    private void Update() {
        if (DirectionalLight.transform.hasChanged) {
            Vector3 l = DirectionalLight.transform.forward;
            RayTracingShader.SetVector("_DirectionalLight", new Vector4(l.x, l.y, l.z, DirectionalLight.intensity));
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        InitShaders();

        Camera camera = GetComponent<Camera>();
        RayTracingShader.SetMatrix("_CameraToWorld", camera.cameraToWorldMatrix);
        RayTracingShader.SetMatrix("_CameraInverseProjection", camera.projectionMatrix.inverse);
        RayTracingShader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
        Vector3 l = DirectionalLight.transform.forward;
        RayTracingShader.SetVector("_DirectionalLight", new Vector4(l.x, l.y, l.z, DirectionalLight.intensity));
        RayTracingShader.SetBuffer(0, "_Spheres", _SphereBuffer);

        RayTracingShader.SetTexture(0, "Result", target);
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        RayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        HandleAliasing(destination);
    }

    private void InitShaders() {
        if (target == null || target.width != Screen.width || target.height != Screen.height) {
            if (target != null) target.Release();

            target = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.enableRandomWrite = true;
            target.Create();
        }

        if (aliasing == null) aliasing = new Material(Shader.Find("Hidden/FXAA"));
    }

    private void HandleAliasing(RenderTexture destination) {
        aliasing.SetFloat("_ContrastThreshold", contrastThreshold);
        aliasing.SetFloat("_RelativeThreshold", relativeThreshold);
        aliasing.SetFloat("_SubpixelBlending", subpixelBlending);

        if (luminanceSource == LuminanceMode.Calculate) {
            aliasing.DisableKeyword("LUMINANCE_GREEN");
            RenderTexture luminanceTex = RenderTexture.GetTemporary(target.width, target.height, 0, target.format);
            Graphics.Blit(target, luminanceTex, aliasing, LUMINANCE_PASS);
            Graphics.Blit(luminanceTex, destination, aliasing, FXAA_PASS);
            RenderTexture.ReleaseTemporary(luminanceTex);
        } else {
            if (luminanceSource == LuminanceMode.Green)
                aliasing.EnableKeyword("LUMINANCE_GREEN");
            else
                aliasing.DisableKeyword("LUMINANCE_GREEN");

            Graphics.Blit(target, destination, aliasing, FXAA_PASS);
        }
    }
}
