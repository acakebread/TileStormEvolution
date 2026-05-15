using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MassiveHadronLtd
{
	public static class SkyboxUtility
	{
		private static readonly Dictionary<string, Material> ImportedSkyboxCache = new(System.StringComparer.OrdinalIgnoreCase);
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

		public static Material CreateImportedSkyboxMaterial(Texture texture, string sourceName)
		{
			if (texture == null)
				return null;

			var key = sourceName?.Trim();
			if (!string.IsNullOrWhiteSpace(key) && ImportedSkyboxCache.TryGetValue(key, out var cached) && cached != null)
				return cached;

			var shader = Shader.Find("Skybox/Panoramic") ?? Shader.Find("Skybox/6 Sided") ?? Shader.Find("Universal Render Pipeline/Simple Lit");
			if (shader == null)
				return null;

			var material = new Material(shader)
			{
				name = string.IsNullOrWhiteSpace(sourceName) ? "Imported Skybox" : Path.GetFileNameWithoutExtension(sourceName)
			};

			material.mainTexture = texture;
			if (material.HasProperty("_MainTex"))
				material.SetTexture("_MainTex", texture);
			if (material.HasProperty("_Tex"))
				material.SetTexture("_Tex", texture);

			if (material.HasProperty("_ImageType"))
				material.SetFloat("_ImageType", 0f);
			if (material.HasProperty("_Exposure"))
				material.SetFloat("_Exposure", 1f);
			if (material.HasProperty("_Rotation"))
				material.SetFloat("_Rotation", 0f);

			if (!string.IsNullOrWhiteSpace(key))
				ImportedSkyboxCache[key] = material;

			return material;
		}

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
