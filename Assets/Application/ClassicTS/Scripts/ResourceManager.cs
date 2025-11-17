// ResourceManager.cs — FINAL HYBRID VERSION (transition-safe)

//#define USING_INDIVIDUAL_MAPS

using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ClassicTilestorm
{
	public static class ResourceManager
	{
		private static DatabaseSerializer.DatabaseData _db;           // From original DatabaseSerializer
		private static Map[] _individualMaps;                         // From ResourceSerializer (overrides)

		// Public access — maps prefer individual files, everything else from DatabaseSerializer
		public static IList<Map> Maps => _individualMaps ?? _db?.maps ?? new Map[0];
		public static IList<TileDef> TileDefs => _db?.tiledefs ?? new TileDef[0];
		public static IList<Theme> Themes => _db?.themes ?? new Theme[0];
		public static IList<TextureSet> TextureSets => _db?.texture_set ?? new TextureSet[0];
		public static IList<Button> Buttons => _db?.buttons ?? new Button[0];

		public static bool IsInitialized => _db != null;

		public static void Initialize()
		{
			if (_db != null) return;

			// 1. Load the original database exactly as before
			_db = DatabaseSerializer.LoadData();
			if (_db == null)
			{
				Debug.LogError("ResourceManager: Failed to load from DatabaseSerializer!");
				return;
			}
#if USING_INDIVIDUAL_MAPS
			// 2. Try to load individual map files — these override everything
			_individualMaps = ResourceSerializer.TryLoadIndividualMaps();

			if (_individualMaps.Length > 0)
			{
				Debug.Log($"ResourceManager: Using {_individualMaps.Length} individual map files (StreamingAssets/Maps/) — overriding built-in maps");
			}
			else
			{
				Debug.Log($"ResourceManager: No individual map files found — using {_db.maps.Length} maps from database.json");
				_individualMaps = null; // Explicitly null so Maps falls back cleanly
			}
#endif
		}

		// ──────────────────────────────────────────────────────────────
		// Helper lookups — unchanged, still use DatabaseSerializer data
		// ──────────────────────────────────────────────────────────────
		public static TileDef GetTileDef(string szType) =>
			string.IsNullOrEmpty(szType) ? null : _db?.tiledefs.FirstOrDefault(td => td?.szType == szType);

		public static Theme GetTheme(string themeName) =>
			string.IsNullOrEmpty(themeName) ? null : _db?.themes.FirstOrDefault(t => t?.name == themeName);

		public static TextureSet GetTextureSet(string name) =>
			string.IsNullOrEmpty(name) ? null : _db?.texture_set.FirstOrDefault(ts => ts?.name == name);

		// ──────────────────────────────────────────────────────────────
		// Map mutation — smart: updates both in-memory sources and saves to file
		// ──────────────────────────────────────────────────────────────
		public static void ApplyMapChanges(Map updatedMap)
		{
			if (updatedMap == null) return;

			bool savedToFile = false;

			// If we're using individual files → update that array and save to disk
			if (_individualMaps != null)
			{
				for (int i = 0; i < _individualMaps.Length; i++)
				{
					if (_individualMaps[i].name == updatedMap.name)
					{
						_individualMaps[i] = updatedMap;
						ResourceSerializer.SaveMap(updatedMap);
						savedToFile = true;
						break;
					}
				}

				// If map wasn't found in individual files, add it (new map created in editor)
				if (!savedToFile)
				{
					_individualMaps = _individualMaps.Concat(new[] { updatedMap }).ToArray();
					ResourceSerializer.SaveMap(updatedMap);
					savedToFile = true;
				}
			}

			// Always keep the original database in sync (safe for old code paths)
			if (_db != null)
			{
				for (int i = 0; i < _db.maps.Length; i++)
				{
					if (_db.maps[i].name == updatedMap.name)
					{
						_db.maps[i] = updatedMap;
						break;
					}
				}
			}
		}

		// These two are only for legacy save paths — still work
		public static void UpdateChanges() => DatabaseSerializer.UpdateDatabase(_db);
		public static void SaveToDisk() => DatabaseSerializer.SaveDatabase(_db);
		public static DatabaseSerializer.DatabaseData GetCurrentDatabaseData() => _db;
	}
}