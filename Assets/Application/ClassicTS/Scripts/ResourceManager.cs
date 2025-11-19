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

			var pristine = PreviewSettings.PristineDatabaseJsonFile;
			if (pristine == null)
			{
				Debug.LogError("ResourceManager: Pristine database TextAsset is missing!");
				return;
			}

			var mutable = ResourceFileIO.GetMutableDatabaseTextAsset(pristine);
			_db = ResourceSerializer.DeserializeDatabase(mutable.text);

			if (_db == null)
			{
				Debug.LogWarning("Mutable database corrupted or invalid → restoring from pristine copy");
				ResourceFileIO.OverwriteMutableDatabaseWithPristine(pristine);

				mutable = ResourceFileIO.GetMutableDatabaseTextAsset(pristine);
				_db = ResourceSerializer.DeserializeDatabase(mutable.text);

				if (_db == null)
				{
					Debug.LogError("FATAL: Even pristine database failed to load. Cannot continue.");
					return;
				}

				Debug.Log("Successfully recovered database from pristine version.");
			}

#if USING_INDIVIDUAL_MAPS
    _individualMaps = ResourceFileIO.LoadIndividualMaps();
    if (_individualMaps != null && _individualMaps.Length > 0)
        Debug.Log($"ResourceManager: Loaded {_individualMaps.Length} individual map files (overriding bundled)");
    else
        _individualMaps = null;
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

		// ResourceManager.cs — FINAL SaveToDisk (uses ResourceFileIO only)
		public static void SaveToDisk()
		{
			if (_individualMaps != null && _individualMaps.Length > 0)
			{
				foreach (var map in _individualMaps)
					ResourceFileIO.SaveIndividualMap(map);

				Debug.Log($"ResourceManager: Saved {_individualMaps.Length} individual map files.");
				return;
			}

			if (_db != null)
			{
				ResourceFileIO.SaveDatabase(_db);
				Debug.Log("ResourceManager: Saved full database.json");
				return;
			}

			Debug.LogWarning("ResourceManager.SaveToDisk(): Nothing to save — no data loaded.");
		}

		public static DatabaseSerializer.DatabaseData GetCurrentData() => _db;
	}
}