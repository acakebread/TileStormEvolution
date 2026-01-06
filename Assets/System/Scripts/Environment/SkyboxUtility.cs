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
		public static void SetSkybox(string pathOrName)
		{
			Material material = GetSkyboxMaterial(pathOrName);
			RenderSettings.skybox = material ? material : defaultSkyboxMaterial;
		}

		public static Material LoadSkyboxMaterial(string pathOrName)
		{
			return GetSkyboxMaterial(pathOrName);
		}

		private static Material GetSkyboxMaterial(string pathOrName)
		{
			if (string.IsNullOrEmpty(pathOrName))
				return null;

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
			return AssetRegistry<Material>.FindMaterial(normalized);
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
	}
}

//using UnityEngine;
//using System.IO;

//namespace MassiveHadronLtd
//{
//	public delegate Material SkyboxSearchProvider(string materialName);

//	public static class SkyboxUtility
//	{
//		public static SkyboxSearchProvider CustomSearchProvider { get; set; }

//		private static Material defaultSkyboxMaterial;
//		private static Material lastSkyboxMaterial = null;
//		private static Cubemap lastCubemap = null;

//		private static Cubemap reusableSixSidedCubemap = null;
//		private static int lastCubemapSize = 0;

//		static SkyboxUtility()
//		{
//			defaultSkyboxMaterial = RenderSettings.skybox;
//		}

//		public static void SetSkybox(string pathOrName)
//		{
//			Material material = GetSkyboxMaterial(pathOrName);
//			RenderSettings.skybox = material ? material : defaultSkyboxMaterial;
//		}

//		public static Material LoadSkyboxMaterial(string pathOrName)
//		{
//			return GetSkyboxMaterial(pathOrName);
//		}

//		private static Material GetSkyboxMaterial(string pathOrName)
//		{
//			if (string.IsNullOrEmpty(pathOrName))
//				return null;

//			string normalized = pathOrName.Replace("\\", "/").Trim('/');

//			string noExtension = Path.GetFileNameWithoutExtension(normalized);

//			//string loadKey = noExtension.EndsWith("Skybox", System.StringComparison.OrdinalIgnoreCase)
//			//	? noExtension
//			//	: noExtension + "Skybox";

//			string loadKey = noExtension;
//			string directory = Path.GetDirectoryName(normalized)?.Replace("\\", "/").Trim('/') ?? "";
//			string fullLoadPath = string.IsNullOrEmpty(directory) ? loadKey : directory + "/" + loadKey;

//			Material material = Resources.Load<Material>(fullLoadPath);
//			if (material != null)
//				return material;

//			if (CustomSearchProvider != null)
//			{
//				string cleanName = loadKey;
//				material = CustomSearchProvider(cleanName);
//				if (material != null)
//					return material;
//			}

//			Debug.LogWarning($"SkyboxUtility: Could not find skybox material '{fullLoadPath}' (tried direct load and custom search).");
//			return null;
//		}

//		// ===================================================================
//		// FIXED 6-SIDED CUBEMAP GENERATION — CORRECT FACE MAPPING
//		// ===================================================================

//		public static void SetSkyboxCubemap(Material waterMaterial, Material skyboxMaterial)
//		{
//			if (waterMaterial == null)
//			{
//				Debug.LogWarning("Water material is null.");
//				return;
//			}

//			if (skyboxMaterial == null)
//			{
//				Debug.LogWarning("Skybox material is null.");
//				waterMaterial.SetTexture("_Skybox", null);
//				return;
//			}

//			// Cache hit
//			if (skyboxMaterial == lastSkyboxMaterial && lastCubemap != null)
//			{
//				waterMaterial.SetTexture("_Skybox", lastCubemap);
//				return;
//			}

//			Cubemap cubemap = null;

//			// 1. Single cubemap texture (panoramic or pre-baked)
//			if (skyboxMaterial.HasProperty("_Tex"))
//			{
//				cubemap = skyboxMaterial.GetTexture("_Tex") as Cubemap;
//			}
//			else if (skyboxMaterial.HasProperty("_MainTex"))
//			{
//				cubemap = skyboxMaterial.GetTexture("_MainTex") as Cubemap;
//			}

//			if (cubemap != null)
//			{
//				lastSkyboxMaterial = skyboxMaterial;
//				lastCubemap = cubemap;
//				waterMaterial.SetTexture("_Skybox", cubemap);
//				return;
//			}

//			// 2. 6-sided skybox
//			if (skyboxMaterial.HasProperty("_FrontTex"))
//			{
//				Texture2D front = skyboxMaterial.GetTexture("_FrontTex") as Texture2D;
//				Texture2D back = skyboxMaterial.GetTexture("_BackTex") as Texture2D;
//				Texture2D left = skyboxMaterial.GetTexture("_LeftTex") as Texture2D;
//				Texture2D right = skyboxMaterial.GetTexture("_RightTex") as Texture2D;
//				Texture2D up = skyboxMaterial.GetTexture("_UpTex") as Texture2D;
//				Texture2D down = skyboxMaterial.GetTexture("_DownTex") as Texture2D;

//				if (front && back && left && right && up && down)
//				{
//					int size = front.width;

//					if (reusableSixSidedCubemap == null || lastCubemapSize != size)
//					{
//						reusableSixSidedCubemap = new Cubemap(size, TextureFormat.RGBA32, false);
//						lastCubemapSize = size;
//					}

//					// CORRECT MAPPING
//					CopyFace(reusableSixSidedCubemap, CubemapFace.PositiveZ, front);  // Front → +Z
//					CopyFace(reusableSixSidedCubemap, CubemapFace.NegativeZ, back);   // Back  → -Z
//					CopyFace(reusableSixSidedCubemap, CubemapFace.PositiveX, left);   // Left  → +X
//					CopyFace(reusableSixSidedCubemap, CubemapFace.NegativeX, right);  // Right → -X
//					CopyFace(reusableSixSidedCubemap, CubemapFace.PositiveY, up);     // Up    → +Y
//					CopyFace(reusableSixSidedCubemap, CubemapFace.NegativeY, down);   // Down  → -Y

//					reusableSixSidedCubemap.Apply();

//					cubemap = reusableSixSidedCubemap;
//				}
//				else
//				{
//					Debug.LogWarning("Incomplete 6-sided skybox textures.");
//				}
//			}

//			waterMaterial.SetTexture("_Skybox", cubemap);
//			lastSkyboxMaterial = skyboxMaterial;
//			lastCubemap = cubemap;
//		}

//		private static void CopyFace(Cubemap target, CubemapFace face, Texture2D source)
//		{
//			if (source == null || target == null) return;

//			if (source.isReadable)
//			{
//				target.SetPixels(source.GetPixels(), face);
//				return;
//			}

//			RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
//			Graphics.Blit(source, rt);

//			RenderTexture prev = RenderTexture.active;
//			RenderTexture.active = rt;

//			Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
//			readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
//			readable.Apply();

//			target.SetPixels(readable.GetPixels(), face);

//			RenderTexture.active = prev;
//			RenderTexture.ReleaseTemporary(rt);
//			Object.DestroyImmediate(readable);
//		}
//	}
//}

//using UnityEngine;

//namespace MassiveHadronLtd
//{
//	public static class SkyboxUtility
//	{
//		private static Cubemap defaultCubemap = null;
//		private static Material lastSkyboxMaterial = null;
//		private static Cubemap lastCubemap = null;
//		private static Material defaultSkyboxMaterial;

//		// Reusable cubemap for all 6-sided skyboxes
//		private static Cubemap reusableSixSidedCubemap = null;
//		private static int lastCubemapSize = 0;

//		static SkyboxUtility()
//		{
//			defaultSkyboxMaterial = RenderSettings.skybox;
//		}

//		public static void SetSkybox(string pathPrefix, string skyboxName)
//		{
//			if (string.IsNullOrEmpty(pathPrefix) || string.IsNullOrEmpty(skyboxName))
//			{
//				Debug.LogWarning("Path prefix or skybox name is null/empty.");
//				return;
//			}

//			var skyboxPath = $"{pathPrefix}{skyboxName}".Replace(".mat", "");
//			var material = Resources.Load<Material>(skyboxPath);
//			RenderSettings.skybox = material ? material : defaultSkyboxMaterial;
//		}

//		public static Material LoadSkyboxMaterial(string pathPrefix, string skyboxName)
//		{
//			if (string.IsNullOrEmpty(pathPrefix) || string.IsNullOrEmpty(skyboxName))
//			{
//				Debug.LogWarning("Path prefix or skybox name is null/empty.");
//				return null;
//			}

//			var skyboxPath = $"{pathPrefix}{skyboxName}Skybox".Replace(".mat", "");
//			return Resources.Load<Material>(skyboxPath);
//		}

//		/// <summary>
//		/// Sets the water reflection cubemap to match the current skybox.
//		/// Handles both cubemap-based and 6-sided skyboxes.
//		/// </summary>
//		public static void SetSkyboxCubemap(Material waterMaterial, Material skyboxMaterial)
//		{
//			if (waterMaterial == null)
//			{
//				Debug.LogWarning("Water material is null.");
//				return;
//			}

//			if (skyboxMaterial == null)
//			{
//				Debug.LogWarning("Skybox material is null.");
//				waterMaterial.SetTexture("_Skybox", defaultCubemap);
//				return;
//			}

//			// Skip if already processed with the same material
//			if (skyboxMaterial == lastSkyboxMaterial && lastCubemap != null)
//			{
//				waterMaterial.SetTexture("_Skybox", lastCubemap);
//				return;
//			}

//			Cubemap cubemap = null;

//			// --- 1️⃣ Cubemap-based skyboxes (e.g., Panorama or pre-baked cubemap) ---
//			if (skyboxMaterial.HasProperty("_Tex"))
//			{
//				cubemap = skyboxMaterial.GetTexture("_Tex") as Cubemap;
//			}
//			else if (skyboxMaterial.HasProperty("_MainTex"))
//			{
//				cubemap = skyboxMaterial.GetTexture("_MainTex") as Cubemap;
//			}

//			if (cubemap != null)
//			{
//				lastSkyboxMaterial = skyboxMaterial;
//				lastCubemap = cubemap;
//				waterMaterial.SetTexture("_Skybox", cubemap);
//				return;
//			}

//			// --- 2️⃣ 6-Sided skyboxes ---
//			if (skyboxMaterial.HasProperty("_FrontTex"))
//			{
//				Texture2D front = skyboxMaterial.GetTexture("_FrontTex") as Texture2D;
//				Texture2D back = skyboxMaterial.GetTexture("_BackTex") as Texture2D;
//				Texture2D left = skyboxMaterial.GetTexture("_LeftTex") as Texture2D;
//				Texture2D right = skyboxMaterial.GetTexture("_RightTex") as Texture2D;
//				Texture2D up = skyboxMaterial.GetTexture("_UpTex") as Texture2D;
//				Texture2D down = skyboxMaterial.GetTexture("_DownTex") as Texture2D;

//				if (front && back && left && right && up && down)
//				{
//					int size = front.width;

//					// Reuse or create cubemap only if size changed
//					if (reusableSixSidedCubemap == null || lastCubemapSize != size)
//					{
//						// Old one will be garbage collected safely (no DestroyImmediate needed)
//						reusableSixSidedCubemap = new Cubemap(size, TextureFormat.RGBA32, false);
//						lastCubemapSize = size;
//					}

//					// Overwrite faces
//					CopyFace(reusableSixSidedCubemap, CubemapFace.PositiveZ, front); // +Z
//					CopyFace(reusableSixSidedCubemap, CubemapFace.NegativeZ, back);  // -Z
//					CopyFace(reusableSixSidedCubemap, CubemapFace.NegativeX, right); // -X
//					CopyFace(reusableSixSidedCubemap, CubemapFace.PositiveX, left);  // +X
//					CopyFace(reusableSixSidedCubemap, CubemapFace.PositiveY, down);  // +Y (down in skybox = up in cubemap?)
//					CopyFace(reusableSixSidedCubemap, CubemapFace.NegativeY, up);    // -Y

//					reusableSixSidedCubemap.Apply();

//					cubemap = reusableSixSidedCubemap;
//				}
//				else
//				{
//					Debug.LogWarning("Incomplete 6-sided skybox textures, using fallback cubemap.");
//					cubemap = defaultCubemap;
//				}
//			}
//			else
//			{
//				Debug.LogWarning($"Skybox shader '{skyboxMaterial.shader.name}' does not expose a cubemap or 6-face properties.");
//				cubemap = defaultCubemap;
//			}

//			// Final assignment and caching
//			waterMaterial.SetTexture("_Skybox", cubemap);
//			lastSkyboxMaterial = skyboxMaterial;
//			lastCubemap = cubemap;
//		}

//		/// <summary>
//		/// Copies a Texture2D face into a Cubemap face.
//		/// Optimized: uses direct GetPixels if texture is readable.
//		/// </summary>
//		private static void CopyFace(Cubemap target, CubemapFace face, Texture2D source)
//		{
//			if (source == null || target == null) return;

//			// Fast path: texture is readable (enable "Read/Write Enabled" in import settings for best perf)
//			if (source.isReadable)
//			{
//				target.SetPixels(source.GetPixels(), face);
//				return;
//			}

//			// Slow path: GPU to CPU readback (only when texture is not readable)
//			RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
//			Graphics.Blit(source, rt);

//			RenderTexture previousActive = RenderTexture.active;
//			RenderTexture.active = rt;

//			Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
//			readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
//			readable.Apply();

//			target.SetPixels(readable.GetPixels(), face);

//			RenderTexture.active = previousActive;
//			RenderTexture.ReleaseTemporary(rt);
//			Object.DestroyImmediate(readable); // Safe: temporary non-asset texture
//		}
//	}
//}