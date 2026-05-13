using System.Collections;
using System.IO;
using UnityEngine;

namespace MassiveHadronLtd
{
	public class WavefrontTest : MonoBehaviour
	{
		[Header("Test Setup")]
		public GameObject cubePrefab;
		private GameObject testObject;
		private Material currentMaterial;
		private MeshFilter meshFilter;

		private string ObjPath => Path.Combine(Application.persistentDataPath, "TestModel.obj");
		private string MtlPath => Path.Combine(Application.persistentDataPath, "TestMaterial.mtl");

		private string TestTextureName = "test";

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
			Material sourceMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
			sourceMat.name = "TestMaterial";

			sourceMat.color = new Color(0.1f, 0.1f, 0.1f, 1f);
			sourceMat.SetFloat("_Smoothness", 0.7f);
			sourceMat.SetFloat("_Metallic", 0.1f);

			Texture2D testTex = Resources.Load<Texture2D>(TestTextureName);
			if (testTex != null)
			{
				sourceMat.SetTexture("_BaseMap", testTex);
				sourceMat.SetTexture("_MainTex", testTex);
				sourceMat.SetTexture("_EmissionMap", testTex);
			}

			sourceMat.EnableKeyword("_EMISSION");
			sourceMat.SetColor("_EmissionColor", new Color(0.1f, 1f, 0.1f, 1f));

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
				Material loadedMat = wavefrontMat.ToUnityMaterial();

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
	}
}