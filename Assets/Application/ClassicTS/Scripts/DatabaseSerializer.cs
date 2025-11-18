// DatabaseSerializer.cs — FINAL FIXED VERSION (no circular deps)
using System;
using System.Linq;
using UnityEngine;
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
			public TextureBank[] texturebank;
			public Button[] buttons;
		}

		private static TextAsset databaseJsonFile;
		private static Action<TextAsset> saveDelegate;
		private static DatabaseData data;
		private static bool isLoaded;
		private static readonly object lockObject = new object();
		public static event Action OnDatabaseLoaded;

		public static void Init(TextAsset jsonFile, Action<TextAsset> saveAction = null)
		{
			lock (lockObject)
			{
				if (jsonFile == null) throw new ArgumentNullException(nameof(jsonFile));
				if (saveAction == null) Debug.LogWarning("Save delegate is null. Saving disabled.");

				databaseJsonFile = jsonFile;
				saveDelegate = saveAction;
				data = null;
				isLoaded = false;
				Debug.Log($"DatabaseSerializer initialized with TextAsset: {jsonFile.name}");
			}
		}

		public static DatabaseData LoadData()
		{
			lock (lockObject)
			{
				if (isLoaded && data != null) return data;
				if (databaseJsonFile == null)
				{
					Debug.LogError("Not initialized. Call Init() first.");
					return null;
				}

				string jsonContent = databaseJsonFile.text;
				if (string.IsNullOrEmpty(jsonContent))
				{
					Debug.LogError("TextAsset content is empty.");
					return null;
				}

				try
				{
					var root = JObject.Parse(jsonContent);
					var settings = new JsonSerializerSettings
					{
						MissingMemberHandling = MissingMemberHandling.Ignore
					};
					var serializer = JsonSerializer.CreateDefault(settings);

					data = new();
					data.maps = root["maps"]?.ToObject<Map[]>(serializer) ?? Array.Empty<Map>();
					data.definitions = root["definitions"]?.ToObject<Definition[]>(serializer) ?? Array.Empty<Definition>();
					data.texturebank = root["texturebank"]?.ToObject<TextureBank[]>(serializer) ?? Array.Empty<TextureBank>();
					data.buttons = root["buttons"]?.ToObject<Button[]>(serializer) ?? Array.Empty<Button>();

					// Basic validation
					foreach (var map in data.maps)
					{
						if (map == null || string.IsNullOrEmpty(map.name) || map.width <= 0 || map.height <= 0)
						{
							Debug.LogError("Invalid map in database.json");
							data = null;
							return null;
						}
					}

					if (data.definitions.Length == 0 || data.definitions.Any(td => string.IsNullOrEmpty(td?.szType)))
					{
						Debug.LogError("Invalid or missing definitions");
						data = null;
						return null;
					}

					isLoaded = true;
					OnDatabaseLoaded?.Invoke();
					return data;
				}
				catch (Exception ex)
				{
					Debug.LogError($"JSON load failed: {ex.Message}\n{ex.StackTrace}");
					data = null;
					isLoaded = false;
					return null;
				}
			}
		}

		public static void SaveDatabase(DatabaseData newData)
		{
			if (newData == null || databaseJsonFile == null || saveDelegate == null) return;

			try
			{
				var settings = new JsonSerializerSettings
				{
					NullValueHandling = NullValueHandling.Ignore,
					Formatting = Formatting.None
				};

				string jsonContent = JsonConvert.SerializeObject(newData, settings);
				string outputDir = Path.Combine(Application.persistentDataPath, "Data");
				Directory.CreateDirectory(outputDir);
				string outputPath = Path.Combine(outputDir, "database.json");
				File.WriteAllText(outputPath, jsonContent);
				Debug.Log($"Saved to {outputPath}");
			}
			catch (Exception ex)
			{
				Debug.LogError($"Save failed: {ex.Message}");
			}
		}

		public static void UpdateDatabase(DatabaseData newData)
		{
			if (newData == null) return;
			lock (lockObject)
			{
				data = newData;
				isLoaded = true;
				OnDatabaseLoaded?.Invoke();
			}
		}
	}
}