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
		private GameObject testCube;
		private Material currentMaterial;

		private string SavePath => Path.Combine(Application.persistentDataPath, "TestMaterial.json");
		private string TestTextureName = "test";   // Resources/test.png (without extension)

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
			Material sourceMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
			sourceMat.name = "TestToxicMaterial";

			// Much more reasonable values
			sourceMat.color = new Color(0.1f, 0.8f, 0.2f, 1f);        // nice green base
			sourceMat.SetFloat("_Smoothness", 0.7f);
			sourceMat.SetFloat("_Metallic", 0.1f);

			// Gentle emission
			sourceMat.EnableKeyword("_EMISSION");
			sourceMat.SetColor("_EmissionColor", new Color(0.2f, 1.2f, 0.3f, 1f)); // much softer

			// Load texture
			Texture2D testTex = Resources.Load<Texture2D>(TestTextureName);
			if (testTex != null)
			{
				sourceMat.SetTexture("_BaseMap", testTex);
				sourceMat.SetTexture("_MainTex", testTex);   // some shaders still use this
				sourceMat.mainTexture = testTex;
			}
			else
			{
				Debug.LogWarning($"Could not find Resources/{TestTextureName}.png");
			}

			var portable = new PortableMaterial(sourceMat, "TestToxicMaterial");

			// Apply to cube
			if (testCube != null)
			{
				if (currentMaterial != null) Destroy(currentMaterial);
				currentMaterial = portable.ToUnityMaterial();
				ApplyMaterialToCube(currentMaterial);
			}

			// Export
			string json = JsonConvert.SerializeObject(portable, Formatting.Indented);
			File.WriteAllText(SavePath, json);

			Debug.Log($"Material exported successfully -> {SavePath}");
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

					currentMaterial = portable.ToUnityMaterial();
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
	}
}
