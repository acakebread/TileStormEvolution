using System.Collections;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace MassiveHadronLtd
{
	public class PortableMaterialTest : MonoBehaviour
	{
		[Header("Test Setup")]
		public GameObject cubePrefab;           // Assign a cube in inspector
		public Material testMaterial;           // Optional source material
		public Texture2D testTexture;           // Optional texture override
		private GameObject testCube;
		private Material currentMaterial;

		private string SavePath => Path.Combine(Application.persistentDataPath, "TestMaterial.json");

		private void Start()
		{
			CreateSpinningCube();
			Debug.Log($"PortableMaterial Test Ready!\nSave path: {SavePath}");
		}

		private void CreateSpinningCube()
		{
			if (cubePrefab != null)
				testCube = Instantiate(cubePrefab);
			else
			{
				testCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
				testCube.name = "Test Spinning Cube";
			}

			testCube.transform.position = new Vector3(0, 1, 0);
			testCube.transform.localScale = Vector3.one * 2f;

			// Initial plain material
			currentMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
			currentMaterial.color = Color.gray;
			ApplyMaterialToCube(currentMaterial);
		}

		private void Update()
		{
			if (testCube != null)
				testCube.transform.Rotate(0, 30 * Time.deltaTime, 0);
		}

		private void OnGUI()
		{
			GUILayout.BeginArea(new Rect(20, 20, 400, 500));
			GUILayout.Label("=== PortableMaterial Test ===", GUILayout.Height(30));

			if (GUILayout.Button("1. Create & Apply Material + Export", GUILayout.Height(50)))
			{
				CreateAndExportMaterial();
			}

			if (GUILayout.Button("2. Load Material from Disk", GUILayout.Height(50)))
			{
				LoadAndApplyMaterial();
			}

			if (GUILayout.Button("Reset to Plain Cube", GUILayout.Height(40)))
			{
				ResetCube();
			}

			GUILayout.Space(20);
			GUILayout.Label($"Save Path:\n{SavePath}");
			GUILayout.EndArea();
		}

		private void CreateAndExportMaterial()
		{
			Material sourceMat = CreateSourceMaterial();
			Texture2D testTex = ResolvePrimaryTestTexture(sourceMat);
			if (testTex != null)
				ApplyTexture(sourceMat, testTex);

			var portable = new PortableMaterial(sourceMat, "TestMaterial");

			// Apply to cube
			if (testCube != null)
			{
				if (currentMaterial != null) Destroy(currentMaterial);
				currentMaterial = portable.ToUnityMaterial(ResolveTestTexture);
				ApplyMaterialToCube(currentMaterial);
			}

			// Export
			string json = JsonConvert.SerializeObject(portable, Formatting.Indented);
			File.WriteAllText(SavePath, json);

			Debug.Log($"Material exported successfully → {SavePath}");
			Debug.Log($"Texture used: {(testTex != null ? testTex.name : "none")}");
		}

		private void LoadAndApplyMaterial()
		{
			if (!File.Exists(SavePath))
			{
				Debug.LogError($"No saved material found at: {SavePath}");
				return;
			}

			try
			{
				string json = File.ReadAllText(SavePath);
				var portable = JsonConvert.DeserializeObject<PortableMaterial>(json);

				if (portable != null)
				{
					if (currentMaterial != null) Destroy(currentMaterial);

					currentMaterial = portable.ToUnityMaterial(ResolveTestTexture);
					ApplyMaterialToCube(currentMaterial);

					Debug.Log($"Material loaded and applied: {portable.name}");
				}
			}
			catch (System.Exception ex)
			{
				Debug.LogError($"Failed to load material: {ex.Message}");
			}
		}

		private void ResetCube()
		{
			if (currentMaterial != null)
			{
				Destroy(currentMaterial);
				currentMaterial = null;
			}

			if (testCube != null)
			{
				var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
				mat.color = Color.gray;
				mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
				currentMaterial = mat;
				ApplyMaterialToCube(mat);
			}

			Debug.Log("Cube reset to plain material.");
		}

		private void OnDestroy()
		{
			if (currentMaterial != null)
				Destroy(currentMaterial);
		}

		private void ApplyMaterialToCube(Material mat)
		{
			if (testCube == null || mat == null)
				return;

			var rend = testCube.GetComponent<Renderer>();
			if (rend == null)
				return;

			rend.sharedMaterial = mat;
			rend.SetPropertyBlock(null);
			rend.UpdateGIMaterials();

			StartCoroutine(RebindMaterialNextFrame(rend, mat));
		}

		private IEnumerator RebindMaterialNextFrame(Renderer rend, Material mat)
		{
			yield return null;

			if (rend == null || mat == null)
				yield break;

			rend.sharedMaterial = mat;
			rend.SetPropertyBlock(null);
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
				mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
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
