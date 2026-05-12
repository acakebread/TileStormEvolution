using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ClassicTilestorm.Editor
{
	public class ClassicTilestormTools : EditorWindow
	{
		private TextAsset databaseJson;

		[MenuItem("Tools/Classic Tilestorm/Atomic Map Tools")]
		public static void ShowWindow() => GetWindow<ClassicTilestormTools>("CT Atomic Tools");

		private void OnGUI()
		{
			GUILayout.Label("Database Source", EditorStyles.boldLabel);
			databaseJson = (TextAsset)EditorGUILayout.ObjectField("database.json", databaseJson, typeof(TextAsset), false);

			GUILayout.Space(10);

			if (databaseJson == null)
			{
				EditorGUILayout.HelpBox("Drag your database.json TextAsset here first.", MessageType.Warning);
				return;
			}

			if (GUILayout.Button("Export ALL Maps → Full AtomicMap Files (with deps)"))
				ExportAllMapsAsAtomic();

			if (GUILayout.Button("Convert Definitions → Clean Modern Format"))
				DefinitionConverter.Convert(databaseJson);
		}

		private void ExportAllMapsAsAtomic()
		{
			// Use the NEW unified serializer
			var db = ResourceSerializer.LoadDatabase(databaseJson.text);
			if (db == null || db.maps == null || db.maps.Length == 0)
			{
				Debug.LogError("Failed to parse database.json – aborting export.");
				return;
			}

			// Temporarily inject into ResourceManager so ExportAtomicMap can resolve dependencies
			var field = typeof(ResourceManager).GetField("_db",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
			var previous = field?.GetValue(null);
			field?.SetValue(null, db);

			string outputFolder = Path.Combine(Application.dataPath, "StreamingAssets", "Maps");
			Directory.CreateDirectory(outputFolder);

			int count = 0;
			foreach (var map in db.maps)
			{
				if (map == null) continue;
				string path = Path.Combine(outputFolder, $"{map.name}.json");
				ResourceSerializer.ExportAtomicMap(map, path);
				count++;
			}

			// Restore previous state
			field?.SetValue(null, previous);

			Debug.Log($"Batch export complete: {count} full AtomicMap files → Assets/StreamingAssets/Maps/");
			AssetDatabase.Refresh();
		}
	}

	// Updated DefinitionConverter – still works on raw TextAsset
	public static class DefinitionConverter
	{
		[System.Serializable]
		private class OldDefinition
		{
			public string szType;
			public string szGeom;
			public string szBank;

			public bool bNorth, bSouth, bEast, bWest;
			public bool bStart, bEnd, bConsole, bSlide, bRoll, bFold, bDoor, bGang;

			public int nPickup; // 0=none, 1=coin, 2=key, 3=health, 4=ammo
		}

		[System.Serializable]
		private class OldDatabaseWrapper { public OldDefinition[] definitions; }

		public static void Convert(TextAsset databaseAsset)
		{
			string assetPath = AssetDatabase.GetAssetPath(databaseAsset);
			string fullPath = Path.GetFullPath(assetPath);

			string json = File.ReadAllText(fullPath);
			var wrapper = JsonConvert.DeserializeObject<OldDatabaseWrapper>(json);

			if (wrapper?.definitions == null || wrapper.definitions.Length == 0)
			{
				Debug.LogError("No definitions found in database.json");
				return;
			}

			var newDefs = new List<Definition>();

			foreach (var old in wrapper.definitions)
			{
				var def = new Definition
				{
					name = old.szType?.Trim(),
					model = !string.IsNullOrEmpty(old.szGeom) ? old.szGeom.Replace(".x", "").Trim() : null,
					//texture = !string.IsNullOrEmpty(old.szBank) ? old.szBank.Trim() : "Default"
					material = null
				};

				var flags = new List<string>();
				if (old.bStart) flags.Add("Start");
				if (old.bEnd) flags.Add("End");
				if (old.bConsole) flags.Add("Console");
				if (old.bSlide) flags.Add("Slide");
				if (old.bRoll) flags.Add("Roll");
				if (old.bFold) flags.Add("Fold");
				if (old.bDoor) flags.Add("Door");
				if (old.bGang) flags.Add("Gang");
				//if (flags.Count > 0) def.flags = string.Join(",", flags);

				var dirs = new List<char>();
				if (old.bNorth) dirs.Add('N');
				if (old.bSouth) dirs.Add('S');
				if (old.bEast) dirs.Add('E');
				if (old.bWest) dirs.Add('W');
				//if (dirs.Count > 0) def.connections = new string(dirs.ToArray());

				//def.pickup = old.nPickup switch
				//{
				//	1 => "Coin",
				//	2 => "Key",
				//	3 => "Health",
				//	4 => "Ammo",
				//	_ => null
				//};

				newDefs.Add(def);
			}

			var settings = new JsonSerializerSettings
			{
				NullValueHandling = NullValueHandling.Ignore,
				DefaultValueHandling = DefaultValueHandling.Ignore,
				Formatting = Formatting.Indented
			};

			string cleanJson = JsonConvert.SerializeObject(new { definitions = newDefs }, settings);
			File.WriteAllText(fullPath, cleanJson);

			Debug.Log($"Definition conversion complete → {newDefs.Count} clean definitions written to:\n{fullPath}");
			AssetDatabase.Refresh();
		}
	}
}