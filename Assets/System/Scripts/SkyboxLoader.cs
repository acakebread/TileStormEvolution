using UnityEngine;

public static class SkyboxUtility
{
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
			return;
		}

		Cubemap cubemap = null;

		// Check if the material actually has the property before accessing it
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
			return;
		}

		// Handle "Skybox/6 Sided" manually – it has 6 separate 2D textures
		if (skyboxMaterial.HasProperty("_FrontTex"))
		{
			Texture front = skyboxMaterial.GetTexture("_FrontTex");
			Texture back = skyboxMaterial.GetTexture("_BackTex");
			Texture left = skyboxMaterial.GetTexture("_LeftTex");
			Texture right = skyboxMaterial.GetTexture("_RightTex");
			Texture up = skyboxMaterial.GetTexture("_UpTex");
			Texture down = skyboxMaterial.GetTexture("_DownTex");

			if (front != null && back != null && left != null && right != null && up != null && down != null)
			{
				Debug.LogWarning("Skybox is 6-sided. No cubemap property available. " +
								 "Consider baking these six textures into a Cubemap if required.");
				// You could create a Cubemap dynamically here if needed, but Unity doesn’t do this automatically.
			}
		}
		else
		{
			Debug.LogWarning($"Skybox shader '{skyboxMaterial.shader.name}' does not expose a cubemap property.");
		}
	}
}
