//#if UNITY_EDITOR
//using UnityEditor;
//using UnityEngine;
//using ClassicTilestorm.Assets;

//namespace ClassicTilestorm.Assets
//{
//	[InitializeOnLoad]
//	public static class AssetConfigurationEditorInitializer
//	{
//		static AssetConfigurationEditorInitializer()
//		{
//			// Delay the initialization so Unity's asset database and editor state are ready
//			EditorApplication.delayCall += EnsureInitialized;
//		}

//		public static void EnsureInitialized()
//		{
//			if (IsInitialized)
//				return;

//			try
//			{
//				AssetConfiguration.Initialize();
//				IsInitialized = true;
//				Debug.Log("[Editor] AssetConfiguration initialized successfully from editor.");
//			}
//			catch (System.Exception e)
//			{
//				Debug.LogError($"[AssetConfiguration] Failed to initialize from editor: {e}");
//			}
//		}

//		private static bool IsInitialized { get; set; } = false;

//		// Reset flag when exiting play mode (optional but good practice)
//		[InitializeOnLoadMethod]
//		private static void RegisterPlayModeCallback()
//		{
//			EditorApplication.playModeStateChanged += state =>
//			{
//				if (state == PlayModeStateChange.ExitingPlayMode)
//					IsInitialized = false;
//			};
//		}
//	}
//}
//#endif