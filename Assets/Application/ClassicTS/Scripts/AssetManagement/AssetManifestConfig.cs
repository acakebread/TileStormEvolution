using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm.Assets
{
	/// <summary>
	/// Central configuration for asset name manifests.
	/// Removes duplication between ProjectAssets and AssetManifestGenerator.
	/// </summary>
	public static class AssetManifestConfig
	{
		public const string ManifestRootFolder = "AssetManifests";

		/// <summary>
		/// Defines which manifest file should be used for a given asset type and root set.
		/// </summary>
		public static string GetManifestName<T>(string[] roots = null) where T : UnityEngine.Object
		{
			if (typeof(T) == typeof(GameObject))
				return "Models";

			if (typeof(T) == typeof(Texture))
				return "Textures";

			if (typeof(T) == typeof(Material))
			{
				if (roots != null && roots.Any(r => r != null && r.Contains("SkyCubes", StringComparison.OrdinalIgnoreCase)))
					return "SkyCubes";
				return "Materials";
			}

			if (typeof(T) == typeof(TextAsset))
				return "Maps";

			if (typeof(T) == typeof(AudioClip))
			{
				if (roots != null && roots.Any(r => r != null && (r.Contains("Music", StringComparison.OrdinalIgnoreCase))))
					return "Music";
				return "Sounds";
			}

			return "Unknown";
		}

		/// <summary>
		/// Returns all manifest definitions used by the generator.
		/// This is the single source of truth for what gets written during build.
		/// </summary>
		public static IEnumerable<(string ManifestName, Type AssetType, Func<IEnumerable<string>> GetRoots)> GetAllManifestDefinitions()
		{
			yield return ("Models", typeof(GameObject), () => AssetRegistry<GameObject>.GetRegisteredModelRoots());
			yield return ("Prefabs", typeof(GameObject), () => AssetRegistry<GameObject>.GetRegisteredPrefabRoots());
			yield return ("Textures", typeof(Texture), () => AssetRegistry<Texture>.GetRegisteredTextureRoots());
			yield return ("Materials", typeof(Material), () => AssetRegistry<Material>.GetRegisteredMaterialRoots());
			yield return ("SkyCubes", typeof(Material), () => AssetRegistry<Material>.GetRegisteredSkyboxRoots());
			yield return ("Sounds", typeof(AudioClip), () => AssetRegistry<AudioClip>.GetRegisteredSoundRoots());
			yield return ("Music", typeof(AudioClip), () => AssetRegistry<AudioClip>.GetRegisteredMusicRoots());
		}
	}
}
