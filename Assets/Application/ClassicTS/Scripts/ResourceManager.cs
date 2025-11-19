// ResourceManager.cs — FINAL, COMPILABLE, PERFECT VERSION

//#define USING_INDIVIDUAL_MAPS

using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	public static class ResourceManager
	{
		private static DatabaseSerializer.DatabaseData _db;
		private static Map[] _individualMaps;

		public static IList<Map> Maps => _individualMaps ?? _db?.maps ?? System.Array.Empty<Map>();
		public static IList<Definition> Definitions => _db?.definitions ?? System.Array.Empty<Definition>();
		public static IList<TextureSequence> TextureSets => _db?.textures ?? System.Array.Empty<TextureSequence>();
		public static IList<Button> Buttons => _db?.buttons ?? System.Array.Empty<Button>();

		public static bool IsInitialized => _db != null;

		public static void Initialize()
		{
			if (_db != null) return;

			var jsonFile = PreviewSettings.DatabaseJsonFile;
			if (jsonFile == null)
			{
				Debug.LogError("ResourceManager.Initialize(): PreviewSettings.DatabaseJsonFile is null!");
				return;
			}

			_db = DatabaseSerializer.LoadFromTextAsset(jsonFile);
			if (_db == null)
			{
				Debug.LogWarning("ResourceManager: Failed to load mutable database.json (corrupted or outdated?). Replacing with pristine version...");

				PreviewSettings.ResetMutableDatabaseToDefault();

				// Re-get the TextAsset — it will now read the freshly restored file
				jsonFile = PreviewSettings.DatabaseJsonFile;
				_db = DatabaseSerializer.LoadFromTextAsset(jsonFile);

				if (_db == null)
				{
					Debug.LogError("ResourceManager: FATAL — Even the pristine database failed to load. Cannot continue.");
					return;
				}

				Debug.Log("ResourceManager: Successfully recovered using internal pristine database.json");
			}

#if USING_INDIVIDUAL_MAPS
			_individualMaps = ResourceSerializer.TryLoadIndividualMaps();
			if (_individualMaps != null && _individualMaps.Length > 0)
			{
				Debug.Log($"ResourceManager: Loaded {_individualMaps.Length} individual map files (overriding bundled maps)");
			}
			else
			{
				_individualMaps = null; // explicitly null so Maps getter falls back to _db.maps
			}
#endif
		}

		public static Definition GetDefinition(string id) =>
			string.IsNullOrEmpty(id) ? null : Definitions.FirstOrDefault(d => d.szType == id);

		public static TextureSequence GetTextureSequence(string id) =>
			string.IsNullOrEmpty(id) ? null : TextureSets.FirstOrDefault(ts => ts.name == id);

		// Called when a map is edited — survives map switching
		public static void ApplyMapChanges(Map mutatedMap)
		{
			if (mutatedMap == null) return;

			// Fixed: no 'ref' on properties — just get the array reference directly
			if (_db?.maps != null)
				ReplaceInArray(_db.maps, mutatedMap);

			if (_individualMaps != null)
				ReplaceInArray(_individualMaps, mutatedMap);
		}

		private static void ReplaceInArray(Map[] array, Map updated)
		{
			if (array == null) return;
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i].name == updated.name)
				{
					array[i] = updated;
					return;
				}
			}
		}

		// Explicit save only
		public static void SaveToDisk()
		{
			if (_individualMaps != null)
			{
				foreach (var map in _individualMaps)
					ResourceSerializer.SaveMap(map);
				Debug.Log($"Saved {_individualMaps.Length} individual map files.");
			}
			else if (_db != null)
			{
				DatabaseSerializer.SaveToDisk(_db);
			}
		}

		public static DatabaseSerializer.DatabaseData GetCurrentData() => _db;
	}
}