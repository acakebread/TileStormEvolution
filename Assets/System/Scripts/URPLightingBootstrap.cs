using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MassiveHadronLtd
{
	[DefaultExecutionOrder(-1000)]
	public class URPLightingBootstrap : MonoBehaviour
	{
		private static URPLightingBootstrap _instance;
		private Camera _bootstrapCamera;
		private Material _dummyLitMaterial;
		private Mesh _quadMesh;
		private CommandBuffer _cmd;

		private void Awake()
		{
			if (_instance != null)
			{
				Destroy(gameObject);
				return;
			}
			_instance = this;

			// Create hidden camera
			GameObject camGO = new GameObject("URP_LightingBootstrapCamera");
			_bootstrapCamera = camGO.AddComponent<Camera>();
			_bootstrapCamera.enabled = true;
			_bootstrapCamera.clearFlags = CameraClearFlags.SolidColor;
			_bootstrapCamera.backgroundColor = Color.black;
			_bootstrapCamera.cullingMask = 0; // render nothing else
			_bootstrapCamera.nearClipPlane = 0.1f;
			_bootstrapCamera.farClipPlane = 1f;
			_bootstrapCamera.transform.position = new Vector3(0, 0, -0.5f);

			_bootstrapCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();

			// Create a tiny quad mesh
			_quadMesh = new Mesh();
			_quadMesh.vertices = new Vector3[]
			{
				new Vector3(-0.1f, -0.1f, 0f),
				new Vector3(0.1f, -0.1f, 0f),
				new Vector3(-0.1f, 0.1f, 0f),
				new Vector3(0.1f, 0.1f, 0f)
			};
			_quadMesh.uv = new Vector2[]
			{
				new Vector2(0,0),
				new Vector2(1,0),
				new Vector2(0,1),
				new Vector2(1,1)
			};
			_quadMesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
			_quadMesh.RecalculateNormals();

			// Use a Lit material so URP binds lighting constants
			_dummyLitMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
			_dummyLitMaterial.color = Color.white;

			// Setup a command buffer to force draw directly
			_cmd = new CommandBuffer { name = "URP Lighting Bootstrap Draw" };
		}

		private void LateUpdate()
		{
			if (_bootstrapCamera == null || _dummyLitMaterial == null || _quadMesh == null)
				return;

			_cmd.Clear();

			// Draw quad directly into camera target, ignoring culling masks
			_cmd.DrawMesh(_quadMesh, Matrix4x4.identity, _dummyLitMaterial);
			Graphics.ExecuteCommandBuffer(_cmd);
		}

		private void OnDestroy()
		{
			if (_cmd != null)
				_cmd.Release();
			if (_bootstrapCamera != null)
				Destroy(_bootstrapCamera.gameObject);
			if (_dummyLitMaterial != null)
				Destroy(_dummyLitMaterial);
			if (_quadMesh != null)
				Destroy(_quadMesh);
		}
	}
}