//using System.Collections;
//using System.IO;
//using UnityEngine;

//namespace MassiveHadronLtd
//{
//	public class PortableWavefrontMaterialTest : MonoBehaviour
//	{
//		[Header("Test Setup")]
//		public GameObject cubePrefab;
//		private GameObject testCube;
//		private Material currentMaterial;

//		private string SavePath => Path.Combine(Application.persistentDataPath, "TestMaterial.mtl");

//		private string TestTextureName = "test";   // Resources/test.png

//		private void Start()
//		{
//			CreateSpinningCube();
//			Debug.Log($"Portable Wavefront MTL Test Ready!\nSave path: {SavePath}");
//		}

//		private void CreateSpinningCube()
//		{
//			if (cubePrefab != null)
//				testCube = Instantiate(cubePrefab);
//			else
//			{
//				testCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
//				testCube.name = "Test Spinning Cube";
//			}

//			testCube.transform.position = new Vector3(0, 1, 0);
//			testCube.transform.localScale = Vector3.one * 2f;

//			// Initial plain material
//			currentMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
//			currentMaterial.color = Color.gray;
//			ApplyMaterialToCube(currentMaterial);
//		}

//		private void Update()
//		{
//			if (testCube != null)
//				testCube.transform.Rotate(0, 30 * Time.deltaTime, 0);
//		}

//		private void OnGUI()
//		{
//			GUILayout.BeginArea(new Rect(20, 20, 440, 700));
//			GUILayout.Label("=== Portable Wavefront MTL Test ===", GUILayout.Height(30));

//			if (GUILayout.Button("1. Create & Export Unity Material → MTL", GUILayout.Height(60)))
//			{
//				CreateAndExportMaterial();
//			}

//			if (GUILayout.Button("2. Load Wavefront .mtl", GUILayout.Height(50)))
//			{
//				LoadWavefrontMaterial();
//			}

//			if (GUILayout.Button("Reset to Plain Cube", GUILayout.Height(40)))
//			{
//				ResetCube();
//			}

//			GUILayout.Space(20);
//			GUILayout.Label($"MTL Save Path:\n{SavePath}");
//			GUILayout.EndArea();
//		}

//		private void CreateAndExportMaterial()
//		{
//			Material sourceMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
//			sourceMat.name = "TestMaterial";

//			// Reasonable base color
//			sourceMat.color = new Color(0.1f, 0.1f, 0.1f, 1f);
//			sourceMat.SetFloat("_Smoothness", 0.7f);
//			sourceMat.SetFloat("_Metallic", 0.1f);

//			// Load texture
//			Texture2D testTex = Resources.Load<Texture2D>(TestTextureName);
//			if (testTex != null)
//			{
//				sourceMat.SetTexture("_BaseMap", testTex);
//				sourceMat.SetTexture("_MainTex", testTex);
//				sourceMat.mainTexture = testTex;

//				// Set same texture as emission map (like original test)
//				sourceMat.SetTexture("_EmissionMap", testTex);
//			}
//			else
//			{
//				Debug.LogWarning($"Could not find Resources/{TestTextureName}.png");
//			}

//			// Enable emission
//			sourceMat.EnableKeyword("_EMISSION");
//			sourceMat.SetColor("_EmissionColor", new Color(0.1f, 1f, 0.1f, 1f));
//			sourceMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

//			// === Convert to PortableWavefrontMaterial ===
//			var wavefront = new PortableWavefrontMaterial();
//			wavefront.FromUnityMaterial(sourceMat, "TestMaterial");

//			// Apply to cube
//			if (testCube != null)
//			{
//				if (currentMaterial != null) Destroy(currentMaterial);
//				currentMaterial = wavefront.ToUnityMaterial();
//				ApplyMaterialToCube(currentMaterial);
//			}

//			// === Export to .mtl ===
//			wavefront.ExportToMtl(SavePath);

//			Debug.Log($"✅ Material created and exported to Wavefront MTL → {SavePath}");
//			Debug.Log($"Texture used: {(testTex != null ? testTex.name : "none")}");
//		}

//		private void LoadWavefrontMaterial()
//		{
//			if (!File.Exists(SavePath))
//			{
//				Debug.LogError($"No saved .mtl file found at: {SavePath}\nClick button 1 first.");
//				return;
//			}

//			try
//			{
//				var wavefront = new PortableWavefrontMaterial(SavePath);

//				if (currentMaterial != null) Destroy(currentMaterial);

//				currentMaterial = wavefront.ToUnityMaterial();
//				ApplyMaterialToCube(currentMaterial);

//				Debug.Log($"✅ Wavefront MTL loaded and applied: {wavefront.name}");
//			}
//			catch (System.Exception ex)
//			{
//				Debug.LogError($"Failed to load material: {ex.Message}");
//			}
//		}

//		private void ResetCube()
//		{
//			if (currentMaterial != null)
//			{
//				Destroy(currentMaterial);
//				currentMaterial = null;
//			}

//			if (testCube != null)
//			{
//				var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
//				mat.color = Color.gray;
//				currentMaterial = mat;
//				ApplyMaterialToCube(mat);
//			}

//			Debug.Log("Cube reset to plain material.");
//		}

//		private void OnDestroy()
//		{
//			if (currentMaterial != null)
//				Destroy(currentMaterial);
//		}

//		private void ApplyMaterialToCube(Material mat)
//		{
//			if (testCube == null || mat == null) return;

//			var rend = testCube.GetComponent<Renderer>();
//			if (rend == null) return;

//			rend.sharedMaterial = mat;
//			rend.SetPropertyBlock(null);
//			StartCoroutine(RebindMaterialNextFrame(rend, mat));
//		}

//		private IEnumerator RebindMaterialNextFrame(Renderer rend, Material mat)
//		{
//			yield return null;
//			if (rend == null || mat == null) yield break;
//			rend.sharedMaterial = mat;
//			rend.SetPropertyBlock(null);
//		}
//	}
//}

////using System.Collections;
////using System.IO;
////using UnityEngine;

////namespace MassiveHadronLtd
////{
////	public class PortableWavefrontMaterialTest : MonoBehaviour
////	{
////		[Header("Test Setup")]
////		public GameObject cubePrefab;
////		private GameObject testCube;
////		private Material currentMaterial;

////		private string SavePath => Path.Combine(Application.persistentDataPath, "TestMaterial.mtl");

////		private void Start()
////		{
////			CreateSpinningCube();
////			Debug.Log($"Portable Wavefront MTL Test Ready!\nMTL path: {SavePath}");
////		}

////		private void CreateSpinningCube()
////		{
////			if (cubePrefab != null)
////				testCube = Instantiate(cubePrefab);
////			else
////			{
////				testCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
////				testCube.name = "Test Spinning Cube";
////			}

////			testCube.transform.position = new Vector3(0, 1, 0);
////			testCube.transform.localScale = Vector3.one * 2f;

////			currentMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
////			currentMaterial.color = Color.gray;
////			ApplyMaterialToCube(currentMaterial);
////		}

////		private void Update()
////		{
////			if (testCube != null)
////				testCube.transform.Rotate(0, 30 * Time.deltaTime, 0);
////		}

////		private void OnGUI()
////		{
////			GUILayout.BeginArea(new Rect(20, 20, 420, 650));
////			GUILayout.Label("=== Portable Wavefront MTL Test ===", GUILayout.Height(30));

////			if (GUILayout.Button("Create Rich Test MTL File (with Emission)", GUILayout.Height(50)))
////			{
////				CreateRichTestMtlFile();
////			}

////			if (GUILayout.Button("1. Load Wavefront .mtl", GUILayout.Height(50)))
////			{
////				LoadWavefrontMaterial();
////			}

////			if (GUILayout.Button("Reset to Plain Cube", GUILayout.Height(40)))
////			{
////				ResetCube();
////			}

////			GUILayout.Space(20);
////			GUILayout.Label($"MTL Path:\n{SavePath}");
////			GUILayout.EndArea();
////		}

////		private void CreateRichTestMtlFile()
////		{
////			string mtlContent = @"# Rich Test Wavefront MTL - Demonstrates Base + Emission textures
////newmtl TestEmissiveMaterial

////# Colors
////Kd 0.15 0.15 0.15
////Ke 0.2 0.8 0.2

////# Scalars
////Ns 800.0
////d 1.0

////# Textures
////map_Kd test.png
////map_Ke test.png

////# Optional extra maps (will be supported)
////# map_Ks specular.png
////# map_bump normal.png
////";

////			File.WriteAllText(SavePath, mtlContent);
////			Debug.Log($"✅ Rich Test MTL with Emission created at:\n{SavePath}");
////			Debug.Log("This MTL uses map_Kd + map_Ke + Ke (emission color)");
////		}

////		private void LoadWavefrontMaterial()
////		{
////			if (!File.Exists(SavePath))
////			{
////				Debug.LogError($"No MTL file found at: {SavePath}. Click 'Create Rich Test MTL File' first.");
////				return;
////			}

////			try
////			{
////				var wavefront = new PortableWavefrontMaterial(SavePath);

////				if (currentMaterial != null) Destroy(currentMaterial);

////				currentMaterial = wavefront.ToUnityMaterial();
////				ApplyMaterialToCube(currentMaterial);

////				Debug.Log($"✅ Wavefront MTL loaded successfully: {wavefront.name}");
////				Debug.Log($"   Properties: {wavefront.properties.Count} | Keywords: {wavefront.enabledKeywords.Count}");
////			}
////			catch (System.Exception ex)
////			{
////				Debug.LogError($"Failed to load MTL: {ex.Message}");
////			}
////		}

////		private void ResetCube()
////		{
////			if (currentMaterial != null)
////			{
////				Destroy(currentMaterial);
////				currentMaterial = null;
////			}

////			if (testCube != null)
////			{
////				var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
////				mat.color = Color.gray;
////				currentMaterial = mat;
////				ApplyMaterialToCube(mat);
////			}
////			Debug.Log("Cube reset to plain material.");
////		}

////		private void OnDestroy()
////		{
////			if (currentMaterial != null)
////				Destroy(currentMaterial);
////		}

////		private void ApplyMaterialToCube(Material mat)
////		{
////			if (testCube == null || mat == null) return;

////			var rend = testCube.GetComponent<Renderer>();
////			if (rend == null) return;

////			rend.sharedMaterial = mat;
////			rend.SetPropertyBlock(null);
////			StartCoroutine(RebindMaterialNextFrame(rend, mat));
////		}

////		private IEnumerator RebindMaterialNextFrame(Renderer rend, Material mat)
////		{
////			yield return null;
////			if (rend == null || mat == null) yield break;
////			rend.sharedMaterial = mat;
////			rend.SetPropertyBlock(null);
////		}
////	}
////}