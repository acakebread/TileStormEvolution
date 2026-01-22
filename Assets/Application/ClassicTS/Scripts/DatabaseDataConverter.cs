using UnityEngine;
using MassiveHadronLtd;
using MassiveHadronLtd.IDs.HTB50;
using System.Collections.Generic;
using System;
using System.Linq;

namespace ClassicTilestorm
{
    public static class DatabaseDataConverter
    {
		//remove when databases converted
		public static void LegacyDataConverter(DatabaseData data)
		{
			// PHASE 1: One-time fixup — assign missing hashids deterministically from id
			bool defsChanged = false;
			// PHASE 1: assign missing hashids using full-range 32-bit
			foreach (var def in data.definitions.Where(d => d != null && string.IsNullOrEmpty(d.hashid)))
			{
				if (!string.IsNullOrEmpty(def.id))
				{
					int hash32 = RadixHash.GetStableHash32(def.id);
					def.hashid = HTB50.EncodeFixed(hash32, ResourceManager.HTB50Settings.FixedLength, padChar: '0', appendFlavor: false);
				}
				else
				{
					// Very rare fallback
					string fallbackInput = Guid.NewGuid().ToString();
					int hash32 = RadixHash.GetStableHash32(fallbackInput);
					def.hashid = HTB50.EncodeFixed(hash32, ResourceManager.HTB50Settings.FixedLength, padChar: '0', appendFlavor: false);
					Debug.LogWarning($"Generated fallback hashid for definition with no id");
				}
				defsChanged = true;
			}
			if (defsChanged)
			{
				Debug.Log($"Assigned deterministic hashids to {data.definitions.Count(d => !string.IsNullOrEmpty(d.hashid))} definitions");
			}

			// PHASE 2: Convert legacy name-only tables → hashid-based tables (only if needed)
			int mapsMigrated = 0;
			int entriesConverted = 0;

			// Pre-build fast name → hash lookup
			var nameToHash = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (var def in data.definitions.Where(d => d != null))
			{
				if (!string.IsNullOrEmpty(def.id) && !string.IsNullOrEmpty(def.hashid))
				{
					nameToHash[def.id] = def.hashid;
				}
			}

			foreach (var map in data.maps.Where(m => m != null && m.table != null))
			{
				bool mapChanged = false;
				var newTable = new List<string>(map.table.Length);

				for (int i = 0; i < map.table.Length; i++)
				{
					string current = map.table[i]?.Trim();

					if (string.IsNullOrEmpty(current))
					{
						newTable.Add("");
						continue;
					}

					// Skip if it already looks like a hash (6 chars, alphanumeric)
					if (current.Length == ResourceManager.HTB50Settings.FixedLength &&
						current.All(c => char.IsLetterOrDigit(c)))
					{
						newTable.Add(current);
						continue;
					}

					// Try to resolve as name
					var def = ResourceManager.GetDefinition(current);
					string hashToUse = def?.hashid;

					if (string.IsNullOrEmpty(hashToUse))
					{
						// Fallback: known name in lookup
						if (nameToHash.TryGetValue(current, out string knownHash))
						{
							hashToUse = knownHash;
						}
						else
						{
							// Generate deterministic hash Full-range 32-bit stable hash (no modulus)
							int hash32 = RadixHash.GetStableHash32(current);

							// Keep fixed length 6 with padding, exactly as before
							hashToUse = HTB50.EncodeFixed(hash32, ResourceManager.HTB50Settings.FixedLength, padChar: '0', appendFlavor: false);

							Debug.LogWarning($"Generated hash '{hashToUse}' for unmapped tile '{current}' in map '{map.name}'");
						}
					}

					if (current != hashToUse)
					{
						newTable.Add(hashToUse);
						mapChanged = true;
						entriesConverted++;
					}
					else
					{
						newTable.Add(current);
					}
				}

				if (mapChanged)
				{
					map.table = newTable.ToArray();
					mapsMigrated++;
					Debug.Log($"Migrated table to hashids in map '{map.name}' ({entriesConverted} entries)");
				}
			}

			if (mapsMigrated > 0 || entriesConverted > 0)
			{
				Debug.Log($"Load-time migration: {mapsMigrated} maps, {entriesConverted} entries converted to hashids");
			}
		}
	}
}