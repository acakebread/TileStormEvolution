using MassiveHadronLtd;
using UnityEngine;

namespace ClassicTilestorm.Assets
{
	public static class AssetConfiguration
	{
		public static void Initialize()
		{
			AssetRegistry<GameObject>.ClearRegisteredRoots();
			AssetRegistry<Texture>.ClearRegisteredRoots();
			AssetRegistry<Material>.ClearRegisteredRoots();
			AssetRegistry<AudioClip>.ClearRegisteredRoots();

			foreach (var root in ApplicationSettings.GetGeometryPaths())
				ModelAssets.RegisterRoot(root);
			foreach (var root in ApplicationSettings.GetPrefabPaths())
				PrefabAssets.RegisterRoot(root);
			foreach (var root in ApplicationSettings.GetTexturePaths())
				TextureAssets.RegisterRoot(root);
			foreach (var root in ApplicationSettings.GetMaterialPaths())
				MaterialAssets.RegisterRoot(root);
			foreach (var root in ApplicationSettings.GetSkyCubePaths())
				SkyboxAssets.RegisterRoot(root);
			foreach (var root in ApplicationSettings.GetSoundPaths())
				SoundAssets.RegisterRoot(root); // e.g. "ClassicTS/Sounds/"
			foreach (var root in ApplicationSettings.GetMusicPaths())
				MusicAssets.RegisterRoot(root); // e.g. "ClassicTS/Music/"
			ResourceResolvers.TextureResolver = new TextureResourceResolver();
			ResourceResolvers.SkyboxResolver = new SkyboxResourceResolver();
			ResourceResolvers.MusicResolver = new MusicResourceResolver();
			ModelAssets.RefreshRegistry(forceRefresh: true);
		}

		public static void ClearAllCaches()
		{
			ModelAssets.ClearCache();
			PrefabAssets.ClearCache();
			TextureAssets.ClearCache();
			MaterialAssets.ClearCache();
			PrefabResourceTable.ClearCache();
			TextureResourceTable.ClearCache();
			MaterialResourceTable.ClearCache();
			SkycubeResourceTable.ClearCache();
			MusicResourceTable.ClearCache();
			SoundResourceTable.ClearCache();
			CharacterResourceTable.ClearCache();
			EffectResourceTable.ClearCache();
			ImportedResourceLoader.ClearCache();
			ProjectAssets.RefreshAllNameCaches();
			//don't know if these need clearing
			//SkyboxAssets.ClearCache();
			//SoundAssets.ClearCache();
			//MusicAssets.ClearCache();
		}
	}
}
