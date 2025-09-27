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

	public static Material CreateFrostedMaterial(Color baseColor, float frostRadius = 64f, RenderTexture reflectionTexture = null, Texture2D noiseTexture = null, float noiseStrength = 0.02f)
	{
		var frostedShader = Shader.Find("Unlit/URPFrosted");
		if (!frostedShader)
		{
			Debug.LogWarning("MaterialUtils: Unlit/URPFrosted shader not found! Falling back to URP/Unlit.");
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

	public static Material CreateFrostMaterial(Color baseColor, float depth = 1f, RenderTexture reflectionTexture = null, Texture2D noiseTexture = null, float noiseStrength = 0.02f)
	{
		var frostShader = Shader.Find("Unlit/URPFrost");
		if (!frostShader)
		{
			Debug.LogWarning("MaterialUtils: Unlit/URPFrost shader not found! Falling back to URP/Unlit.");
			return CreateTransparentUnlitMaterial(new Color(0.1f, 0.1f, 0.1f, 0.5f));
		}

		var material = new Material(frostShader) { renderQueue = (int)RenderQueue.Transparent };
		material.SetColor("_BaseColor", baseColor);
		material.SetFloat("_Depth", depth);
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

	public static Material CreateFrostOpaqueMaterial(Color baseColor, float depth = 1f, RenderTexture reflectionTexture = null, Texture2D noiseTexture = null, float noiseStrength = 0.02f)
	{
		var frostShader = Shader.Find("Unlit/URPFrostOpaque");
		if (!frostShader)
		{
			Debug.LogWarning("MaterialUtils: Unlit/URPFrostOpaque shader not found! Falling back to URP/Unlit.");
			return new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.25f, 0.25f, 0.25f, 1.0f) }; // Opaque fallback
		}

		var material = new Material(frostShader) { renderQueue = (int)RenderQueue.Geometry };
		material.SetColor("_BaseColor", baseColor);
		material.SetFloat("_Depth", depth);
		material.SetFloat("_NoiseStrength", noiseStrength);
		if (reflectionTexture != null)
			material.SetTexture("_MainTex", reflectionTexture);
		if (noiseTexture != null)
			material.SetTexture("_NoiseTex", noiseTexture);

		// Clear unrelated properties
		if (material.HasProperty("_FilmIntensity"))
			material.SetFloat("_FilmIntensity", 0);
		if (material.HasProperty("_NoiseScale"))
			material.SetFloat("_NoiseScale", 0);

		// Force shader recompilation
		material.shader = frostShader;
		return material;
	}

	public static Material CreateWaterMaterial(Color baseColor, RenderTexture reflectionTexture, float rippleSpeed = 0.5f, float rippleAmplitude = 0.5f, float rippleFrequency = 0.5f, float rippleOffset = 0.5f, float depthThreshold = 5.0f, float depthTolerance = 0.01f, float waterPlaneY = 0.0f, float debugDepthScalar = 0.0f)
	{
		var waterShader = Shader.Find("Unlit/URPWater");
		if (!waterShader)
		{
			Debug.LogWarning("MaterialUtils: Unlit/URPWater shader not found! Falling back to URP/Unlit.");
			return CreateTransparentUnlitMaterial(new Color(0.1f, 0.1f, 0.1f, 0.5f));
		}

		var material = new Material(waterShader) { renderQueue = (int)RenderQueue.Transparent + 1 };
		material.SetColor("_BaseColor", baseColor);
		material.SetFloat("_RippleSpeed", rippleSpeed);
		material.SetFloat("_RippleAmplitude", rippleAmplitude);
		material.SetFloat("_RippleFrequency", rippleFrequency);
		material.SetFloat("_RippleOffset", rippleOffset);
		material.SetFloat("_TimeSeed", 0f);
		material.SetFloat("_DepthThreshold", depthThreshold);
		material.SetFloat("_DepthTolerance", depthTolerance);
		material.SetFloat("_WaterPlaneY", waterPlaneY);
		material.SetFloat("_DebugDepthScalar", debugDepthScalar);
		if (reflectionTexture != null)
			material.SetTexture("_MainTex", reflectionTexture);

		// Clear unrelated properties
		if (material.HasProperty("_NoiseTex"))
			material.SetTexture("_NoiseTex", null);
		if (material.HasProperty("_Depth"))
			material.SetFloat("_Depth", 0);
		if (material.HasProperty("_NoiseStrength"))
			material.SetFloat("_NoiseStrength", 0);
		if (material.HasProperty("_FilmIntensity"))
			material.SetFloat("_FilmIntensity", 0);
		if (material.HasProperty("_NoiseScale"))
			material.SetFloat("_NoiseScale", 0);

		// Force shader recompilation
		material.shader = waterShader;
		return material;
	}

	public static Material CreateWaterMaterialOpaque(Color baseColor, RenderTexture reflectionTexture, float rippleSpeed = 0.5f, float rippleAmplitude = 0.5f, float rippleFrequency = 0.5f, float rippleOffset = 0.5f, float depthThreshold = 5.0f, float depthTolerance = 0.01f, float debugDepthScalar = 0.0f)
	{
		var waterShader = Shader.Find("Unlit/URPWaterOpaque");
		if (!waterShader)
		{
			Debug.LogWarning("MaterialUtils: Unlit/URPWaterOpaque shader not found! Falling back to URP/Unlit.");
			return new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.25f, 0.5f, 0.75f, 1.0f) }; // Opaque fallback
		}

		var material = new Material(waterShader) { renderQueue = (int)RenderQueue.Geometry };
		material.SetColor("_BaseColor", baseColor);
		material.SetFloat("_RippleSpeed", rippleSpeed);
		material.SetFloat("_RippleAmplitude", rippleAmplitude);
		material.SetFloat("_RippleFrequency", rippleFrequency);
		material.SetFloat("_RippleOffset", rippleOffset);
		material.SetFloat("_TimeSeed", 0f);
		material.SetFloat("_DepthThreshold", depthThreshold);
		material.SetFloat("_DepthTolerance", depthTolerance);
		material.SetFloat("_DebugDepthScalar", debugDepthScalar);
		if (reflectionTexture != null)
			material.SetTexture("_MainTex", reflectionTexture);

		// Clear unrelated properties
		if (material.HasProperty("_NoiseTex"))
			material.SetTexture("_NoiseTex", null);
		if (material.HasProperty("_Depth"))
			material.SetFloat("_Depth", 0);
		if (material.HasProperty("_NoiseStrength"))
			material.SetFloat("_NoiseStrength", 0);
		if (material.HasProperty("_FilmIntensity"))
			material.SetFloat("_FilmIntensity", 0);
		if (material.HasProperty("_NoiseScale"))
			material.SetFloat("_NoiseScale", 0);

		// Force shader recompilation
		material.shader = waterShader;
		return material;
	}

	public static Material CreateOceanOpaqueMaterial(Color baseColor, float rippleSpeed = 0.5f, float rippleAmplitude = 0.5f, float rippleFrequency = 0.5f, float rippleOffset = 0.5f, float frostDepth = 0.5f, float frostNoiseStrength = 0.02f, float frostThreshold = 0.8f, float frostFadeRange = 0.1f, RenderTexture reflectionTexture = null, Texture2D noiseTexture = null)
	{
		var oceanShader = Shader.Find("Unlit/URPOceanOpaque");
		if (!oceanShader)
		{
			Debug.LogError("MaterialUtils: Unlit/URPOceanOpaque shader not found! Ensure the shader file is in the project and named correctly.");
			return new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.25f, 0.5f, 0.75f, 1.0f) };
		}
		Debug.Log("MaterialUtils: Successfully found Unlit/URPOceanOpaque shader.");

		var material = new Material(oceanShader) { renderQueue = (int)RenderQueue.Geometry };
		material.SetColor("_BaseColor", baseColor);
		material.SetFloat("_RippleSpeed", rippleSpeed);
		material.SetFloat("_RippleAmplitude", rippleAmplitude);
		material.SetFloat("_RippleFrequency", rippleFrequency);
		material.SetFloat("_RippleOffset", rippleOffset);
		material.SetFloat("_DepthThreshold", 128.0f); // Maps to _DepthMax, default 128
		material.SetFloat("_FrostDepth", frostDepth); // Maps to _Depth
		material.SetFloat("_FrostNoiseStrength", frostNoiseStrength); // Maps to _NoiseStrength
		material.SetFloat("_FrostThreshold", frostThreshold);
		material.SetFloat("_FrostFadeRange", frostFadeRange);
		if (reflectionTexture != null)
			material.SetTexture("_MainTex", reflectionTexture);
		if (noiseTexture != null)
			material.SetTexture("_NoiseTex", noiseTexture);

		// Clear unrelated properties
		if (material.HasProperty("_Depth"))
			material.SetFloat("_Depth", 0);
		if (material.HasProperty("_FilmIntensity"))
			material.SetFloat("_FilmIntensity", 0);
		if (material.HasProperty("_NoiseScale"))
			material.SetFloat("_NoiseScale", 0);

		// Force shader recompilation
		material.shader = oceanShader;
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