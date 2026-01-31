using UnityEngine;
using MassiveHadronLtd;

namespace MassiveHadronLtd
{
	public static class GroundPlaneHelper
	{
		private static Mesh _sharedGroundMesh;
		private static Material _sharedGroundMaterial;
		private static Texture2D _sharedGroundTexture;

		private const string GROUND_NAME = "InfiniteGroundPlane";
		private const float GROUND_Y_OFFSET = -0.02f;
		private const float PLANE_SIZE = 2000f;     // -1000 → +1000
		private const float UV_SCALE = 64f;


		/// <summary>
		/// Creates or returns an already existing large ground plane GameObject.
		/// Returns null if creation failed (shader not found, etc.).
		/// </summary>
		public static GameObject Instantiate()
		{
			// Early out if we already have one in the scene
			var existing = GameObject.Find(GROUND_NAME);
			if (existing != null)
				return existing;

			// Lazy-create shared assets (only once)
			if (_sharedGroundMesh == null)
			{
				if (!CreateSharedGroundAssets())
					return null;
			}

			// Create actual GameObject + MeshRenderer
			var go = new GameObject(GROUND_NAME)
			{
				isStatic = true,
				layer = 0,                    // usually default / ground layer
				tag = "Untagged"
			};

			go.transform.position = new Vector3(0, GROUND_Y_OFFSET, 0);
			// go.transform.localScale = Vector3.one;   ← not needed

			var meshFilter = go.AddComponent<MeshFilter>();
			meshFilter.sharedMesh = _sharedGroundMesh;

			var meshRenderer = go.AddComponent<MeshRenderer>();
			meshRenderer.sharedMaterial = _sharedGroundMaterial;
			meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			meshRenderer.receiveShadows = true;
			meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
			meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;

			// Optional: make it easier to identify / hide in hierarchy
			go.hideFlags = HideFlags.NotEditable;

			return go;
		}


		private static bool CreateSharedGroundAssets()
		{
			// ─────────────────────────────────────
			//  Mesh
			// ─────────────────────────────────────
			_sharedGroundMesh = new Mesh
			{
				name = "GroundPlaneMesh_Infinite",
				vertices = new[]
				{
				new Vector3(-PLANE_SIZE/2, 0, -PLANE_SIZE/2),
				new Vector3(-PLANE_SIZE/2, 0,  PLANE_SIZE/2),
				new Vector3( PLANE_SIZE/2, 0,  PLANE_SIZE/2),
				new Vector3( PLANE_SIZE/2, 0, -PLANE_SIZE/2),
			},
				triangles = new[] { 0, 1, 2, 0, 2, 3 },
				uv = new[]
				{
				new Vector2(0,       0),
				new Vector2(0,       UV_SCALE),
				new Vector2(UV_SCALE, UV_SCALE),
				new Vector2(UV_SCALE, 0),
			}
			};

			_sharedGroundMesh.RecalculateNormals();
			_sharedGroundMesh.RecalculateBounds();
			_sharedGroundMesh.hideFlags = HideFlags.HideAndDontSave;


			// ─────────────────────────────────────
			//  Texture
			// ─────────────────────────────────────
			_sharedGroundTexture = TextureUtils.GenerateSeamlessValueNoise(256, 0.28f, 0.42f);
			if (_sharedGroundTexture == null)
			{
				Debug.LogError("Failed to generate ground noise texture");
				return false;
			}
			_sharedGroundTexture.hideFlags = HideFlags.HideAndDontSave;


			// ─────────────────────────────────────
			//  Material (URP Unlit)
			// ─────────────────────────────────────
			var shader = Shader.Find("Universal Render Pipeline/Unlit");
			if (shader == null)
			{
				Debug.LogError("Shader not found: Universal Render Pipeline/Unlit");
				return false;
			}

			_sharedGroundMaterial = new Material(shader)
			{
				name = "GroundPlane_Unlit",
				hideFlags = HideFlags.HideAndDontSave
			};

			_sharedGroundMaterial.SetFloat("_Surface", 0f);           // Opaque
			_sharedGroundMaterial.SetTexture("_BaseMap", _sharedGroundTexture);
			_sharedGroundMaterial.SetColor("_BaseColor", Color.white * 0.92f);

			return true;
		}


		/// <summary>
		/// Call this when you want to clean up everything (usually on domain reload / application quit / tool destroy)
		/// </summary>
		public static void Cleanup()
		{
			if (_sharedGroundMesh != null)
			{
				Object.DestroyImmediate(_sharedGroundMesh);
				_sharedGroundMesh = null;
			}

			if (_sharedGroundMaterial != null)
			{
				Object.DestroyImmediate(_sharedGroundMaterial);
				_sharedGroundMaterial = null;
			}

			if (_sharedGroundTexture != null)
			{
				Object.DestroyImmediate(_sharedGroundTexture);
				_sharedGroundTexture = null;
			}

			var existing = GameObject.Find(GROUND_NAME);
			if (existing != null)
				Object.DestroyImmediate(existing);
		}
	}
}