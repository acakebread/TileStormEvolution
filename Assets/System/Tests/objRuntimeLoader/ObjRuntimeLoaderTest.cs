using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using MassiveHadronLtd;

public class ObjExternalLoaderTest : MonoBehaviour
{
	[Header("Full OBJ file path OR URL")]
	public string path;

	[Header("Optional Override Material")]
	[SerializeField] private Material materialOverride;

	private IEnumerator Start()
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			Debug.LogError("No path specified");
			yield break;
		}

		// ---------- LOAD OBJ ----------
		string objText = null;

		yield return LoadText(path, result => objText = result);

		if (string.IsNullOrEmpty(objText))
		{
			Debug.LogError("Failed to load OBJ");
			yield break;
		}

		Mesh mesh = ObjRuntimeLoader.LoadFromText(objText);

		// ---------- COMPONENTS ----------
		var filter = GetComponent<MeshFilter>();
		if (!filter) filter = gameObject.AddComponent<MeshFilter>();

		var renderer = GetComponent<MeshRenderer>();
		if (!renderer) renderer = gameObject.AddComponent<MeshRenderer>();

		filter.mesh = mesh;

		// ---------- MATERIAL ----------
		if (materialOverride != null)
		{
			renderer.material = materialOverride;
		}
		else
		{
			Material mat = CreateDefaultURPMaterial();

			// Try load texture next to OBJ
			string texPath = GetTexturePath(path);

			Texture2D tex = null;
			yield return LoadTexture(texPath, t => tex = t);

			if (tex != null)
			{
				mat.mainTexture = tex;
				mat.SetTexture("_BaseMap", tex); // URP property
			}

			renderer.material = mat;
		}

		Debug.Log($"OBJ loaded from: {path}");
	}

	// ============================================================
	// MATERIAL CREATION
	// ============================================================

	private Material CreateDefaultURPMaterial()
	{
		var shader = Shader.Find("Universal Render Pipeline/Simple Lit");

		var mat = new Material(shader);
		//mat.color = new Color(0.25f, 0.25f, 0.35f, 1f);
		mat.color = Color.white;

		return mat;
	}

	// ============================================================
	// PATH HELPERS
	// ============================================================

	private string GetTexturePath(string objPath)
	{
		string dir = Path.GetDirectoryName(objPath);
		string file = Path.GetFileNameWithoutExtension(objPath);

		// Prefer PNG
		string png = CombinePath(dir, file + ".png");
		if (FileExists(png)) return png;

		// Optional fallbacks
		string jpg = CombinePath(dir, file + ".jpg");
		if (FileExists(jpg)) return jpg;

		string bmp = CombinePath(dir, file + ".bmp");
		if (FileExists(bmp)) return bmp;

		return null;
	}

	private static string CombinePath(string a, string b)
	{
		if (string.IsNullOrEmpty(a)) return b;
		return a.Replace("\\", "/") + "/" + b;
	}

	private static bool FileExists(string p)
	{
#if UNITY_WEBGL && !UNITY_EDITOR
        return !string.IsNullOrEmpty(p); // cannot check directly in WebGL
#else
		return File.Exists(p);
#endif
	}

	// ============================================================
	// LOADERS
	// ============================================================

	private IEnumerator LoadText(string filePath, Action<string> result)
	{
#if UNITY_WEBGL && !UNITY_EDITOR
        using var www = UnityWebRequest.Get(filePath);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(www.error);
            result(null);
            yield break;
        }

        result(www.downloadHandler.text);
#else
		if (!File.Exists(filePath))
		{
			Debug.LogError($"File not found: {filePath}");
			result(null);
			yield break;
		}

		result(File.ReadAllText(filePath));
		yield return null;
#endif
	}

	private IEnumerator LoadTexture(string filePath, Action<Texture2D> result)
	{
		if (string.IsNullOrEmpty(filePath))
		{
			result(null);
			yield break;
		}

#if UNITY_WEBGL && !UNITY_EDITOR
        using var www = UnityWebRequestTexture.GetTexture(filePath);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"Texture load failed: {www.error}");
            result(null);
            yield break;
        }

        result(DownloadHandlerTexture.GetContent(www));
#else
		if (!File.Exists(filePath))
		{
			result(null);
			yield break;
		}

		byte[] bytes = File.ReadAllBytes(filePath);

		Texture2D tex = new Texture2D(2, 2);
		tex.LoadImage(bytes);

		result(tex);
		yield return null;
#endif
	}
}



//using System;
//using System.Collections;
//using System.IO;
//using UnityEngine;
//using UnityEngine.Networking;
//using MassiveHadronLtd;

//public class ObjExternalLoaderTest : MonoBehaviour
//{
//	[Header("Full file path OR URL")]
//	public string path;

//	[Header("Optional Material")]
//	[SerializeField] private Material material;

//	private IEnumerator Start()
//	{
//		if (string.IsNullOrWhiteSpace(path))
//		{
//			Debug.LogError("No path specified");
//			yield break;
//		}

//		string objText = null;

//#if UNITY_WEBGL && !UNITY_EDITOR
//        // WebGL must use web request
//        using (UnityWebRequest www = UnityWebRequest.Get(path))
//        {
//            yield return www.SendWebRequest();

//            if (www.result != UnityWebRequest.Result.Success)
//            {
//                Debug.LogError($"Failed to load OBJ: {www.error}");
//                yield break;
//            }

//            objText = www.downloadHandler.text;
//        }
//#else
//		// Desktop / Editor
//		if (!File.Exists(path))
//		{
//			Debug.LogError($"File not found: {path}");
//			yield break;
//		}

//		objText = File.ReadAllText(path);
//#endif

//		Mesh mesh = ObjRuntimeLoader.LoadFromText(objText);

//		var filter = GetComponent<MeshFilter>();
//		if (!filter) filter = gameObject.AddComponent<MeshFilter>();

//		var renderer = GetComponent<MeshRenderer>();
//		if (!renderer) renderer = gameObject.AddComponent<MeshRenderer>();

//		filter.mesh = mesh;

//		if (material != null)
//			renderer.material = material;

//		Debug.Log($"OBJ loaded from: {path}");
//	}
//}


//using MassiveHadronLtd;
//using UnityEngine;

//[RequireComponent(typeof(Transform))]
//public class ObjRuntimeLoaderTest : MonoBehaviour
//{
//	[Header("OBJ Resource Path (inside Resources folder)")]
//	[SerializeField] private string resourcePath = "Models/jun_boundary_tree_double";

//	[Header("Optional Material")]
//	[SerializeField] private Material material;

//	private void Start()
//	{
//		LoadObj();
//	}

//	private void LoadObj()
//	{
//		TextAsset objFile = Resources.Load<TextAsset>(resourcePath);

//		if (objFile == null)
//		{
//			Debug.LogError($"OBJ not found at Resources/{resourcePath}");
//			return;
//		}

//		Mesh mesh = ObjRuntimeLoader.LoadFromText(objFile.text, resourcePath);

//		if (mesh == null)
//		{
//			Debug.LogError("Failed to create mesh from OBJ");
//			return;
//		}

//		// Ensure components exist
//		MeshFilter filter = GetComponent<MeshFilter>();
//		if (filter == null)
//			filter = gameObject.AddComponent<MeshFilter>();

//		MeshRenderer renderer = GetComponent<MeshRenderer>();
//		if (renderer == null)
//			renderer = gameObject.AddComponent<MeshRenderer>();

//		filter.mesh = mesh;

//		if (material != null)
//			renderer.material = material;

//		Debug.Log($"OBJ loaded successfully: {mesh.vertexCount} vertices");
//	}
//}
