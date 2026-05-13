using System.IO;
using UnityEngine;

namespace MassiveHadronLtd
{
	public class WavefrontUtilityTest : MonoBehaviour
	{
		[Header("Test Setup")]
		public GameObject cubePrefab;
		private GameObject currentModel;

		private string ObjPath => Path.Combine(Application.persistentDataPath, "TestModel.obj");
		private string MtlPath => Path.Combine(Application.persistentDataPath, "TestMaterial.mtl");

		private void Start()
		{
			CreateTestModel();
			Debug.Log($"WavefrontUtility Test Ready!\nBase path: {Application.persistentDataPath}");
		}

		private void CreateTestModel()
		{
			if (cubePrefab != null)
				currentModel = Instantiate(cubePrefab);
			else
			{
				currentModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
				currentModel.name = "Test Wavefront Object";
			}

			currentModel.transform.position = new Vector3(0, 1, 0);
			currentModel.transform.localScale = Vector3.one * 2f;
		}

		private void Update()
		{
			if (currentModel != null)
				currentModel.transform.Rotate(0, 30 * Time.deltaTime, 0);
		}

		private void OnGUI()
		{
			GUILayout.BeginArea(new Rect(20, 20, 560, 700));
			GUILayout.Label("=== WavefrontUtility Test ===", GUILayout.Height(40));

			if (GUILayout.Button("1. Create & Export Test Model + Material + Texture", GUILayout.Height(60)))
			{
				CreateAndExportTestModel();
			}

			if (GUILayout.Button("2. Load with WavefrontUtility", GUILayout.Height(60)))
			{
				LoadWithUtility(ObjPath);
			}

			if (GUILayout.Button("3. Load from Custom Path...", GUILayout.Height(50)))
			{
				LoadFromCustomPath();
			}

			if (GUILayout.Button("Reset", GUILayout.Height(40)))
			{
				ResetModel();
			}

			GUILayout.Space(20);
			GUILayout.Label($"Default OBJ: {ObjPath}");
			GUILayout.EndArea();
		}

		// ==================== TEST CREATION LOGIC ====================
		private void CreateAndExportTestModel()
		{
			string directory = Path.GetDirectoryName(MtlPath) ?? Application.persistentDataPath;
			Directory.CreateDirectory(directory);

			// Create material exactly like the original working test
			Material sourceMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
			sourceMat.name = "TestMaterial";

			sourceMat.color = new Color(0.1f, 0.1f, 0.1f, 1f);
			sourceMat.SetFloat("_Smoothness", 0.7f);
			sourceMat.SetFloat("_Metallic", 0.1f);

			Texture2D testTex = Resources.Load<Texture2D>("test");
			if (testTex != null)
			{
				sourceMat.SetTexture("_BaseMap", testTex);
				sourceMat.SetTexture("_MainTex", testTex);
				sourceMat.SetTexture("_EmissionMap", testTex);

				// Copy texture to disk
				string texPath = Path.Combine(directory, "test.png");
				SaveTextureAsPng(testTex, texPath);
			}

			sourceMat.EnableKeyword("_EMISSION");
			sourceMat.SetColor("_EmissionColor", new Color(0.1f, 1f, 0.1f, 1f));
			sourceMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

			MaterialUtils.ForceMaterialRefresh(sourceMat);

			// Export MTL
			var wMat = new WavefrontMaterial();
			wMat.FromUnityMaterial(sourceMat, "TestMaterial");
			wMat.ExportToMtl(MtlPath);

			// Export OBJ
			var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
			var wMesh = new WavefrontMesh();
			wMesh.FromUnityMesh(cube.GetComponent<MeshFilter>().sharedMesh, "TestCube", "TestMaterial");
			wMesh.ExportToObj(ObjPath, "TestMaterial.mtl");

			Object.DestroyImmediate(cube);

			Debug.Log($"✅ Test files created!\nOBJ: {ObjPath}\nMTL: {MtlPath}\nPNG should be at: {Path.Combine(directory, "test.png")}");
		}

		private static void SaveTextureAsPng(Texture2D tex, string filePath)
		{
			try
			{
				// Force readable copy if needed
				Texture2D copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
				copy.SetPixels(tex.GetPixels());
				copy.Apply();

				byte[] bytes = copy.EncodeToPNG();
				File.WriteAllBytes(filePath, bytes);
				Object.DestroyImmediate(copy);

				Debug.Log($"Texture copied to: {filePath}");
			}
			catch (System.Exception ex)
			{
				Debug.LogError($"Failed to save texture: {ex.Message}");
			}
		}

		// ==================== LOAD METHODS ====================
		private void LoadWithUtility(string objPath)
		{
			if (currentModel != null) Destroy(currentModel);
			currentModel = WavefrontUtility.Load(objPath, "Loaded Wavefront Model");

			if (currentModel != null)
			{
				currentModel.transform.position = new Vector3(0, 1, 0);
				currentModel.transform.localScale = Vector3.one * 2f;
			}
		}

		private void LoadFromCustomPath()
		{
#if UNITY_EDITOR
			string path = UnityEditor.EditorUtility.OpenFilePanel("Load OBJ File", Application.persistentDataPath, "obj");
			if (!string.IsNullOrEmpty(path))
				LoadWithUtility(path);
#else
            Debug.Log("Custom path loading only works in Editor.");
#endif
		}

		private void ResetModel()
		{
			if (currentModel != null) Destroy(currentModel);
			CreateTestModel();
		}

		private void OnDestroy()
		{
			if (currentModel != null) Destroy(currentModel);
		}
	}
}