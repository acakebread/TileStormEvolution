namespace ClassicTilestorm.Assets
{
	public static class AssetConfiguration
	{
		public static void Initialize()
		{
			ModelAssets.NameRemapper = ClassicTileStormAssetRemapHelper.RemapName;

			// FIX: Trim and normalize the path - this is critical
			ModelAssets.RegisterRoot(AssetPath.GeometryPath?.Trim('/').Trim());
			ModelAssets.RegisterRoot("Levels");//'HD' paths
			ModelAssets.RegisterRoot("Levels/Med");//'HD' paths
			PrefabAssets.RegisterRoot(AssetPath.PrefabPath?.Trim('/').Trim());
			TextureAssets.RegisterRoot(AssetPath.TexturePath?.Trim('/').Trim());
			Texture2DAssets.RegisterRoot(AssetPath.TexturePath?.Trim('/').Trim());
			AnimMaterialInfoManager.ClearCache();
			MaterialAssets.RegisterRoot(AssetPath.MaterialPath?.Trim('/').Trim());
			SkyboxAssets.RegisterRoot(AssetPath.SkycubesPath?.Trim('/').Trim());
			SoundAssets.RegisterRoot(AssetPath.SoundPath?.Trim('/').Trim()); // e.g. "ClassicTS/Sounds/"
			MusicAssets.RegisterRoot(AssetPath.MusicPath?.Trim('/').Trim()); // e.g. "ClassicTS/Music/"
		}

		public static void ClearAllCaches()
		{
			ModelAssets.ClearCache();
			PrefabAssets.ClearCache();
			TextureAssets.ClearCache();
			Texture2DAssets.ClearCache();
			AnimMaterialInfoManager.ClearCache();
			MaterialAssets.ClearCache();
			//don't know if these need clearing
			//SkyboxAssets.ClearCache();
			//SoundAssets.ClearCache();
			//MusicAssets.ClearCache();
		}
	}
}
