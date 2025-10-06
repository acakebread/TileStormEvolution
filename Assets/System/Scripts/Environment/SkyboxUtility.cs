using UnityEngine;

public static class SkyboxUtility
{
	// Optional: Reference to a default cubemap to use as a fallback (set in Inspector or code)
	private static Cubemap defaultCubemap = null;
	private static Material lastSkyboxMaterial = null;
	private static Cubemap lastCubemap = null;

	private static Material defaultSkyboxMaterial;

	/// <summary>
	/// Static constructor to automatically initialize the default skybox material from the current RenderSettings.
	/// </summary>
	static SkyboxUtility()
	{
		defaultSkyboxMaterial = RenderSettings.skybox;
	}

	/// <summary>
	/// Loads a skybox material from Resources using the provided path prefix and skybox name,
	/// then sets RenderSettings.skybox to it or falls back to the initial default skybox material.
	/// Constructs the resource path as "{pathPrefix}{skyboxName}Skybox" (with .mat extension removed if present).
	/// </summary>
	/// <param name="pathPrefix">The base path for skybox resources (e.g., from PreviewSettings).</param>
	/// <param name="skyboxName">The name of the skybox (e.g., music name or custom ID).</param>
	public static void SetSkybox(string pathPrefix, string skyboxName)
	{
		if (string.IsNullOrEmpty(pathPrefix) || string.IsNullOrEmpty(skyboxName))
		{
			Debug.LogWarning("Path prefix or skybox name is null/empty.");
			return;
		}

		var skyboxPath = $"{pathPrefix}{skyboxName}Skybox".Replace(".mat", "");
		var material = Resources.Load<Material>(skyboxPath);
		RenderSettings.skybox = material ? material : defaultSkyboxMaterial;
	}

	/// <summary>
	/// Loads a skybox material from Resources using the provided path prefix and skybox name.
	/// Constructs the resource path as "{pathPrefix}{skyboxName}Skybox" (with .mat extension removed if present).
	/// </summary>
	/// <param name="pathPrefix">The base path for skybox resources (e.g., from PreviewSettings).</param>
	/// <param name="skyboxName">The name of the skybox (e.g., music name or custom ID).</param>
	/// <returns>The loaded Material, or null if not found.</returns>
	public static Material LoadSkyboxMaterial(string pathPrefix, string skyboxName)
	{
		if (string.IsNullOrEmpty(pathPrefix) || string.IsNullOrEmpty(skyboxName))
		{
			Debug.LogWarning("Path prefix or skybox name is null/empty.");
			return null;
		}

		var skyboxPath = $"{pathPrefix}{skyboxName}Skybox".Replace(".mat", "");
		return Resources.Load<Material>(skyboxPath);
	}

	/// <summary>
	/// Attempts to extract a cubemap from a skybox material and assign it to the water material.
	/// Supports both cubemap-based and 6-sided skybox shaders.
	/// </summary>
	public static void SetSkyboxCubemap(Material waterMaterial, Material skyboxMaterial)
	{
		if (waterMaterial == null)
		{
			Debug.LogWarning("Water material is null.");
			return;
		}

		if (skyboxMaterial == null)
		{
			Debug.LogWarning("Skybox material is null.");
			waterMaterial.SetTexture("_Skybox", defaultCubemap); // Use default cubemap or null
			return;
		}

		// Skip if the skybox material hasn't changed
		if (skyboxMaterial == lastSkyboxMaterial && lastCubemap != null)
		{
			waterMaterial.SetTexture("_Skybox", lastCubemap);
			return;
		}

		Cubemap cubemap = null;

		// Check for cubemap-based skybox
		if (skyboxMaterial.HasProperty("_Tex"))
		{
			cubemap = skyboxMaterial.GetTexture("_Tex") as Cubemap;
		}
		else if (skyboxMaterial.HasProperty("_MainTex"))
		{
			cubemap = skyboxMaterial.GetTexture("_MainTex") as Cubemap;
		}

		if (cubemap != null)
		{
			waterMaterial.SetTexture("_Skybox", cubemap);
			lastSkyboxMaterial = skyboxMaterial;
			lastCubemap = cubemap;
			return;
		}

		// Handle "Skybox/6 Sided" – no cubemap creation, use fallback
		if (skyboxMaterial.HasProperty("_FrontTex"))
		{
			// Skip cubemap creation for 6-sided skyboxes; use fallback
			waterMaterial.SetTexture("_Skybox", defaultCubemap); // Use default cubemap or null
			lastSkyboxMaterial = skyboxMaterial;
			lastCubemap = defaultCubemap;
		}
		else
		{
			Debug.LogWarning($"Skybox shader '{skyboxMaterial.shader.name}' does not expose a cubemap property.");
			waterMaterial.SetTexture("_Skybox", defaultCubemap); // Use default cubemap or null
			lastSkyboxMaterial = skyboxMaterial;
			lastCubemap = defaultCubemap;
		}
	}
}