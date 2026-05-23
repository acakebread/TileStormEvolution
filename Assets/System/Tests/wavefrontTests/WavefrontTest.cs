using System.Collections;
using System.IO;
using UnityEngine;

namespace MassiveHadronLtd
{
	public class WavefrontTest : MonoBehaviour
	{
		[Header("Test Setup")]
		public GameObject cubePrefab;
		public Material testMaterial;
		public Texture2D testTexture;
		private GameObject testObject;
		private Material currentMaterial;
		private MeshFilter meshFilter;

		private string ObjPath => Path.Combine(Application.persistentDataPath, "TestModel.obj");
		private string MtlPath => Path.Combine(Application.persistentDataPath, "TestMaterial.mtl");

		private void Start()
		{
			CreateTestObject();
			Debug.Log($"=== Combined Wavefront Test Ready ===\nOBJ: {ObjPath}\nMTL: {MtlPath}");
		}

		private void CreateTestObject()
		{
			if (cubePrefab != null)
				testObject = Instantiate(cubePrefab);
			else
			{
				testObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
				testObject.name = "Test Wavefront Object";
			}

			testObject.transform.position = new Vector3(0, 1, 0);
			testObject.transform.localScale = Vector3.one * 2f;

			meshFilter = testObject.GetComponent<MeshFilter>();
			var renderer = testObject.GetComponent<Renderer>();

			// Store a reference but do NOT destroy the original asset material
			currentMaterial = renderer.sharedMaterial;
		}

		private void Update()
		{
			if (testObject != null)
				testObject.transform.Rotate(0, 30 * Time.deltaTime, 0);
		}

		private void OnGUI()
		{
			GUILayout.BeginArea(new Rect(20, 20, 520, 800));
			GUILayout.Label("=== Combined Wavefront Test (OBJ + MTL) ===", GUILayout.Height(40));

			if (GUILayout.Button("1. Create & Export Full Model + Material", GUILayout.Height(70)))
			{
				CreateAndExportFullModel();
			}

			if (GUILayout.Button("2. Load OBJ + MTL", GUILayout.Height(60)))
			{
				LoadFullModel();
			}

			if (GUILayout.Button("Reset", GUILayout.Height(40)))
			{
				ResetObject();
			}

			GUILayout.Space(20);
			GUILayout.Label($"OBJ: {ObjPath}");
			GUILayout.Label($"MTL: {MtlPath}");
			GUILayout.EndArea();
		}

		private void CreateAndExportFullModel()
		{
			Material sourceMat = CreateSourceMaterial();
			Texture2D testTex = ResolvePrimaryTestTexture(sourceMat);
			if (testTex != null)
				ApplyTexture(sourceMat, testTex);

			// Apply safely
			if (testObject != null)
			{
				var rend = testObject.GetComponent<Renderer>();
				if (rend != null)
				{
					SafeDestroyMaterial(currentMaterial);
					currentMaterial = sourceMat;
					rend.sharedMaterial = currentMaterial;
				}
			}

			// Export
			var wavefrontMat = new WavefrontMaterial();
			wavefrontMat.FromUnityMaterial(sourceMat, "TestMaterial");
			wavefrontMat.ExportToMtl(MtlPath);

			var wavefrontMesh = new WavefrontMesh();
			wavefrontMesh.FromUnityMesh(meshFilter.sharedMesh, "TestCube", "TestMaterial");
			wavefrontMesh.ExportToObj(ObjPath, "TestMaterial.mtl");

			Debug.Log("✅ Full model + material exported successfully!");
		}

		private void LoadFullModel()
		{
			if (!File.Exists(ObjPath) || !File.Exists(MtlPath))
			{
				Debug.LogError("Missing OBJ or MTL file. Export first using button 1.");
				return;
			}

			try
			{
				// Load Material
				var wavefrontMat = new WavefrontMaterial(MtlPath);
				Material loadedMat = wavefrontMat.ToUnityMaterial(ResolveTestTexture);

				// Load Mesh
				var wavefrontMesh = new WavefrontMesh(ObjPath);
				Mesh loadedMesh = wavefrontMesh.ToUnityMesh();

				// Apply
				if (testObject != null)
				{
					meshFilter = testObject.GetComponent<MeshFilter>();
					var rend = testObject.GetComponent<Renderer>();

					if (meshFilter != null)
						meshFilter.sharedMesh = loadedMesh;

					if (rend != null)
					{
						SafeDestroyMaterial(currentMaterial);
						currentMaterial = loadedMat;
						rend.sharedMaterial = currentMaterial;
					}
				}

				Debug.Log($"✅ Loaded full Wavefront model + material");
			}
			catch (System.Exception ex)
			{
				Debug.LogError($"Failed to load: {ex.Message}");
			}
		}

		private void ResetObject()
		{
			if (testObject != null)
				Destroy(testObject);

			CreateTestObject();
		}

		// Improved safe material destroyer
		private void SafeDestroyMaterial(Material mat)
		{
			if (mat == null) return;

			// Don't destroy built-in or asset materials
			if (mat.hideFlags == HideFlags.None ||
				mat.name.Contains("Default") ||
				mat.name.Contains("Lit"))
				return;

			if (Application.isPlaying)
				Destroy(mat);
			else
				DestroyImmediate(mat, true);
		}

		private void OnDestroy()
		{
			SafeDestroyMaterial(currentMaterial);
			if (testObject != null)
				Destroy(testObject);
		}

		private Material CreateSourceMaterial()
		{
			var mat = testMaterial != null
				? new Material(testMaterial)
				: new Material(Shader.Find("Universal Render Pipeline/Lit"));

			mat.name = "TestMaterial";

			if (testMaterial == null)
			{
				mat.color = new Color(0.1f, 0.1f, 0.1f, 1f);
				mat.SetFloat("_Smoothness", 0.7f);
				mat.SetFloat("_Metallic", 0.1f);
				mat.EnableKeyword("_EMISSION");
				mat.SetColor("_EmissionColor", new Color(0.1f, 1f, 0.1f, 1f));
			}

			return mat;
		}

		private Texture2D ResolvePrimaryTestTexture(Material sourceMat)
		{
			if (testTexture != null)
				return testTexture;

			return sourceMat != null ? sourceMat.mainTexture as Texture2D : null;
		}

		private Texture ResolveTestTexture(string textureName)
		{
			if (string.IsNullOrWhiteSpace(textureName))
				return null;

			if (TextureNameMatches(testTexture, textureName))
				return testTexture;

			if (testMaterial != null)
				return FindTextureOnMaterial(testMaterial, textureName);

			return null;
		}

		private static Texture FindTextureOnMaterial(Material material, string textureName)
		{
			if (material == null || material.shader == null)
				return null;

			var count = material.shader.GetPropertyCount();
			for (var i = 0; i < count; i++)
			{
				if (material.shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture)
					continue;

				var texture = material.GetTexture(material.shader.GetPropertyName(i));
				if (TextureNameMatches(texture, textureName))
					return texture;
			}

			return null;
		}

		private static bool TextureNameMatches(Texture texture, string textureName)
		{
			if (texture == null || string.IsNullOrWhiteSpace(textureName))
				return false;

			var requested = Path.GetFileNameWithoutExtension(textureName.Trim());
			return string.Equals(texture.name, requested, System.StringComparison.OrdinalIgnoreCase);
		}

		private static void ApplyTexture(Material material, Texture texture)
		{
			if (material == null || texture == null)
				return;

			if (material.HasProperty("_BaseMap"))
				material.SetTexture("_BaseMap", texture);
			if (material.HasProperty("_MainTex"))
				material.SetTexture("_MainTex", texture);
			if (material.HasProperty("_EmissionMap"))
				material.SetTexture("_EmissionMap", texture);

			material.mainTexture = texture;
		}
	}
}
