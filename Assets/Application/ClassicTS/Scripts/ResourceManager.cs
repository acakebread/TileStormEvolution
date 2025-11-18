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
		public static IList<TextureBank> TextureSets => _db?.textureBanks ?? System.Array.Empty<TextureBank>();
		public static IList<Button> Buttons => _db?.buttons ?? System.Array.Empty<Button>();

		public static bool IsInitialized => _db != null;

		// Exact same signature — your MainController calls this
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
				Debug.LogError("ResourceManager: Failed to load database.json!");
				return;
			}

#if USING_INDIVIDUAL_MAPS
            _individualMaps = ResourceSerializer.TryLoadIndividualMaps();
            if (_individualMaps != null && _individualMaps.Length > 0)
            {
                Debug.Log($"ResourceManager: Loaded {_individualMaps.Length} individual map files");
            }
            else
            {
                _individualMaps = null;
            }
#endif
		}

		public static Definition GetDefinition(string szType) =>
			string.IsNullOrEmpty(szType) ? null : Definitions.FirstOrDefault(d => d.szType == szType);

		public static TextureBank GetTextureBank(string name) =>
			string.IsNullOrEmpty(name) ? null : TextureSets.FirstOrDefault(ts => ts.name == name);

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