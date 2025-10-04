using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Camera))]
public class DualCameraRenderToTexture : MonoBehaviour
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
//public class DualCameraRenderToTexture : MonoBehaviour
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
//	private Camera reflectionCameraMain;
//	//public Camera renderCamera;
//	//private Camera reflectionCameraRender;
//	public RenderTexture renderTexture;

//	private Mesh effectMesh;
//	private Material effectMaterial;
//	private bool isMaterialDynamic;

//	[SerializeField] private Vector3 planeNormal = Vector3.up;
//	[SerializeField] private float offset = 0f;

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

//		mainCamera.depth = -5;

//		// Allocate render texture with depth buffer
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
//		isMaterialDynamic = true;

//		{
//			var obj = new GameObject("ReflectionCameraMain");// { hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector };
//			obj.transform.SetParent(transform, false);
//			reflectionCameraMain = obj.AddComponent<Camera>();
//			reflectionCameraMain.clearFlags = CameraClearFlags.Nothing;
//			reflectionCameraMain.cullingMask = mainCamera.cullingMask;
//			reflectionCameraMain.depth = -1;
//			reflectionCameraMain.enabled = true;
//			reflectionCameraMain.targetTexture = null;

//			var provider = obj.AddComponent<CameraCommandProvider>();
//			if (provider == null)
//			{
//				Debug.LogError("ReflectionEffectCamera: Failed to add CameraCommandProvider to ReflectionCameraMain", this);
//				enabled = false;
//				return;
//			}

//			provider.RegisterCommand(RenderPassEvent.BeforeRendering, (cmd, cam) => { cmd.SetInvertCulling(true); });
//			provider.RegisterCommand(RenderPassEvent.AfterRendering, (cmd, cam) => { cmd.SetInvertCulling(false); });

//			var data = obj.AddComponent<UniversalAdditionalCameraData>();
//			data.renderType = CameraRenderType.Overlay;

//			// Set clearDepth in a safe, “Inspector-like” way
//			URPCameraHelper.SetClearDepth(data, false);
//		}

//		{
//			CameraCommandProvider provider = mainCamera.gameObject.GetComponent<CameraCommandProvider>();
//			if (provider == null)
//				provider = mainCamera.gameObject.AddComponent<CameraCommandProvider>();

//			provider.RegisterCommand(RenderPassEvent.AfterRendering,
//				(cmd, cam) =>
//				{
//					FrustumPlaneIntersection.GenerateFrustumPlaneIntersectionMesh(mainCamera, planeNormal, offset, effectMesh);
//					if (effectMesh != null && effectMesh.vertexCount >= 3 && effectMesh.triangles.Length >= 3 && effectMaterial != null)
//					{
//						effectMaterial.SetPass(0);
//						cmd.DrawMesh(effectMesh, Matrix4x4.identity, effectMaterial, 0, 0);
//					}
//				}
//			);

//			var data = mainCamera.gameObject.GetComponent<UniversalAdditionalCameraData>();
//			data.cameraStack.Clear();
//			data.cameraStack.Add(reflectionCameraMain);
//		}

//		//{
//		//	var obj = new GameObject("RenderCamera");// { hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector };
//		//	obj.transform.SetParent(transform, false);
//		//	renderCamera = obj.AddComponent<Camera>();
//		//	renderCamera.CopyFrom(mainCamera);
//		//	renderCamera.clearFlags = CameraClearFlags.Skybox;
//		//	renderCamera.cullingMask = mainCamera.cullingMask;
//		//	renderCamera.depth = -2;
//		//	renderCamera.enabled = true;
//		//	renderCamera.targetTexture = renderTexture;
//		//	var data = obj.AddComponent<UniversalAdditionalCameraData>();
//		//	data.renderType = CameraRenderType.Base;

//		//	var renderProvider = obj.AddComponent<CameraCommandProvider>();
//		//	if (renderProvider == null)
//		//	{
//		//		Debug.LogError("ReflectionEffectCamera: Failed to add CameraCommandProvider to ReflectionCameraRender", this);
//		//		enabled = false;
//		//		return;
//		//	}

//		//	renderProvider.RegisterCommand(RenderPassEvent.BeforeRendering, (cmd, cam) => { cmd.SetInvertCulling(true); });
//		//	renderProvider.RegisterCommand(RenderPassEvent.AfterRendering, (cmd, cam) => { cmd.SetInvertCulling(false); });

//		//	//{
//		//	//	var renderObj = new GameObject("ReflectionCameraRender");// { hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector };
//		//	//	renderObj.transform.SetParent(renderCamera.transform, false);
//		//	//	reflectionCameraRender = renderObj.AddComponent<Camera>();
//		//	//	reflectionCameraRender.CopyFrom(renderCamera);
//		//	//	reflectionCameraRender.clearFlags = CameraClearFlags.Nothing;
//		//	//	reflectionCameraRender.cullingMask = mainCamera.cullingMask;

//		//	//	var renderData = renderObj.AddComponent<UniversalAdditionalCameraData>();
//		//	//	renderData.renderType = CameraRenderType.Overlay;

//		//	//	// Set clearDepth in a safe, “Inspector-like” way
//		//	//	URPCameraHelper.SetClearDepth(renderData, false);
//		//	//}

//		//	var base_data = obj.GetComponent<UniversalAdditionalCameraData>();
//		//	if (base_data == null)
//		//		base_data = obj.AddComponent<UniversalAdditionalCameraData>();

//		//	//base_data.cameraStack.Clear();
//		//	//base_data.cameraStack.Add(reflectionCameraRender);
//		//}
//	}

//	public void Update()
//	{
//		effectMaterial.SetTexture("_MainTex", renderTexture);
//		effectMaterial.SetFloat("_Radius", frostRadius);
//		effectMaterial.SetColor("_BaseColor", baseColor);
//	}

//	private void LateUpdate()
//	{
//		if (reflectionCameraMain != null)
//		{
//			//SyncCameraProperties(mainCamera, reflectionCameraMain);
//			var reflectionMat = MatrixUtils.GetReflectionMatrix(planeNormal, offset);
//			reflectionCameraMain.worldToCameraMatrix = mainCamera.worldToCameraMatrix * reflectionMat;
//			reflectionCameraMain.projectionMatrix = mainCamera.projectionMatrix;
//		}

//		//if (reflectionCameraRender != null)
//		//{
//		//	//SyncCameraProperties(mainCamera, reflectionCameraRender);
//		//	var reflectionMat = MatrixUtils.GetReflectionMatrix(planeNormal, offset);
//		//	reflectionCameraRender.worldToCameraMatrix = mainCamera.worldToCameraMatrix * reflectionMat;
//		//	reflectionCameraRender.projectionMatrix = mainCamera.projectionMatrix;
//		//}

//		//if (renderCamera != null)
//		//{
//		//	//SyncCameraProperties(mainCamera, reflectionCameraRender);
//		//	var reflectionMat = MatrixUtils.GetReflectionMatrix(planeNormal, offset);
//		//	renderCamera.worldToCameraMatrix = mainCamera.worldToCameraMatrix * reflectionMat;
//		//	renderCamera.projectionMatrix = mainCamera.projectionMatrix;
//		//}

//	}

//	void OnDestroy()
//	{
//		//if (renderCamera != null)
//		//	renderCamera.targetTexture = null;

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