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
			var material = string.IsNullOrEmpty(pathOrName)
				? defaultSkyboxMaterial
				: GetSkyboxMaterial(pathOrName);

			RenderSettings.skybox = material ? material : defaultSkyboxMaterial;

			// Properly clear the ref-counted tinted cubemap
			CubemapUtility.ClearCurrentCache();

			OnSkyboxChanged?.Invoke(RenderSettings.skybox);
		}

		public static Material GetSkyboxMaterialForName(string pathOrName)
			=> string.IsNullOrEmpty(pathOrName)
				? defaultSkyboxMaterial
				: GetSkyboxMaterial(pathOrName);

		public static Color ComputeBrightColor(Material skybox, float threshold = 0.85f)
		{
			var tinted = CubemapUtility.GetTintedCubemap(skybox);
			return CubemapUtility.ComputeBrightColor(tinted, threshold);
		}

		public static Color ComputeAmbientColor(Material skybox, float power = 1f)
		{
			var tinted = CubemapUtility.GetTintedCubemap(skybox);
			return CubemapUtility.ComputeAmbientColor(tinted, power);
		}

		private static Material GetSkyboxMaterial(string pathOrName = null)
			=> ResourceResolvers.SkyboxResolver?.Find(pathOrName) ?? defaultSkyboxMaterial;

		/// <summary>
		/// Returns the raw _Tint color from the skybox material exactly as set in the inspector.
		/// </summary>
		public static Color GetSkyboxTint(Material skyMat = null)
		{
			if (skyMat == null)
				skyMat = RenderSettings.skybox;

			if (skyMat == null)
				return Color.white;

			if (skyMat.HasProperty("_Tint"))
				return skyMat.GetColor("_Tint");

			if (skyMat.HasProperty("_SkyTint"))
				return skyMat.GetColor("_SkyTint");

			return Color.white;
		}
	}
}
