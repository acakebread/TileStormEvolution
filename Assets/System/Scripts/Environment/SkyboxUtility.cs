using UnityEngine;

namespace MassiveHadronLtd
{
	public static class SkyboxUtility
	{
		private static Cubemap defaultCubemap = null;
		private static Material lastSkyboxMaterial = null;
		private static Cubemap lastCubemap = null;
		private static Material defaultSkyboxMaterial;

		static SkyboxUtility()
		{
			defaultSkyboxMaterial = RenderSettings.skybox;
		}

		public static void SetSkybox(string pathPrefix, string skyboxName)
		{
			if (string.IsNullOrEmpty(pathPrefix) || string.IsNullOrEmpty(skyboxName))
			{
				Debug.LogWarning("Path prefix or skybox name is null/empty.");
				return;
			}

			var skyboxPath = $"{pathPrefix}{skyboxName}".Replace(".mat", "");//var skyboxPath = $"{pathPrefix}{skyboxName}Skybox".Replace(".mat", "");
			var material = Resources.Load<Material>(skyboxPath);
			RenderSettings.skybox = material ? material : defaultSkyboxMaterial;
		}

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
		/// Sets the water reflection cubemap to match the current skybox.
		/// Handles both cubemap-based and 6-sided skyboxes.
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
				waterMaterial.SetTexture("_Skybox", defaultCubemap);
				return;
			}

			// Skip if already processed
			if (skyboxMaterial == lastSkyboxMaterial && lastCubemap != null)
			{
				waterMaterial.SetTexture("_Skybox", lastCubemap);
				return;
			}

			Cubemap cubemap = null;

			// --- 1️⃣ Cubemap-based skyboxes ---
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

			// --- 2️⃣ 6-Sided skyboxes ---
			if (skyboxMaterial.HasProperty("_FrontTex"))
			{
				// Extract 6 textures
				Texture2D front = skyboxMaterial.GetTexture("_FrontTex") as Texture2D;
				Texture2D back = skyboxMaterial.GetTexture("_BackTex") as Texture2D;
				Texture2D left = skyboxMaterial.GetTexture("_LeftTex") as Texture2D;
				Texture2D right = skyboxMaterial.GetTexture("_RightTex") as Texture2D;
				Texture2D up = skyboxMaterial.GetTexture("_UpTex") as Texture2D;
				Texture2D down = skyboxMaterial.GetTexture("_DownTex") as Texture2D;

				if (front && back && left && right && up && down)
				{
					int size = front.width; // assume square
					cubemap = new Cubemap(size, TextureFormat.RGBA32, false);

					// Copy each face (GPU->CPU readback)
					// NOTE: This is slow at runtime, but works fine for editor/runtime previewing
					CopyFace(cubemap, CubemapFace.PositiveZ, front);
					CopyFace(cubemap, CubemapFace.NegativeZ, back);
					CopyFace(cubemap, CubemapFace.NegativeX, right);
					CopyFace(cubemap, CubemapFace.PositiveX, left);
					CopyFace(cubemap, CubemapFace.PositiveY, down);
					CopyFace(cubemap, CubemapFace.NegativeY, up);
					cubemap.Apply();
				}
				else
				{
					Debug.LogWarning("Incomplete 6-sided skybox textures, using fallback cubemap.");
					cubemap = defaultCubemap;
				}
			}
			else
			{
				Debug.LogWarning($"Skybox shader '{skyboxMaterial.shader.name}' does not expose a cubemap property.");
				cubemap = defaultCubemap;
			}

			waterMaterial.SetTexture("_Skybox", cubemap);
			lastSkyboxMaterial = skyboxMaterial;
			lastCubemap = cubemap;
		}

		/// <summary>
		/// Copies a Texture2D face into a Cubemap face (CPU readback).
		/// </summary>
		private static void CopyFace(Cubemap target, CubemapFace face, Texture2D source)
		{
			if (source == null || target == null)
				return;

			// Ensure readable texture
			RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0);
			Graphics.Blit(source, rt);
			RenderTexture.active = rt;

			Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
			readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
			readable.Apply();

			Color[] pixels = readable.GetPixels();
			target.SetPixels(pixels, face);

			RenderTexture.active = null;
			RenderTexture.ReleaseTemporary(rt);
			Object.DestroyImmediate(readable);
		}
	}
}