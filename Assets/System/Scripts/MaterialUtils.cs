using UnityEngine;
using UnityEngine.Rendering;

public static class MaterialUtils
{
	public static Material CreateSurfaceFilmMaterial(Color baseColor, Texture2D noiseTexture, float filmIntensity = 0.2f, float noiseScale = 1f)
	{
		var surfaceFilmShader = Shader.Find("Unlit/URPSurfaceFilm");
		if (!surfaceFilmShader)
		{
			Debug.LogWarning("MaterialUtils: Unlit/URPSurfaceFilm shader not found! Falling back to URP/Unlit.");
			return CreateTransparentUnlitMaterial(new Color(0.1f, 0.1f, 0.1f, 0.5f));
		}

		if (!noiseTexture)
		{
			Debug.LogWarning("MaterialUtils: Noise texture is null! Falling back to URP/Unlit.");
			return CreateTransparentUnlitMaterial(new Color(0.1f, 0.1f, 0.1f, 0.5f));
		}

		var material = new Material(surfaceFilmShader) { renderQueue = (int)RenderQueue.Transparent };
		material.SetColor("_BaseColor", baseColor);
		material.SetTexture("_NoiseTex", noiseTexture);
		material.SetFloat("_FilmIntensity", filmIntensity);
		material.SetFloat("_NoiseScale", noiseScale);
		material.SetFloat("_Surface", 1f);
		material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
		material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
		material.SetFloat("_ZWrite", 0f);
		material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
		material.SetOverrideTag("RenderType", "Transparent");
		// Clear frosted-specific properties
		if (material.HasProperty("_MainTex"))
			material.SetTexture("_MainTex", null);
		if (material.HasProperty("_Radius"))
			material.SetFloat("_Radius", 0);
		if (material.HasProperty("_NoiseStrength"))
			material.SetFloat("_NoiseStrength", 0);
		return material;
	}

	public static Material CreateFrostedMaterial(Color baseColor, float frostRadius = 12f, RenderTexture reflectionTexture = null, Texture2D noiseTexture = null, float noiseStrength = 0.02f)
	{
		var frostedShader = Shader.Find("Unlit/URPFrostedGlass");
		if (!frostedShader)
		{
			Debug.LogWarning("MaterialUtils: Unlit/URPFrostedGlass shader not found! Falling back to URP/Unlit.");
			return CreateTransparentUnlitMaterial(new Color(0.1f, 0.1f, 0.1f, 0.5f));
		}

		var material = new Material(frostedShader) { renderQueue = (int)RenderQueue.Transparent };
		material.SetColor("_BaseColor", baseColor);
		material.SetFloat("_Radius", frostRadius);
		material.SetFloat("_NoiseStrength", noiseStrength);
		material.SetFloat("_Surface", 1f);
		material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
		material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
		material.SetFloat("_ZWrite", 0f);
		material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
		material.SetOverrideTag("RenderType", "Transparent");
		if (reflectionTexture != null)
			material.SetTexture("_MainTex", reflectionTexture);
		if (noiseTexture != null)
			material.SetTexture("_NoiseTex", noiseTexture);
		// Clear surface film properties
		if (material.HasProperty("_FilmIntensity"))
			material.SetFloat("_FilmIntensity", 0);
		if (material.HasProperty("_NoiseScale"))
			material.SetFloat("_NoiseScale", 0);
		return material;
	}

	public static Material CreateTransparentUnlitMaterial(Color baseColor)
	{
		var unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
		if (!unlitShader)
		{
			Debug.LogError("MaterialUtils: Universal Render Pipeline/Unlit shader not found! Ensure URP is installed.");
			return null;
		}

		var material = new Material(unlitShader) { renderQueue = (int)RenderQueue.Transparent };
		material.SetColor("_BaseColor", baseColor);
		material.SetFloat("_Surface", 1f);
		material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
		material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
		material.SetFloat("_ZWrite", 0f);
		material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
		material.SetOverrideTag("RenderType", "Transparent");
		// Clear all effect-specific properties
		if (material.HasProperty("_MainTex"))
			material.SetTexture("_MainTex", null);
		if (material.HasProperty("_NoiseTex"))
			material.SetTexture("_NoiseTex", null);
		if (material.HasProperty("_Radius"))
			material.SetFloat("_Radius", 0);
		if (material.HasProperty("_NoiseStrength"))
			material.SetFloat("_NoiseStrength", 0);
		if (material.HasProperty("_FilmIntensity"))
			material.SetFloat("_FilmIntensity", 0);
		if (material.HasProperty("_NoiseScale"))
			material.SetFloat("_NoiseScale", 0);
		return material;
	}
}