using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace MassiveHadronLtd
{
	public class DirectCommandBufferTest : MonoBehaviour
	{
		[Header("Required Material")]
		public Material testMaterial;

		[Header("Output UI")]
		public RawImage outputRawImage;

		[Header("Projection Camera (assign external Camera here)")]
		public Camera projectionCamera;

		[Header("Virtual Command Camera")]
		public TestCommandCamera customCam = new TestCommandCamera();

		private RenderTexture rt;
		private Mesh quadMesh;

		private void Awake()
		{
			if (testMaterial == null)
			{
				Debug.LogError("testMaterial is required!", this);
				return;
			}

			if (projectionCamera == null)
			{
				Debug.LogError("Assign a Projection Camera in Inspector!", this);
				return;
			}

			CreateQuadMesh();

			customCam.RecalculateMatrices();

			// Attach the hook to the projectionCamera GameObject
			var hook = projectionCamera.gameObject.GetComponent<CameraCommandHook>();
			if (hook == null)
			{
				hook = projectionCamera.gameObject.AddComponent<CameraCommandHook>();
			}

			// Set the hook's provider reference to THIS manager
			hook.Provider = this;

			Debug.Log($"Provider hook attached to camera: {projectionCamera.name}");
		}

		private void Start()
		{
			SetupRenderTexture();
		}

		private void Update()
		{
			customCam.position = transform.position;
			customCam.rotation = transform.rotation;
			customCam.RecalculateMatrices();
		}

		private void LateUpdate()
		{
			if (projectionCamera)
			{
				projectionCamera.Render();
				UpdateUI();
			}
		}

		private void SetupRenderTexture()
		{
			if (projectionCamera == null) return;

			projectionCamera.enabled = false;
			projectionCamera.cullingMask = 0;
			projectionCamera.clearFlags = CameraClearFlags.SolidColor;

			Color bg = projectionCamera.backgroundColor;
			if (bg.a < 1f)
			{
				bg.a = 1f;
				projectionCamera.backgroundColor = bg;
			}

			Debug.Log($"Projection camera clear color: {projectionCamera.backgroundColor}");

			rt = new RenderTexture(1024, 768, 24, RenderTextureFormat.DefaultHDR)
			{
				name = "DirectTest_RT",
				antiAliasing = 1,
				filterMode = FilterMode.Bilinear
			};
			rt.Create();

			projectionCamera.targetTexture = rt;
			projectionCamera.aspect = rt.width / (float)rt.height;

			if (!projectionCamera.GetComponent<UniversalAdditionalCameraData>())
			{
				projectionCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
			}

			UpdateUI();
		}

		private void UpdateUI()
		{
			if (outputRawImage && rt && rt.IsCreated())
			{
				outputRawImage.texture = rt;
				outputRawImage.color = Color.white;
			}
		}

		private void CreateQuadMesh()
		{
			quadMesh = new Mesh
			{
				vertices = new[]
				{
					new Vector3(-1f, -1f, 0f),
					new Vector3(-1f,  1f, 0f),
					new Vector3( 1f,  1f, 0f),
					new Vector3( 1f, -1f, 0f)
				},
				uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right },
				triangles = new[] { 0, 1, 2, 0, 2, 3 }
			};
			quadMesh.RecalculateBounds();
		}

		private void DrawTestQuad(RasterCommandBuffer cmd)
		{
			if (!testMaterial) return;
			Matrix4x4 objectMatrix = Matrix4x4.TRS(customCam.position, customCam.rotation, Vector3.one).inverse;
			cmd.DrawMesh(quadMesh, objectMatrix, testMaterial, 0, 0);
		}

		// ────────────────────────────────────────────────────────────────
		// Provider methods – called by the hook on the camera GameObject
		// ────────────────────────────────────────────────────────────────

		public bool HasCommands(RenderPassEvent evt)
		{
			return evt == RenderPassEvent.BeforeRenderingOpaques;
		}

		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, Camera camera)
		{
			if (evt != RenderPassEvent.BeforeRenderingOpaques) return;

			DrawTestQuad(cmd);
		}

		private void OnDestroy()
		{
			if (rt != null) rt.Release();
			if (quadMesh != null) Destroy(quadMesh);
		}
	}

	// ────────────────────────────────────────────────────────────────
	// Hook – attached to the projectionCamera GameObject
	// This is what the feature finds via GetComponent<IDirectCommandProvider>()
	// ────────────────────────────────────────────────────────────────
	internal class CameraCommandHook : MonoBehaviour, IDirectCommandProvider
	{
		public DirectCommandBufferTest Provider { get; set; }  // ← typed to your manager class

		public bool HasCommands(RenderPassEvent evt) => Provider?.HasCommands(evt) ?? false;

		public void ExecuteCommands(RenderPassEvent evt, RasterCommandBuffer cmd, Camera camera)
			=> Provider?.ExecuteCommands(evt, cmd, camera);
	}
}