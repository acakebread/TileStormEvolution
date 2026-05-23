using System.IO;
using UnityEngine;

namespace MassiveHadronLtd
{
	public class WavefrontUtilityTest : MonoBehaviour
	{
		[Header("Test Setup")]
		public GameObject cubePrefab;
		public Material testMaterial;
		public Texture2D testTexture;
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

		private void CreateAndExportTestModel()
		{
			string directory = Path.GetDirectoryName(MtlPath) ?? Application.persistentDataPath;
			Directory.CreateDirectory(directory);

			Material sourceMat = CreateSourceMaterial();
			Texture2D testTex = ResolvePrimaryTestTexture(sourceMat);
			if (testTex != null)
			{
				ApplyTexture(sourceMat, testTex);
				string textureStem = Path.GetFileNameWithoutExtension(testTex.name);
				string texPath = Path.Combine(directory, $"{textureStem}.png");
				SaveTextureAsPng(testTex, texPath);
			}

			MaterialUtils.ForceMaterialRefresh(sourceMat);

			var wMat = new WavefrontMaterial();
			wMat.FromUnityMaterial(sourceMat, "TestMaterial");
			wMat.ExportToMtl(MtlPath);

			var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
			var wMesh = new WavefrontMesh();
			wMesh.FromUnityMesh(cube.GetComponent<MeshFilter>().sharedMesh, "TestCube", "TestMaterial");
			wMesh.ExportToObj(ObjPath, "TestMaterial.mtl");

			Object.DestroyImmediate(cube);

			Debug.Log($"Test files created.\nOBJ: {ObjPath}\nMTL: {MtlPath}");
		}

		private static void SaveTextureAsPng(Texture2D tex, string filePath)
		{
			try
			{
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
