using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace MassiveHadronLtd
{
	public static class MaterialUtils
	{
		[Serializable]
		public struct PerfectMirrorDefaults
		{
			public Color tint;
			public static PerfectMirrorDefaults Get() => new() { tint = new Color(0.5f, 0.5f, 0.5f, 1f) };
		}

		[Serializable]
		public struct SurfaceFilmDefaults
		{
			public Color tint;
			public float noiseScale;
			public static SurfaceFilmDefaults Get() => new() { tint = new Color(0.5f, 0.5f, 0.5f, 0.5f), noiseScale = 1f };
		}

		[Serializable]
		public struct FrostDefaults
		{
			public Color tint;
			public float depth;
			public float noiseStrength;
			public static FrostDefaults Get() => new() { tint = new Color(1f, 1f, 1f, 0.1f), depth = 0.05f, noiseStrength = 0.2f };
		}

		[Serializable]
		public struct WaterDefaults
		{
			public Color tint;
			public float rippleSpeed;
			public float rippleAmplitude;
			public float rippleFrequency;
			public float rippleOffset;
			public float reflectionStrength;
			public static WaterDefaults Get() => new()
			{
				tint = new Color(0.05f, 0.05f, 0.05f, 0.25f),
				rippleSpeed = 0.2f,
				rippleAmplitude = 0.075f,
				rippleFrequency = 0.35f,
				rippleOffset = 0f,
				reflectionStrength = 0.95f
			};
		}

		[Serializable]
		public struct OceanDefaults
		{
			public Color tint;
			public float rippleSpeed;
			public float rippleAmplitude;
			public float rippleFrequency;
			public float rippleOffset;
			public float frostDepth;
			public float noiseStrength;
			public float frostThreshold;
			public float frostFadeRange;
			public static OceanDefaults Get() => new()
			{
				tint = new Color(0.5f, 0.5f, 0.5f, 0.5f),
				rippleSpeed = 0.05f,
				rippleAmplitude = 0.05f,
				rippleFrequency = 0.015f,
				rippleOffset = 0f,
				frostDepth = 0.15f,
				noiseStrength = 0.15f,
				frostThreshold = 0.65f,
				frostFadeRange = 0.065f
			};
		}

		[Serializable]
		public struct FresnelReflectionDefaults
		{
			public float reflectionStrength;
			public float fresnelSharpness;        // ← changed
			public static FresnelReflectionDefaults Get() => new()
			{
				reflectionStrength = 0.25f,
				fresnelSharpness = 12f
			};
		}

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

		public static Material CreatePerfectMirrorOpaqueMaterial(RenderTexture reflectionTexture, Color dimColor)
		{
			var shader = Shader.Find("Unlit/URPMirrorOpaque");
			if (!shader)
			{
				Debug.LogError("Unlit/URPMirrorOpaque not found!");
				return new Material(Shader.Find("Universal Render Pipeline/Unlit"));
			}

			var mat = new Material(shader)
			{
				renderQueue = (int)RenderQueue.Geometry
			};

			mat.SetTexture("_MainTex", reflectionTexture);
			mat.SetColor("_DimColor", dimColor);

			return mat;
		}

		public static Material CreateMirrorWithFilmOpaque(
			RenderTexture reflectionTexture,
			Color dimColor,
			Texture2D noiseTexture,
			float filmIntensity = 0.2f,
			float noiseScale = 1f)
		{
			var shader = Shader.Find("Unlit/URPMirrorWithFilmOpaque");
			if (!shader)
			{
				Debug.LogError("Unlit/URPMirrorWithFilmOpaque not found!");
				return new Material(Shader.Find("Universal Render Pipeline/Unlit"));
			}

			var mat = new Material(shader)
			{
				renderQueue = (int)RenderQueue.Geometry
			};

			mat.SetTexture("_MainTex", reflectionTexture);
			mat.SetColor("_DimColor", dimColor);
			mat.SetTexture("_NoiseTex", noiseTexture);
			mat.SetFloat("_FilmIntensity", filmIntensity);
			mat.SetFloat("_NoiseScale", noiseScale);

			return mat;
		}

		public static Material CreatePerlinWangOpaque(
					RenderTexture reflectionTexture,
					Color dimColor,
					Texture2D noiseTexture,
					float filmIntensity = 0.2f,
					float noiseScale = 1f,
					float reflectionStrength = 0.25f,
					Texture skyboxTexture = null,
					float fresnelSharpness = 12f)
		{
			var shader = Shader.Find("Unlit/URPPerlinWangOpaque");
			if (!shader)
			{
				Debug.LogError("Unlit/URPPerlinWangOpaque not found!");
				return new Material(Shader.Find("Universal Render Pipeline/Unlit"));
			}

			var mat = new Material(shader)
			{
				renderQueue = (int)RenderQueue.Geometry
			};

			mat.SetTexture("_MainTex", reflectionTexture);
			mat.SetColor("_DimColor", dimColor);
			mat.SetTexture("_NoiseTex", noiseTexture);
			mat.SetFloat("_FilmIntensity", filmIntensity);
			mat.SetFloat("_NoiseScale", noiseScale);

			// New Fresnel reflection properties
			mat.SetFloat("_ReflectionStrength", reflectionStrength);
			mat.SetFloat("_FresnelSharpness", fresnelSharpness);
			if (skyboxTexture != null)
				mat.SetTexture("_Skybox", skyboxTexture);

			return mat;
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

		public static Material CreateFrostOpaqueMaterial(
			Color baseColor,
			float depth = 1f,
			RenderTexture reflectionTexture = null,
			Texture2D noiseTexture = null,
			float noiseStrength = 0.02f,
			float reflectionStrength = 0.25f,
			Texture skyboxTexture = null,
			float fresnelSharpness = 12f)
		{
			var frostShader = Shader.Find("Unlit/URPFrostOpaque");
			if (!frostShader)
			{
				Debug.LogWarning("MaterialUtils: Unlit/URPFrostOpaque shader not found! Falling back to URP/Unlit.");
				return new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.25f, 0.25f, 0.25f, 1.0f) };
			}

			var material = new Material(frostShader)
			{
				renderQueue = (int)RenderQueue.Geometry
			};

			material.SetColor("_BaseColor", baseColor);
			material.SetFloat("_Depth", depth);
			material.SetFloat("_NoiseStrength", noiseStrength);

			// New Fresnel reflection properties
			material.SetFloat("_ReflectionStrength", reflectionStrength);
			material.SetFloat("_FresnelSharpness", fresnelSharpness);

			if (skyboxTexture != null)
				material.SetTexture("_Skybox", skyboxTexture);

			if (reflectionTexture != null)
				material.SetTexture("_MainTex", reflectionTexture);
			if (noiseTexture != null)
				material.SetTexture("_NoiseTex", noiseTexture);

			// Clear unrelated properties
			if (material.HasProperty("_FilmIntensity"))
				material.SetFloat("_FilmIntensity", 0);
			if (material.HasProperty("_NoiseScale"))
				material.SetFloat("_NoiseScale", 0);

			material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
			material.SetOverrideTag("RenderType", "Opaque");

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

		public static Material CreateWaterMaterialOpaque(
			Color baseColor,
			RenderTexture reflectionTexture,
			float rippleSpeed = 0.5f,
			float rippleAmplitude = 0.5f,
			float rippleFrequency = 0.5f,
			float rippleOffset = 0.5f,
			float reflectionStrength = 0.8f,
			Texture skyboxTexture = null,
			float fresnelSharpness = 12f,           // ← added
			float depthThreshold = 5.0f,
			float depthTolerance = 0.01f,
			float debugDepthScalar = 0.0f)
		{
			var waterShader = Shader.Find("Unlit/URPWaterOpaque");
			if (!waterShader)
			{
				Debug.LogWarning("MaterialUtils: Unlit/URPWaterOpaque shader not found! Falling back to URP/Unlit.");
				return new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.25f, 0.5f, 0.75f, 1.0f) };
			}

			var material = new Material(waterShader) { renderQueue = (int)RenderQueue.Geometry };

			material.SetColor("_BaseColor", baseColor);
			material.SetFloat("_RippleSpeed", rippleSpeed);
			material.SetFloat("_RippleAmplitude", rippleAmplitude);
			material.SetFloat("_RippleFrequency", rippleFrequency);
			material.SetFloat("_RippleOffset", rippleOffset);
			material.SetFloat("_ReflectionStrength", reflectionStrength);
			material.SetFloat("_FresnelSharpness", fresnelSharpness);     // ← added

			if (skyboxTexture != null)
				material.SetTexture("_Skybox", skyboxTexture);

			material.SetFloat("_TimeSeed", 0f);
			material.SetFloat("_DepthThreshold", depthThreshold);
			material.SetFloat("_DepthTolerance", depthTolerance);
			material.SetFloat("_DebugDepthScalar", debugDepthScalar);

			if (reflectionTexture != null)
				material.SetTexture("_MainTex", reflectionTexture);

			// Clear unrelated properties
			if (material.HasProperty("_NoiseTex")) material.SetTexture("_NoiseTex", null);
			if (material.HasProperty("_Depth")) material.SetFloat("_Depth", 0);
			if (material.HasProperty("_NoiseStrength")) material.SetFloat("_NoiseStrength", 0);
			if (material.HasProperty("_FilmIntensity")) material.SetFloat("_FilmIntensity", 0);
			if (material.HasProperty("_NoiseScale")) material.SetFloat("_NoiseScale", 0);

			material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
			material.SetOverrideTag("RenderType", "Opaque");

			material.shader = waterShader; // Force recompilation
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

			material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
			material.SetOverrideTag("RenderType", "Opaque");

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

		public static Material CreateAlwaysOnTopUnlitMaterial(
			Color baseColor = default,
			bool ignoreDepthTest = true,     // ← controls ZTest Never / Always
			bool disableDepthWrite = true)     // ← usually want this true
		{
			//var shader = Shader.Find("Universal Render Pipeline/Unlit");
			//if (shader == null)
			//{
			//	Debug.LogError("URP Unlit shader not found. Is URP installed and set as active render pipeline?");
			//	return null;
			//}

			var shader = Shader.Find("Custom/AlwaysOnTopUnlit");
			if (shader == null)
			{
				Debug.LogError("Custom/AlwaysOnTopUnlit shader not found.");
				return null;
			}

			var mat = new Material(shader)
			{
				// Important: place it after all normal transparent objects
				renderQueue = (int)RenderQueue.Overlay   // 4000 — very late
			};

			// Core transparent blending
			mat.SetFloat("_Surface", 1f);                    // Transparent
			mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
			mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
			mat.SetFloat("_ZWrite", disableDepthWrite ? 0f : 1f);
			mat.SetInt("_ZTest", ignoreDepthTest ? (int)CompareFunction.Always : (int)CompareFunction.LessEqual);

			mat.SetColor("_BaseColor", baseColor == default ? Color.white : baseColor);

			// Make sure URP knows it's transparent
			mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
			mat.SetOverrideTag("RenderType", "Transparent");

			// Optional: prevent any texture/noise/effect interference
			mat.SetTexture("_MainTex", null);
			if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", null);
			if (mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", null);

			return mat;
		}

		public static Material CreateOpaqueUnlitMaterial(Color baseColor)
		{
			var unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
			if (!unlitShader)
			{
				Debug.LogError("MaterialUtils: Universal Render Pipeline/Unlit shader not found! Ensure URP is installed.");
				return null;
			}

			var material = new Material(unlitShader) { renderQueue = (int)RenderQueue.Geometry };
			material.SetColor("_BaseColor", baseColor);
			material.SetFloat("_Surface", 0f); // Opaque surface
			material.SetFloat("_SrcBlend", (float)BlendMode.One);
			material.SetFloat("_DstBlend", (float)BlendMode.Zero);
			material.SetFloat("_ZWrite", 1f); // Enable ZWrite for opaque materials
			material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
			material.SetOverrideTag("RenderType", "Opaque");

			//// Clear unrelated properties
			//if (material.HasProperty("_MainTex"))
			//	material.SetTexture("_MainTex", null);
			//if (material.HasProperty("_NoiseTex"))
			//	material.SetTexture("_NoiseTex", null);
			//if (material.HasProperty("_Radius"))
			//	material.SetFloat("_Radius", 0);
			//if (material.HasProperty("_NoiseStrength"))
			//	material.SetFloat("_NoiseStrength", 0);
			//if (material.HasProperty("_FilmIntensity"))
			//	material.SetFloat("_FilmIntensity", 0);
			//if (material.HasProperty("_NoiseScale"))
			//	material.SetFloat("_NoiseScale", 0);

			// Force shader recompilation
			material.shader = unlitShader;
			return material;
		}

		//public static Material CreateTransparentLineMaterial(Color color)
		//{
		//	// Sprites/Default supports vertex colors (required for LineRenderer gradients)
		//	var shader = Shader.Find("Sprites/Default");
		//	if (!shader)
		//	{
		//		Debug.LogError("MaterialUtils: Sprites/Default shader not found! Falling back to Transparent Unlit.");
		//		return CreateTransparentUnlitMaterial(color);
		//	}

		//	var mat = new Material(shader)
		//	{
		//		renderQueue = (int)RenderQueue.Transparent
		//	};

		//	mat.SetColor("_Color", color);
		//	return mat;
		//}

		public static Material CreateTransparentLineMaterial(Color color)
		{
			// Sprites/Default is perfect: supports vertex colors + tint + alpha
			var shader = Shader.Find("Sprites/Default");
			if (!shader)
			{
				Debug.LogWarning("MaterialUtils: Sprites/Default shader not found! Falling back to URP Unlit (no vertex color support).");
				return CreateTransparentUnlitMaterial(color);
			}

			var mat = new Material(shader)
			{
				name = "TransparentLineMaterial",
				renderQueue = (int)RenderQueue.Transparent,
				hideFlags = HideFlags.HideAndDontSave // optional: cleaner in editor
			};

			mat.SetColor("_Color", color);
			mat.SetInt("_ZWrite", 0);
			mat.SetInt("_Cull", (int)CullMode.Off);
			mat.EnableKeyword("_ALPHATEST_ON"); // optional: better edge behavior
			mat.SetOverrideTag("RenderType", "Transparent");
			mat.SetFloat("_Surface", 1f);
			mat.SetFloat("_BlendOp", (float)BlendOp.Add);
			mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
			mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);

			return mat;
		}

		public static Material CreateAdditiveUnlitMaterial(Color color)
		{
			var unlitShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
			if (!unlitShader)
			{
				Debug.LogError("CreateAdditiveUnlitMaterial: No Unlit shader found!");
				return null;
			}

			var material = new Material(unlitShader)
			{
				hideFlags = HideFlags.HideAndDontSave
			};

			material.SetColor("_BaseColor", color);

			// Additive blending
			material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
			material.SetInt("_DstBlend", (int)BlendMode.One);

			material.SetInt("_ZWrite", 0);
			material.SetInt("_ZTest", (int)CompareFunction.Always); // ← fixed

			material.SetInt("_Cull", (int)CullMode.Off);

			// URP keywords
			material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
			material.DisableKeyword("_SURFACE_TYPE_OPAQUE");
			material.EnableKeyword("_ALPHABLEND_ON");
			material.DisableKeyword("_ALPHATEST_ON");
			material.DisableKeyword("_ALPHAPREMULTIPLY_ON");

			material.SetOverrideTag("RenderType", "Transparent");
			material.renderQueue = 4000; // draw last

			return material;
		}

		public static Material CreateEmissiveMaterial(Color color)
		{
			var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
			material.SetColor("_BaseColor", new Color(0f, 0f, 0f, 1f));
			material.EnableKeyword("_EMISSION");
			material.SetColor("_EmissionColor", color);
			return material;
		}

		public static Color EmissiiveColour(Material mat, Color fallback = default)
		{
			if (mat == null) return fallback;

			// Most common emission color names in order of probability
			string[] candidates = { "_EmissionColor", "_EmissiveColor", "_TintColor", "_Color" };

			foreach (var prop in candidates)
			{
				if (mat.HasProperty(prop))
				{
					var c = mat.GetColor(prop);
					// Very small values usually aren't intended to glow
					if (c.maxColorComponent > 0.02f)
						return c;
				}
			}

			return fallback;
		}

		// Check if this material is intended to be emissive
		public static bool IsEmissive(Material material)
		{
			if (material == null) return false;

			// Classic lit shaders
			if (material.IsKeywordEnabled("_EMISSION"))
				return true;

			// Emission map is almost always a strong indicator
			if (material.GetTexture("_EmissionMap") != null)
				return true;

			// Try most common emission color property names
			string[] emissionColorNames = new[]
			{
				"_EmissionColor",
				"_EmissiveColor",           // HDRP / some custom
				"_TintColor",               // ← very common in legacy additive particles!
				"_Color",                   // sometimes abused as emission
				"_BaseColor",               // URP/HDRP style
			};

			foreach (var prop in emissionColorNames)
			{
				if (material.HasProperty(prop))
				{
					var color = material.GetColor(prop);
					// Using 0.01f threshold prevents almost-black from being considered emissive
					if (color.maxColorComponent > 0.01f)
						return true;
				}
			}

			return false;
		}

		public static void ForceMaterialRefresh(Material mat)
		{
			if (mat == null) return;

			// Force emission refresh
			if (mat.HasProperty("_EmissionColor"))
			{
				Color col = mat.GetColor("_EmissionColor");
				mat.SetColor("_EmissionColor", col);
			}

			// Dirty the material to force Unity to re-evaluate
			if (mat.HasProperty("_Surface"))
				mat.SetFloat("_Surface", mat.GetFloat("_Surface"));

			mat.SetFloat("_Smoothness", mat.GetFloat("_Smoothness"));
		}
	}
}