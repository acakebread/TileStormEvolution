// File: ClassicTileStormAssetRemap.cs
using UnityEngine;
using System.Collections.Generic;
using System;

namespace ClassicTilestorm
{
	[CreateAssetMenu(fileName = "ClassicTileStormAssetRemap", menuName = "ClassicTilestorm/Asset Remap Table", order = 100)]
	public class ClassicTileStormAssetRemap : ScriptableObject
	{
		[System.Serializable]
		public class RemapEntry
		{
			public string legacyName;   // e.g. "ss_tile_boundary"
			public string replacement;  // e.g. "ss_tile_boundary_hd" or a completely different name
		}

		[Tooltip("Legacy model names ? new/replacement model names. Checked before fallback.")]
		public List<RemapEntry> remaps = new List<RemapEntry>();

		// Runtime fast lookup
		private Dictionary<string, string> lookup;

		public void Initialize()
		{
			if (lookup != null) return;

			lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			foreach (var entry in remaps)
			{
				if (!string.IsNullOrEmpty(entry.legacyName) && !string.IsNullOrEmpty(entry.replacement))
				{
					string key = entry.legacyName.Trim();
					string value = entry.replacement.Trim();

					if (lookup.ContainsKey(key))
					{
						Debug.LogWarning($"[AssetRemap] Duplicate legacy name '{key}' — keeping first entry.");
					}
					else
					{
						lookup[key] = value;
					}
				}
			}
		}

		public bool TryGetReplacement(string legacyName, out string replacement)
		{
			Initialize();
			return lookup.TryGetValue(legacyName, out replacement);
		}
	}
}