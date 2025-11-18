//// AtomicMapExporter.cs — ZERO changes to MapManager, 100% safe
//using UnityEngine;
//using System.IO;
//using System.Linq;
//using Newtonsoft.Json;

//namespace ClassicTilestorm
//{
//	public static class AtomicMapExporter
//	{
//		private static readonly JsonSerializerSettings JsonSettings = new()
//		{
//			NullValueHandling = NullValueHandling.Ignore,
//			Formatting = Formatting.Indented
//		};

//		public static void ExportCurrentMap(string customName = null)
//		{
//			var mapManager = FindObjectOfType<MapManager>();
//			if (mapManager == null)
//			{
//				Debug.LogError("No MapManager found in scene!");
//				return;
//			}

//			var map = mapManager.CurrentMap;
//			if (map == null || string.IsNullOrEmpty(map.name))
//			{
//				Debug.LogError("Current map is invalid!");
//				return;
//			}

//			// Collect all used definition types from the map
//			var usedTypes = map.table?
//				.Where(t => !string.IsNullOrEmpty(t))
//				.Distinct()
//				.ToArray() ?? System.Array.Empty<string>();

//			// Get definitions used in this map
//			var usedDefs = ResourceManager.Definitions
//				.Where(d => usedTypes.Contains(d.szType))
//				.ToArray();

//			// Get texture banks used by these definitions
//			var usedTextureIds = usedDefs
//				.Where(d => !string.IsNullOrEmpty(d.szBank))
//				.Select(d => d.szBank)
//				.Distinct()
//				.ToArray();

//			var usedTextures = ResourceManager.TextureSets
//				.Where(ts => usedTextureIds.Contains(ts.name))
//				.ToArray();

//			var atomic = new AtomicMap
//			{
//				map = map,
//				definitions = usedDefs,
//				textures = usedTextures
//			};

//			string fileName = customName ?? map.name;
//			string path = Path.Combine(Application.persistentDataPath, "Maps", $"{fileName}.json");
//			Directory.CreateDirectory(Path.GetDirectoryName(path)!);

//			string json = JsonConvert.SerializeObject(atomic, JsonSettings);
//			File.WriteAllText(path, json);

//			Debug.Log($"Atomic map exported: {path}");
//			Debug.Log($"   Map: {map.name} ({map.width}x{map.height})");
//			Debug.Log($"   Definitions: {usedDefs.Length}");
//			Debug.Log($"   Textures: {usedTextures.Length}");
//		}
//	}
//}