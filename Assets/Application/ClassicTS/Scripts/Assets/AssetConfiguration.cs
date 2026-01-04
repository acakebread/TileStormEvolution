namespace ClassicTilestorm.Assets
{
	public static class AssetConfiguration
	{
		public static void Initialize()
		{
			// Geometry models
			ModelAssets.RegisterRoot(AssetPath.GeometryPath);
			ModelAssets.RegisterRoot("Levels");
			ModelAssets.NameRemapper = ClassicTileStormAssetRemapHelper.RemapName;

			// Runtime prefabs (flame, spark, etc.)
			PrefabAssets.RegisterRoot(AssetPath.PrefabPath);

			// All materials — including skyboxes
			MaterialAssets.RegisterRoot(AssetPath.MaterialPath);
			MaterialAssets.RegisterRoot(AssetPath.SkycubesPath);
		}

		public static void ClearAllCaches()
		{
			ModelAssets.ClearCache();
			PrefabAssets.ClearCache();
			MaterialAssets.ClearCache();
		}
	}
}