using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MassiveHadronLtd;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ClassicTilestorm.Assets;
using UnityEngine;

namespace ClassicTilestorm
{
	public static class DefinitionCatalog
	{
		public enum DefinitionStorageLocation
		{
			Internal = 0,
			External = 1
		}

		public readonly struct DefinitionEntry
		{
			public HashId HashId { get; }
			public Definition Definition { get; }
			public DefinitionStorageLocation StorageLocation { get; }
			public string FilePath { get; }

			public DefinitionEntry(HashId hashId, Definition definition, DefinitionStorageLocation storageLocation, string filePath = null)
			{
				HashId = hashId;
				Definition = definition;
				StorageLocation = storageLocation;
				FilePath = string.IsNullOrWhiteSpace(filePath) ? null : filePath;
			}

			public bool IsInternal => StorageLocation == DefinitionStorageLocation.Internal;
			public bool IsExternal => StorageLocation == DefinitionStorageLocation.External;

			public string DisplayName => Definition != null && !string.IsNullOrWhiteSpace(Definition.name)
				? Definition.name
				: !string.IsNullOrWhiteSpace(FilePath)
					? Path.GetFileNameWithoutExtension(FilePath)
					: null;
		}

		private static readonly JsonSerializerSettings DefinitionSerializerSettings = new()
		{
			Converters = { new DefinitionConverter() },
			NullValueHandling = NullValueHandling.Ignore
		};

		private static List<DefinitionEntry> InternalDefinitions;
		private static List<DefinitionEntry> ExternalDefinitions;
		private static Dictionary<HashId, DefinitionEntry> InternalDefinitionIndex;
		private static Dictionary<HashId, DefinitionEntry> ExternalDefinitionIndex;
		private static bool InternalDefinitionsLoaded;
		private static bool ExternalDefinitionsLoaded;

		public static string PersistentDefinitionsFolder => ApplicationSettings.SystemDefinitionsFolder;
		public static string InternalDefinitionsFile => Path.Combine(ApplicationSettings.InternalResourcesProjectPath, "AssetDatabase", "definitions.json");

		public static void ClearCache()
		{
			InternalDefinitions = null;
			ExternalDefinitions = null;
			InternalDefinitionIndex = null;
			ExternalDefinitionIndex = null;
			InternalDefinitionsLoaded = false;
			ExternalDefinitionsLoaded = false;
		}

		public static DefinitionStorageLocation GetStorageLocation(HashId hash)
		{
			if (hash == 0)
				return DefinitionStorageLocation.Internal;

			EnsureInternalDefinitions();
			if (InternalDefinitionIndex != null && InternalDefinitionIndex.ContainsKey(hash))
				return DefinitionStorageLocation.Internal;

			EnsureExternalDefinitions();
			if (ExternalDefinitionIndex != null && ExternalDefinitionIndex.ContainsKey(hash))
				return DefinitionStorageLocation.External;

			return DefinitionStorageLocation.External;
		}

		public static bool IsInternalDefinition(HashId hash)
		{
			return GetStorageLocation(hash) == DefinitionStorageLocation.Internal;
		}

		public static IReadOnlyList<DefinitionEntry> GetInternalDefinitions(bool forceRefresh = false)
		{
			EnsureInternalDefinitions(forceRefresh);
			return InternalDefinitions != null ? (IReadOnlyList<DefinitionEntry>)InternalDefinitions : Array.Empty<DefinitionEntry>();
		}

		public static IReadOnlyList<DefinitionEntry> GetExternalDefinitions(bool forceRefresh = false)
		{
			EnsureExternalDefinitions(forceRefresh);
			return ExternalDefinitions != null ? (IReadOnlyList<DefinitionEntry>)ExternalDefinitions : Array.Empty<DefinitionEntry>();
		}

		public static IReadOnlyList<DefinitionEntry> GetAvailableDefinitions(bool forceRefresh = false)
		{
			var entries = new List<DefinitionEntry>();
			var seenHashes = new HashSet<HashId>();

			foreach (var entry in GetInternalDefinitions(forceRefresh))
			{
				if (entry.Definition == null || seenHashes.Contains(entry.HashId))
					continue;

				entries.Add(entry);
				seenHashes.Add(entry.HashId);
			}

			foreach (var entry in GetExternalDefinitions(forceRefresh))
			{
				if (entry.Definition == null || seenHashes.Contains(entry.HashId))
					continue;

				entries.Add(entry);
				seenHashes.Add(entry.HashId);
			}

			return entries;
		}

		public static bool TryGetDefinition(HashId hash, out Definition definition)
		{
			definition = LoadDefinition(hash);
			return definition != null;
		}

		public static Definition LoadDefinition(HashId hash)
		{
			if (hash == 0)
			{
				EnsureInternalDefinitions();
				var defaultEntry = InternalDefinitions?.FirstOrDefault(entry => entry.HashId == 0 && entry.Definition != null);
				return defaultEntry.HasValue ? defaultEntry.Value.Definition : Definition.Default;
			}

			EnsureInternalDefinitions();
			if (InternalDefinitionIndex != null && InternalDefinitionIndex.TryGetValue(hash, out var internalEntry))
				return internalEntry.Definition;

			EnsureExternalDefinitions();
			if (ExternalDefinitionIndex != null && ExternalDefinitionIndex.TryGetValue(hash, out var externalEntry))
				return externalEntry.Definition;

			return null;
		}

		public static bool SaveInternalDefinitions(IEnumerable<Definition> definitions)
		{
			try
			{
				var list = definitions?.Where(def => def != null).ToArray() ?? Array.Empty<Definition>();
				Directory.CreateDirectory(Path.GetDirectoryName(InternalDefinitionsFile));
				var json = JsonConvert.SerializeObject(list, Formatting.Indented, DefinitionSerializerSettings);
				WriteJsonIfChanged(InternalDefinitionsFile, json);
				InternalDefinitionsLoaded = false;
				return true;
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"DefinitionCatalog: failed to save internal definitions: {ex.Message}");
				return false;
			}
		}

		public static bool SaveExternalDefinition(Definition definition)
		{
			if (definition == null)
				return false;

			try
			{
				Directory.CreateDirectory(PersistentDefinitionsFolder);

				definition.EnsureHashID();
				var hash = definition.HashID;
				if (hash == 0)
					return false;

				var fileName = BuildFileName(definition);
				var path = Path.Combine(PersistentDefinitionsFolder, fileName);
				foreach (var existing in Directory.EnumerateFiles(PersistentDefinitionsFolder, "*.json", SearchOption.TopDirectoryOnly).ToArray())
				{
					if (!TryGetDefinitionHashFromFileName(existing, out var existingHash) || existingHash != hash)
						continue;

					if (!string.Equals(existing, path, StringComparison.OrdinalIgnoreCase) &&
						File.Exists(existing))
					{
						File.Delete(existing);
					}
				}

				var json = JsonConvert.SerializeObject(definition, Formatting.Indented, DefinitionSerializerSettings);
				WriteJsonIfChanged(path, json);
				ExternalDefinitionsLoaded = false;
				WebGLPersistentStorage.Flush();
				return true;
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"DefinitionCatalog: failed to save external definition '{definition?.name}': {ex.Message}");
				return false;
			}
		}

		public static bool DeleteExternalDefinition(HashId hash)
		{
			if (hash == 0)
				return false;

			var file = EnumerateExternalDefinitionFiles()
				.FirstOrDefault(f => TryGetDefinitionHashFromFileName(f, out var fileHash) && fileHash == hash);

			if (file == null)
				return false;

			File.Delete(file);
			ExternalDefinitionsLoaded = false;
			WebGLPersistentStorage.Flush();
			return true;
		}

		public static int CleanupExternalDefinitionsCollidingWithInternal()
		{
			EnsureInternalDefinitions();

			var internalHashes = GetInternalDefinitions(forceRefresh: true)
				.Where(entry => entry.Definition != null)
				.Select(entry => entry.HashId)
				.ToHashSet();

			if (internalHashes.Count == 0)
				return 0;

			var removed = 0;
			foreach (var file in EnumerateExternalDefinitionFiles().ToArray())
			{
				if (!TryGetDefinitionHashFromFileName(file, out var hash) || !internalHashes.Contains(hash))
					continue;

				try
				{
					File.Delete(file);
					removed++;
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"DefinitionCatalog: failed to delete colliding external definition '{file}': {ex.Message}");
				}
			}

			if (removed > 0)
				ExternalDefinitionsLoaded = false;

			return removed;
		}

		public static bool TryGetDefinitionHashFromFileName(string filePath, out HashId hash)
		{
			hash = 0;

			if (string.IsNullOrWhiteSpace(filePath))
				return false;

			var stem = Path.GetFileNameWithoutExtension(filePath);
			if (string.IsNullOrWhiteSpace(stem))
				return false;

			var separatorIndex = stem.LastIndexOf(ResourceFileNameBuilder.FileHashSeparator, StringComparison.Ordinal);
			var hashText = separatorIndex >= 0 && separatorIndex < stem.Length - ResourceFileNameBuilder.FileHashSeparator.Length
				? stem.Substring(separatorIndex + ResourceFileNameBuilder.FileHashSeparator.Length)
				: stem;
			return ResourceIdUtil.TryParseCanonicalHash(hashText, out hash);
		}

		public static string BuildFileName(Definition definition)
		{
			definition?.EnsureHashID();
			return ResourceFileNameBuilder.BuildJsonFileName(definition?.HashID ?? 0);
		}

		private static void EnsureInternalDefinitions(bool forceRefresh = false)
		{
			if (InternalDefinitionsLoaded && !forceRefresh)
				return;

			InternalDefinitionsLoaded = true;
			InternalDefinitions = new List<DefinitionEntry>();
			InternalDefinitionIndex = new Dictionary<HashId, DefinitionEntry>();

			var asset = LoadDefinitionsAsset();
			if (asset == null || string.IsNullOrWhiteSpace(asset.text))
				return;

			foreach (var entry in ParseDefinitionEntries(asset.text, DefinitionStorageLocation.Internal, null))
			{
				if (entry.Definition == null)
					continue;

				InternalDefinitions.Add(entry);
				if (!InternalDefinitionIndex.ContainsKey(entry.HashId))
					InternalDefinitionIndex[entry.HashId] = entry;
			}
		}

		private static void EnsureExternalDefinitions(bool forceRefresh = false)
		{
			if (ExternalDefinitionsLoaded && !forceRefresh)
				return;

			ExternalDefinitionsLoaded = true;
			ExternalDefinitions = new List<DefinitionEntry>();
			ExternalDefinitionIndex = new Dictionary<HashId, DefinitionEntry>();

			var internalHashes = GetInternalDefinitions(forceRefresh: false)
				.Where(entry => entry.Definition != null)
				.Select(entry => entry.HashId)
				.ToHashSet();

			foreach (var file in EnumerateExternalDefinitionFiles())
			{
				if (!TryGetDefinitionHashFromFileName(file, out var hash))
					continue;

				if (hash == 0 || internalHashes.Contains(hash) || ExternalDefinitionIndex.ContainsKey(hash))
					continue;

				var definition = LoadDefinitionFromFile(file);
				if (definition == null)
					continue;

				var entry = new DefinitionEntry(hash, definition, DefinitionStorageLocation.External, file);
				ExternalDefinitions.Add(entry);
				ExternalDefinitionIndex[hash] = entry;
			}
		}

		private static IEnumerable<string> EnumerateExternalDefinitionFiles()
		{
			if (!Directory.Exists(PersistentDefinitionsFolder))
				yield break;

			foreach (var file in Directory.EnumerateFiles(PersistentDefinitionsFolder, "*.json", SearchOption.TopDirectoryOnly))
				yield return file;
		}

		private static IEnumerable<DefinitionEntry> ParseDefinitionEntries(string json, DefinitionStorageLocation storageLocation, string filePath)
		{
			if (string.IsNullOrWhiteSpace(json))
				yield break;

			JToken parsed;
			try
			{
				parsed = JToken.Parse(json);
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"DefinitionCatalog: failed to parse definitions json: {ex.Message}");
				yield break;
			}

			var serializer = JsonSerializer.Create(DefinitionSerializerSettings);

			if (parsed is JArray definitionsArray)
			{
				foreach (var token in definitionsArray)
				{
					if (token == null || token.Type != JTokenType.Object)
						continue;

					var definition = token.ToObject<Definition>(serializer);
					if (definition == null)
						continue;

					yield return new DefinitionEntry(definition.HashID, definition, storageLocation, filePath);
				}
				yield break;
			}

			if (parsed is JObject definitionsRoot && definitionsRoot["definitions"] is JArray nestedDefinitions)
			{
				foreach (var token in nestedDefinitions)
				{
					if (token == null || token.Type != JTokenType.Object)
						continue;

					var definition = token.ToObject<Definition>(serializer);
					if (definition == null)
						continue;

					yield return new DefinitionEntry(definition.HashID, definition, storageLocation, filePath);
				}
			}
		}

		private static Definition LoadDefinitionFromFile(string filePath)
		{
			try
			{
				var json = File.ReadAllText(filePath);
				if (string.IsNullOrWhiteSpace(json))
					return null;

				var parsed = JToken.Parse(json);
				var serializer = JsonSerializer.Create(DefinitionSerializerSettings);

				if (parsed is JObject root && root["definitions"] is JArray nested)
					return nested.First?.ToObject<Definition>(serializer);

				return parsed.ToObject<Definition>(serializer);
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"DefinitionCatalog: failed to load definition '{filePath}': {ex.Message}");
				return null;
			}
		}

		private static TextAsset LoadDefinitionsAsset()
		{
			var asset = Resources.Load<TextAsset>("AssetDatabase/definitions");
			if (asset != null)
				return asset;

			return Resources.Load<TextAsset>("AssetDatabase/definitions.json");
		}

		private static void WriteJsonIfChanged(string path, string json)
		{
			if (string.IsNullOrWhiteSpace(path))
				throw new ArgumentException("Path must not be empty", nameof(path));

			Directory.CreateDirectory(Path.GetDirectoryName(path));

			if (File.Exists(path))
			{
				var existing = File.ReadAllText(path);
				if (string.Equals(existing, json, StringComparison.Ordinal))
					return;
			}

			File.WriteAllText(path, json);
		}
	}
}
