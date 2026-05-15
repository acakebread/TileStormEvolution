using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MassiveHadronLtd;
using UnityEngine;

namespace ClassicTilestorm.Assets
{
	public static class ResourceIdUtil
	{
		public static bool TryParseCanonicalHash(string value, out HashId hash)
		{
			hash = 0;
			if (string.IsNullOrWhiteSpace(value))
				return false;

			var trimmed = value.Trim();
			if (trimmed.Length != HTB50Settings.FixedLength)
				return false;

			try
			{
				hash = HTB50.Decode(trimmed);
			}
			catch
			{
				hash = 0;
				return false;
			}

			return string.Equals(HTB50Settings.ToString(hash), trimmed, StringComparison.Ordinal);
		}
	}

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

			var trimmed = identifier.Trim();
			if (displayToHash.ContainsKey(trimmed))
				return trimmed;

			if (TryResolveHash(trimmed, out var hash) && hashToEntry.TryGetValue(hash, out var entry))
				return entry.DisplayName;

			return null;
		}

		public string ToHashOrOriginal(string identifier)
		{
			if (string.IsNullOrWhiteSpace(identifier))
				return identifier;

			Refresh(false);

			var trimmed = identifier.Trim();
			if (displayToHash.TryGetValue(trimmed, out var hash))
				return HTB50Settings.ToString(hash);

			return TryResolveHash(trimmed, out hash)
				? HTB50Settings.ToString(hash)
				: identifier;
		}

		public bool TryResolveResourceKey(string identifier, out string resourceKey)
		{
			resourceKey = null;
			if (string.IsNullOrWhiteSpace(identifier))
				return false;

			Refresh(false);

			var trimmed = identifier.Trim();
			if (displayToHash.TryGetValue(trimmed, out var hash) &&
				hashToEntry.TryGetValue(hash, out var byDisplay) &&
				!string.IsNullOrWhiteSpace(byDisplay.ResourceKey))
			{
				resourceKey = byDisplay.ResourceKey;
				return true;
			}

			if (TryResolveHash(trimmed, out hash) &&
				hashToEntry.TryGetValue(hash, out var entry) &&
				!string.IsNullOrWhiteSpace(entry.ResourceKey))
			{
				resourceKey = entry.ResourceKey;
				return true;
			}

			resourceKey = trimmed;
			return false;
		}

		public void RegisterInternal(string hashId, string resourceKey)
		{
			if (string.IsNullOrWhiteSpace(hashId) || string.IsNullOrWhiteSpace(resourceKey))
				return;

			if (!ResourceIdUtil.TryParseCanonicalHash(hashId, out var hash))
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

					if (!ResourceIdUtil.TryParseCanonicalHash(parts[0].Trim(), out var hash))
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

			return ResourceIdUtil.TryParseCanonicalHash(identifier.Trim(), out hash);
		}
	}

	internal sealed class AliasHashTable
	{
		private readonly Dictionary<HashId, string> hashToDisplay = new();
		private readonly Dictionary<string, HashId> displayToHash = new(StringComparer.OrdinalIgnoreCase);

		public AliasHashTable(IEnumerable<string> canonicalNames, IDictionary<string, string> aliases = null)
		{
			if (canonicalNames != null)
			{
				foreach (var name in canonicalNames)
				{
					if (string.IsNullOrWhiteSpace(name))
						continue;

					RegisterCanonical(name.Trim());
				}
			}

			if (aliases != null)
			{
				foreach (var pair in aliases)
				{
					if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
						continue;

					if (!displayToHash.TryGetValue(pair.Value.Trim(), out var hash))
						continue;

					displayToHash[pair.Key.Trim()] = hash;
				}
			}
		}

		public void ClearCache()
		{
			// Fixed table; nothing to clear.
		}

		public string GetDisplayName(string identifier)
		{
			if (string.IsNullOrWhiteSpace(identifier))
				return null;

			var trimmed = identifier.Trim();
			if (displayToHash.TryGetValue(trimmed, out var hash) && hashToDisplay.TryGetValue(hash, out var display))
				return display;

			if (ResourceIdUtil.TryParseCanonicalHash(trimmed, out hash) && hashToDisplay.TryGetValue(hash, out var entry))
				return entry;

			return null;
		}

		public string GetHashForDisplayName(string displayName)
		{
			if (string.IsNullOrWhiteSpace(displayName))
				return null;

			var trimmed = displayName.Trim();
			return displayToHash.TryGetValue(trimmed, out var hash)
				? HTB50Settings.ToString(hash)
				: null;
		}

		public string ToHashOrOriginal(string identifier)
		{
			if (string.IsNullOrWhiteSpace(identifier))
				return identifier;

			var trimmed = identifier.Trim();
			if (displayToHash.TryGetValue(trimmed, out var hash))
				return HTB50Settings.ToString(hash);

			return ResourceIdUtil.TryParseCanonicalHash(trimmed, out hash)
				? HTB50Settings.ToString(hash)
				: identifier;
		}

		public bool TryResolveResourceKey(string identifier, out string resourceKey)
		{
			resourceKey = null;
			if (string.IsNullOrWhiteSpace(identifier))
				return false;

			var trimmed = identifier.Trim();
			if (displayToHash.TryGetValue(trimmed, out var hash) && hashToDisplay.TryGetValue(hash, out var display))
			{
				resourceKey = display;
				return true;
			}

			if (ResourceIdUtil.TryParseCanonicalHash(trimmed, out hash) && hashToDisplay.TryGetValue(hash, out var entry))
			{
				resourceKey = entry;
				return true;
			}

			resourceKey = trimmed;
			return false;
		}

		public IReadOnlyList<string> GetDisplayNames()
		{
			return hashToDisplay.Values
				.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
				.ToArray();
		}

		private void RegisterCanonical(string displayName)
		{
			if (string.IsNullOrWhiteSpace(displayName))
				return;

			if (displayToHash.ContainsKey(displayName))
				return;

			var hash = (HashId)RadixHash.GetStableHash32(displayName);
			displayToHash[displayName] = hash;
			hashToDisplay[hash] = displayName;
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

	public static class MaterialResourceTable
	{
		private static readonly ManifestHashTable Table = new("AssetManifests/Materials");

		public static void ClearCache() => Table.ClearCache();
		public static string GetDisplayName(string identifier) => Table.GetDisplayName(identifier);
		public static string GetHashForDisplayName(string displayName) => Table.GetHashForDisplayName(displayName);
		public static string ToHashOrOriginal(string identifier) => Table.ToHashOrOriginal(identifier);
		public static bool TryResolveResourceKey(string identifier, out string resourceKey) => Table.TryResolveResourceKey(identifier, out resourceKey);
	}

	public static class SoundResourceTable
	{
		private static readonly ManifestHashTable Table = new("AssetManifests/Sounds");

		public static void ClearCache() => Table.ClearCache();
		public static string GetDisplayName(string identifier) => Table.GetDisplayName(identifier);
		public static string GetHashForDisplayName(string displayName) => Table.GetHashForDisplayName(displayName);
		public static string ToHashOrOriginal(string identifier) => Table.ToHashOrOriginal(identifier);
		public static bool TryResolveResourceKey(string identifier, out string resourceKey) => Table.TryResolveResourceKey(identifier, out resourceKey);
	}

	public static class CharacterResourceTable
	{
		private static readonly AliasHashTable Table = new(new[]
		{
			"Eggbot Default",
			"Eggbot Industrial",
			"Eggbot Egypt",
			"Eggbot Medieval",
			"Eggbot Jungle",
		});

		public static void ClearCache() => Table.ClearCache();
		public static string GetDisplayName(string identifier) => Table.GetDisplayName(identifier);
		public static string GetHashForDisplayName(string displayName) => Table.GetHashForDisplayName(displayName);
		public static string ToHashOrOriginal(string identifier) => Table.ToHashOrOriginal(identifier);
		public static bool TryResolveResourceKey(string identifier, out string resourceKey) => Table.TryResolveResourceKey(identifier, out resourceKey);
		public static IReadOnlyList<string> GetDisplayNames() => Table.GetDisplayNames();
	}

	public static class EffectResourceTable
	{
		private static readonly AliasHashTable Table = new(
			new[]
			{
				"Debug",
				"Mirror",
				"Film",
				"Frost",
				"Water",
			},
			new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["PerfectMirror"] = "Mirror",
				["SurfaceFilm"] = "Film",
				["FrostEffect"] = "Frost",
				["WaterEffect"] = "Water",
			});

		public static void ClearCache() => Table.ClearCache();
		public static string GetDisplayName(string identifier) => Table.GetDisplayName(identifier);
		public static string GetHashForDisplayName(string displayName) => Table.GetHashForDisplayName(displayName);
		public static string ToHashOrOriginal(string identifier) => Table.ToHashOrOriginal(identifier);
		public static bool TryResolveResourceKey(string identifier, out string resourceKey) => Table.TryResolveResourceKey(identifier, out resourceKey);
		public static IReadOnlyList<string> GetDisplayNames() => Table.GetDisplayNames();
	}
}
