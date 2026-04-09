using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace MassiveHadronLtd
{
	public static class CubemapUtility
	{
		// Main cache for the tinted cubemap (used by water shader + compute functions)
		private static Cubemap s_CurrentTintedCubemap;
		private static Material s_CurrentSkyMat;

		// Lightweight caches for the compute functions (they depend on extra parameters)
		private static Color s_CachedAmbientColor = new Color(0.25f, 0.25f, 0.35f);
		private static float s_CachedAmbientPower = -1f;

		private static Color s_CachedBrightColor = Color.white;
		private static float s_CachedBrightThreshold = -1f;

		public static Cubemap GetSkyboxAsCubemap(Material skybox = null)
		{
			skybox ??= RenderSettings.skybox;
			if (null != skybox)
			{
				if (skybox.HasProperty("_Tex"))
					return skybox.GetTexture("_Tex") as Cubemap;
				if (skybox.HasProperty("_MainTex"))
					return skybox.GetTexture("_MainTex") as Cubemap;
			}
			return null;
		}

		public static Color ComputeBrightColor(Cubemap cubemap, float threshold = 0.85f)
		{
			if (cubemap == null)
				return Color.white;

			if (!cubemap.isReadable)
			{
				Debug.LogWarning($"Cubemap '{cubemap.name}' is not readable.");
				return Color.white;
			}

			// Use cache if the cubemap matches current tinted one AND threshold is the same
			if (cubemap == s_CurrentTintedCubemap && Mathf.Approximately(threshold, s_CachedBrightThreshold))
				return s_CachedBrightColor;

			// === Build one large array containing ALL pixels from all 6 faces ===
			var faceSize = cubemap.width;
			var pixels = new Color[faceSize * faceSize * 6];

			for (var i = 0; i < 6; i++)
			{
				var face = cubemap.GetPixels((CubemapFace)i);
				Array.Copy(face, 0, pixels, faceSize * faceSize * i, face.Length);
			}

			var result = ColourUtils.ThresholdColour(pixels, threshold);

			// Cache the result
			if (cubemap == s_CurrentTintedCubemap)
			{
				s_CachedBrightColor = result;
				s_CachedBrightThreshold = threshold;
			}

			return result;
		}

		/// <summary>
		/// Computes a good ambient colour from the skybox cubemap using luminance-weighted averaging.
		/// Brighter areas (clouds, sun, sky) contribute much more than dark areas.
		/// </summary>
		public static Color ComputeAmbientColor(Cubemap cubemap, float power = 1f)
		{
			if (cubemap == null)
				return new Color(0.25f, 0.25f, 0.35f);

			if (!cubemap.isReadable)
			{
				Debug.LogWarning($"Cubemap '{cubemap.name}' is not readable.");
				return new Color(0.25f, 0.25f, 0.35f);
			}

			// Use cache if the cubemap matches current tinted one AND power is the same
			if (cubemap == s_CurrentTintedCubemap && Mathf.Approximately(power, s_CachedAmbientPower))
				return s_CachedAmbientColor;

			var pixels = GetAllPixels(cubemap);

			if (pixels.Length == 0)
				return new Color(0.25f, 0.25f, 0.35f);

			var result = ColourUtils.ComputeAmbientColor(pixels, power);

			// Cache the result
			if (cubemap == s_CurrentTintedCubemap)
			{
				s_CachedAmbientColor = result;
				s_CachedAmbientPower = power;
			}

			return result;
		}

		/// <summary>
		/// Returns a single flat array containing ALL pixels from all 6 faces of the cubemap.
		/// Useful for full-image processing like ambient colour calculation.
		/// </summary>
		private static Color[] GetAllPixels(Cubemap cubemap)
		{
			if (cubemap == null)
				return Array.Empty<Color>();

			if (!cubemap.isReadable)
			{
				Debug.LogWarning($"Cubemap '{cubemap.name}' is not readable. Cannot extract pixels.");
				return Array.Empty<Color>();
			}

			var length = cubemap.width * cubemap.height;
			var result = new Color[length * 6];

			for (var i = 0; i < 6; i++)
			{
				var face = cubemap.GetPixels((CubemapFace)i);
				Array.Copy(face, 0, result, length * i, face.Length);
			}

			return result;
		}

		public static Color SampleCubemap(Cubemap cubemap, Vector3 dir)
		{
			dir.Normalize();

			float absX = Mathf.Abs(dir.x);
			float absY = Mathf.Abs(dir.y);
			float absZ = Mathf.Abs(dir.z);

			CubemapFace face;
			float u, v;

			if (absX >= absY && absX >= absZ)
			{
				face = dir.x > 0 ? CubemapFace.PositiveX : CubemapFace.NegativeX;
				u = dir.x > 0 ? -dir.z : dir.z;
				v = -dir.y;
				float abs = absX;
				u /= abs; v /= abs;
			}
			else if (absY >= absX && absY >= absZ)
			{
				face = dir.y > 0 ? CubemapFace.PositiveY : CubemapFace.NegativeY;
				u = dir.x;
				v = dir.y > 0 ? dir.z : -dir.z;
				float abs = absY;
				u /= abs; v /= abs;
			}
			else
			{
				face = dir.z > 0 ? CubemapFace.PositiveZ : CubemapFace.NegativeZ;
				u = dir.z > 0 ? dir.x : -dir.x;
				v = -dir.y;
				float abs = absZ;
				u /= abs; v /= abs;
			}

			u = 0.5f * (u + 1f);
			v = 0.5f * (v + 1f);

			int size = cubemap.width;
			int px = Mathf.Clamp((int)(u * (size - 1)), 0, size - 1);
			int py = Mathf.Clamp((int)(v * (size - 1)), 0, size - 1);

			return cubemap.GetPixel(face, px, py);
		}


		///// <summary>
		///// Creates a tinted cubemap that matches what the skybox material shows.
		///// Works around non-readable cubemaps and WebGL limitations.
		///// </summary>
		//public static Cubemap GetTintedCubemapCPU(Material skyMat, int resolution = 256)
		//{
		//	if (skyMat == null) return null;

		//	// Fast reuse
		//	if (skyMat == s_CurrentSkyMat && s_CurrentTintedCubemap != null)
		//		return s_CurrentTintedCubemap;

		//	// Destroy old
		//	if (s_CurrentTintedCubemap != null)
		//	{
		//		UnityEngine.Object.DestroyImmediate(s_CurrentTintedCubemap);
		//		s_CurrentTintedCubemap = null;
		//	}

		//	InvalidateComputeCaches();

		//	Cubemap source = GetSkyboxAsCubemap(skyMat);
		//	if (source == null) return null;

		//	Color tint = Color.white;
		//	if (skyMat.HasProperty("_Tint"))
		//		tint = skyMat.GetColor("_Tint");
		//	else if (skyMat.HasProperty("_SkyTint"))
		//		tint = skyMat.GetColor("_SkyTint");

		//	var tintedCubemap = new Cubemap(resolution, TextureFormat.RGBA32, false)
		//	{
		//		filterMode = FilterMode.Trilinear,
		//		wrapMode = TextureWrapMode.Clamp,
		//		name = "TintedSky_" + (skyMat.name ?? "Unnamed")
		//	};

		//	bool success = false;

		//	// Try CPU pixel copy first (fastest, works on WebGL if readable)
		//	if (source.isReadable)
		//	{
		//		try
		//		{
		//			int srcSize = source.width;

		//			for (int face = 0; face < 6; face++)
		//			{
		//				Color[] srcPixels = source.GetPixels((CubemapFace)face);
		//				Color[] dstPixels = new Color[resolution * resolution];

		//				for (int y = 0; y < resolution; y++)
		//				{
		//					for (int x = 0; x < resolution; x++)
		//					{
		//						float u = (x + 0.5f) / resolution;
		//						float v = (y + 0.5f) / resolution;

		//						int sx = Mathf.FloorToInt(u * (srcSize - 1));
		//						int sy = Mathf.FloorToInt(v * (srcSize - 1));

		//						Color c = srcPixels[sy * srcSize + sx];
		//						c.r *= tint.r;
		//						c.g *= tint.g;
		//						c.b *= tint.b;

		//						dstPixels[y * resolution + x] = c;
		//					}
		//				}

		//				tintedCubemap.SetPixels(dstPixels, (CubemapFace)face);
		//			}

		//			tintedCubemap.Apply();
		//			success = true;
		//		}
		//		catch
		//		{
		//			success = false;
		//		}
		//	}

		//	// Fallback: use RenderToCubemap (works in Editor, often fails in WebGL)
		//	if (!success)
		//	{
		//		Debug.Log("reverting to fallback");
		//		var tempGo = new GameObject("SkyboxBaker") { hideFlags = HideFlags.HideAndDontSave };
		//		var bakerCam = tempGo.AddComponent<Camera>();

		//		try
		//		{
		//			bakerCam.clearFlags = CameraClearFlags.Skybox;
		//			bakerCam.cullingMask = 0;
		//			bakerCam.farClipPlane = 1000f;
		//			bakerCam.allowHDR = false;           // safer for WebGL
		//			bakerCam.backgroundColor = Color.black;

		//			var currentSky = RenderSettings.skybox;
		//			RenderSettings.skybox = skyMat;

		//			success = bakerCam.RenderToCubemap(tintedCubemap);

		//			RenderSettings.skybox = currentSky;
		//		}
		//		finally
		//		{
		//			UnityEngine.Object.DestroyImmediate(bakerCam);
		//			UnityEngine.Object.DestroyImmediate(tempGo);
		//		}

		//		if (!success)
		//		{
		//			Debug.LogWarning($"GetTintedCubemap failed for {skyMat.name}. Using untinted fallback.");
		//			UnityEngine.Object.DestroyImmediate(tintedCubemap);
		//			return source;   // return original as fallback
		//		}
		//	}

		//	s_CurrentTintedCubemap = tintedCubemap;
		//	s_CurrentSkyMat = skyMat;

		//	return tintedCubemap;
		//}

		/// <summary>
		/// Returns a tinted version of the skybox as a Cubemap.
		/// Only ONE tinted cubemap is kept in memory at any time (ideal for WebGL).
		/// When the skybox material changes, the old cubemap is automatically destroyed.
		/// </summary>
		public static Cubemap GetTintedCubemap(Material skyMat, int resolution = 256)
		{
			if (skyMat == null) return null;

			// Fast path: reuse if it's the same skybox material we already baked
			if (skyMat == s_CurrentSkyMat && s_CurrentTintedCubemap != null)
				return s_CurrentTintedCubemap;

			// Destroy the previous tinted cubemap to free memory
			if (s_CurrentTintedCubemap != null)
			{
				UnityEngine.Object.DestroyImmediate(s_CurrentTintedCubemap);
				s_CurrentTintedCubemap = null;
			}

			// Invalidate compute caches because the underlying cubemap will change
			InvalidateComputeCaches();

			// Create and bake new tinted cubemap
			var tintedCubemap = new Cubemap(resolution, TextureFormat.RGBA32, false)
			{
				filterMode = FilterMode.Trilinear,
				wrapMode = TextureWrapMode.Clamp,
				name = "TintedSkyReflection_" + (skyMat.name ?? "Unnamed")
			};

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

			UnityEngine.Object.DestroyImmediate(bakerCam);
			UnityEngine.Object.DestroyImmediate(tempGo);

			// Cache the new cubemap
			s_CurrentTintedCubemap = tintedCubemap;
			s_CurrentSkyMat = skyMat;

			return tintedCubemap;
		}

		public static Cubemap GetTintedCubemapInstance(Material skyMat, int resolution = 256)
		{
			if (skyMat == null) return null;

			// Create and bake new tinted cubemap
			var tintedCubemap = new Cubemap(resolution, TextureFormat.RGBA32, false)
			{
				filterMode = FilterMode.Trilinear,
				wrapMode = TextureWrapMode.Clamp,
				name = "TintedSkyReflection_" + (skyMat.name ?? "Unnamed")
			};

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

			UnityEngine.Object.DestroyImmediate(bakerCam);
			UnityEngine.Object.DestroyImmediate(tempGo);

			return tintedCubemap;
		}


		///// <summary>
		///// Returns a tinted version of the skybox as a Cubemap.
		///// Only ONE tinted cubemap is kept in memory at any time (ideal for WebGL).
		///// When the skybox material changes, the old cubemap is automatically destroyed.
		///// 
		///// Note: This version requires the skybox material to expose a "_Tex" or "_MainTex" Cubemap property
		///// (standard for most Skybox/Cubemap shaders). It applies a simple multiplicative tint.
		///// </summary>
		//public static Cubemap GetTintedCubemap(Material skyMat, Color tint = default, int resolution = 256)
		//{
		//	if (skyMat == null)
		//		return null;

		//	// Use white as default tint if none provided (neutral = no change)
		//	if (tint == default)
		//		tint = Color.white;

		//	// Fast path: reuse if it's the same skybox material we already baked
		//	if (skyMat == s_CurrentSkyMat && s_CurrentTintedCubemap != null)
		//		return s_CurrentTintedCubemap;

		//	// Destroy the previous tinted cubemap to free memory
		//	if (s_CurrentTintedCubemap != null)
		//	{
		//		UnityEngine.Object.DestroyImmediate(s_CurrentTintedCubemap);
		//		s_CurrentTintedCubemap = null;
		//	}

		//	// Invalidate compute caches because the underlying cubemap will change
		//	InvalidateComputeCaches();

		//	// Extract the source cubemap from the skybox material
		//	Cubemap sourceCubemap = null;

		//	// Common property names for skybox cubemap textures
		//	if (skyMat.HasProperty("_Tex"))
		//		sourceCubemap = skyMat.GetTexture("_Tex") as Cubemap;
		//	else if (skyMat.HasProperty("_MainTex"))
		//		sourceCubemap = skyMat.GetTexture("_MainTex") as Cubemap;

		//	if (sourceCubemap == null)
		//	{
		//		Debug.LogWarning($"GetTintedCubemap: Could not find a Cubemap texture in material '{skyMat.name}'. " +
		//						 "Make sure the material uses a Skybox/Cubemap shader or similar.");
		//		return null;
		//	}

		//	// Create tinted version using the provided helper
		//	Cubemap tintedCubemap = TintCubemap(sourceCubemap, tint);

		//	// Optional: give it a descriptive name for debugging
		//	tintedCubemap.name = $"TintedSkyReflection_{skyMat.name ?? "Unnamed"}_Tint{tint}";

		//	// Cache the new cubemap
		//	s_CurrentTintedCubemap = tintedCubemap;
		//	s_CurrentSkyMat = skyMat;

		//	return tintedCubemap;
		//}

		//public static Cubemap TintCubemap(Cubemap source, Color tint)
		//{
		//	if (source == null) return null;
		//	int size = source.width;
		//	Cubemap result = new Cubemap(size, source.format, false);

		//	for (int i = 0; i < 6; i++)
		//	{
		//		Color[] facePixels = source.GetPixels((CubemapFace)i);
		//		for (int p = 0; p < facePixels.Length; p++)
		//			facePixels[p] *= tint; // apply tint

		//		result.SetPixels(facePixels, (CubemapFace)i);
		//	}

		//	result.Apply();
		//	return result;
		//}

		//public static System.Collections.IEnumerator GetTintedCubemapAsync(Material skyMat, Action<Cubemap> onReady, int resolution = 128)
		//{
		//	var go = new GameObject("SkyboxProbe") { hideFlags = HideFlags.HideAndDontSave };
		//	var probe = go.AddComponent<ReflectionProbe>();

		//	probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
		//	probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
		//	probe.resolution = resolution;
		//	probe.cullingMask = 0;
		//	probe.clearFlags = ReflectionProbeClearFlags.Skybox;
		//	probe.backgroundColor = Color.black;

		//	var oldSky = RenderSettings.skybox;
		//	RenderSettings.skybox = skyMat;

		//	probe.RenderProbe();

		//	// 🔥 CRITICAL: wait at least one frame (sometimes two in WebGL)
		//	yield return null;
		//	yield return null;

		//	Cubemap baked = probe.texture as Cubemap;

		//	if (baked == null)
		//	{
		//		Debug.LogError("ReflectionProbe failed after wait.");
		//		onReady?.Invoke(null);
		//	}
		//	else
		//	{
		//		// 🔥 DO NOT CopyTexture (ASTC issue)
		//		// Just use it directly

		//		onReady?.Invoke(baked);
		//	}

		//	RenderSettings.skybox = oldSky;
		//	UnityEngine.Object.DestroyImmediate(go);
		//}

		/// <summary>
		/// Clears the cached tinted cubemap and all compute results.
		/// Call this when the skybox is changing (e.g. inside SkyboxUtility.SetSkybox).
		/// </summary>
		public static void ClearCurrentCache()
		{
			if (s_CurrentTintedCubemap != null)
			{
				UnityEngine.Object.DestroyImmediate(s_CurrentTintedCubemap);
				s_CurrentTintedCubemap = null;
			}
			s_CurrentSkyMat = null;

			InvalidateComputeCaches();
		}

		private static void InvalidateComputeCaches()
		{
			s_CachedAmbientPower = -1f;
			s_CachedBrightThreshold = -1f;
		}
	}
}
