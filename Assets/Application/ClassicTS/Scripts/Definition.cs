using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using MassiveHadronLtd;
using MassiveHadronLtd.IDs.HTB50;

namespace ClassicTilestorm
{
	[Serializable]
	public class Definition
	{
		public string hashid;   // new: stable hash-based ID (future primary id)
		public string id;       // current: human-friendly / legacy name
		public string model;
		public string texture;
		public string material;
		public string flags;         // comma/space separated, e.g. "Drag, Roll, Dock"
		public string connections;   // e.g. "NSEW" (uppercase, no separators)

		//[JsonIgnore] public string id { get => hashid; }//future replacement for hashid obviously this currently conflicts with the existing use of 'id'
		[JsonIgnore] public string name { get => id; }//future replacement for id - just the display name in the editor

		// ── CONNECTIONS (settable) ────────────────────────────────────────────
		[JsonIgnore] public bool bNorth { get => HasConnection('N'); set => SetConnection('N', value); }
		[JsonIgnore] public bool bSouth { get => HasConnection('S'); set => SetConnection('S', value); }
		[JsonIgnore] public bool bEast { get => HasConnection('E'); set => SetConnection('E', value); }
		[JsonIgnore] public bool bWest { get => HasConnection('W'); set => SetConnection('W', value); }

		// ── FLAGS (settable) ──────────────────────────────────────────────────
		[JsonIgnore] public bool bDrag { get => HasFlag("Drag"); set => SetFlag("Drag", value); }
		[JsonIgnore] public bool bRoll { get => HasFlag("Roll"); set => SetFlag("Roll", value); }
		[JsonIgnore] public bool bDock { get => HasFlag("Dock"); set => SetFlag("Dock", value); }
		[JsonIgnore] public bool bDoor { get => HasFlag("Door"); set => SetFlag("Door", value); }
		[JsonIgnore] public bool bStart { get => HasFlag("Start"); set => SetFlag("Start", value); }
		[JsonIgnore] public bool bEnd { get => HasFlag("End"); set => SetFlag("End", value); }
		[JsonIgnore] public bool bConsole { get => HasFlag("Console"); set => SetFlag("Console", value); }
		[JsonIgnore] public bool bPuzzleBlock { get => HasFlag("PuzzleBlock"); set => SetFlag("PuzzleBlock", value); }
		[JsonIgnore] public bool bSway { get => HasFlag("Sway"); set => SetFlag("Sway", value); }
		[JsonIgnore] public bool bWash { get => HasFlag("Wash"); set => SetFlag("Wash", value); }

		// ── CONDITIONAL SERIALIZATION ─────────────────────────────────────────
		public bool ShouldSerializehashid() => !string.IsNullOrEmpty(hashid);
		public bool ShouldSerializeid() => !string.IsNullOrEmpty(id);
		public bool ShouldSerializemodel() => !string.IsNullOrEmpty(model);
		public bool ShouldSerializetexture() => !string.IsNullOrEmpty(texture);
		public bool ShouldSerializematerial() => !string.IsNullOrEmpty(material);
		public bool ShouldSerializeflags() => !string.IsNullOrEmpty(flags);
		public bool ShouldSerializeconnections() => !string.IsNullOrEmpty(connections);

		// ── INTERNAL HELPERS ──────────────────────────────────────────────────
		private HashSet<string> _flagCache;
		private HashSet<char> _connCache;

		public bool HasFlag(string flag)
		{
			if (_flagCache == null) RebuildFlagCache();
			return _flagCache.Contains(flag, StringComparer.OrdinalIgnoreCase);
		}

		public bool HasConnection(char dir)
		{
			if (_connCache == null) RebuildConnectionCache();
			return _connCache.Contains(char.ToUpperInvariant(dir));
		}

		private void RebuildFlagCache()
		{
			_flagCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			if (!string.IsNullOrEmpty(flags))
			{
				foreach (var f in flags.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
				{
					var trimmed = f.Trim();
					if (!string.IsNullOrEmpty(trimmed))
						_flagCache.Add(trimmed);
				}
			}
		}

		private void RebuildConnectionCache()
		{
			_connCache = new HashSet<char>();
			if (!string.IsNullOrEmpty(connections))
			{
				foreach (char c in connections)
				{
					if (char.IsLetter(c))
						_connCache.Add(char.ToUpperInvariant(c));
				}
			}
		}

		private void SetFlag(string flag, bool enabled)
		{
			if (_flagCache == null) RebuildFlagCache();

			bool currentlyHas = _flagCache.Contains(flag, StringComparer.OrdinalIgnoreCase);
			if (enabled == currentlyHas) return;

			var flagList = string.IsNullOrEmpty(flags)
				? new List<string>()
				: flags.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
					   .Select(s => s.Trim())
					   .Where(f => !string.Equals(f, flag, StringComparison.OrdinalIgnoreCase))
					   .ToList();

			if (enabled) flagList.Add(flag);
			flags = string.Join(", ", flagList);
			RebuildFlagCache();
		}

		private void SetConnection(char dirChar, bool enabled)
		{
			char dir = char.ToUpperInvariant(dirChar);
			if (_connCache == null) RebuildConnectionCache();

			bool currentlyHas = _connCache.Contains(dir);
			if (enabled == currentlyHas) return;

			var connList = new List<char>();
			if (!string.IsNullOrEmpty(connections))
			{
				foreach (char c in connections)
				{
					if (char.IsLetter(c) && char.ToUpperInvariant(c) != dir)
						connList.Add(char.ToUpperInvariant(c));
				}
			}

			if (enabled) connList.Add(dir);
			connList.Sort((a, b) => GetDirectionOrder(a).CompareTo(GetDirectionOrder(b)));

			connections = new string(connList.ToArray());
			RebuildConnectionCache();
		}

		private static int GetDirectionOrder(char dir)
		{
			return dir switch
			{
				'N' => 0,
				'E' => 1,
				'S' => 2,
				'W' => 3,
				_ => 999
			};
		}

		public static Definition Default => GetDefaultTile();   // cached if you want, but not necessary

		// Hashid-only check — cleanest long-term
		public bool IsDefault() => string.Equals( hashid, GetDefaultTile().hashid, StringComparison.Ordinal);

		// ── FACTORY ────────────────────────────────────────────────────────────
		public static Definition GetDefaultTile()
		{
			// This is the ONLY place where the legacy name is written as a literal
			const string legacyNameForHash = "tile_empty";

			long hash64 = RadixHash.HashToRange64(legacyNameForHash, HTB50Settings.Modulus);
			string stable = HTB50.EncodeFixed(hash64, HTB50Settings.FixedLength, appendFlavor: false);

			return new Definition
			{
				id = legacyNameForHash,     // kept for compatibility / serialization / inspector
				hashid = stable,

				model = null,
				texture = null,
				material = null,
				flags = null,
				connections = null
				// future empty-state fields go here
			};
		}

		// ── STABLE ID ACCESS ──────────────────────────────────────────────────
		public string GetStableId()
		{
			if (!string.IsNullOrEmpty(hashid))
				return hashid;

			if (!string.IsNullOrEmpty(id))
			{
				long hash64 = RadixHash.HashToRange64(id, HTB50Settings.Modulus);
				return HTB50.EncodeFixed(hash64, HTB50Settings.FixedLength, appendFlavor: false);
			}

			Debug.LogWarning("Definition has no id or hashid — generating random stable ID.");
			long random64 = RadixHash.GenerateRandomInRange64(HTB50Settings.Modulus);
			return HTB50.EncodeFixed(random64, HTB50Settings.FixedLength, appendFlavor: false, padChar: '0');
		}

		// ── SAFE UNIQUE HASH GENERATOR (used by ResourceManager) ──────────────
		public static string GenerateUniqueStableId(string input, HashSet<string> existingIds)
		{
			long hash64 = RadixHash.HashToRange64(input, HTB50Settings.Modulus);
			string candidate = HTB50.EncodeFixed(hash64, HTB50Settings.FixedLength, appendFlavor: false);

			if (!existingIds.Contains(candidate))
				return candidate;

			Debug.LogWarning($"Rare collision on input '{input}' — retrying with salt");
			return GenerateUniqueStableId(input + "_s", existingIds);
		}

		public static class HTB50Settings
		{
			public const int Radix = 50;
			public const int FixedLength = 6;
			public const long Modulus = 15625000000L;  // 50^6
		}
	}

	public static class DefinitionExtensions
	{
		public static bool HasConnection(this Definition def, char dir)
			=> def?.HasConnection(dir) ?? false;
	}
}