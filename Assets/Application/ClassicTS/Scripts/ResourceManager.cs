// ResourceManager.cs — HIGH-LEVEL API ONLY
using UnityEngine;
using System.Linq;

namespace ClassicTilestorm
{
	public static class ResourceManager
	{
		private static ResourceSerializer.DatabaseData _db;
		private static Map[] _individualMaps;

		public static System.Collections.Generic.IList<Map> Maps => _individualMaps ?? _db?.maps ?? System.Array.Empty<Map>();
		public static System.Collections.Generic.IList<Definition> Definitions => _db?.definitions ?? System.Array.Empty<Definition>();
		public static System.Collections.Generic.IList<TextureSequence> TextureSets => _db?.textures ?? System.Array.Empty<TextureSequence>();
		public static System.Collections.Generic.IList<Button> Buttons => _db?.buttons ?? System.Array.Empty<Button>();

		public static bool IsInitialized => _db != null;

		public static void Initialize()
		{
			if (_db != null) return;

			var pristine = PreviewSettings.PristineDatabaseJsonFile;
			if (pristine == null)
			{
				Debug.LogError("ResourceManager: Pristine database TextAsset missing in Preview... assign it in PreviewSettings!");
				return;
			}

			var mutable = ResourceFileIO.GetMutableDatabaseTextAsset(pristine);
			_db = ResourceSerializer.DeserializeDatabase(mutable.text);

			if (_db == null)
			{
				Debug.LogWarning("Mutable database corrupted → restoring pristine copy");
				ResourceFileIO.OverwriteMutableDatabaseWithPristine(pristine);

				mutable = ResourceFileIO.GetMutableDatabaseTextAsset(pristine);
				_db = ResourceSerializer.DeserializeDatabase(mutable.text);

				if (_db == null)
				{
					Debug.LogError("FATAL: Even pristine database failed to load!");
					return;
				}
			}

#if USING_INDIVIDUAL_MAPS
            _individualMaps = ResourceFileIO.LoadIndividualMaps();
            if (_individualMaps != null && _individualMaps.Length > 0)
                Debug.Log($"ResourceManager: Loaded {_individualMaps.Length} individual maps (overriding bundled)");
#endif
		}

		public static Definition GetDefinition(string id)
			=> string.IsNullOrEmpty(id) ? null : Definitions.FirstOrDefault(d => d.szType == id);

		public static TextureSequence GetTextureSequence(string id)
			=> string.IsNullOrEmpty(id) ? null : TextureSets.FirstOrDefault(ts => ts.name == id);

		public static void ApplyMapChanges(Map mutatedMap)
		{
			if (mutatedMap == null) return;
			if (_db?.maps != null) ReplaceInArray(_db.maps, mutatedMap);
			if (_individualMaps != null) ReplaceInArray(_individualMaps, mutatedMap);
		}

		private static void ReplaceInArray(Map[] array, Map updated)
		{
			for (int i = 0; i < array.Length; i++)
				if (array[i].name == updated.name)
				{ array[i] = updated; return; }
		}

		public static void SaveToDisk()
		{
			if (_individualMaps != null && _individualMaps.Length > 0)
			{
				foreach (var map in _individualMaps)
					ResourceFileIO.SaveIndividualMap(map);
				Debug.Log($"Saved {_individualMaps.Length} individual maps");
				return;
			}

			if (_db != null)
			{
				ResourceFileIO.SaveDatabase(_db);
				Debug.Log("Saved full database.json");
			}
		}

		public static ResourceSerializer.DatabaseData GetCurrentData() => _db;
	}
}