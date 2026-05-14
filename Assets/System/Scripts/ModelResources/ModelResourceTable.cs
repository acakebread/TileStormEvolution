using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MassiveHadronLtd
{
	public static class ModelResourceTable
	{
		public enum EntryKind
		{
			Resource = 0,
			File = 1
		}

		public readonly struct Entry
		{
			public readonly string HashId;
			public readonly string ResourcePath;
			public readonly string FilePath;
			public readonly string DisplayName;
			public readonly EntryKind Kind;

			public Entry(string hashId, string resourcePath, string filePath, string displayName, EntryKind kind)
			{
				HashId = hashId;
				ResourcePath = resourcePath;
				FilePath = filePath;
				DisplayName = displayName;
				Kind = kind;
			}

			public bool IsImported => Kind == EntryKind.File;
		}

		private const string TableFileName = "ModelResourceTable.tsv";
		private const string ImportedRootFolder = "Imported";
		private static readonly Dictionary<string, Entry> HashToEntry = new(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, string> DisplayToHash = new(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, string> PathToHash = new(StringComparer.OrdinalIgnoreCase);
		private static readonly List<Entry> CachedEntries = new();
		private static bool loaded;

		public static Func<IEnumerable<string>> InternalModelNamesProvider { get; set; }

		public static string TablePath => Path.Combine(Application.persistentDataPath, TableFileName);

		public static void Refresh(bool forceRefresh = false)
		{
			if (loaded && !forceRefresh && CachedEntries.Count > 0)
				return;

			LoadOrBootstrap();
		}

		public static IReadOnlyList<Entry> GetEntries(bool forceRefresh = false)
		{
			Refresh(forceRefresh);
			return CachedEntries;
		}

		public static bool TryGetEntry(string hashId, out Entry entry)
		{
			Refresh(false);
			if (!string.IsNullOrWhiteSpace(hashId) && HashToEntry.TryGetValue(hashId.Trim(), out entry))
				return true;

			entry = default;
			return false;
		}

		public static string GetDisplayName(string hashId)
		{
			if (string.IsNullOrWhiteSpace(hashId))
				return null;

			Refresh(false);
			return HashToEntry.TryGetValue(hashId.Trim(), out var entry) ? entry.DisplayName : null;
		}

		public static string GetHashForDisplayName(string displayName)
		{
			if (string.IsNullOrWhiteSpace(displayName))
				return null;

			Refresh(false);
			return DisplayToHash.TryGetValue(displayName.Trim(), out var hash) ? hash : null;
		}

		public static string GetHashForPath(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
				return null;

			Refresh(false);
			string normalized = NormalizePath(path);
			if (PathToHash.TryGetValue(normalized, out var hash))
				return hash;

			string resolved = ResolveImportedPath(normalized);
			return !string.IsNullOrWhiteSpace(resolved) && PathToHash.TryGetValue(resolved, out hash)
				? hash
				: null;
		}

		public static string GetPathForHash(string hashId)
		{
			return TryGetEntry(hashId, out var entry)
				? (!string.IsNullOrWhiteSpace(entry.FilePath) ? entry.FilePath : entry.ResourcePath)
				: null;
		}

		public static void RegisterInternal(string hashId, string resourcePath, string displayName = null)
		{
			Upsert(new Entry(
				hashId: NormalizeHash(hashId),
				resourcePath: NormalizePath(resourcePath),
				filePath: null,
				displayName: NormalizeDisplay(displayName ?? Path.GetFileNameWithoutExtension(resourcePath)),
				kind: EntryKind.Resource),
				persist: false);
		}

		public static void RegisterImported(string hashId, string filePath, string displayName = null)
		{
			Upsert(new Entry(
				hashId: NormalizeHash(hashId),
				resourcePath: null,
				filePath: ResolveImportedPath(filePath),
				displayName: NormalizeDisplay(displayName ?? Path.GetFileNameWithoutExtension(filePath)),
				kind: EntryKind.File),
				persist: true);
		}

		public static void ClearRuntimeCache()
		{
			HashToEntry.Clear();
			DisplayToHash.Clear();
			PathToHash.Clear();
			CachedEntries.Clear();
			loaded = false;
		}

		private static void LoadOrBootstrap()
		{
			HashToEntry.Clear();
			DisplayToHash.Clear();
			PathToHash.Clear();
			CachedEntries.Clear();

			LoadFromDisk();
			BootstrapInternalModels();
			SaveToDisk();

			loaded = true;
		}

		private static void LoadFromDisk()
		{
			if (!File.Exists(TablePath))
				return;

			try
			{
				foreach (var rawLine in File.ReadAllLines(TablePath))
				{
					if (string.IsNullOrWhiteSpace(rawLine))
						continue;

					string line = rawLine.Trim();
					if (line.StartsWith("#"))
						continue;

					var parts = line.Split('\t');
					if (parts.Length < 3)
						continue;

					if (!Enum.TryParse(parts[1], true, out EntryKind kind))
						kind = EntryKind.Resource;

					var entry = new Entry(
						hashId: NormalizeHash(parts[0]),
						resourcePath: kind == EntryKind.Resource ? NormalizePath(parts[2]) : null,
						filePath: kind == EntryKind.File ? ResolveImportedPath(parts[2]) : null,
						displayName: parts.Length > 3 ? NormalizeDisplay(parts[3]) : null,
						kind: kind);

					Upsert(entry, persist: false);
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"ModelResourceTable: failed to load table '{TablePath}': {ex.Message}");
			}
		}

		private static void BootstrapInternalModels()
		{
			var internalEntries = EnumerateCurrentInternalModels();
			foreach (var entry in internalEntries)
			{
				if (!HashToEntry.ContainsKey(entry.HashId))
					Upsert(entry, persist: false);
			}
		}

		private static IEnumerable<Entry> EnumerateCurrentInternalModels()
		{
			IEnumerable<string> names = InternalModelNamesProvider?.Invoke();

			if (names == null)
			{
				var roots = AssetRegistry<GameObject>.GetRegisteredModelRoots()
					.Where(r => !string.IsNullOrWhiteSpace(r))
					.Select(r => r.Trim('/').Trim())
					.Where(r => !string.IsNullOrWhiteSpace(r))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToArray();

				if (roots.Length == 0)
					yield break;

				names = ResourceUtils.GetAssetNamesFromResources<GameObject>(roots, string.Empty);
			}

			names = names
				.Where(n => !string.IsNullOrWhiteSpace(n))
				.Select(n => n.Trim())
				.Distinct(StringComparer.OrdinalIgnoreCase);

			foreach (var name in names)
			{
				string hash = HTB50.EncodeFixed(RadixHash.GetStableHash32(name), 6);
				yield return new Entry(
					hashId: hash,
					resourcePath: name,
					filePath: null,
					displayName: name,
					kind: EntryKind.Resource);
			}
		}

		private static void Upsert(Entry entry, bool persist)
		{
			if (string.IsNullOrWhiteSpace(entry.HashId))
				return;

			HashToEntry[entry.HashId] = entry;

			if (!string.IsNullOrWhiteSpace(entry.DisplayName))
				DisplayToHash[entry.DisplayName] = entry.HashId;

			string path = entry.Kind == EntryKind.File ? entry.FilePath : entry.ResourcePath;
			if (!string.IsNullOrWhiteSpace(path))
				PathToHash[NormalizeLookupPath(path)] = entry.HashId;

			int idx = CachedEntries.FindIndex(e => e.HashId.Equals(entry.HashId, StringComparison.OrdinalIgnoreCase));
			if (idx >= 0)
				CachedEntries[idx] = entry;
			else
				CachedEntries.Add(entry);

			if (persist)
				SaveToDisk();
		}

		private static void SaveToDisk()
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(TablePath) ?? Application.persistentDataPath);

				var lines = new List<string>
				{
					"# MassiveHadron model resource table",
					"# hashId<TAB>kind<TAB>path<TAB>displayName"
				};

				foreach (var entry in CachedEntries.OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase))
				{
					string path = entry.Kind == EntryKind.File
						? ToPersistedImportedPath(entry.FilePath)
						: entry.ResourcePath;
					lines.Add(string.Join("\t", new[]
					{
						entry.HashId ?? string.Empty,
						entry.Kind.ToString(),
						path ?? string.Empty,
						entry.DisplayName ?? string.Empty
					}));
				}

				File.WriteAllLines(TablePath, lines);
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"ModelResourceTable: failed to save table '{TablePath}': {ex.Message}");
			}
		}

		private static string NormalizeHash(string hashId)
			=> string.IsNullOrWhiteSpace(hashId) ? null : hashId.Trim();

		private static string NormalizeDisplay(string value)
			=> string.IsNullOrWhiteSpace(value) ? null : value.Trim();

		private static string NormalizePath(string value)
			=> string.IsNullOrWhiteSpace(value) ? null : value.Replace('\\', '/').Trim();

		private static string ResolveImportedPath(string value)
		{
			string normalized = NormalizePath(value);
			if (string.IsNullOrWhiteSpace(normalized))
				return null;

			if (Path.IsPathRooted(normalized))
				return normalized;

			string importedRelative = normalized;
			if (importedRelative.StartsWith(ImportedRootFolder + "/", StringComparison.OrdinalIgnoreCase))
				importedRelative = importedRelative.Substring(ImportedRootFolder.Length + 1);

			return NormalizePath(Path.Combine(Application.persistentDataPath, ImportedRootFolder, importedRelative));
		}

		private static string ToPersistedImportedPath(string value)
		{
			string normalized = NormalizePath(value);
			if (string.IsNullOrWhiteSpace(normalized))
				return null;

			string importedRoot = NormalizePath(Path.Combine(Application.persistentDataPath, ImportedRootFolder));
			if (normalized.StartsWith(importedRoot + "/", StringComparison.OrdinalIgnoreCase))
				return normalized.Substring(importedRoot.Length + 1);

			if (normalized.StartsWith(ImportedRootFolder + "/", StringComparison.OrdinalIgnoreCase))
				return normalized.Substring(ImportedRootFolder.Length + 1);

			return normalized;
		}

		private static string NormalizeLookupPath(string value)
		{
			string normalized = NormalizePath(value);
			if (string.IsNullOrWhiteSpace(normalized))
				return null;

			if (Path.IsPathRooted(normalized))
				return normalized;

			string importedRelative = normalized;
			if (importedRelative.StartsWith(ImportedRootFolder + "/", StringComparison.OrdinalIgnoreCase))
				importedRelative = importedRelative.Substring(ImportedRootFolder.Length + 1);

			return NormalizePath(Path.Combine(Application.persistentDataPath, ImportedRootFolder, importedRelative));
		}
	}
}
