using System;
using UnityEngine;

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

//using System;
//using UnityEngine;

//namespace MassiveHadronLtd
//{
//	public static class CubemapUtility
//	{
//		// Cache for the currently active tinted skybox cubemap
//		private static Cubemap s_CurrentTintedCubemap;
//		private static Material s_CurrentSkyMat;

//		public static Color ComputeBrightColor(Cubemap cubemap, float threshold = 0.85f)
//		{
//			if (cubemap == null)
//				return Color.white;

//			if (!cubemap.isReadable)
//			{
//				Debug.LogWarning($"Cubemap '{cubemap.name}' is not readable.");
//				return Color.white;
//			}

//			// === Build one large array containing ALL pixels from all 6 faces ===
//			var faceSize = cubemap.width;
//			var pixels = new Color[faceSize * faceSize * 6];

//			for (var i = 0; i < 6; i++)
//			{
//				var face = cubemap.GetPixels((CubemapFace)i);
//				Array.Copy(face, 0, pixels, faceSize * faceSize * i, face.Length);
//			}

//			return ColourUtils.ThresholdColour(pixels, threshold);
//		}

//		/// <summary>
//		/// Computes a good ambient colour from the skybox cubemap using luminance-weighted averaging.
//		/// Brighter areas (clouds, sun, sky) contribute much more than dark areas.
//		/// </summary>
//		public static Color ComputeAmbientColor(Cubemap cubemap, float power = 1f)
//		{
//			if (cubemap == null)
//				return new Color(0.25f, 0.25f, 0.35f);

//			if (!cubemap.isReadable)
//			{
//				Debug.LogWarning($"Cubemap '{cubemap.name}' is not readable.");
//				return new Color(0.25f, 0.25f, 0.35f);
//			}

//			var pixels = GetAllPixels(cubemap);

//			if (pixels.Length == 0)
//				return new Color(0.25f, 0.25f, 0.35f);

//			return ColourUtils.ComputeAmbientColor(pixels, power);
//		}

//		/// <summary>
//		/// Returns a single flat array containing ALL pixels from all 6 faces of the cubemap.
//		/// Useful for full-image processing like ambient colour calculation.
//		/// </summary>
//		private static Color[] GetAllPixels(Cubemap cubemap)
//		{
//			if (cubemap == null)
//				return Array.Empty<Color>();

//			if (!cubemap.isReadable)
//			{
//				Debug.LogWarning($"Cubemap '{cubemap.name}' is not readable. Cannot extract pixels.");
//				return Array.Empty<Color>();
//			}

//			var length = cubemap.width * cubemap.height;
//			var result = new Color[length * 6];

//			for (var i = 0; i < 6; i++)
//			{
//				var face = cubemap.GetPixels((CubemapFace)i);
//				Array.Copy(face, 0, result, length * i, face.Length);
//			}

//			return result;
//		}

//		public static Color SampleCubemap(Cubemap cubemap, Vector3 dir)
//		{
//			dir.Normalize();   // safe, in case of floating point drift

//			float absX = Mathf.Abs(dir.x);
//			float absY = Mathf.Abs(dir.y);
//			float absZ = Mathf.Abs(dir.z);

//			CubemapFace face;
//			float u, v;

//			if (absX >= absY && absX >= absZ)
//			{
//				face = dir.x > 0 ? CubemapFace.PositiveX : CubemapFace.NegativeX;
//				u = dir.x > 0 ? -dir.z : dir.z;
//				v = -dir.y;
//				float abs = absX;
//				u /= abs; v /= abs;
//			}
//			else if (absY >= absX && absY >= absZ)
//			{
//				face = dir.y > 0 ? CubemapFace.PositiveY : CubemapFace.NegativeY;
//				u = dir.x;
//				v = dir.y > 0 ? dir.z : -dir.z;
//				float abs = absY;
//				u /= abs; v /= abs;
//			}
//			else
//			{
//				face = dir.z > 0 ? CubemapFace.PositiveZ : CubemapFace.NegativeZ;
//				u = dir.z > 0 ? dir.x : -dir.x;
//				v = -dir.y;
//				float abs = absZ;
//				u /= abs; v /= abs;
//			}

//			u = 0.5f * (u + 1f);
//			v = 0.5f * (v + 1f);

//			int size = cubemap.width;
//			int px = Mathf.Clamp((int)(u * (size - 1)), 0, size - 1);
//			int py = Mathf.Clamp((int)(v * (size - 1)), 0, size - 1);

//			return cubemap.GetPixel(face, px, py);
//		}

//		/// <summary>
//		/// Returns a tinted version of the skybox as a Cubemap.
//		/// Only ONE tinted cubemap is kept in memory at any time (ideal for WebGL).
//		/// When the skybox material changes, the old cubemap is automatically destroyed.
//		/// </summary>
//		public static Cubemap GetTintedCubemap(Material skyMat, int resolution = 256)
//		{
//			if (skyMat == null) return null;

//			// Fast path: reuse if it's the same skybox material we already baked
//			if (skyMat == s_CurrentSkyMat && s_CurrentTintedCubemap != null)
//				return s_CurrentTintedCubemap;

//			// Destroy the previous tinted cubemap to free memory (very important for WebGL)
//			if (s_CurrentTintedCubemap != null)
//			{
//				UnityEngine.Object.DestroyImmediate(s_CurrentTintedCubemap);
//				s_CurrentTintedCubemap = null;
//			}

//			// Create and bake new tinted cubemap
//			var tintedCubemap = new Cubemap(resolution, TextureFormat.RGBA32, true)
//			{
//				filterMode = FilterMode.Bilinear,
//				wrapMode = TextureWrapMode.Clamp,
//				name = "TintedSkyReflection_" + (skyMat.name ?? "Unnamed")
//			};

//			var tempGo = new GameObject("SkyboxBaker") { hideFlags = HideFlags.HideAndDontSave };
//			var bakerCam = tempGo.AddComponent<Camera>();

//			bakerCam.clearFlags = CameraClearFlags.Skybox;
//			bakerCam.cullingMask = 0;
//			bakerCam.farClipPlane = 1000f;
//			bakerCam.allowHDR = true;
//			bakerCam.backgroundColor = Color.black;

//			var currentSky = RenderSettings.skybox;
//			RenderSettings.skybox = skyMat;
//			bakerCam.RenderToCubemap(tintedCubemap);
//			RenderSettings.skybox = currentSky;

//			UnityEngine.Object.DestroyImmediate(bakerCam);
//			UnityEngine.Object.DestroyImmediate(tempGo);

//			// Cache the new cubemap
//			s_CurrentTintedCubemap = tintedCubemap;
//			s_CurrentSkyMat = skyMat;

//			return tintedCubemap;
//		}

//		/// <summary>
//		/// Clears the cached tinted cubemap. Call this when the skybox is changing
//		/// (e.g. inside SkyboxUtility.SetSkybox or OnSkyboxChanged) to ensure old memory is released immediately.
//		/// </summary>
//		public static void ClearCurrentCache()
//		{
//			if (s_CurrentTintedCubemap != null)
//			{
//				UnityEngine.Object.DestroyImmediate(s_CurrentTintedCubemap);
//				s_CurrentTintedCubemap = null;
//			}
//			s_CurrentSkyMat = null;
//		}
//	}
//}

//using System;
//using UnityEngine;

//namespace MassiveHadronLtd
//{
//	public static class CubemapUtility
//	{
//		//public static Color ComputeBrightColor(Material skybox, float threshold = 0.85f) => ComputeBrightColor(GetTintedCubemap(skybox), threshold);

//		public static Color ComputeBrightColor(Cubemap cubemap, float threshold = 0.85f)
//		{
//			if (cubemap == null)
//				return Color.white;

//			if (!cubemap.isReadable)
//			{
//				Debug.LogWarning($"Cubemap '{cubemap.name}' is not readable.");
//				return Color.white;
//			}

//			// === Build one large array containing ALL pixels from all 6 faces ===
//			var faceSize = cubemap.width;
//			var pixels = new Color[faceSize * faceSize * 6];//totalPixels

//			for (var i = 0; i < 6; i++)
//			{
//				var face = cubemap.GetPixels((CubemapFace)i);
//				Array.Copy(face, 0, pixels, faceSize * faceSize * i, face.Length);
//			}

//			return ColourUtils.ThresholdColour(pixels, threshold);
//		}

//		/// <summary>
//		/// Computes a good ambient colour from the skybox cubemap using luminance-weighted averaging.
//		/// Brighter areas (clouds, sun, sky) contribute much more than dark areas.
//		/// </summary>
//		public static Color ComputeAmbientColor(Cubemap cubemap, float power = 1f)
//		{
//			if (cubemap == null)
//				return new Color(0.25f, 0.25f, 0.35f);

//			if (!cubemap.isReadable)
//			{
//				Debug.LogWarning($"Cubemap '{cubemap.name}' is not readable.");
//				return new Color(0.25f, 0.25f, 0.35f);
//			}

//			var pixels = GetAllPixels(cubemap);

//			if (pixels.Length == 0)
//				return new Color(0.25f, 0.25f, 0.35f);

//			return ColourUtils.ComputeAmbientColor(pixels, power);
//		}

//		/// <summary>
//		/// Returns a single flat array containing ALL pixels from all 6 faces of the cubemap.
//		/// Useful for full-image processing like ambient colour calculation.
//		/// </summary>
//		private static Color[] GetAllPixels(Cubemap cubemap)
//		{
//			if (cubemap == null)
//				return Array.Empty<Color>();

//			if (!cubemap.isReadable)
//			{
//				Debug.LogWarning($"Cubemap '{cubemap.name}' is not readable. Cannot extract pixels.");
//				return Array.Empty<Color>();
//			}

//			var length = cubemap.width * cubemap.height;
//			var result = new Color[length * 6];

//			for (var i = 0; i < 6; i++)
//			{
//				var face = cubemap.GetPixels((CubemapFace)i);
//				Array.Copy(face, 0, result, length * i, face.Length);
//			}

//			return result;
//		}

//		public static Color SampleCubemap(Cubemap cubemap, Vector3 dir)
//		{
//			dir.Normalize();   // safe, in case of floating point drift

//			float absX = Mathf.Abs(dir.x);
//			float absY = Mathf.Abs(dir.y);
//			float absZ = Mathf.Abs(dir.z);

//			CubemapFace face;
//			float u, v;

//			if (absX >= absY && absX >= absZ)
//			{
//				face = dir.x > 0 ? CubemapFace.PositiveX : CubemapFace.NegativeX;
//				u = dir.x > 0 ? -dir.z : dir.z;
//				v = -dir.y;
//				float abs = absX;
//				u /= abs; v /= abs;
//			}
//			else if (absY >= absX && absY >= absZ)
//			{
//				face = dir.y > 0 ? CubemapFace.PositiveY : CubemapFace.NegativeY;
//				u = dir.x;
//				v = dir.y > 0 ? dir.z : -dir.z;
//				float abs = absY;
//				u /= abs; v /= abs;
//			}
//			else
//			{
//				face = dir.z > 0 ? CubemapFace.PositiveZ : CubemapFace.NegativeZ;
//				u = dir.z > 0 ? dir.x : -dir.x;
//				v = -dir.y;
//				float abs = absZ;
//				u /= abs; v /= abs;
//			}

//			u = 0.5f * (u + 1f);
//			v = 0.5f * (v + 1f);

//			int size = cubemap.width;
//			int px = Mathf.Clamp((int)(u * (size - 1)), 0, size - 1);
//			int py = Mathf.Clamp((int)(v * (size - 1)), 0, size - 1);

//			return cubemap.GetPixel(face, px, py);
//		}

//		public static Cubemap GetTintedCubemap(Material skyMat, int resolution = 512)
//		{
//			if (skyMat == null) return null;

//			var tintedCubemap = new Cubemap(resolution, TextureFormat.RGBA32, true)
//			{
//				filterMode = FilterMode.Bilinear,
//				wrapMode = TextureWrapMode.Clamp,
//				name = "TintedSkyReflection_" + (skyMat.name ?? "Unnamed")
//			};

//			var tempGo = new GameObject("SkyboxBaker") { hideFlags = HideFlags.HideAndDontSave };
//			var bakerCam = tempGo.AddComponent<Camera>();

//			bakerCam.clearFlags = CameraClearFlags.Skybox;
//			bakerCam.cullingMask = 0;
//			bakerCam.farClipPlane = 1000f;
//			bakerCam.allowHDR = true;
//			bakerCam.backgroundColor = Color.black;

//			var currentSky = RenderSettings.skybox;
//			RenderSettings.skybox = skyMat;
//			bakerCam.RenderToCubemap(tintedCubemap);
//			RenderSettings.skybox = currentSky;

//			UnityEngine.Object.DestroyImmediate(bakerCam);
//			UnityEngine.Object.DestroyImmediate(tempGo);

//			return tintedCubemap;
//		}
//	}
//}

