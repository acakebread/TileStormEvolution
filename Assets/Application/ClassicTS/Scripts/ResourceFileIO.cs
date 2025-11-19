// ResourceFileIO.cs — ONLY PLACE THAT TOUCHES THE DISK
using UnityEngine;
using System.IO;
using System.Linq;

namespace ClassicTilestorm
{
	public static class ResourceFileIO
	{
		private static readonly string DatabaseFolder = Path.Combine(Application.persistentDataPath, "Data");
		private static readonly string ExportFolder = Path.Combine(Application.persistentDataPath, "Maps");
		private static readonly string IndividualMapsFolder = Path.Combine(Application.streamingAssetsPath, "Maps");

		static ResourceFileIO()
		{
			Directory.CreateDirectory(DatabaseFolder);
			Directory.CreateDirectory(ExportFolder);
			Directory.CreateDirectory(IndividualMapsFolder);
		}

		// ─────── Mutable Database (persistentDataPath) ───────
		public static TextAsset GetMutableDatabaseTextAsset(TextAsset pristine)
		{
			string path = Path.Combine(DatabaseFolder, pristine.name.EndsWith(".json") ? pristine.name : pristine.name + ".json");

			if (!File.Exists(path))
			{
				File.WriteAllText(path, pristine.text);
				Debug.Log($"ResourceFileIO: Created mutable database → {path}");
			}

			string content = File.ReadAllText(path);
			var asset = new TextAsset(content) { name = pristine.name };
			return asset;
		}

		public static void OverwriteMutableDatabaseWithPristine(TextAsset pristine)
		{
			string path = Path.Combine(DatabaseFolder, pristine.name.EndsWith(".json") ? pristine.name : pristine.name + ".json");
			File.WriteAllText(path, pristine.text);
			Debug.Log($"ResourceFileIO: Restored pristine database → {path}");
		}

		public static void SaveDatabase(ResourceSerializer.DatabaseData data)
		{
			string path = Path.Combine(DatabaseFolder, "database.json");
			string json = ResourceSerializer.SerializeDatabase(data, pretty: false);
			File.WriteAllText(path, json);
			Debug.Log($"ResourceFileIO: Saved full database → {path}");
		}

		// ─────── Individual Maps (StreamingAssets) ───────
		public static Map[] LoadIndividualMaps()
		{
			var list = new System.Collections.Generic.List<Map>();
			if (!Directory.Exists(IndividualMapsFolder)) return list.ToArray();

			foreach (string file in Directory.GetFiles(IndividualMapsFolder, "*.json"))
			{
				try
				{
					string json = File.ReadAllText(file);
					var map = ResourceSerializer.DeserializeMap(json);
					if (map != null)
					{
						if (string.IsNullOrEmpty(map.name))
							map.name = Path.GetFileNameWithoutExtension(file);
						list.Add(map);
					}
				}
				catch (System.Exception ex)
				{
					Debug.LogError($"ResourceFileIO: Failed to load individual map {file}: {ex.Message}");
				}
			}
			return list.ToArray();
		}

		public static bool SaveIndividualMap(Map map)
		{
			if (map == null || string.IsNullOrEmpty(map.name)) return false;

			string safeName = string.Join("_", map.name.Split(Path.GetInvalidFileNameChars()));
			string path = Path.Combine(IndividualMapsFolder, safeName + ".json");
			string json = ResourceSerializer.SerializeMap(map, pretty: false);
			File.WriteAllText(path, json);
			Debug.Log($"ResourceFileIO: Saved individual map → {path}");
			return true;
		}

		// ─────── Atomic Export ───────
		public static void ExportAtomicMap(Map map, string overridePath = null)
		{
			var usedTypes = map.table?.Where(t => !string.IsNullOrEmpty(t)).Distinct() ?? System.Array.Empty<string>();
			var usedDefs = ResourceManager.Definitions.Where(d => usedTypes.Contains(d.szType)).ToArray();
			var usedBanks = usedDefs.Where(d => !string.IsNullOrEmpty(d.szBank)).Select(d => d.szBank).Distinct();
			var usedTextures = ResourceManager.TextureSets.Where(ts => usedBanks.Contains(ts.name)).ToArray();

			string json = ResourceSerializer.SerializeAtomic(map, usedDefs, usedTextures);

			string folder = string.IsNullOrEmpty(overridePath) ? ExportFolder : Path.GetDirectoryName(overridePath);
			Directory.CreateDirectory(folder);
			string path = string.IsNullOrEmpty(overridePath)
				? Path.Combine(folder, $"{map.name}.json")
				: overridePath;

			File.WriteAllText(path, json);
			Debug.Log($"ResourceFileIO: Exported atomic map → {path}");
		}
	}
}