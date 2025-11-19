// DatabaseSerializer.cs — FINAL, PERFECT, MINIFIED + NULLS STRIPPED
using UnityEngine;
using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ClassicTilestorm
{
	public static class DatabaseSerializer
	{
		[Serializable]
		public class DatabaseData
		{
			public Map[] maps;
			public Definition[] definitions;
			public TextureSequence[] textures;
			public Button[] buttons;
		}

		public static DatabaseData LoadFromTextAsset(TextAsset jsonFile)
		{
			if (jsonFile == null || string.IsNullOrEmpty(jsonFile.text))
			{
				Debug.LogError("DatabaseSerializer: TextAsset is null or empty.");
				return null;
			}

			try
			{
				var root = JObject.Parse(jsonFile.text);

				var serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings
				{
					MissingMemberHandling = MissingMemberHandling.Ignore
				});

				var data = new DatabaseData
				{
					maps = root["maps"]?.ToObject<Map[]>(serializer) ?? Array.Empty<Map>(),
					definitions = root["definitions"]?.ToObject<Definition[]>(serializer) ?? Array.Empty<Definition>(),
					textures = root["textures"]?.ToObject<TextureSequence[]>(serializer) ?? Array.Empty<TextureSequence>(),
					buttons = root["buttons"]?.ToObject<Button[]>(serializer) ?? Array.Empty<Button>()
				};

				// ─────── FINAL VALIDATION: Required core content must exist ───────
				// Fail fast if any required array is null (shouldn't happen, but defensive)
				if (data.maps == null || data.definitions == null || data.textures == null)
				{
					Debug.LogError("DatabaseSerializer: Deserialization returned null for one or more required arrays (maps/definitions/textures). File is corrupted.");
					return null;
				}

				// Required: at least one map, one definition, and one texture sequence
				if (data.maps.Length == 0 || data.definitions.Length == 0 || data.textures.Length == 0)
				{
					Debug.LogError($"DatabaseSerializer: INVALID DATABASE — missing required content!\n" +
								   $"   Maps: {data.maps.Length} (requires ≥1)\n" +
								   $"   Definitions: {data.definitions.Length} (requires ≥1)\n" +
								   $"   Textures: {data.textures.Length} (requires ≥1)\n" +
								   $"   Buttons: {data.buttons?.Length ?? 0} (optional — ignored)");

					Debug.LogError("This file is corrupted, outdated, or manually edited incorrectly. Falling back to pristine internal database.");
					return null;
				}

				// Success!
				Debug.Log($"DatabaseSerializer: Valid database loaded — {data.maps.Length} maps, {data.definitions.Length} definitions, {data.textures.Length} texture sets, {data.buttons?.Length ?? 0} buttons");
				return data;
			}
			catch (JsonReaderException jex)
			{
				Debug.LogError($"DatabaseSerializer: JSON syntax error → {jex.Message} (Line {jex.LineNumber}, Col {jex.LinePosition})");
				return null;
			}
			catch (Exception ex)
			{
				Debug.LogError($"DatabaseSerializer: Unexpected error during load → {ex}");
				return null;
			}
		}

		public static void SaveToDisk(DatabaseData data, string overridePath = null)
		{
			if (data == null) return;

			try
			{
				var settings = new JsonSerializerSettings
				{
					NullValueHandling = NullValueHandling.Ignore,  // CRITICAL: strips nulls
					Formatting = Formatting.None                    // CRITICAL: minified output
				};

				string json = JsonConvert.SerializeObject(data, settings);

				string path = overridePath ?? Path.Combine(Application.persistentDataPath, "Data", "database.json");
				Directory.CreateDirectory(Path.GetDirectoryName(path)!);
				File.WriteAllText(path, json);

				Debug.Log($"Database saved (minified, nulls stripped) to {path} ({json.Length} bytes)");
			}
			catch (Exception ex)
			{
				Debug.LogError($"Failed to save database: {ex.Message}");
			}
		}
	}
}