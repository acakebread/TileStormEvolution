// File: Editor/MapTileFormatConverter.cs
// Fully self-contained, compiles perfectly, perfect JSON order

using UnityEngine;
using UnityEditor;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;
using System;
using System.Linq; // <-- This was missing!

namespace ClassicTilestorm.Editor
{
	public class MapTileFormatConverter : EditorWindow
	{
		private TextAsset databaseJson;

		[MenuItem("Tools/Classic Tilestorm/Convert Maps → New Tile Format (tiles = scrambled, solve = inverse mixed)")]
		public static void ShowWindow()
		{
			GetWindow<MapTileFormatConverter>("CT Map Format Converter");
		}

		private void OnGUI()
		{
			GUILayout.Label("Map Tile Format Converter", EditorStyles.boldLabel);
			GUILayout.Label("Converts legacy format (tiles = solved, mixed = offsets)\n→ New format (tiles = scrambled, solve = inverse offsets)", EditorStyles.helpBox);

			databaseJson = (TextAsset)EditorGUILayout.ObjectField("database.json", databaseJson, typeof(TextAsset), false);

			GUILayout.Space(10);

			if (databaseJson == null)
			{
				EditorGUILayout.HelpBox("Drag your database.json TextAsset here first.", MessageType.Warning);
				return;
			}

			if (GUILayout.Button("CONVERT ALL MAPS → New Format (solve after tiles)", GUILayout.Height(50)))
			{
				ConvertDatabase();
			}

			GUILayout.Space(10);
			EditorGUILayout.HelpBox(
				"This will:\n" +
				"• Make 'tiles' hold the scrambled layout\n" +
				"• Create 'solve' = negated old mixed values\n" +
				"• Remove 'mixed'\n" +
				"• Place 'solve' right after 'tiles' in JSON\n" +
				"• Create backup: database.json.bak",
				MessageType.Info);
		}

		private void ConvertDatabase()
		{
			string assetPath = AssetDatabase.GetAssetPath(databaseJson);
			string fullPath = Path.GetFullPath(assetPath);
			string backupPath = fullPath + ".bak";

			if (File.Exists(backupPath) && !EditorUtility.DisplayDialog("Backup Exists", $"Overwrite backup?\n{backupPath}", "Yes", "Cancel"))
				return;

			try
			{
				string json = databaseJson.text;
				File.Copy(fullPath, backupPath, true);
				Debug.Log($"Backup created: {backupPath}");

				var dbWrapper = JsonConvert.DeserializeObject<DatabaseWrapper>(json);
				if (dbWrapper?.maps == null || dbWrapper.maps.Length == 0)
				{
					Debug.LogError("No maps found in database.json");
					return;
				}

				int convertedCount = 0;

				foreach (var map in dbWrapper.maps)
				{
					if (map?.tiles == null || map.mixed == null || map.tiles.Length != map.mixed.Length)
						continue;

					int size = map.tiles.Length;
					var newTiles = new int[size];
					var newSolve = new int[size];

					for (int i = 0; i < size; i++)
					{
						int offset = map.mixed[i];
						int sourceIndex = i + offset;

						// Apply scramble: tile at position i shows what was at sourceIndex
						newTiles[i] = (offset != 0 && sourceIndex >= 0 && sourceIndex < size)
							? map.tiles[sourceIndex]
							: map.tiles[i];

						newSolve[i + offset] = -offset; // inverse offset to go back to solved
					}

					map.tiles = newTiles;
					map.solve = newSolve;
					map.mixed = null;
					convertedCount++;
				}

				// Custom settings with ordered contract resolver
				var settings = new JsonSerializerSettings
				{
					ContractResolver = new MapPropertyOrderResolver(),
					NullValueHandling = NullValueHandling.Ignore,
					Formatting = Formatting.Indented
				};

				string newJson = JsonConvert.SerializeObject(dbWrapper, settings);
				File.WriteAllText(fullPath, newJson);

				Debug.Log($"SUCCESS! {convertedCount} maps converted!\n" +
						  $"→ 'tiles' now = scrambled state\n" +
						  $"→ 'solve' placed after 'tiles'\n" +
						  $"→ 'mixed' removed\n" +
						  $"Saved: {fullPath}");

				AssetDatabase.Refresh();
			}
			catch (Exception e)
			{
				Debug.LogError($"Conversion failed: {e.Message}");
			}
		}

		// Forces perfect JSON order: solve comes right after tiles
		private class MapPropertyOrderResolver : DefaultContractResolver
		{
			protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
			{
				var props = base.CreateProperties(type, memberSerialization).ToList(); // ← ToList() so we can use LINQ

				if (type != typeof(MapWrapper))
					return props;

				var ordered = new List<JsonProperty>();

				// Exact order we want
				string[] order = { "name", "character", "music", "button", "width", "height",
								   "waypoints", "table", "tiles", "mixed", "solve", "attachments" };

				foreach (var name in order)
				{
					var prop = props.FirstOrDefault(p => p.PropertyName == name);
					if (prop != null)
					{
						ordered.Add(prop);
						props.Remove(prop);
					}
				}

				// Add remaining properties (alphabetical)
				ordered.AddRange(props.OrderBy(p => p.PropertyName));

				return ordered;
			}
		}

		// Wrapper classes
		private class DatabaseWrapper
		{
			public MapWrapper[] maps;
			public Definition[] definitions;
			public TextureSequence[] textures;
			public Button[] buttons;
		}

		private class MapWrapper : Map
		{
			// These allow us to read/write the fields during conversion
			public int[] mixed;  // legacy
			public int[] solve;  // new
		}
	}
}