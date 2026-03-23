using UnityEngine;
using System.IO;

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

		static SkyboxUtility()
		{
			defaultSkyboxMaterial = RenderSettings.skybox;
		}

		/// <summary>
		/// Sets the skybox.
		/// - If pathOrName contains '/' → treated as full Resources path (direct load)
		/// - Otherwise → treated as name only, uses AssetRegistry<Material>.FindMaterial (configured roots)
		/// No automatic "Skybox" append — you control it in MainController
		/// </summary>
		public static void SetSkybox(string pathOrName = null)
		{
			var material = string.IsNullOrEmpty(pathOrName) ? defaultSkyboxMaterial : GetSkyboxMaterial(pathOrName);
			RenderSettings.skybox = material ? material : defaultSkyboxMaterial;
		}

		//not currently used
		//public static Material LoadSkyboxMaterial(string pathOrName = null) => string.IsNullOrEmpty(pathOrName) ? defaultSkyboxMaterial : GetSkyboxMaterial(pathOrName);
		public static Material GetSkyboxMaterialForName(string pathOrName) => string.IsNullOrEmpty(pathOrName) ? defaultSkyboxMaterial : GetSkyboxMaterial(pathOrName);

		private static Material GetSkyboxMaterial(string pathOrName = null)
		{
			if (string.IsNullOrEmpty(pathOrName))
				return defaultSkyboxMaterial;

			string normalized = pathOrName.Replace("\\", "/").Trim('/');

			// If it contains a path separator → direct Resources load
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

			// Otherwise — name only, use registry
			return AssetRegistry<Material>.FindMaterial(normalized) ?? defaultSkyboxMaterial;
		}

		// === YOUR FULL CUBEMAP LOGIC — UNCHANGED ===
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

		/// <summary>
		/// Gets a cubemap with the current skybox rendered including tint, exposure, rotation, etc.
		/// Uses a temporary camera to bake the skybox appearance.
		/// Caches the result until the skybox material changes.
		/// </summary>
		/// <param name="overrideSkybox">Optional override material (e.g. from CameraRenderSettingsOverride). If null, uses RenderSettings.skybox.</param>
		/// <param name="resolution">Cubemap resolution (power of two recommended: 256, 512, 1024). Higher = better quality, slower bake.</param>
		/// <returns>Tinted/baked cubemap or null if no skybox available</returns>
		public static Cubemap GetTintedSkyboxCubemap(Material overrideSkybox = null, int resolution = 512)
		{
			Material skyMat = overrideSkybox ?? RenderSettings.skybox;
			if (skyMat == null) return null;

			// Cache hit: same source material → reuse
			if (lastTintedCubemap != null && lastTintedSourceMaterial == skyMat)
			{
				return lastTintedCubemap;
			}

			// Clean up old one if exists
			if (lastTintedCubemap != null)
			{
				Object.DestroyImmediate(lastTintedCubemap);
				lastTintedCubemap = null;
			}

			// Create new cubemap
			Cubemap tintedCubemap = new Cubemap(resolution, TextureFormat.RGBA32, true)
			{
				filterMode = FilterMode.Bilinear,
				wrapMode = TextureWrapMode.Clamp,
				name = "TintedSkyReflection_" + (skyMat.name ?? "Unnamed")
			};

			// Temporary camera to render only the skybox
			var tempGo = new GameObject("SkyboxBaker") { hideFlags = HideFlags.HideAndDontSave };
			var bakerCam = tempGo.AddComponent<Camera>();

			bakerCam.clearFlags = CameraClearFlags.Skybox;
			bakerCam.cullingMask = 0;                    // No scene objects
			bakerCam.farClipPlane = 1000f;
			bakerCam.allowHDR = true;                 // Preserve HDR if sky is HDR
			bakerCam.backgroundColor = Color.black;          // Safety fallback

			// Important: force this camera to use the desired skybox material
			RenderSettings.skybox = skyMat;                  // Temporarily set (will be restored later if needed)
			bakerCam.RenderToCubemap(tintedCubemap);
			// Note: if you have per-camera sky overrides in URP, you might need to copy more settings here

			// Cleanup temp objects
			Object.DestroyImmediate(tempGo);

			// Cache result
			lastTintedCubemap = tintedCubemap;
			lastTintedSourceMaterial = skyMat;

			return tintedCubemap;
		}

		// Optional: call this when you know the skybox changed dramatically (day/night cycle, manual change)
		public static void InvalidateTintedCache()
		{
			if (lastTintedCubemap != null)
			{
				Object.DestroyImmediate(lastTintedCubemap);
				lastTintedCubemap = null;
			}
			lastTintedSourceMaterial = null;
		}
	}
}
