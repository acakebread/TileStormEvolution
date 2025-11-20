using UnityEngine;
using System.Linq;
using Newtonsoft.Json;


#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

namespace ClassicTilestorm
{
	[System.Serializable]
	public class DatabaseData
	{
		public Map[] maps;
		public Definition[] definitions;
		public TextureSequence[] textures;
		public Button[] buttons;
	}

	public static class ResourceManager
	{
		private static DatabaseData _db;
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
				Debug.LogError("ResourceManager: PristineDatabaseJsonFile not assigned in PreviewSettings!");
				return;
			}

			_db = ResourceSerializer.DeserializeDatabase(pristine.text);

#if USING_INDIVIDUAL_MAPS
    _individualMaps = ResourceFileIO.LoadIndividualMaps();
    if (_individualMaps != null && _individualMaps.Length > 0)
        Debug.Log($"ResourceManager: Loaded {_individualMaps.Length} individual maps (overriding bundled)");
#endif
		}

		public static Definition GetDefinition(string id)
			=> string.IsNullOrEmpty(id) ? null : Definitions.FirstOrDefault(d => d.id == id);

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
					ResourceSerializer.SaveIndividualMap(map);
				Debug.Log($"Saved {_individualMaps.Length} individual maps");
				return;
			}

			if (_db != null)
			{
				ResourceSerializer.SaveDatabase(_db);
				Debug.Log("Saved full database.json");
			}
		}

		public static DatabaseData GetCurrentData() => _db;

		public static void ReloadDatabase()
		{
			_db = null;
			_individualMaps = null;
			Initialize();
			Debug.Log("Database reloaded from PristineDatabaseJsonFile");
		}

#if UNITY_EDITOR
		public static void SaveDatabaseToProject()
		{
			if (_db == null)
			{
				Debug.LogError("Cannot save: database not loaded");
				return;
			}

			var pristine = PreviewSettings.PristineDatabaseJsonFile;
			if (pristine == null)
			{
				Debug.LogError("PreviewSettings.PristineDatabaseJsonFile is not assigned!");
				return;
			}

			string assetPath = AssetDatabase.GetAssetPath(pristine);
			if (string.IsNullOrEmpty(assetPath) || assetPath.Contains("Resources/unity_builtin_extra"))
			{
				Debug.LogError("Cannot save to project: not a real project asset.");
				return;
			}

			string fullPath = Path.GetFullPath(assetPath);

			ResourceSerializer.SaveDatabase(_db, fullPath);

			AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
			Debug.Log($"[Editor] Database saved to project asset → {assetPath}");
		}
#endif
	}
}