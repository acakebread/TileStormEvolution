using ClassicTilestorm.Assets;
using System.IO;
using UnityEngine;

namespace MassiveHadronLtd
{
	public static class SkyboxUtility
	{
		public static System.Action<Material> OnSkyboxChanged;

		private static Material defaultSkyboxMaterial;
		//private static Cubemap lastTintedCubemap = null;
		//private static Material lastTintedSourceMaterial = null;

		static SkyboxUtility() => defaultSkyboxMaterial = RenderSettings.skybox;

		public static void SetSkybox(string pathOrName = null)
		{
			var material = string.IsNullOrEmpty(pathOrName) ? defaultSkyboxMaterial : GetSkyboxMaterial(pathOrName);
			RenderSettings.skybox = material ? material : defaultSkyboxMaterial;

			OnSkyboxChanged?.Invoke(RenderSettings.skybox);
		}

		public static Material GetSkyboxMaterialForName(string pathOrName)
			=> string.IsNullOrEmpty(pathOrName) ? defaultSkyboxMaterial : GetSkyboxMaterial(pathOrName);

		private static Material GetSkyboxMaterial(string pathOrName = null)
		{
			//if (string.IsNullOrEmpty(pathOrName))
			//	return defaultSkyboxMaterial;

			//string normalized = pathOrName.Replace("\\", "/").Trim('/');

			//if (normalized.Contains("/"))
			//{
			//	string loadPath = Path.GetFileNameWithoutExtension(normalized);
			//	string directory = Path.GetDirectoryName(normalized)?.Replace("\\", "/").Trim('/') ?? "";
			//	string fullPath = string.IsNullOrEmpty(directory) ? loadPath : directory + "/" + loadPath;

			//	Material direct = Resources.Load<Material>(fullPath);
			//	if (direct != null)
			//		return direct;

			//	Debug.LogWarning($"SkyboxUtility: Direct load failed for '{fullPath}'");
			//	return null;
			//}

			//return AssetRegistry<Material>.FindSkybox(normalized) ?? defaultSkyboxMaterial;


			return SkyboxAssets.Find(pathOrName) ?? defaultSkyboxMaterial;
		}

		public static Cubemap GetTintedSkyboxCubemap(Material overrideSkybox = null, int resolution = 512)
		{
			Material skyMat = overrideSkybox ?? RenderSettings.skybox;
			if (skyMat == null) return null;

			//if (lastTintedCubemap != null && lastTintedSourceMaterial == skyMat)
			//	return lastTintedCubemap;

			//if (lastTintedCubemap != null)
			//{
			//	Object.DestroyImmediate(lastTintedCubemap);
			//	lastTintedCubemap = null;
			//}

			Cubemap tintedCubemap = new Cubemap(resolution, TextureFormat.RGBA32, true)
			{
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Clamp,
				name = "TintedSkyReflection_" + (skyMat.name ?? "Unnamed")
			};

			//return tintedCubemap;

			var tempGo = new GameObject("SkyboxBaker") { hideFlags = HideFlags.HideAndDontSave };
			var bakerCam = tempGo.AddComponent<Camera>();

			bakerCam.clearFlags = CameraClearFlags.Skybox;
			bakerCam.cullingMask = 0;
			bakerCam.farClipPlane = 1000f;
			bakerCam.allowHDR = true;
			bakerCam.backgroundColor = Color.black;

			var currentSky = RenderSettings.skybox;
			RenderSettings.skybox = skyMat;
			bakerCam.RenderToCubemap(tintedCubemap);
			RenderSettings.skybox = currentSky;

			Object.DestroyImmediate(bakerCam);
			Object.DestroyImmediate(tempGo);

			//lastTintedCubemap = tintedCubemap;
			//lastTintedSourceMaterial = skyMat;

			return tintedCubemap;
		}

		//public static void InvalidateTintedCache()
		//{
		//	if (lastTintedCubemap != null)
		//	{
		//		Object.DestroyImmediate(lastTintedCubemap);
		//		lastTintedCubemap = null;
		//	}
		//	lastTintedSourceMaterial = null;
		//}
	}
}