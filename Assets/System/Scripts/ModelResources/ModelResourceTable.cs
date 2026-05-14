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
			public readonly string Value;
			public readonly EntryKind Kind;

			public Entry(string hashId, string value, EntryKind kind)
			{
				HashId = hashId;
				Value = value;
				Kind = kind;
			}

			public string DisplayName => string.IsNullOrWhiteSpace(Value)
				? null
				: Path.GetFileNameWithoutExtension(Value);

			public string ResourcePath => Kind == EntryKind.Resource ? Value : null;
			public string FilePath => Kind == EntryKind.File ? ResolveImportedPath(HashId, Value) : null;
		}

		private const string InternalTableResourcePath = "AssetManifests/Models";
		private const string ImportedTableFileName = "ImportedModelTable.tsv";
		private const string ImportedRootFolder = "Imported";

		private static readonly Dictionary<string, Entry> HashToEntry = new(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, string> DisplayToHash = new(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, string> PathToHash = new(StringComparer.OrdinalIgnoreCase);
		private static readonly List<Entry> CachedEntries = new();
		private static bool loaded;

		public static string ImportedTablePath => Path.Combine(Application.persistentDataPath, ImportedTableFileName);

		public static void Refresh(bool forceRefresh = false)
		{
			if (loaded && !forceRefresh && CachedEntries.Count > 0)
				return;

			LoadTables();
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

			string resolved = ResolveImportedPathFromAny(normalized);
			return !string.IsNullOrWhiteSpace(resolved) && PathToHash.TryGetValue(resolved, out hash)
				? hash
				: null;
		}

		public static string GetPathForHash(string hashId)
		{
			return TryGetEntry(hashId, out var entry)
				? (entry.Kind == EntryKind.File ? entry.FilePath : entry.ResourcePath)
				: null;
		}

		public static void RegisterInternal(string hashId, string resourceKey)
		{
			Upsert(new Entry(
				hashId: NormalizeHash(hashId),
				value: NormalizeValue(resourceKey),
				kind: EntryKind.Resource),
				persist: false);
		}

		public static void RegisterImported(string hashId, string filePath)
		{
			string fileName = NormalizeValue(Path.GetFileName(filePath));
			Upsert(new Entry(
				hashId: NormalizeHash(hashId),
				value: fileName,
				kind: EntryKind.File),
				persist: true,
				absoluteImportedPath: ResolveImportedPath(hashId, fileName, filePath));
		}

		public static void ClearRuntimeCache()
		{
			HashToEntry.Clear();
			DisplayToHash.Clear();
			PathToHash.Clear();
			CachedEntries.Clear();
			loaded = false;
		}

		private static void LoadTables()
		{
			HashToEntry.Clear();
			DisplayToHash.Clear();
			PathToHash.Clear();
			CachedEntries.Clear();

			LoadInternalResourceTable();
			LoadImportedTable();

			SaveImportedTable();
			loaded = true;
		}

		private static void LoadInternalResourceTable()
		{
			var table = Resources.Load<TextAsset>(InternalTableResourcePath);
			if (table == null || string.IsNullOrWhiteSpace(table.text))
				return;

			foreach (var entry in ParseTableLines(table.text, EntryKind.Resource))
				Upsert(entry, persist: false);
		}

		private static void LoadImportedTable()
		{
			if (!File.Exists(ImportedTablePath))
				return;

			try
			{
				foreach (var entry in ParseTableLines(File.ReadAllText(ImportedTablePath), EntryKind.File))
					Upsert(entry, persist: false);
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"ModelResourceTable: failed to load imported table '{ImportedTablePath}': {ex.Message}");
			}
		}

		private static IEnumerable<Entry> ParseTableLines(string content, EntryKind kind)
		{
			foreach (var rawLine in content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
			{
				string line = rawLine.Trim();
				if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
					continue;

				var parts = line.Split('\t');
				if (parts.Length < 2)
					continue;

				string hash = NormalizeHash(parts[0]);
				if (string.IsNullOrWhiteSpace(hash))
					continue;

				string value = parts[parts.Length - 1];

				if (kind == EntryKind.File)
					value = Path.GetFileName(value);

				yield return new Entry(hash, NormalizeValue(value), kind);
			}
		}

		private static void Upsert(Entry entry, bool persist, string absoluteImportedPath = null)
		{
			if (string.IsNullOrWhiteSpace(entry.HashId))
				return;

			HashToEntry[entry.HashId] = entry;

			if (!string.IsNullOrWhiteSpace(entry.DisplayName))
				DisplayToHash[entry.DisplayName] = entry.HashId;

			string path = entry.Kind == EntryKind.File
				? (string.IsNullOrWhiteSpace(absoluteImportedPath) ? ResolveImportedPath(entry.HashId, entry.Value) : absoluteImportedPath)
				: entry.ResourcePath;

			if (!string.IsNullOrWhiteSpace(path))
				PathToHash[NormalizePath(path)] = entry.HashId;

			int idx = CachedEntries.FindIndex(e => e.HashId.Equals(entry.HashId, StringComparison.OrdinalIgnoreCase));
			if (idx >= 0)
				CachedEntries[idx] = entry;
			else
				CachedEntries.Add(entry);

			if (persist)
				SaveImportedTable();
		}

		private static void SaveImportedTable()
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(ImportedTablePath) ?? Application.persistentDataPath);

				var lines = new List<string>
				{
					"# MassiveHadron imported model table",
					"# hashId<TAB>filename"
				};

				foreach (var entry in CachedEntries
					.Where(e => e.Kind == EntryKind.File)
					.OrderBy(e => e.HashId, StringComparer.OrdinalIgnoreCase))
				{
					lines.Add(string.Join("\t", new[]
					{
						entry.HashId ?? string.Empty,
						entry.Value ?? string.Empty
					}));
				}

				File.WriteAllLines(ImportedTablePath, lines);
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"ModelResourceTable: failed to save imported table '{ImportedTablePath}': {ex.Message}");
			}
		}

		private static string NormalizeHash(string hashId)
			=> string.IsNullOrWhiteSpace(hashId) ? null : hashId.Trim();

		private static string NormalizeValue(string value)
			=> string.IsNullOrWhiteSpace(value) ? null : value.Replace('\\', '/').Trim();

		private static string NormalizePath(string value)
			=> string.IsNullOrWhiteSpace(value) ? null : value.Replace('\\', '/').Trim();

		private static string ResolveImportedPath(string hashId, string fileName, string absolutePath = null)
		{
			if (!string.IsNullOrWhiteSpace(absolutePath) && Path.IsPathRooted(absolutePath))
				return NormalizePath(absolutePath);

			string safeHash = NormalizeHash(hashId);
			string safeFile = NormalizeValue(fileName);
			if (string.IsNullOrWhiteSpace(safeHash) || string.IsNullOrWhiteSpace(safeFile))
				return null;

			return NormalizePath(Path.Combine(Application.persistentDataPath, ImportedRootFolder, safeHash, safeFile));
		}

		private static string ResolveImportedPathFromAny(string value)
		{
			string normalized = NormalizePath(value);
			if (string.IsNullOrWhiteSpace(normalized))
				return null;

			if (Path.IsPathRooted(normalized))
				return normalized;

			return NormalizePath(Path.Combine(Application.persistentDataPath, normalized));
		}
	}
}
