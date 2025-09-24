using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Camera))]
public class SingleCameraRenderToTexture : MonoBehaviour
{
    private class CameraCommandProvider : MonoBehaviour, ICommandBufferProvider
    {
        private readonly Dictionary<RenderPassEvent, Action<RasterCommandBuffer, Camera>> commands = new();

        public void RegisterCommand(RenderPassEvent evt, Action<RasterCommandBuffer, Camera> command) => commands[evt] = command;

        public bool HasCommands(RenderPassEvent evt) => commands.ContainsKey(evt) && commands[evt] != null;

        public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer commandBuffer, Camera camera)
        {
            if (commands.ContainsKey(evt) && commands[evt] != null)
            {
                try { commands[evt].Invoke(commandBuffer, camera); }
                catch (Exception e) { Debug.LogError($"CameraCommandProvider: Error executing command for event {evt}, camera {camera.name}: {e.Message}"); }
            }
        }

        void OnDestroy() => commands.Clear();
    }

    public RenderTexture renderTexture;
    public Camera textureCamera;

	private Camera mainCamera;
    private Mesh effectMesh;
    private Material effectMaterial;
    private bool isMaterialDynamic;

    [SerializeField] private Vector3 planeNormal = Vector3.up;
    [SerializeField] private float offset = 0f;
    [SerializeField] private bool renderEffectMesh = true; // Toggle to test without effectMesh

	[SerializeField, Range(1, 120)] private float frostRadius = 64f;
	[SerializeField] private Color baseColor = new Color(0.25f, 0.25f, 0.25f, 1f);

	void Start()
    {
        mainCamera = GetComponent<Camera>();
        if (mainCamera == null)
        {
            Debug.LogError("Camera component missing.");
            enabled = false;
            return;
        }

        // Allocate render texture with depth buffer
        renderTexture = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32)
        {
            name = "RenderTexture",
            useMipMap = false,
            autoGenerateMips = false,
			filterMode = FilterMode.Bilinear,
			useDynamicScale = true
		};
        renderTexture.Create();

		{
            var obj = new GameObject("RenderCamera");// { hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector };
			obj.transform.SetParent(transform, false);
			textureCamera = obj.AddComponent<Camera>();
			textureCamera.CopyFrom(mainCamera);
			textureCamera.clearFlags = CameraClearFlags.Skybox;
			textureCamera.cullingMask = mainCamera.cullingMask;
			textureCamera.depth = 0;
			textureCamera.enabled = true;
			textureCamera.targetTexture = renderTexture;
			var data = obj.AddComponent<UniversalAdditionalCameraData>();
			data.renderType = CameraRenderType.Base;
		}

        if (renderEffectMesh)
        {
            effectMesh = new Mesh();
			effectMaterial = MaterialUtils.CreateFrostedMaterial(baseColor, frostRadius, renderTexture, null, 0);
            isMaterialDynamic = true;
            Debug.Log($"Created material with shader: {effectMaterial.shader.name}");
        }

        CameraCommandProvider provider = mainCamera.gameObject.GetComponent<CameraCommandProvider>();
        if (provider == null)
            provider = mainCamera.gameObject.AddComponent<CameraCommandProvider>();

        if (renderEffectMesh)
        {
            provider.RegisterCommand(RenderPassEvent.AfterRenderingTransparents,
                (cmd, cam) =>
                {
                    FrustumPlaneIntersection.GenerateFrustumPlaneIntersectionMesh(mainCamera, planeNormal, offset, effectMesh);

                    if (effectMesh != null && effectMesh.vertexCount >= 3 && effectMesh.triangles.Length >= 3 && effectMaterial != null)
                    {
                        effectMaterial.SetPass(0);
                        cmd.DrawMesh(effectMesh, Matrix4x4.identity, effectMaterial, 0, 0);
                    }
                }
            );
        }
    }

	public void Update()
	{
		effectMaterial.SetTexture("_MainTex", renderTexture);
		effectMaterial.SetFloat("_Radius", frostRadius);
		effectMaterial.SetColor("_BaseColor", baseColor);
	}

	void OnDestroy()
    {
        if (mainCamera != null)
            mainCamera.targetTexture = null;

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }

        if (effectMaterial != null && isMaterialDynamic)
            DestroyImmediate(effectMaterial);

        if (effectMesh != null)
            DestroyImmediate(effectMesh);
    }
}