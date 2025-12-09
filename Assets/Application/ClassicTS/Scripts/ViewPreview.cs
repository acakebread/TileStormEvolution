using MassiveHadronLtd;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ClassicTilestorm
{
	public class ViewPreview : MonoBehaviour
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

		private Camera previewCam;
		private GameObject camGO;
		private RenderTexture renderTexture;
		private Rect previewRect;

		private const float PREVIEW_HEIGHT = 200f;
		private const float MARGIN = 10f;

		private View currentView;
		private IMapManager mapManager;

		// --- Ground plane resources ---
		private Mesh groundMesh;
		private Material groundMat;
		private Texture2D groundTex;

		public static ViewPreview Create()
		{
			var go = new GameObject("ViewPreview");
			DontDestroyOnLoad(go);
			return go.AddComponent<ViewPreview>();
		}

		private void Awake()
		{
			camGO = new GameObject("PreviewCamera");
			camGO.transform.SetParent(transform);
			previewCam = camGO.AddComponent<Camera>();
			previewCam.enabled = false;

			// Hide editor gizmos
			previewCam.cullingMask &= ~(1 << LayerMask.NameToLayer("Editor"));

			renderTexture = new RenderTexture(320, 200, 24, RenderTextureFormat.ARGB32);
			renderTexture.Create();
			previewCam.targetTexture = renderTexture;

			gameObject.hideFlags = HideFlags.HideAndDontSave;

			// === GROUND PLANE SETUP (URP COMPATIBLE) ===

			// Create ground mesh (plane at -0.2 Y)
			groundMesh = new Mesh();
			groundMesh.vertices = new Vector3[]
			{
				new Vector3(-512, -0.2f, -512),
				new Vector3(-512, -0.2f,  512),
				new Vector3( 512, -0.2f,  512),
				new Vector3( 512, -0.2f, -512)
			};
			groundMesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
			//float repeats = 512f / 16f;
			groundMesh.uv = new Vector2[] { new(0, 0), new(0, 16), new(16, 16), new(16, 0) };
			groundMesh.RecalculateNormals();

			// === Create noise texture ===
			groundTex = TextureUtils.GenerateSeamlessValueNoise(256, 0.25f, 0.35f);

			// === Create URP Unlit material ===
			var shader = Shader.Find("Universal Render Pipeline/Unlit");
			groundMat = new Material(shader)
			{
				hideFlags = HideFlags.HideAndDontSave,
				renderQueue = (int)RenderQueue.Geometry
			};
			groundMat.SetFloat("_Surface", 0f);   // Opaque
			groundMat.SetTexture("_BaseMap", groundTex);
			groundMat.SetColor("_BaseColor", Color.white);

			// Add custom URP command provider
			var provider = camGO.AddComponent<CameraCommandProvider>();

			// Register draw command
			provider.RegisterCommand(
				RenderPassEvent.BeforeRenderingOpaques,
				(cmd, cam) =>
				{
					if (groundMesh == null || groundMat == null) return;

					Matrix4x4 matrix = Matrix4x4.TRS(
						Vector3.zero,
						Quaternion.identity,
						Vector3.one
					);

					groundMat.SetPass(0);
					cmd.DrawMesh(groundMesh, matrix, groundMat, 0, 0);
				});
		}

		public void Show(View view, IMapManager manager)
		{
			currentView = view;
			mapManager = manager;
			gameObject.SetActive(true);
		}

		public void Hide()
		{
			currentView = null;
			mapManager = null;
			gameObject.SetActive(false);
		}

		private void LateUpdate()
		{
			if (currentView == null || mapManager == null || !gameObject.activeSelf) return;

			Vector3 worldPos = mapManager.TileWorldPosition(currentView.tile) + currentView.Position;
			previewCam.transform.position = worldPos;
			previewCam.transform.rotation = currentView.Rotation;

			previewCam.fieldOfView = View.FOV;

			previewCam.nearClipPlane = 0.1f;
			previewCam.farClipPlane = 999;

			previewCam.Render();
		}

		private void OnGUI()
		{
			if (!gameObject.activeSelf || renderTexture == null || currentView == null) return;

			float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
			float previewWidth = PREVIEW_HEIGHT * aspect;

			previewRect = new Rect(
				MARGIN,
				Screen.height - PREVIEW_HEIGHT - MARGIN,
				previewWidth,
				PREVIEW_HEIGHT
			);

			GUI.Box(new Rect(previewRect.x - 2, previewRect.y - 2, previewRect.width + 4, previewRect.height + 4), "");
			GUI.DrawTexture(previewRect, renderTexture, ScaleMode.StretchToFill, false);
			GUI.Label(new Rect(previewRect.x + 8, previewRect.y + 8, previewRect.width - 16, 20), "Camera Preview");
		}

		private void OnDestroy()
		{
			if (renderTexture != null && renderTexture.IsCreated())
				renderTexture.Release();

			if (groundMesh != null) DestroyImmediate(groundMesh);
			if (groundMat != null) DestroyImmediate(groundMat);
			if (groundTex != null) DestroyImmediate(groundTex);
		}
	}
}
