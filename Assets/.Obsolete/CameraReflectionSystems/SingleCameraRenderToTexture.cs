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

	Camera overlayCamera;

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

		{
			var obj = new GameObject("OverlayCamera");// { hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector };
			obj.transform.SetParent(transform, false);
			overlayCamera = obj.AddComponent<Camera>();
			overlayCamera.clearFlags = CameraClearFlags.Nothing;
			overlayCamera.cullingMask = mainCamera.cullingMask;
			overlayCamera.enabled = true;
			overlayCamera.targetTexture = renderTexture;
			overlayCamera.fieldOfView = mainCamera.fieldOfView * 0.75f;

			var provider = obj.AddComponent<CameraCommandProvider>();
			if (provider == null)
			{
				Debug.LogError("ReflectionEffectCamera: Failed to add CameraCommandProvider to ReflectionCameraMain", this);
				enabled = false;
				return;
			}

			provider.RegisterCommand(RenderPassEvent.BeforeRendering, (cmd, cam) => { cmd.SetInvertCulling(true); });
			provider.RegisterCommand(RenderPassEvent.AfterRendering, (cmd, cam) => { cmd.SetInvertCulling(false); });

			var data = obj.AddComponent<UniversalAdditionalCameraData>();
			data.renderType = CameraRenderType.Overlay;

			// Set clearDepth in a safe, “Inspector-like” way
			URPCameraHelper.SetClearDepth(data, false);


			var texturecam_data = textureCamera.gameObject.GetComponent<UniversalAdditionalCameraData>();
			texturecam_data.cameraStack.Clear();
			texturecam_data.cameraStack.Add(overlayCamera);
		}

		if (renderEffectMesh)
		{
			effectMesh = new Mesh();
			effectMaterial = MaterialUtils.CreateFrostedMaterial(baseColor, frostRadius, renderTexture, null, 0);
			isMaterialDynamic = true;
			Debug.Log($"Created material with shader: {effectMaterial.shader.name}");
		}

		if (renderEffectMesh)
		{
			CameraCommandProvider provider = mainCamera.gameObject.GetComponent<CameraCommandProvider>();
			if (provider == null)
				provider = mainCamera.gameObject.AddComponent<CameraCommandProvider>();

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
		effectMaterial.SetFloat("_Radius", frostRadius);
		effectMaterial.SetColor("_BaseColor", baseColor);
	}

	private void LateUpdate()
	{
		if (overlayCamera != null)
		{
			//SyncCameraProperties(mainCamera, reflectionCameraMain);
			var reflectionMat = MatrixUtils.GetReflectionMatrix(planeNormal, offset);
			overlayCamera.worldToCameraMatrix = mainCamera.worldToCameraMatrix * reflectionMat;
			overlayCamera.projectionMatrix = mainCamera.projectionMatrix;
		}
	}

	void OnDestroy()
	{
		if (mainCamera != null)
			mainCamera.targetTexture = null;

		if (effectMaterial != null && isMaterialDynamic)
			DestroyImmediate(effectMaterial);

		if (effectMesh != null)
			DestroyImmediate(effectMesh);
	}
}


//using System;
//using UnityEngine;
//using System.Collections.Generic;
//using UnityEngine.Rendering;
//using UnityEngine.Rendering.Universal;

//[RequireComponent(typeof(Camera))]
//public class SingleCameraRenderToTexture : MonoBehaviour
//{
//	private class CameraCommandProvider : MonoBehaviour, ICommandBufferProvider
//	{
//		private readonly Dictionary<RenderPassEvent, Action<RasterCommandBuffer, Camera>> commands = new();

//		public void RegisterCommand(RenderPassEvent evt, Action<RasterCommandBuffer, Camera> command) => commands[evt] = command;

//		public bool HasCommands(RenderPassEvent evt) => commands.ContainsKey(evt) && commands[evt] != null;

//		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer commandBuffer, Camera camera)
//		{
//			if (commands.ContainsKey(evt) && commands[evt] != null)
//			{
//				try { commands[evt].Invoke(commandBuffer, camera); }
//				catch (Exception e) { Debug.LogError($"CameraCommandProvider: Error executing command for event {evt}, camera {camera.name}: {e.Message}"); }
//			}
//		}

//		void OnDestroy() => commands.Clear();
//	}

//	private Camera mainCamera;
//	public RenderTexture renderTexture;

//	private Mesh effectMesh;
//	private Material effectMaterial;
//	private bool isMaterialDynamic;

//	[SerializeField] private Vector3 planeNormal = Vector3.up;
//	[SerializeField] private float offset = 0f;
//	[SerializeField] private Material customEffectMaterial;

//	[SerializeField, Range(1, 120)] private float frostRadius = 64f;
//	[SerializeField] private Color baseColor = new Color(0.25f, 0.25f, 0.25f, 1f);

//	void Start()
//	{
//		mainCamera = GetComponent<Camera>();
//		if (mainCamera == null)
//		{
//			Debug.LogError("Camera component missing.");
//			enabled = false;
//			return;
//		}

//		// Allocate render texture with depth buffer to suppress warning
//		renderTexture = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32)
//		{
//			name = "RenderTexture",
//			useMipMap = false,
//			autoGenerateMips = false,
//			filterMode = FilterMode.Bilinear,
//			useDynamicScale = true
//		};
//		renderTexture.Create();

//		effectMesh = new Mesh();
//		effectMaterial = MaterialUtils.CreateFrostedMaterial(baseColor, frostRadius, renderTexture, null, 0);
//		isMaterialDynamic = customEffectMaterial == null; // True if using script-created material

//		if (!mainCamera.gameObject.TryGetComponent<CameraCommandProvider>(out var provider)) provider = mainCamera.gameObject.AddComponent<CameraCommandProvider>();

//		//provider.RegisterCommand(RenderPassEvent.BeforeRendering, (cmd, cam) => { mainCamera.targetTexture = renderTexture; });// First pass: reset targetTexture to render to texture

//		provider.RegisterCommand(RenderPassEvent.AfterRendering,
//			(cmd, cam) =>
//			{
//				FrustumPlaneIntersection.GenerateFrustumPlaneIntersectionMesh(mainCamera, planeNormal, offset, effectMesh);

//				// Assuming renderTexture is your RenderTexture
//				mainCamera.targetTexture = null;

//				// Blit render texture to display buffer first
//				Graphics.Blit(renderTexture, null as RenderTexture);

//				// "Lock" the renderTexture by ensuring it's not the active render target
//				RenderTexture previousActiveRT = RenderTexture.active; // Store the current active RenderTexture
//				RenderTexture.active = null; // Set active to null to prevent writes to any RenderTexture

//				if (effectMesh != null && effectMesh.vertexCount >= 3 && effectMesh.triangles.Length >= 3 && effectMaterial != null)
//				{
//					effectMaterial.SetPass(0);
//					cmd.DrawMesh(effectMesh, Matrix4x4.identity, effectMaterial, 0, 0);
//				}
//				else
//				{
//					Debug.LogWarning("SingleCameraRenderToTexture: Invalid effectMesh or effectMaterial", this);
//				}

//				// "Unlock" the renderTexture by restoring the previous active RenderTexture
//				RenderTexture.active = previousActiveRT;
//			}
//		);
//		RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
//	}

//	public void Update()
//	{
//		effectMaterial.SetFloat("_Radius", frostRadius);
//		effectMaterial.SetColor("_BaseColor", baseColor);
//	}

//	private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
//	{
//		if (camera != mainCamera) return;
//		mainCamera.targetTexture = renderTexture;
//	}

//	void OnDestroy()
//	{
//		RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
//		// Restore original culling mask
//		if (mainCamera != null)
//			mainCamera.targetTexture = null;

//		// Cleanup
//		if (renderTexture != null)
//		{
//			renderTexture.Release();
//			Destroy(renderTexture);
//		}

//		if (effectMaterial != null && isMaterialDynamic)
//			DestroyImmediate(effectMaterial);

//		if (effectMesh != null)
//			DestroyImmediate(effectMesh);
//	}
//}

