//// MapExtractor.cs — Exports ALL maps as MINIFIED individual files
//using UnityEngine;
//using UnityEditor;
//using System.IO;
//using Newtonsoft.Json.Linq;
//using Newtonsoft.Json;

//namespace ClassicTilestorm.Editor
//{
//	public static class MapExtractor
//	{
//		private const string OUTPUT_FOLDER = "StreamingAssets/Maps";

//		[MenuItem("Tools/Classic Tilestorm/Export All Maps → Minified Individual Files")]
//		public static void ExportAllMapsMinified()
//		{
//			var db = Resources.Load<TextAsset>("database");
//			if (db == null)
//			{
//				Debug.LogError("Resources/database.json not found!");
//				return;
//			}

//			string fullPath = Path.Combine(Application.dataPath, OUTPUT_FOLDER);
//			Directory.CreateDirectory(fullPath);

//			var root = JObject.Parse(db.text);
//			var maps = root["maps"] as JArray;

//			if (maps == null || maps.Count == 0)
//			{
//				Debug.LogWarning("No maps found in database.json");
//				return;
//			}

//			int count = 0;
//			foreach (JObject m in maps)
//			{
//				string name = m["name"]?.ToString();
//				if (string.IsNullOrEmpty(name)) continue;

//				// Remove legacy fields
//				m.Remove("szEggbotCostume");
//				m.Remove("szMusic");
//				m.Remove("szButtonID");
//				m.Remove("nWidth");
//				m.Remove("nHeight");

//				// Ensure clean names exist
//				if (m["szEggbotCostume"] != null) m["character"] = m["szEggbotCostume"];
//				if (m["szMusic"] != null) m["music"] = m["szMusic"];
//				if (m["szButtonID"] != null) m["button"] = m["szButtonID"];
//				if (m["nWidth"] != null) m["width"] = m["nWidth"];
//				if (m["nHeight"] != null) m["height"] = m["nHeight"];

//				string safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
//				string path = Path.Combine(fullPath, safeName + ".json");

//				// MINIFIED!
//				File.WriteAllText(path, m.ToString(Formatting.None));
//				count++;
//			}

//			Debug.Log($"Exported {count} maps as MINIFIED individual files → Assets/{OUTPUT_FOLDER}/");
//			AssetDatabase.Refresh();
//		}
//	}
//}