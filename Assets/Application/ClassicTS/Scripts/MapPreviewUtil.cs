using MassiveHadronLtd;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ClassicTilestorm
{
	public static class MapPreviewUtil
	{
		private static GameObject root;
		private static GameObject camGO;
		private static Camera previewCam;
		private static RenderTexture renderTexture;
		private static Mesh groundMesh;
		private static Material groundMat;
		private static Texture2D groundTex;

		private static GameObject previewRoot;
		public static Transform previewMapRoot;

		public static int previewLayer = -1;
		public const string PREVIEW_LAYER_NAME = "Preview";

		public static Camera PreviewCamera => previewCam;
		public static RenderTexture PreviewRenderTexture => renderTexture;
		public static Transform PreviewMapRoot => previewMapRoot;

		public static void SetPreviewLayer(int layer)
		{
			previewLayer = layer;
		}

		private static void EnsurePreviewRoot()
		{
			if (previewRoot != null) return;

			previewRoot = new GameObject("PreviewSceneRoot");
			previewRoot.transform.SetParent(root.transform);
			previewMapRoot = new GameObject("MapCopy").transform;
			previewMapRoot.SetParent(previewRoot.transform);
			previewMapRoot.localPosition = Vector3.zero;
		}

		public static void ClearPreviewMap()
		{
			if (previewMapRoot != null)
			{
				foreach (Transform child in previewMapRoot)
				{
					if (child != null) Object.DestroyImmediate(child.gameObject);
				}
			}
		}

		private static Map currentMap;
		public static void Initialize(Map _map = null)
		{
			if (_map != null) currentMap = _map;

			if (root != null) return;

			root = new GameObject("MAP_PREVIEW_ROOT");

			int previewLayerIndex = LayerMask.NameToLayer(PREVIEW_LAYER_NAME);
			if (previewLayerIndex < 0)
			{
				Debug.LogError($"Layer '{PREVIEW_LAYER_NAME}' not found in Tags and Layers!");
				previewLayerIndex = 0; // fallback
			}

			previewLayer = 1 << previewLayerIndex;

			// Root camera setup
			camGO = new GameObject("PreviewCamera");
			camGO.transform.SetParent(root.transform);

			previewCam = camGO.AddComponent<Camera>();
			previewCam.clearFlags = CameraClearFlags.SolidColor;
			previewCam.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
			previewCam.cullingMask = 1 << previewLayerIndex;
			previewCam.nearClipPlane = 0.1f;
			previewCam.farClipPlane = 2000f;

			var reflectionEffect = camGO.AddComponent<ReflectionEffectCamera>();
			reflectionEffect.SetEffectMode(ReflectionEffectCamera.EffectMode.Water);
			reflectionEffect.SetOffset(-0.2f);

			var previewSkyMat = SkyboxUtility.GetSkyboxMaterialForName(_map?.skybox);
			if (previewSkyMat != null)
				reflectionEffect.SetSkyboxOverride(previewSkyMat);
			else
				Debug.LogWarning($"Preview skybox not found for '{_map?.skybox}' — falling back to global.");

			// Dedicated preview light
			var lightGO = new GameObject("PreviewDirectionalLight");
			lightGO.transform.SetParent(root.transform);
			lightGO.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);

			var previewLight = lightGO.AddComponent<Light>();
			previewLight.type = LightType.Directional;
			previewLight.intensity = 0.5f;
			previewLight.color = new Color(0.75f, 0.75f, 0.75f);
			previewLight.shadows = LightShadows.Soft;
			previewLight.shadowStrength = 0.75f;
			previewLight.renderMode = LightRenderMode.ForcePixel;
			previewLight.cullingMask = 1 << previewLayerIndex;

			// Render Texture
			renderTexture = new RenderTexture(512, 320, 24, RenderTextureFormat.ARGB32)
			{
				filterMode = FilterMode.Bilinear
			};
			renderTexture.Create();
			previewCam.targetTexture = renderTexture;

			//CreateGroundPlane();
			EnsurePreviewRoot();
		}

		public static void SetSkyboxOverride(Material value)
		{
			var reflectionEffect = camGO?.GetComponent<ReflectionEffectCamera>();
			if (reflectionEffect != null)
				reflectionEffect.SetSkyboxOverride(value);
		}

		private static void CreateGroundPlane()
		{
			groundMesh = new Mesh
			{
				vertices = new[]
				{
					new Vector3(-1000f, -0.02f, -1000f),
					new Vector3(-1000f, -0.02f,  1000f),
					new Vector3( 1000f, -0.02f,  1000f),
					new Vector3( 1000f, -0.02f, -1000f),
				},
				triangles = new[] { 0, 1, 2, 0, 2, 3 },
				uv = new[]
				{
					new Vector2(0,   0),
					new Vector2(0,  64),
					new Vector2(64, 64),
					new Vector2(64,  0),
				}
			};
			groundMesh.RecalculateNormals();
			groundMesh.hideFlags = HideFlags.HideAndDontSave;

			groundTex = TextureUtils.GenerateSeamlessValueNoise(256, 0.28f, 0.42f);
			groundTex.hideFlags = HideFlags.HideAndDontSave;

			var shader = Shader.Find("Universal Render Pipeline/Unlit");
			if (shader == null)
			{
				Debug.LogError("URP/Unlit shader not found");
				return;
			}

			groundMat = new Material(shader)
			{
				hideFlags = HideFlags.HideAndDontSave
			};
			groundMat.SetFloat("_Surface", 0f);
			groundMat.SetTexture("_BaseMap", groundTex);
			groundMat.SetColor("_BaseColor", Color.white * 0.92f);

			// Future command buffer support (kept commented)
			/*
            var provider = camGO.AddComponent<CameraCommandProvider>();
            provider.RegisterCommand(
                RenderPassEvent.BeforeRenderingOpaques,
                (cmd, cam) =>
                {
                    if (groundMesh == null || groundMat == null) return;
                    Matrix4x4 matrix = Matrix4x4.Translate(Vector3.down * 0.01f);
                    groundMat.SetPass(0);
                    cmd.DrawMesh(groundMesh, matrix, groundMat, 0, 0);
                });
            */
		}

		public static void Render()
		{
			if (previewCam != null && previewCam.isActiveAndEnabled)
				previewCam.Render();
		}

		public static void Cleanup()
		{
			if (renderTexture != null && renderTexture.IsCreated())
			{
				renderTexture.Release();
				renderTexture = null;
			}

			if (groundMesh) Object.DestroyImmediate(groundMesh);
			if (groundMat) Object.DestroyImmediate(groundMat);
			if (groundTex) Object.DestroyImmediate(groundTex);
			if (root) Object.DestroyImmediate(root);

			root = null;
			camGO = null;
			previewCam = null;
			groundMesh = null;
			groundMat = null;
			groundTex = null;
			previewRoot = null;
			previewMapRoot = null;
		}

		// Commented command buffer helper (kept for future)
		/*
        private class CameraCommandProvider : MonoBehaviour, ICommandBufferProvider
        {
            private System.Collections.Generic.Dictionary<RenderPassEvent, System.Action<RasterCommandBuffer, Camera>> commands = new();

            public void RegisterCommand(RenderPassEvent evt, System.Action<RasterCommandBuffer, Camera> action)
            {
                commands[evt] = action;
            }

            public bool HasCommands(RenderPassEvent evt) => commands.ContainsKey(evt) && commands[evt] != null;

            public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer commandBuffer, Camera camera)
            {
                if (commands.TryGetValue(evt, out var action) && action != null)
                {
                    try { action.Invoke(commandBuffer, camera); }
                    catch (System.Exception e) { Debug.LogError($"Preview command error: {e}"); }
                }
            }

            private void OnDestroy() => commands.Clear();
        }
        */
	}

	public static class MapPreviewExtensions
	{
		public static GameObject InstantiatePreviewCopy(this Map map, Transform parent, int layer)
		{
			return map?.BuildPreviewGeometry(parent, layer);
		}
	}
}
