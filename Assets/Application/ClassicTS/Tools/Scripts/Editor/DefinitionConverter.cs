//using UnityEngine;
//using UnityEditor;
//using Newtonsoft.Json;
//using System.Collections.Generic;
//using System.IO;
//using ClassicTilestorm;

//public static class DefinitionConverter
//{
//	[MenuItem("Tools/Convert Definitions → Clean Modern Format (No Bloat)")]
//	public static void ConvertToCleanModernFormat()
//	{
//		string path = "Assets/Resources/database.json";

//		string json = File.ReadAllText(path);

//		var oldWrapper = JsonConvert.DeserializeObject<OldDatabaseWrapper>(json);
//		var newDefs = new List<Definition>();

//		foreach (var old in oldWrapper.definitions)
//		{
//			var def = new Definition
//			{
//				id = old.szType?.Trim(),
//				model = !string.IsNullOrEmpty(old.szGeom) ? old.szGeom.Replace(".x", "").Trim() : null,
//				texture = !string.IsNullOrEmpty(old.szBank) ? old.szBank.Trim() : "Default"
//			};

//			// Flags - only add if true
//			var flags = new List<string>();
//			if (old.bStart) flags.Add("Start");
//			if (old.bEnd) flags.Add("End");
//			if (old.bConsole) flags.Add("Console");
//			if (old.bSlide) flags.Add("Slide");
//			if (old.bRoll) flags.Add("Roll");
//			if (old.bDock) flags.Add("Dock");
//			if (old.bDoor) flags.Add("Door");
//			if (old.bPuzzleBlock) flags.Add("PuzzleBlock");

//			if (flags.Count > 0)
//				def.flags = string.Join(",", flags);
//			// else: leave null → won't be serialized

//			// Connections
//			var dirs = new List<char>();
//			if (old.bNorth) dirs.Add('N');
//			if (old.bSouth) dirs.Add('S');
//			if (old.bEast) dirs.Add('E');
//			if (old.bWest) dirs.Add('W');
//			if (dirs.Count > 0)
//				def.connections = new string(dirs.ToArray());
//			// else: null → skipped

//			// Pickup - only set if not None
//			def.pickup = old.nPickup switch
//			{
//				1 => "Coin",
//				2 => "Key",
//				3 => "Health",
//				4 => "Ammo",
//				_ => null  // ← becomes "None" only if needed later
//			};

//			newDefs.Add(def);
//		}

//		var settings = new JsonSerializerSettings
//		{
//			NullValueHandling = NullValueHandling.Ignore,
//			DefaultValueHandling = DefaultValueHandling.Ignore
//		};

//		string cleanJson = JsonConvert.SerializeObject(
//			new { definitions = newDefs },
//			Formatting.Indented,
//			settings
//		);

//		File.WriteAllText(path, cleanJson);
//		Debug.Log($"Converted {newDefs.Count} definitions → ULTRA CLEAN modern format. No legacy. No bloat.");
//		AssetDatabase.Refresh();
//	}

//	[System.Serializable]
//	class OldDefinition
//	{
//		public string szType;
//		public string szGeom;
//		public string szBank;
//		public bool bNorth, bSouth, bEast, bWest;
//		public bool bStart, bEnd, bConsole, bSlide, bRoll, bDock, bDoor;
//		public int nPickup;
//		public bool bPuzzleBlock;
//	}

//	[System.Serializable]
//	class OldDatabaseWrapper { public OldDefinition[] definitions; }
//}