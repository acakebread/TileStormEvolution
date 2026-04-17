namespace ClassicTilestorm.Assets
{
	public static class AssetConfiguration
	{
		public static void Initialize()
		{
			ModelAssets.NameRemapper = ClassicTileStormAssetRemapHelper.RemapName;

			// FIX: Trim and normalize the path — this is critical
			ModelAssets.RegisterRoot(AssetPath.GeometryPath?.Trim('/').Trim());
			ModelAssets.RegisterRoot("Levels");//'HD' paths
			ModelAssets.RegisterRoot("Levels/Med");//'HD' paths
			PrefabAssets.RegisterRoot(AssetPath.PrefabPath?.Trim('/').Trim());
			TextureAssets.RegisterRoot(AssetPath.TexturePath?.Trim('/').Trim());
			Texture2DAssets.RegisterRoot(AssetPath.TexturePath?.Trim('/').Trim());
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
			MaterialAssets.ClearCache();
			//don't know if these need clearing
			//SkyboxAssets.ClearCache();
			//SoundAssets.ClearCache();
			//MusicAssets.ClearCache();
		}
	}
}



//using UnityEngine;

//namespace ClassicTilestorm.Assets
//{
//	public static class AssetConfiguration
//	{
//		private static bool _isInitialized = false;

//		public static void Initialize()
//		{
//			if (_isInitialized)
//				return;

//			_isInitialized = true;

//			try
//			{
//				// Safe assignment - the helper is a static class, so we always set the delegate
//				ModelAssets.NameRemapper = ClassicTileStormAssetRemapHelper.RemapName;

//				// Safe root registrations with null/empty checks
//				SafeRegister(ModelAssets.RegisterRoot, AssetPath.GeometryPath, "Geometry");
//				ModelAssets.RegisterRoot("Levels");      // hardcoded
//				ModelAssets.RegisterRoot("Levels/Med");  // hardcoded

//				SafeRegister(PrefabAssets.RegisterRoot, AssetPath.PrefabPath, "Prefab");
//				SafeRegister(TextureAssets.RegisterRoot, AssetPath.TexturePath, "Texture");
//				SafeRegister(Texture2DAssets.RegisterRoot, AssetPath.TexturePath, "Texture2D");
//				SafeRegister(MaterialAssets.RegisterRoot, AssetPath.MaterialPath, "Material");
//				SafeRegister(SkyboxAssets.RegisterRoot, AssetPath.SkycubesPath, "Skycubes");
//				SafeRegister(SoundAssets.RegisterRoot, AssetPath.SoundPath, "Sound");
//				SafeRegister(MusicAssets.RegisterRoot, AssetPath.MusicPath, "Music");

//				Debug.Log("[AssetConfiguration] Initialize completed successfully.");
//			}
//			catch (System.Exception e)
//			{
//				Debug.LogError($"[AssetConfiguration] Initialize failed: {e}");
//#if UNITY_EDITOR
//				// Only rethrow when not in play mode so the menu item shows the error clearly
//				if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
//					throw;
//#endif
//			}
//		}

//		/// <summary>
//		/// Helper to safely trim and register a path only if it's valid
//		/// </summary>
//		private static void SafeRegister(System.Action<string> registerAction, string path, string typeName)
//		{
//			if (string.IsNullOrWhiteSpace(path))
//			{
//				Debug.LogWarning($"[AssetConfiguration] {typeName} path (AssetPath.{typeName}Path) is null or empty - skipping.");
//				return;
//			}

//			var trimmed = path.Trim('/').Trim();
//			if (!string.IsNullOrEmpty(trimmed))
//			{
//				registerAction(trimmed);
//			}
//			else
//			{
//				Debug.LogWarning($"[AssetConfiguration] {typeName} path trimmed to empty - skipping.");
//			}
//		}

//		public static void ClearAllCaches()
//		{
//			ModelAssets.ClearCache();
//			PrefabAssets.ClearCache();
//			TextureAssets.ClearCache();
//			Texture2DAssets.ClearCache();
//			MaterialAssets.ClearCache();
//		}
//	}
//}