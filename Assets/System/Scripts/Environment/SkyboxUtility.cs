using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public static class SkyboxUtility
	{
		private static Material defaultSkyboxMaterial;

		private static Material lastSkyboxMaterial = null;
		private static Cubemap lastCubemap = null;

		private static Cubemap reusableSixSidedCubemap = null;
		private static int lastCubemapSize = 0;

		private static Cubemap lastTintedCubemap = null;
		private static Material lastTintedSourceMaterial = null;

		private static Texture2D lastCubemapPreviewTexture; // cache flattened preview

		static SkyboxUtility()
		{
			defaultSkyboxMaterial = RenderSettings.skybox;
		}

		// ──────────────────────────────────────────────────────────────
		//  Your existing methods (unchanged): SetSkybox, GetSkyboxMaterial…
		// ──────────────────────────────────────────────────────────────

		public static void SetSkybox(string pathOrName = null)
		{
			var material = string.IsNullOrEmpty(pathOrName) ? defaultSkyboxMaterial : GetSkyboxMaterial(pathOrName);
			RenderSettings.skybox = material ? material : defaultSkyboxMaterial;
		}

		public static Material GetSkyboxMaterialForName(string pathOrName)
			=> string.IsNullOrEmpty(pathOrName) ? defaultSkyboxMaterial : GetSkyboxMaterial(pathOrName);

		private static Material GetSkyboxMaterial(string pathOrName = null)
		{
			if (string.IsNullOrEmpty(pathOrName))
				return defaultSkyboxMaterial;

			string normalized = pathOrName.Replace("\\", "/").Trim('/');

			if (normalized.Contains("/"))
			{
				string loadPath = Path.GetFileNameWithoutExtension(normalized);
				string directory = Path.GetDirectoryName(normalized)?.Replace("\\", "/").Trim('/') ?? "";
				string fullPath = string.IsNullOrEmpty(directory) ? loadPath : directory + "/" + loadPath;

				Material direct = Resources.Load<Material>(fullPath);
				if (direct != null)
					return direct;

				Debug.LogWarning($"SkyboxUtility: Direct load failed for '{fullPath}'");
				return null;
			}

			return AssetRegistry<Material>.FindMaterial(normalized) ?? defaultSkyboxMaterial;
		}

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
				waterMaterial.SetTexture("_Skybox", null);
				return;
			}

			if (skyboxMaterial == lastSkyboxMaterial && lastCubemap != null)
			{
				waterMaterial.SetTexture("_Skybox", lastCubemap);
				return;
			}

			Cubemap cubemap = null;

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
				lastSkyboxMaterial = skyboxMaterial;
				lastCubemap = cubemap;
				waterMaterial.SetTexture("_Skybox", cubemap);
				return;
			}

			if (skyboxMaterial.HasProperty("_FrontTex"))
			{
				Texture2D front = skyboxMaterial.GetTexture("_FrontTex") as Texture2D;
				Texture2D back = skyboxMaterial.GetTexture("_BackTex") as Texture2D;
				Texture2D left = skyboxMaterial.GetTexture("_LeftTex") as Texture2D;
				Texture2D right = skyboxMaterial.GetTexture("_RightTex") as Texture2D;
				Texture2D up = skyboxMaterial.GetTexture("_UpTex") as Texture2D;
				Texture2D down = skyboxMaterial.GetTexture("_DownTex") as Texture2D;

				if (front && back && left && right && up && down)
				{
					int size = front.width;

					if (reusableSixSidedCubemap == null || lastCubemapSize != size)
					{
						reusableSixSidedCubemap = new Cubemap(size, TextureFormat.RGBA32, false);
						lastCubemapSize = size;
					}

					CopyFace(reusableSixSidedCubemap, CubemapFace.PositiveZ, front);
					CopyFace(reusableSixSidedCubemap, CubemapFace.NegativeZ, back);
					CopyFace(reusableSixSidedCubemap, CubemapFace.PositiveX, left);
					CopyFace(reusableSixSidedCubemap, CubemapFace.NegativeX, right);
					CopyFace(reusableSixSidedCubemap, CubemapFace.PositiveY, up);
					CopyFace(reusableSixSidedCubemap, CubemapFace.NegativeY, down);

					reusableSixSidedCubemap.Apply();

					cubemap = reusableSixSidedCubemap;
				}
			}

			waterMaterial.SetTexture("_Skybox", cubemap);
			lastSkyboxMaterial = skyboxMaterial;
			lastCubemap = cubemap;
		}

		public static Texture GetSkyboxTexture(Material skyboxMaterial)
		{
			if (skyboxMaterial == null) return null;
			Cubemap cubemap = null;

			if (skyboxMaterial.HasProperty("_Tex"))
				return skyboxMaterial.GetTexture("_Tex") as Cubemap;
			else if (skyboxMaterial.HasProperty("_MainTex"))
				return skyboxMaterial.GetTexture("_MainTex") as Cubemap;

			if (skyboxMaterial.HasProperty("_FrontTex"))
			{
				Texture2D front = skyboxMaterial.GetTexture("_FrontTex") as Texture2D;
				Texture2D back = skyboxMaterial.GetTexture("_BackTex") as Texture2D;
				Texture2D left = skyboxMaterial.GetTexture("_LeftTex") as Texture2D;
				Texture2D right = skyboxMaterial.GetTexture("_RightTex") as Texture2D;
				Texture2D up = skyboxMaterial.GetTexture("_UpTex") as Texture2D;
				Texture2D down = skyboxMaterial.GetTexture("_DownTex") as Texture2D;

				if (front && back && left && right && up && down)
				{
					int size = front.width;

					if (reusableSixSidedCubemap == null || lastCubemapSize != size)
					{
						reusableSixSidedCubemap = new Cubemap(size, TextureFormat.RGBA32, false);
						lastCubemapSize = size;
					}

					CopyFace(reusableSixSidedCubemap, CubemapFace.PositiveZ, front);
					CopyFace(reusableSixSidedCubemap, CubemapFace.NegativeZ, back);
					CopyFace(reusableSixSidedCubemap, CubemapFace.PositiveX, left);
					CopyFace(reusableSixSidedCubemap, CubemapFace.NegativeX, right);
					CopyFace(reusableSixSidedCubemap, CubemapFace.PositiveY, up);
					CopyFace(reusableSixSidedCubemap, CubemapFace.NegativeY, down);

					reusableSixSidedCubemap.Apply();

					cubemap = reusableSixSidedCubemap;
				}
			}
			return cubemap;
		}

		private static void CopyFace(Cubemap target, CubemapFace face, Texture2D source)
		{
			if (source == null || target == null) return;

			if (source.isReadable)
			{
				target.SetPixels(source.GetPixels(), face);
				return;
			}

			RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
			Graphics.Blit(source, rt);

			RenderTexture prev = RenderTexture.active;
			RenderTexture.active = rt;

			Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
			readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
			readable.Apply();

			target.SetPixels(readable.GetPixels(), face);

			RenderTexture.active = prev;
			RenderTexture.ReleaseTemporary(rt);
			Object.DestroyImmediate(readable);
		}

		public static Cubemap GetTintedSkyboxCubemap(Material overrideSkybox = null, int resolution = 512)
		{
			Material skyMat = overrideSkybox ?? RenderSettings.skybox;
			if (skyMat == null) return null;

			if (lastTintedCubemap != null && lastTintedSourceMaterial == skyMat)
				return lastTintedCubemap;

			if (lastTintedCubemap != null)
			{
				Object.DestroyImmediate(lastTintedCubemap);
				lastTintedCubemap = null;
			}

			Cubemap tintedCubemap = new Cubemap(resolution, TextureFormat.RGBA32, true)
			{
				filterMode = FilterMode.Bilinear,
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

			RenderSettings.skybox = skyMat;
			bakerCam.RenderToCubemap(tintedCubemap);

			Object.DestroyImmediate(tempGo);

			lastTintedCubemap = tintedCubemap;
			lastTintedSourceMaterial = skyMat;

			return tintedCubemap;
		}

		public static void InvalidateTintedCache()
		{
			if (lastTintedCubemap != null)
			{
				Object.DestroyImmediate(lastTintedCubemap);
				lastTintedCubemap = null;
			}
			lastTintedSourceMaterial = null;
		}

		// ── Combined utility: color + direction ───────────────────────────────
		public static (Color brightColor, Vector3 lightDirection) AnalyzeSkyboxForLight(
			Material overrideSkybox = null,
			float colorThresholdRatio = 0.9f)
		{
			Cubemap cubemap = GetTintedSkyboxCubemap(overrideSkybox);
			if (cubemap == null)
				return (Color.white, Vector3.zero);

			Color brightColor = ComputeBrightRegionColor(cubemap, colorThresholdRatio);
			Vector3 lightDir = FindLightDirection(cubemap);

			return (brightColor, lightDir);
		}

		public static Color ComputeBrightRegionColor(Cubemap cubemap, float thresholdRatio)
		{
			if (cubemap == null) return Color.white;

			// Use same downscale resolution as FindLightDirection for consistency
			int originalSize = cubemap.width;
			int downscaleFactor = 16; // ← you can expose this as parameter if needed
			int faceSize = Mathf.Max(32, originalSize / downscaleFactor);

			Texture2D sampleCubemap = new Texture2D(faceSize * 4, faceSize * 3, TextureFormat.RGBA32, false);

			// Cache face pixels once (same speedup as in direction function)
			Color[] pxPosZ = cubemap.GetPixels(CubemapFace.PositiveZ);
			Color[] pxPosX = cubemap.GetPixels(CubemapFace.PositiveX);
			Color[] pxNegX = cubemap.GetPixels(CubemapFace.NegativeX);
			Color[] pxPosY = cubemap.GetPixels(CubemapFace.PositiveY);
			Color[] pxNegY = cubemap.GetPixels(CubemapFace.NegativeY); // we need -Y now
			Color[] pxNegZ = cubemap.GetPixels(CubemapFace.NegativeZ);

			// Same fast drawing function (nearest with centering)
			void DrawFaceFast(CubemapFace face, int xOffset, int yOffset)
			{
				Color[] pixels;
				switch (face)
				{
					case CubemapFace.PositiveZ: pixels = pxPosZ; break;
					case CubemapFace.PositiveX: pixels = pxPosX; break;
					case CubemapFace.NegativeX: pixels = pxNegX; break;
					case CubemapFace.PositiveY: pixels = pxPosY; break;
					case CubemapFace.NegativeY: pixels = pxNegY; break;
					case CubemapFace.NegativeZ: pixels = pxNegZ; break;
					default: return;
				}

				float scale = (float)originalSize / faceSize;

				for (int y = 0; y < faceSize; y++)
				{
					for (int x = 0; x < faceSize; x++)
					{
						int srcX = Mathf.Clamp(Mathf.FloorToInt((x + 0.5f) * scale), 0, originalSize - 1);
						int srcY = Mathf.Clamp(Mathf.FloorToInt((y + 0.5f) * scale), 0, originalSize - 1);

						int px = xOffset * faceSize + x;
						int py = yOffset * faceSize + (faceSize - 1 - y);

						sampleCubemap.SetPixel(px, py, pixels[srcY * originalSize + srcX]);
					}
				}
			}

			// Draw all six faces — same layout as your original ComputeBrightRegionColor
			DrawFaceFast(CubemapFace.PositiveZ, 1, 1);
			DrawFaceFast(CubemapFace.PositiveX, 2, 1);
			DrawFaceFast(CubemapFace.NegativeX, 0, 1);
			DrawFaceFast(CubemapFace.PositiveY, 1, 2);
			DrawFaceFast(CubemapFace.NegativeY, 1, 0);
			DrawFaceFast(CubemapFace.NegativeZ, 3, 1);

			sampleCubemap.Apply();

			// ──────────────────────────────────────────────────────────────
			// Now sample only the downscaled pixels — same logic, much fewer iterations
			// ──────────────────────────────────────────────────────────────

			float maxLum = 0f;

			List<Color> cols = new List<Color>();
			List<float> lums = new List<float>();

			void SampleFace(int xOffset, int yOffset)
			{
				for (int y = 0; y < faceSize; y++)
				{
					for (int x = 0; x < faceSize; x++)
					{
						int px = xOffset * faceSize + x;
						int py = yOffset * faceSize + y;

						Color col = sampleCubemap.GetPixel(px, py);

						float lum = col.r * 0.2126f + col.g * 0.7152f + col.b * 0.0722f;

						cols.Add(col);
						lums.Add(lum);

						if (lum > maxLum) maxLum = lum;
					}
				}
			}

			SampleFace(1, 1);
			SampleFace(2, 1);
			SampleFace(0, 1);
			SampleFace(1, 2);
			SampleFace(1, 0);
			SampleFace(3, 1);

			Object.DestroyImmediate(sampleCubemap);

			if (maxLum <= 0f || cols.Count == 0)
				return Color.white;

			float threshold = maxLum * thresholdRatio;

			Color sum = Color.black;
			float weightSum = 0f;

			for (int i = 0; i < cols.Count; i++)
			{
				if (lums[i] >= threshold)
				{
					float weight = lums[i];
					sum += cols[i] * weight;
					weightSum += weight;
				}
			}

			if (weightSum <= 0f)
				return Color.white;

			return sum / weightSum;
		}

		public static Vector3 FindLightDirection(Cubemap cubemap, int downscaleFactor = 16)
		{
			if (cubemap == null) return Vector3.down;

			int originalSize = cubemap.width;
			int faceSize = Mathf.Max(64, originalSize / downscaleFactor);

			// Use Texture2D atlas — same as working version
			Texture2D sampleCubemap = new Texture2D(faceSize * 4, faceSize * 3, TextureFormat.RGBA32, false);

			// Cache full face pixels once — huge speedup
			Color[] pxPosZ = cubemap.GetPixels(CubemapFace.PositiveZ);
			Color[] pxPosX = cubemap.GetPixels(CubemapFace.PositiveX);
			Color[] pxNegX = cubemap.GetPixels(CubemapFace.NegativeX);
			Color[] pxPosY = cubemap.GetPixels(CubemapFace.PositiveY);
			Color[] pxNegZ = cubemap.GetPixels(CubemapFace.NegativeZ);

			// Fast nearest-neighbor downsample — very close to bilinear for this purpose, much faster
			void DrawFaceFast(CubemapFace face, int xOffset, int yOffset)
			{
				Color[] pixels;
				switch (face)
				{
					case CubemapFace.PositiveZ: pixels = pxPosZ; break;
					case CubemapFace.PositiveX: pixels = pxPosX; break;
					case CubemapFace.NegativeX: pixels = pxNegX; break;
					case CubemapFace.PositiveY: pixels = pxPosY; break;
					case CubemapFace.NegativeZ: pixels = pxNegZ; break;
					default: return;
				}

				float scale = (float)originalSize / faceSize;

				for (int y = 0; y < faceSize; y++)
				{
					for (int x = 0; x < faceSize; x++)
					{
						int srcX = Mathf.Clamp(Mathf.FloorToInt((x + 0.5f) * scale), 0, originalSize - 1);
						int srcY = Mathf.Clamp(Mathf.FloorToInt((y + 0.5f) * scale), 0, originalSize - 1);

						int px = xOffset * faceSize + x;
						int py = yOffset * faceSize + (faceSize - 1 - y);

						sampleCubemap.SetPixel(px, py, pixels[srcY * originalSize + srcX]);
					}
				}
			}

			// Draw the faces — fast path
			DrawFaceFast(CubemapFace.PositiveZ, 1, 1);
			DrawFaceFast(CubemapFace.PositiveX, 2, 1);
			DrawFaceFast(CubemapFace.NegativeX, 0, 1);
			DrawFaceFast(CubemapFace.PositiveY, 1, 2);
			DrawFaceFast(CubemapFace.NegativeZ, 3, 1);

			sampleCubemap.Apply();

			// ──────────────────────────────────────────────────────────────
			// Identical brightest pixel search as your working version
			// ──────────────────────────────────────────────────────────────

			float maxLum = -1f;
			Vector2 brightestUV = Vector2.zero;
			CubemapFace brightestFace = CubemapFace.Unknown;

			var facesToCheck = new[]
			{
		CubemapFace.PositiveY,
		CubemapFace.PositiveZ,
		CubemapFace.NegativeZ,
		CubemapFace.PositiveX,
		CubemapFace.NegativeX
	};

			foreach (var face in facesToCheck)
			{
				int xOff = 0, yOff = 0;
				switch (face)
				{
					case CubemapFace.PositiveY: xOff = 1; yOff = 2; break;
					case CubemapFace.PositiveZ: xOff = 1; yOff = 1; break;
					case CubemapFace.NegativeZ: xOff = 3; yOff = 1; break;
					case CubemapFace.PositiveX: xOff = 2; yOff = 1; break;
					case CubemapFace.NegativeX: xOff = 0; yOff = 1; break;
				}

				int yStart = (face == CubemapFace.PositiveY) ? 0 : faceSize / 2;

				for (int y = yStart; y < faceSize; y++)
				{
					for (int x = 0; x < faceSize; x++)
					{
						int px = xOff * faceSize + x;
						int py = yOff * faceSize + y;

						Color col = sampleCubemap.GetPixel(px, py);
						float lum = col.r * 0.2126f + col.g * 0.7152f + col.b * 0.0722f;

						if (lum > maxLum)
						{
							maxLum = lum;
							brightestUV = new Vector2((float)x / faceSize, (float)y / faceSize);
							brightestFace = face;
						}
					}
				}
			}

			Object.DestroyImmediate(sampleCubemap);

			if (maxLum <= 0f || brightestFace == CubemapFace.Unknown)
				return Vector3.down;

			Vector3 dir = PixelToDirection(brightestFace, brightestUV.x, brightestUV.y);
			return -dir.normalized;
		}

		private static Vector3 PixelToDirection(CubemapFace face, float u, float v)
		{
			// Adjust +Y UV to compensate for common 90°/180° rotation in top face
			// Your "rotate 180° works" → try flipping both axes (equivalent to 180°)
			if (face == CubemapFace.PositiveY)
			{
				//u = 1f - u;  // horizontal flip
				v = 1f - v;  // vertical flip
			}

			float x = u * 2f - 1f;
			float y = v * 2f - 1f;

			switch (face)
			{
				// Adjusted mappings — matches most Unity cubemaps / RenderToCubemap
				case CubemapFace.PositiveZ: return new Vector3(x, y, 1f).normalized;   // front
				case CubemapFace.NegativeZ: return new Vector3(-x, y, -1f).normalized;   // back
				case CubemapFace.PositiveX: return new Vector3(1f, y, -x).normalized;    // right
				case CubemapFace.NegativeX: return new Vector3(-1f, y, x).normalized;    // left
				case CubemapFace.PositiveY: return new Vector3(x, 1f, y).normalized;    // up (after UV flip)
				default: return Vector3.up;
			}
		}

		private static void DrawFace(Cubemap cubemap, CubemapFace face, int xOffset, int yOffset)
		{
			Color[] pixels = cubemap.GetPixels(face);
			int faceSize = cubemap.width;

			for (int y = 0; y < faceSize; y++)
			{
				for (int x = 0; x < faceSize; x++)
				{
					int px = xOffset * faceSize + x;
					int py = yOffset * faceSize + (faceSize - 1 - y);
					lastCubemapPreviewTexture.SetPixel(px, py, pixels[y * faceSize + x]);
				}
			}
		}
	}
}