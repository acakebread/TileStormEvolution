using System.IO;
using UnityEngine;

namespace MassiveHadronLtd
{
	public static class WavefrontUtility
	{
		public static GameObject Load(string objPath, string objectName = null)
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

			Material mat = TryLoadMaterial(directory, wavefrontMesh.materialName);
			mr.sharedMaterial = mat ?? new Material(Shader.Find("Universal Render Pipeline/Lit"));

			Debug.Log($"✅ Loaded {go.name} | Material: {(mat != null ? mat.name : "default")}");
			return go;
		}

		private static Material TryLoadMaterial(string baseDirectory, string materialName)
		{
			if (string.IsNullOrEmpty(materialName)) return null;

			string mtlFileName = materialName + ".mtl";
			string mtlPath = Path.Combine(baseDirectory, mtlFileName);

			if (!File.Exists(mtlPath))
			{
				string altPath = Path.Combine(baseDirectory, "Materials", mtlFileName);
				if (File.Exists(altPath)) mtlPath = altPath;
			}

			if (!File.Exists(mtlPath))
			{
				Debug.LogWarning($"MTL not found: {mtlFileName}");
				return null;
			}

			var wMat = new WavefrontMaterial(mtlPath);
			Material mat = wMat.ToUnityMaterial();

			PatchTexturesFromDisk(mat, wMat, baseDirectory);
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
	}
}
