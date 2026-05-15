using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MassiveHadronLtd;
using Newtonsoft.Json;
using UnityEngine;

namespace ClassicTilestorm.Assets
{
	internal sealed class ManifestHashTable
	{
		internal readonly struct Entry
		{
			public readonly HashId HashId;
			public readonly string ResourceKey;

			public Entry(HashId hashId, string resourceKey)
			{
				HashId = hashId;
				ResourceKey = resourceKey;
			}

			public string DisplayName => string.IsNullOrWhiteSpace(ResourceKey)
				? null
				: Path.GetFileNameWithoutExtension(ResourceKey);
		}

		private readonly string manifestResourcePath;
		private readonly Dictionary<HashId, Entry> hashToEntry = new();
		private readonly Dictionary<string, HashId> displayToHash = new(StringComparer.OrdinalIgnoreCase);
		private bool loaded;

		public ManifestHashTable(string manifestResourcePath)
		{
			this.manifestResourcePath = manifestResourcePath;
		}

		public void ClearCache()
		{
			hashToEntry.Clear();
			displayToHash.Clear();
			loaded = false;
		}

		public void Refresh(bool forceRefresh = false)
		{
			if (loaded && !forceRefresh)
				return;

			Load();
		}

		public bool TryGetHashForDisplayName(string displayName, out string hashId)
		{
			hashId = null;
			if (string.IsNullOrWhiteSpace(displayName))
				return false;

			Refresh(false);
			if (!displayToHash.TryGetValue(displayName.Trim(), out var hash))
				return false;

			hashId = HTB50Settings.ToString(hash);
			return true;
		}

		public string GetHashForDisplayName(string displayName)
		{
			return TryGetHashForDisplayName(displayName, out var hashId) ? hashId : null;
		}

		public string GetDisplayName(string identifier)
		{
			if (string.IsNullOrWhiteSpace(identifier))
				return null;

			Refresh(false);
			if (TryResolveHash(identifier, out var hash) && hashToEntry.TryGetValue(hash, out var entry))
				return entry.DisplayName;

			return displayToHash.ContainsKey(identifier.Trim()) ? identifier.Trim() : null;
		}

		public string ToHashOrOriginal(string identifier)
		{
			if (string.IsNullOrWhiteSpace(identifier))
				return identifier;

			Refresh(false);
			if (TryResolveHash(identifier, out var hash))
				return HTB50Settings.ToString(hash);

			var trimmed = identifier.Trim();
			return displayToHash.TryGetValue(trimmed, out hash)
				? HTB50Settings.ToString(hash)
				: identifier;
		}

		public bool TryResolveResourceKey(string identifier, out string resourceKey)
		{
			resourceKey = null;
			if (string.IsNullOrWhiteSpace(identifier))
				return false;

			Refresh(false);

			if (TryResolveHash(identifier, out var hash) && hashToEntry.TryGetValue(hash, out var entry) && !string.IsNullOrWhiteSpace(entry.ResourceKey))
			{
				resourceKey = entry.ResourceKey;
				return true;
			}

			var trimmed = identifier.Trim();
			if (displayToHash.TryGetValue(trimmed, out hash) && hashToEntry.TryGetValue(hash, out var byDisplay) && !string.IsNullOrWhiteSpace(byDisplay.ResourceKey))
			{
				resourceKey = byDisplay.ResourceKey;
				return true;
			}

			resourceKey = trimmed;
			return false;
		}

		public void RegisterInternal(string hashId, string resourceKey)
		{
			if (string.IsNullOrWhiteSpace(hashId) || string.IsNullOrWhiteSpace(resourceKey))
				return;

			if (!TryParseHash(hashId, out var hash))
				return;

			var normalized = resourceKey.Trim();
			var entry = new Entry(hash, normalized);
			hashToEntry[hash] = entry;
			displayToHash[entry.DisplayName ?? normalized] = hash;
		}

		private void Load()
		{
			hashToEntry.Clear();
			displayToHash.Clear();

			var manifest = Resources.Load<TextAsset>(manifestResourcePath);
			if (manifest != null && !string.IsNullOrWhiteSpace(manifest.text))
			{
				foreach (var line in manifest.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
				{
					var trimmed = line.Trim();
					if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
						continue;

					var parts = trimmed.Split('\t');
					if (parts.Length < 2)
						continue;

					if (!TryParseHash(parts[0].Trim(), out var hash))
						continue;

					var resourceKey = parts[parts.Length - 1].Trim();
					if (string.IsNullOrWhiteSpace(resourceKey))
						continue;

					var entry = new Entry(hash, resourceKey);
					hashToEntry[hash] = entry;
					if (!string.IsNullOrWhiteSpace(entry.DisplayName))
						displayToHash[entry.DisplayName] = hash;
				}
			}

			loaded = true;
		}

		private static bool TryResolveHash(string identifier, out HashId hash)
		{
			hash = 0;
			if (string.IsNullOrWhiteSpace(identifier))
				return false;

			var trimmed = identifier.Trim();
			if (TryParseHash(trimmed, out hash))
				return true;

			return false;
		}

		private static bool TryParseHash(string value, out HashId hash)
		{
			hash = 0;
			if (string.IsNullOrWhiteSpace(value))
				return false;

			try
			{
				hash = HTB50.Decode(value.Trim());
				return hash != 0;
			}
			catch
			{
				return false;
			}
		}
	}

	public static class MusicResourceTable
	{
		private static readonly ManifestHashTable Table = new("AssetManifests/Music");

		public static void ClearCache() => Table.ClearCache();
		public static string GetDisplayName(string identifier) => Table.GetDisplayName(identifier);
		public static string GetHashForDisplayName(string displayName) => Table.GetHashForDisplayName(displayName);
		public static string ToHashOrOriginal(string identifier) => Table.ToHashOrOriginal(identifier);
		public static bool TryResolveResourceKey(string identifier, out string resourceKey) => Table.TryResolveResourceKey(identifier, out resourceKey);
	}

	public static class SkycubeResourceTable
	{
		private static readonly ManifestHashTable Table = new("AssetManifests/Skycubes");

		public static void ClearCache() => Table.ClearCache();
		public static string GetDisplayName(string identifier) => Table.GetDisplayName(identifier);
		public static string GetHashForDisplayName(string displayName) => Table.GetHashForDisplayName(displayName);
		public static string ToHashOrOriginal(string identifier) => Table.ToHashOrOriginal(identifier);
		public static bool TryResolveResourceKey(string identifier, out string resourceKey) => Table.TryResolveResourceKey(identifier, out resourceKey);
	}
}
