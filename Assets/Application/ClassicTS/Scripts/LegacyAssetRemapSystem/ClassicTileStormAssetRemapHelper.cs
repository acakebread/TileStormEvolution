// File: ClassicTileStormAssetRemapHelper.cs
using UnityEngine;

namespace ClassicTilestorm
{
	public static class ClassicTileStormAssetRemapHelper
	{
		private static ClassicTileStormAssetRemap instance;

		private static ClassicTileStormAssetRemap Instance
		{
			get
			{
				if (instance == null)
				{
					instance = Resources.Load<ClassicTileStormAssetRemap>("ClassicTileStormAssetRemap");
					if (instance == null)
					{
						Debug.LogError("[AssetRemap] ClassicTileStormAssetRemap.asset not found in any Resources folder! Create it via Assets → Create → ClassicTilestorm → Asset Remap Table");
					}
				}
				return instance;
			}
		}

		/// <summary>
		/// Returns the remapped model name if one exists, otherwise returns the original name.
		/// </summary>
		public static string RemapName(string originalName)
		{
			if (string.IsNullOrEmpty(originalName)) return originalName;

			string clean = System.IO.Path.GetFileNameWithoutExtension(originalName).Trim();

			if (Instance != null && Instance.TryGetReplacement(clean, out string replacement))
			{
				//Debug.Log($"[AssetRemap] Remapped '{clean}' → '{replacement}'");
				return replacement;
			}

			// No remap — return original
			return clean;
		}
	}
}