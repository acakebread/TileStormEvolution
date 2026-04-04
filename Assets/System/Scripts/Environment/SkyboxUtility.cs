using UnityEngine;

namespace MassiveHadronLtd
{
	public static class SkyboxUtility
	{
		public static System.Action<Material> OnSkyboxChanged;
		private static Material defaultSkyboxMaterial;

		static SkyboxUtility() => defaultSkyboxMaterial = RenderSettings.skybox;

		public static void SetSkybox(string pathOrName = null)
		{
			var material = string.IsNullOrEmpty(pathOrName) ? defaultSkyboxMaterial : GetSkyboxMaterial(pathOrName);
			RenderSettings.skybox = material ? material : defaultSkyboxMaterial;
			OnSkyboxChanged?.Invoke(RenderSettings.skybox);
		}

		public static Material GetSkyboxMaterialForName(string pathOrName) => string.IsNullOrEmpty(pathOrName) ? defaultSkyboxMaterial : GetSkyboxMaterial(pathOrName);
		public static Color ComputeBrightColor(Material skybox, float threshold = 0.85f) => CubemapUtility.ComputeBrightColor(CubemapUtility.GetTintedCubemap(skybox), threshold);
		public static Color ComputeAmbientColor(Material skybox, float power = 1f) => CubemapUtility.ComputeAmbientColor(CubemapUtility.GetTintedCubemap(skybox), power);

		private static Material GetSkyboxMaterial(string pathOrName = null) => AssetRegistry<Material>.FindSkybox(pathOrName) ?? defaultSkyboxMaterial;
	}
}