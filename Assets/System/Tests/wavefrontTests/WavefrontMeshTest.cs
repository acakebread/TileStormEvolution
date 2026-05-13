using System.IO;
using UnityEngine;

namespace MassiveHadronLtd
{
	public class WavefrontMeshTest : MonoBehaviour
	{
		[Header("Test Setup")]
		public GameObject cubePrefab;
		private GameObject testObject;
		private MeshFilter currentMeshFilter;

		private string SavePath => Path.Combine(Application.persistentDataPath, "TestMesh.obj");

		private void Start()
		{
			CreateTestObject();
			Debug.Log($"Wavefront Mesh Test Ready!\nOBJ Path: {SavePath}");
		}

		private void CreateTestObject()
		{
			if (cubePrefab != null)
				testObject = Instantiate(cubePrefab);
			else
			{
				testObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
				testObject.name = "Test Wavefront Cube";
			}

			testObject.transform.position = new Vector3(0, 1, 0);
			testObject.transform.localScale = Vector3.one * 2f;

			currentMeshFilter = testObject.GetComponent<MeshFilter>();
		}

		private void Update()
		{
			if (testObject != null)
				testObject.transform.Rotate(0, 30 * Time.deltaTime, 0);
		}

		private void OnGUI()
		{
			GUILayout.BeginArea(new Rect(20, 20, 460, 500));
			GUILayout.Label("=== Wavefront Mesh Test ===", GUILayout.Height(30));

			if (GUILayout.Button("1. Create Cube & Export to .obj", GUILayout.Height(60)))
			{
				CreateAndExportMesh();
			}

			if (GUILayout.Button("2. Load .obj File", GUILayout.Height(50)))
			{
				LoadObjFile();
			}

			if (GUILayout.Button("Reset to Default Cube", GUILayout.Height(40)))
			{
				ResetObject();
			}

			GUILayout.Space(20);
			GUILayout.Label($"OBJ Path:\n{SavePath}");
			GUILayout.EndArea();
		}

		private void CreateAndExportMesh()
		{
			if (currentMeshFilter == null) return;

			var wavefront = new WavefrontMesh();
			wavefront.FromUnityMesh(currentMeshFilter.sharedMesh, "TestCube");

			wavefront.ExportToObj(SavePath);

			Debug.Log($"✅ Cube exported to OBJ: {SavePath}");
		}

		private void LoadObjFile()
		{
			if (!File.Exists(SavePath))
			{
				Debug.LogError($"No OBJ file found at {SavePath}. Export one first using button 1.");
				return;
			}

			try
			{
				var wavefront = new WavefrontMesh(SavePath);
				Mesh loadedMesh = wavefront.ToUnityMesh();

				if (testObject != null)
				{
					var mf = testObject.GetComponent<MeshFilter>();
					if (mf != null)
						mf.sharedMesh = loadedMesh;

					Debug.Log($"✅ Loaded OBJ with {loadedMesh.vertexCount} vertices");
				}
			}
			catch (System.Exception ex)
			{
				Debug.LogError($"Failed to load OBJ: {ex.Message}");
			}
		}

		private void ResetObject()
		{
			if (testObject != null)
				Destroy(testObject);

			CreateTestObject();
			Debug.Log("Reset to default cube.");
		}

		private void OnDestroy()
		{
			if (testObject != null)
				Destroy(testObject);
		}
	}
}