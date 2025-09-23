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

	private RenderTexture renderTexture;
	private Camera mainCamera;

	//private Camera effectCamera;// no longer needed becasuse main camer handles this
	private Mesh effectMesh;
	private Material effectMaterial;
	private bool isMaterialDynamic;

	[SerializeField] private Vector3 planeNormal = Vector3.up;
	[SerializeField] private float offset = 0f;
	[SerializeField] private Material customReflectionMaterial;

	void Start()
	{
		mainCamera = GetComponent<Camera>();
		if (mainCamera == null)
		{
			Debug.LogError("Camera component missing.");
			enabled = false;
			return;
		}

		// Allocate render texture with depth buffer to suppress warning
		renderTexture = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32)
		{
			name = "CustomRenderTexture",
			useMipMap = false,
			autoGenerateMips = false
		};
		renderTexture.Create();

		effectMesh = new Mesh();
		effectMaterial = customReflectionMaterial != null ? customReflectionMaterial : MaterialUtils.CreateTransparentUnlitMaterial(new Color(0.1f, 0.1f, 0.1f, 0.5f));
		isMaterialDynamic = customReflectionMaterial == null; // True if using script-created material

		if (!mainCamera.gameObject.TryGetComponent<CameraCommandProvider>(out var provider)) provider = mainCamera.gameObject.AddComponent<CameraCommandProvider>();

		provider.RegisterCommand(RenderPassEvent.BeforeRendering, (cmd, cam) => { mainCamera.targetTexture = renderTexture; });// First pass: reset targetTexture to render to texture

		provider.RegisterCommand(RenderPassEvent.AfterRenderingTransparents,
			(cmd, cam) =>
			{
				FrustumPlaneIntersection.GenerateFrustumPlaneIntersectionMesh(mainCamera, planeNormal, offset, effectMesh);

				mainCamera.targetTexture = null;

				// Blit render texture to display buffer first
				Graphics.Blit(renderTexture, null as RenderTexture);

				if (effectMesh != null && effectMesh.vertexCount >= 3 && effectMesh.triangles.Length >= 3 && effectMaterial != null)
				{
					effectMaterial.SetPass(0);
					cmd.DrawMesh(effectMesh, Matrix4x4.identity, effectMaterial, 0, 0);
				}
				else
				{
					Debug.LogWarning("SingleCameraRenderToTexture: Invalid effectMesh or effectMaterial", this);
				}
			}
		);
	}

	void OnDestroy()
	{
		// Restore original culling mask
		if (mainCamera != null)
			mainCamera.targetTexture = null;

		// Cleanup
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