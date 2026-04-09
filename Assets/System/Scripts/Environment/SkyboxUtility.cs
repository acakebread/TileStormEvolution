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
			CubemapUtility.ClearCurrentCache();   // Clean everything when skybox changes
			OnSkyboxChanged?.Invoke(RenderSettings.skybox);
		}

		public static Material GetSkyboxMaterialForName(string pathOrName) => string.IsNullOrEmpty(pathOrName) ? defaultSkyboxMaterial : GetSkyboxMaterial(pathOrName);
		public static Color ComputeBrightColor(Material skybox, float threshold = 0.85f) => CubemapUtility.ComputeBrightColor(CubemapUtility.GetTintedCubemap(skybox), threshold);
		public static Color ComputeAmbientColor(Material skybox, float power = 1f) => CubemapUtility.ComputeAmbientColor(CubemapUtility.GetTintedCubemap(skybox), power);

		private static Material GetSkyboxMaterial(string pathOrName = null) => AssetRegistry<Material>.FindSkybox(pathOrName) ?? defaultSkyboxMaterial;

		/// <summary>
		/// Returns the raw _Tint color from the skybox material exactly as set in the inspector.
		/// No extra multiply, no clamping — just pass it through to the shader.
		/// </summary>
		public static Color GetSkyboxTint(Material skyMat = null)
		{
			if (skyMat == null)
				skyMat = RenderSettings.skybox;

			if (skyMat == null)
				return Color.white;

			// Skybox/Cubemap uses _Tint
			if (skyMat.HasProperty("_Tint"))
				return skyMat.GetColor("_Tint");

			// Fallback for procedural skyboxes
			if (skyMat.HasProperty("_SkyTint"))
				return skyMat.GetColor("_SkyTint");

			return Color.white;
		}
	}
}