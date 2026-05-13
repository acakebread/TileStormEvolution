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

			PatchTexturesFromDisk(mat, baseDirectory);
			return mat;
		}

		private static void PatchTexturesFromDisk(Material mat, string baseDirectory)
		{
			if (mat == null) return;

			string[] textureProps = { "_BaseMap", "_MainTex", "_EmissionMap", "_BumpMap" };

			foreach (string prop in textureProps)
			{
				if (!mat.HasProperty(prop)) continue;

				// Get the texture name that was stored in the MTL
				Texture current = mat.GetTexture(prop);
				string texName = current != null ? current.name : null;

				if (string.IsNullOrEmpty(texName)) continue;

				Texture2D diskTex = WavefrontMaterial.TryLoadTexture(baseDirectory, texName);
				if (diskTex != null)
				{
					mat.SetTexture(prop, diskTex);
					if (prop == "_BaseMap" || prop == "_MainTex")
						mat.mainTexture = diskTex;

					Debug.Log($"Patched texture '{texName}' on property {prop}");
				}
			}
		}
	}
}