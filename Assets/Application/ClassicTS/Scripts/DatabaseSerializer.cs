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
			public TextureBank[] textureBanks;
			public Button[] buttons;
		}

		public static DatabaseData LoadFromTextAsset(TextAsset jsonFile)
		{
			if (jsonFile == null) return null;

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
					textureBanks = root["textureBanks"]?.ToObject<TextureBank[]>(serializer) ?? Array.Empty<TextureBank>(),
					buttons = root["buttons"]?.ToObject<Button[]>(serializer) ?? Array.Empty<Button>()
				};

				return data;
			}
			catch (Exception ex)
			{
				Debug.LogError($"DatabaseSerializer: Failed to load - {ex.Message}");
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