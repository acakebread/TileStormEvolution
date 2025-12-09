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
			groundTex = GenerateSeamlessValueNoise(256);// GenerateSeamlessCosineNoise(256, 9.7f, 0.05f, 0.25f); //GenerateTiledPerlin(256, 8);// GenerateDarkNoiseTexture(256, 256);

			// === Create URP Unlit material ===
			var shader = Shader.Find("Universal Render Pipeline/Unlit");
			groundMat = new Material(shader)
			{
				hideFlags = HideFlags.HideAndDontSave,
				renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry
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

		// --- Generates a 256x256 dark noise texture ---
		private Texture2D GenerateDarkNoiseTexture(int width, int height)
		{
			Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
			{
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Repeat,
				hideFlags = HideFlags.HideAndDontSave
			};

			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					float v = Mathf.PerlinNoise(x * 0.05f, y * 0.05f);
					// remap from [0,1] → [0.05,0.25]
					v = Mathf.Lerp(0.05f, 0.25f, v);
					tex.SetPixel(x, y, new Color(v, v, v, 1f));
				}
			}

			tex.Apply();
			return tex;
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

		private Texture2D GenerateTiledPerlin(int size, int period, float minBright = 0.05f, float maxBright = 0.25f)
		{
			Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
			{
				wrapMode = TextureWrapMode.Repeat,
				filterMode = FilterMode.Bilinear
			};

			for (int y = 0; y < size; y++)
			{
				float fy = (float)y / size;
				float wy = fy - Mathf.Floor(fy);
				float sy = wy * wy * (3f - 2f * wy); // smoothstep

				for (int x = 0; x < size; x++)
				{
					float fx = (float)x / size;
					float wx = fx - Mathf.Floor(fx);
					float sx = wx * wx * (3f - 2f * wx); // smoothstep

					// Sample 4 corners of a periodic patch
					float n00 = Mathf.PerlinNoise(fx * period, fy * period);
					float n10 = Mathf.PerlinNoise((fx + 1f) * period, fy * period);
					float n01 = Mathf.PerlinNoise(fx * period, (fy + 1f) * period);
					float n11 = Mathf.PerlinNoise((fx + 1f) * period, (fy + 1f) * period);

					// Bilinear blend to enforce continuity
					float nx0 = Mathf.Lerp(n00, n10, sx);
					float nx1 = Mathf.Lerp(n01, n11, sx);
					float v = Mathf.Lerp(nx0, nx1, sy);

					// dark range
					v = Mathf.Lerp(minBright, maxBright, v);

					tex.SetPixel(x, y, new Color(v, v, v, 1f));
				}
			}

			tex.Apply();
			return tex;
		}


		private Texture2D GenerateSeamlessCosineNoise(int size, float frequency, float minBright, float maxBright)
		{
			Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
			{
				wrapMode = TextureWrapMode.Repeat,
				filterMode = FilterMode.Bilinear
			};

			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float nx = (float)x / size;
					float ny = (float)y / size;

					// *** SEAMLESS procedural noise ***
					float v =
						Mathf.Sin((nx * frequency) * Mathf.PI * 2f) *
						Mathf.Sin((ny * frequency) * Mathf.PI * 2f);

					// v = [-1,1]  →  [0,1]
					v = v * 0.5f + 0.5f;

					v = Mathf.Lerp(minBright, maxBright, v);

					tex.SetPixel(x, y, new Color(v, v, v, 1));
				}
			}

			tex.Apply();
			return tex;
		}


		private Texture2D GenerateSeamlessValueNoise(int size, float minBright = 0.05f, float maxBright = 0.25f)
		{
			Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
			{
				wrapMode = TextureWrapMode.Repeat,
				filterMode = FilterMode.Bilinear
			};

			// Generate a grid of random values INCLUDING the final wrap row/column
			float[,] grid = new float[size + 1, size + 1];
			for (int y = 0; y <= size; y++)
			{
				for (int x = 0; x <= size; x++)
				{
					// wrap edges: copy from opposite side
					int rx = (x == size) ? 0 : x;
					int ry = (y == size) ? 0 : y;

					grid[x, y] = UnityEngine.Random.value;
				}
			}

			// Now fill the texture using bilinear-interpolated value noise
			for (int y = 0; y < size; y++)
			{
				float fy = (float)y / size;
				int iy = Mathf.FloorToInt(fy * size);
				float ty = fy * size - iy;

				for (int x = 0; x < size; x++)
				{
					float fx = (float)x / size;
					int ix = Mathf.FloorToInt(fx * size);
					float tx = fx * size - ix;

					float v00 = grid[ix, iy];
					float v10 = grid[ix + 1, iy];
					float v01 = grid[ix, iy + 1];
					float v11 = grid[ix + 1, iy + 1];

					// smooth interpolation
					float sx = tx * tx * (3 - 2 * tx);
					float sy = ty * ty * (3 - 2 * ty);

					float nx0 = Mathf.Lerp(v00, v10, sx);
					float nx1 = Mathf.Lerp(v01, v11, sx);
					float v = Mathf.Lerp(nx0, nx1, sy);

					v = Mathf.Lerp(minBright, maxBright, v);

					tex.SetPixel(x, y, new Color(v, v, v, 1));
				}
			}

			tex.Apply();
			return tex;
		}




	}
}



//using UnityEngine;
//using UnityEngine.Rendering.Universal;

//namespace ClassicTilestorm
//{
//	public class ViewPreview : MonoBehaviour
//	{
//		private Camera previewCam;
//		private GameObject camGO;
//		private RenderTexture renderTexture;
//		private Rect previewRect;

//		private const float PREVIEW_HEIGHT = 200f;
//		private const float MARGIN = 10f;

//		private View currentView;
//		private IMapManager mapManager;

//		// --- Ground plane resources ---
//		private Mesh groundMesh;
//		private Material groundMat;

//		public static ViewPreview Create()
//		{
//			var go = new GameObject("ViewPreview");
//			DontDestroyOnLoad(go);
//			return go.AddComponent<ViewPreview>();
//		}

//		private void Awake()
//		{
//			camGO = new GameObject("PreviewCamera");
//			camGO.transform.SetParent(transform);
//			previewCam = camGO.AddComponent<Camera>();
//			previewCam.enabled = false;

//			// Hide editor gizmos
//			previewCam.cullingMask &= ~(1 << LayerMask.NameToLayer("Editor"));

//			renderTexture = new RenderTexture(320, 200, 24, RenderTextureFormat.ARGB32);
//			renderTexture.Create();
//			previewCam.targetTexture = renderTexture;

//			gameObject.hideFlags = HideFlags.HideAndDontSave;

//			// === GROUND PLANE SETUP (URP COMPATIBLE) ===

//			// Create ground mesh
//			groundMesh = new Mesh();
//			groundMesh.vertices = new Vector3[]
//			{
//				new Vector3(-500, -0.2f, -500),
//				new Vector3(-500, -0.2f,  500),
//				new Vector3( 500, -0.2f,  500),
//				new Vector3( 500, -0.2f, -500)
//			};
//			groundMesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
//			groundMesh.RecalculateNormals();

//			// Create material
//			groundMat = new Material(Shader.Find("Unlit/Color"))
//			{
//				hideFlags = HideFlags.HideAndDontSave
//			};
//			groundMat.color = new Color(0.35f, 0.35f, 0.38f, 1f);

//			// Add custom URP command provider
//			var provider = camGO.AddComponent<MassiveHadronLtd.ReflectionEffectCamera.CameraCommandProvider>();

//			// Register draw command
//			provider.RegisterCommand(
//				RenderPassEvent.BeforeRenderingOpaques,
//				(cmd, cam) =>
//				{
//					if (groundMesh == null || groundMat == null) return;

//					var matrix = Matrix4x4.TRS(
//						Vector3.zero,
//						Quaternion.identity,
//						Vector3.one
//					);

//					groundMat.SetPass(0);
//					cmd.DrawMesh(groundMesh, matrix, groundMat, 0, 0);
//				});
//		}

//		public void Show(View view, IMapManager manager)
//		{
//			currentView = view;
//			mapManager = manager;
//			gameObject.SetActive(true);
//		}

//		public void Hide()
//		{
//			currentView = null;
//			mapManager = null;
//			gameObject.SetActive(false);
//		}

//		private void LateUpdate()
//		{
//			if (currentView == null || mapManager == null || !gameObject.activeSelf) return;

//			Vector3 worldPos = mapManager.TileWorldPosition(currentView.tile) + currentView.Position;
//			previewCam.transform.position = worldPos;
//			previewCam.transform.rotation = currentView.Rotation;

//			previewCam.fieldOfView = View.FOV;

//			previewCam.nearClipPlane = 0.1f;
//			previewCam.farClipPlane = 999;

//			previewCam.Render();
//		}

//		private void OnGUI()
//		{
//			if (!gameObject.activeSelf || renderTexture == null || currentView == null) return;

//			float aspect = Camera.main != null ? Camera.main.aspect : 16f / 9f;
//			float previewWidth = PREVIEW_HEIGHT * aspect;

//			previewRect = new Rect(
//				MARGIN,
//				Screen.height - PREVIEW_HEIGHT - MARGIN,
//				previewWidth,
//				PREVIEW_HEIGHT
//			);

//			GUI.Box(new Rect(previewRect.x - 2, previewRect.y - 2, previewRect.width + 4, previewRect.height + 4), "");
//			GUI.DrawTexture(previewRect, renderTexture, ScaleMode.StretchToFill, false);
//			GUI.Label(new Rect(previewRect.x + 8, previewRect.y + 8, previewRect.width - 16, 20), "Camera Preview");
//		}

//		private void OnDestroy()
//		{
//			if (renderTexture != null && renderTexture.IsCreated())
//				renderTexture.Release();

//			if (groundMesh != null) DestroyImmediate(groundMesh);
//			if (groundMat != null) DestroyImmediate(groundMat);
//		}
//	}
//}
