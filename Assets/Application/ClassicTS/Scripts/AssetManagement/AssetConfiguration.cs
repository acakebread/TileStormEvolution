using MassiveHadronLtd;

namespace ClassicTilestorm.Assets
{
	public static class AssetConfiguration
	{
		public static void Initialize()
		{
			ModelAssets.NameRemapper = ClassicTileStormAssetRemapHelper.RemapName;

			// FIX: Trim and normalize the path - this is critical
			ModelAssets.RegisterRoot(AssetPath.GeometryPath?.Trim('/').Trim());
			ModelAssets.RegisterRoot("Levels");
			ModelAssets.RegisterRoot("Levels/Med");
			PrefabAssets.RegisterRoot(AssetPath.PrefabPath?.Trim('/').Trim());
			TextureAssets.RegisterRoot(AssetPath.TexturePath?.Trim('/').Trim());
			MaterialAssets.RegisterRoot(AssetPath.MaterialPath?.Trim('/').Trim());
			SkyboxAssets.RegisterRoot(AssetPath.SkycubesPath?.Trim('/').Trim());
			SoundAssets.RegisterRoot(AssetPath.SoundPath?.Trim('/').Trim()); // e.g. "ClassicTS/Sounds/"
			MusicAssets.RegisterRoot(AssetPath.MusicPath?.Trim('/').Trim()); // e.g. "ClassicTS/Music/"
			ResourceResolvers.TextureResolver = new TextureResourceResolver();
			ResourceResolvers.SkyboxResolver = new SkyboxResourceResolver();
			ResourceResolvers.MusicResolver = new MusicResourceResolver();
			ResourceResolvers.GeometryMaterialsPathResolver = new GeometryMaterialsPathResolver();
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
