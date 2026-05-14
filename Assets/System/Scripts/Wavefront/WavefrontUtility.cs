using System.IO;
using UnityEngine;

namespace MassiveHadronLtd
{
	public static class WavefrontUtility
	{
		public static GameObject Load(string objPath, string objectName = null, bool asTemplate = false)
		{
			if (!File.Exists(objPath))
			{
				Debug.LogError($"OBJ file not found: {objPath}");
				return null;
			}

			string directory = Path.GetDirectoryName(objPath);

			var wavefrontMesh = new WavefrontMesh(objPath);
			Mesh mesh = wavefrontMesh.ToUnityMesh();

			var go = new GameObject(objectName ?? wavefrontMesh.name ?? Path.GetFileNameWithoutExtension(objPath));
			var mf = go.AddComponent<MeshFilter>();
			var mr = go.AddComponent<MeshRenderer>();

			mf.sharedMesh = mesh;

			Material mat = TryLoadMaterial(directory, wavefrontMesh.materialLibrary, wavefrontMesh.materialName);
			mr.sharedMaterial = mat ?? new Material(Shader.Find("Universal Render Pipeline/Lit"));

			if (asTemplate)
			{
				go.hideFlags = HideFlags.HideAndDontSave;
				go.SetActive(false);
			}

			Debug.Log($"✅ Loaded {go.name} | Material: {(mat != null ? mat.name : "default")}");
			return go;
		}

		private static Material TryLoadMaterial(string baseDirectory, string materialLibrary, string materialName)
		{
			string mtlPath = ResolveRelativePath(baseDirectory, materialLibrary);
			if (string.IsNullOrEmpty(mtlPath) && !string.IsNullOrEmpty(materialName))
				mtlPath = Path.Combine(baseDirectory, materialName + ".mtl");

			if (!File.Exists(mtlPath))
			{
				string mtlLabel = !string.IsNullOrEmpty(materialLibrary) ? materialLibrary : materialName + ".mtl";
				Debug.LogWarning($"MTL not found: {mtlLabel}");
				return null;
			}

			var wMat = new WavefrontMaterial(mtlPath);
			Material mat = wMat.ToUnityMaterial();

			PatchTexturesFromDisk(mat, wMat, Path.GetDirectoryName(mtlPath));
			return mat;
		}

		private static void PatchTexturesFromDisk(Material mat, WavefrontMaterial wavefrontMaterial, string baseDirectory)
		{
			if (mat == null || wavefrontMaterial?.properties == null) return;

			foreach (var prop in wavefrontMaterial.properties)
			{
				if (string.IsNullOrEmpty(prop?.name) || string.IsNullOrEmpty(prop.texture))
					continue;

				if (!mat.HasProperty(prop.name))
					continue;

				Texture2D diskTex = WavefrontMaterial.TryLoadTexture(baseDirectory, prop.texture);
				if (diskTex != null)
				{
					mat.SetTexture(prop.name, diskTex);
					mat.SetTextureScale(prop.name, new Vector2(prop.textureScaleX, prop.textureScaleY));
					mat.SetTextureOffset(prop.name, new Vector2(prop.textureOffsetX, prop.textureOffsetY));

					if (prop.name == "_BaseMap" || prop.name == "_MainTex")
						mat.mainTexture = diskTex;

					if (prop.name == "_BaseMap" && mat.HasProperty("_MainTex"))
						mat.SetTexture("_MainTex", diskTex);
					else if (prop.name == "_MainTex" && mat.HasProperty("_BaseMap"))
						mat.SetTexture("_BaseMap", diskTex);

					Debug.Log($"Patched texture '{prop.texture}' on property {prop.name}");
				}
			}
		}

		private static string ResolveRelativePath(string baseDirectory, string relativePath)
		{
			if (string.IsNullOrEmpty(baseDirectory) || string.IsNullOrEmpty(relativePath))
				return relativePath;

			if (Path.IsPathRooted(relativePath))
				return relativePath;

			return Path.GetFullPath(Path.Combine(baseDirectory, relativePath));
		}
	}
}
