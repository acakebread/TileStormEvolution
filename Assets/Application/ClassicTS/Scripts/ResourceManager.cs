using UnityEngine;
using System.Linq;

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
		public static DatabaseData GetCurrentData() => _db;

		public static System.Collections.Generic.IList<Map> Maps => _db?.maps ?? System.Array.Empty<Map>();
		public static System.Collections.Generic.IList<Definition> Definitions => _db?.definitions ?? System.Array.Empty<Definition>();
		public static System.Collections.Generic.IList<TextureSequence> TextureSets => _db?.textures ?? System.Array.Empty<TextureSequence>();
		public static System.Collections.Generic.IList<Button> Buttons => _db?.buttons ?? System.Array.Empty<Button>();

		public static Definition GetDefinition(string id) => string.IsNullOrEmpty(id) ? null : Definitions.FirstOrDefault(d => d.id == id);
		public static TextureSequence GetTextureSequence(string id) => string.IsNullOrEmpty(id) ? null : TextureSets.FirstOrDefault(ts => ts.name == id);

		public static void LoadDatabase(TextAsset dbAsset = null)
		{
			if (dbAsset == null) // Safe for Unity objects
				dbAsset = PreviewSettings.DatabaseJsonFile;

			if (dbAsset == null)
			{
				Debug.LogError("ResourceManager: DatabaseJsonFile not assigned in PreviewSettings!");
				return;
			}

			_db = ResourceSerializer.DeserializeDatabase(dbAsset.text);
			Debug.Log("Database loaded from DatabaseJsonFile");
		}

		//in memory only
		public static void ApplyMapChanges(Map mutatedMap)
		{
			if (mutatedMap == null) return;
			if (_db?.maps != null) ReplaceInArray(_db.maps, mutatedMap);

			static void ReplaceInArray(Map[] array, Map updated)
			{
				for (int i = 0; i < array.Length; i++)
					if (array[i].name == updated.name)
					{ array[i] = updated; return; }
			}
		}

#if UNITY_EDITOR
		public static void SaveDatabase(TextAsset dbAsset = null)
		{
			if (_db == null)
			{
				Debug.LogError("Cannot save: database not loaded");
				return;
			}

			if (dbAsset == null) // Safe for Unity objects
				dbAsset = PreviewSettings.DatabaseJsonFile;

			if (dbAsset == null)
			{
				Debug.LogError("PreviewSettings.DatabaseJsonFile is not assigned!");
				return;
			}

			string assetPath = AssetDatabase.GetAssetPath(dbAsset);
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