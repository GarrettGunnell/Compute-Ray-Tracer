using System.Runtime;
using System.Collections.Generic;
using UnityEngine;

public class RayTracingMaster : MonoBehaviour {
    
    public ComputeShader RayTracingShader;
    public Texture SkyboxTexture;
    public Light DirectionalLight;

    public Vector2 sphereRadius = new Vector2(3.0f, 8.0f);
    public uint maxSpheres = 100;
    public float spherePlacementRadius = 100.0f;

    private ComputeBuffer _SphereBuffer;
    private RenderTexture target;
    private uint currentSample = 0;
    private Material aliasing;

    struct Sphere {
        public Vector3 position;
        public float radius;
        public Vector3 albedo;
        public Vector3 specular;
    }

    private void OnEnable() {
        currentSample = 0;
        SetUpScene();
    }

    private void SetUpScene() {
        List<Sphere> spheres = new List<Sphere>();

        for (int i = 0; i < maxSpheres; ++i) {
            Sphere sphere = new Sphere();

            sphere.radius = sphereRadius.x + Random.value * (sphereRadius.y - sphereRadius.x);
            Vector2 randomPos = Random.insideUnitCircle * spherePlacementRadius;
            sphere.position = new Vector3(randomPos.x, sphere.radius, randomPos.y);

            foreach (Sphere other in spheres) {
                float minDist = sphere.radius + other.radius;
                if (Vector3.SqrMagnitude(sphere.position - other.position) < minDist * minDist)
                    goto SkipSphere;
            }

            Color color = Random.ColorHSV();
            bool metal = Random.value < 0.5f;
            sphere.albedo = metal ? Vector3.zero : new Vector3(color.r, color.g, color.b);
            sphere.specular = metal ? new Vector3(color.r, color.g, color.b) : Vector3.one * 0.04f;

            spheres.Add(sphere);

            SkipSphere:
                continue;
        }

        _SphereBuffer = new ComputeBuffer(spheres.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Sphere)));
        _SphereBuffer.SetData(spheres);
    }

    private void OnDisable() {
        if (_SphereBuffer != null) {
            _SphereBuffer.Release();
        }
    }

    private void Update() {
        if (transform.hasChanged) {
            currentSample = 0;
            transform.hasChanged = false;
        }

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
        RayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));
        Vector3 l = DirectionalLight.transform.forward;
        RayTracingShader.SetVector("_DirectionalLight", new Vector4(l.x, l.y, l.z, DirectionalLight.intensity));
        RayTracingShader.SetBuffer(0, "_Spheres", _SphereBuffer);

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
