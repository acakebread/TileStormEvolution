namespace ClassicTilestorm.Assets
{
	public static class AssetConfiguration
	{
		public static void Initialize()
		{
			ModelAssets.NameRemapper = ClassicTileStormAssetRemapHelper.RemapName;

			// FIX: Trim and normalize the path — this is critical
			string geometryRoot = AssetPath.GeometryPath.Trim('/').Trim();
			ModelAssets.RegisterRoot(geometryRoot);

			ModelAssets.RegisterRoot("Levels");
			ModelAssets.RegisterRoot("Levels/Med");
			PrefabAssets.RegisterRoot(AssetPath.PrefabPath.Trim('/').Trim());
			MaterialAssets.RegisterRoot(AssetPath.MaterialPath.Trim('/').Trim());
			MaterialAssets.RegisterRoot(AssetPath.SkycubesPath.Trim('/').Trim());
		}

		public static void ClearAllCaches()
		{
			ModelAssets.ClearCache();
			PrefabAssets.ClearCache();
			MaterialAssets.ClearCache();
		}
	}
}