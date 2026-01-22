using System;
using System.Linq;
using System.Collections.Generic;
using MassiveHadronLtd;
using MassiveHadronLtd.IDs.HTB50;

namespace ClassicTilestorm
{
	[Serializable]
	public class DatabaseData
	{
		public Map[] maps;
		public Definition[] definitions;
		public TextureSequence[] textures;
		public Legacy.Button[] buttons;
	}

	public static class ResourceManager
	{
		private static DatabaseData _db;
		public static DatabaseData database { get => _db; set => _db = value; }

		public static IList<Map> Maps => _db?.maps ?? Array.Empty<Map>();
		public static IList<Definition> Definitions => _db?.definitions ?? Array.Empty<Definition>();
		public static IList<TextureSequence> TextureSequences => _db?.textures ?? Array.Empty<TextureSequence>();
		public static IList<Legacy.Button> Buttons => _db?.buttons ?? Array.Empty<Legacy.Button>();

		public static Definition GetDefinition(string idOrHash)
		{
			if (string.IsNullOrEmpty(idOrHash)) return null;

			// Prefer stable hashid
			var byHash = Definitions.FirstOrDefault(d => string.Equals(d.hashid, idOrHash, StringComparison.Ordinal));
			if (byHash != null) return byHash;

			// Fallback to legacy id
			return Definitions.FirstOrDefault(d => string.Equals(d.id, idOrHash, StringComparison.Ordinal));
		}

		public static TextureSequence GetTextureSequence(string id)
			=> string.IsNullOrEmpty(id) ? null : TextureSequences.FirstOrDefault(ts => ts.id == id);

		// ── DEFINITION CREATION WITH OPTIONAL UNIQUENESS CHECK ────────────────
		public static Definition FindOrCreateDefaultTile()
		{
			var prototype = Definition.GetDefaultTile();
			string expectedHash = prototype.hashid;

			// Only hashid matters from now on
			var match = Definitions.FirstOrDefault(d =>
				string.Equals(d.hashid, expectedHash, StringComparison.Ordinal));

			if (match != null)
			{
				return match;
			}

			// Canonical default tile is missing → insert it
			var list = (_db?.definitions ?? Array.Empty<Definition>()).ToList();
			list.Insert(0, prototype);           // position 0 = conventional for "nothing"
			_db.definitions = list.ToArray();

			return prototype;
		}

		public static Definition CreateDefinition(
			string name = null,
			string model = "tile_flat",
			string texture = "Default",
			bool ensureUniqueHash = false)  // ← default false = fast & safe enough
		{
			var def = new Definition
			{
				id = name ?? StringUtil.GenerateAssetId(),
				model = model,
				texture = texture
			};

			long random64 = RadixHash.GenerateRandomInRange64(Definition.HTB50Settings.Modulus);
			var hashid = HTB50.EncodeFixed(random64, Definition.HTB50Settings.FixedLength, appendFlavor: false, padChar: '0');

			if (ensureUniqueHash)
			{
				var existing = new HashSet<string>(
					Definitions.Where(d => !string.IsNullOrEmpty(d.hashid))
								.Select(d => d.hashid),
					StringComparer.Ordinal
				);

				def.hashid = Definition.GenerateUniqueStableId(hashid, existing);
			}
			else
			{
				// Fast deterministic path (recommended default)
				long hashValue = RadixHash.HashToRange64(hashid, Definition.HTB50Settings.Modulus);
				def.hashid = HTB50.EncodeFixed(hashValue, Definition.HTB50Settings.FixedLength, appendFlavor: false);
			}

			return def;
		}

		// ── EXISTING INSERT METHODS (updated to use factory if desired) ────────
		public static void InsertDefinitionAfter(string afterId, Definition newDef)
		{
			if (_db?.definitions == null) return;

			// Optional: you can force uniqueness here too
			// newDef.hashid = Definition.GenerateUniqueStableId(newDef.id, GetCurrentHashIds());

			var list = _db.definitions.ToList();
			int index = list.Count;

			if (!string.IsNullOrEmpty(afterId))
			{
				int found = list.FindIndex(d => d.id == afterId);
				if (found >= 0) index = found + 1;
			}

			list.Insert(index, newDef);
			_db.definitions = list.ToArray();
		}

		public static void InsertDefinitionAt(int index, Definition newDef)
		{
			if (_db?.definitions == null || index < 0) return;

			var list = _db.definitions.ToList();
			if (index > list.Count) index = list.Count;

			list.Insert(index, newDef);
			_db.definitions = list.ToArray();
		}

		// Helper to get current hash set (used above if needed)
		private static HashSet<string> GetCurrentHashIds()
		{
			return new HashSet<string>(
				Definitions.Where(d => !string.IsNullOrEmpty(d.hashid))
						   .Select(d => d.hashid),
				StringComparer.Ordinal
			);
		}

		// ── OTHER METHODS (unchanged or lightly cleaned) ──────────────────────
		public static void DeleteDefinition(string id)
		{
			if (_db?.definitions == null) return;
			var list = _db.definitions.ToList();
			list.RemoveAll(d => d.id == id);
			_db.definitions = list.ToArray();
		}

		public static void MoveDefinitionUp(string id)
		{
			if (_db?.definitions == null) return;
			var list = _db.definitions.ToList();
			int idx = list.FindIndex(d => d.id == id);
			if (idx <= 0) return;
			(list[idx - 1], list[idx]) = (list[idx], list[idx - 1]);
			_db.definitions = list.ToArray();
		}

		public static void MoveDefinitionDown(string id)
		{
			if (_db?.definitions == null) return;
			var list = _db.definitions.ToList();
			int idx = list.FindIndex(d => d.id == id);
			if (idx < 0 || idx >= list.Count - 1) return;
			(list[idx + 1], list[idx]) = (list[idx], list[idx + 1]);
			_db.definitions = list.ToArray();
		}

		public static string GenerateUniqueNewDefinitionId(string prefix = "new_def_id")
		{
			int n = 1;
			string candidate;
			var existingIds = Definitions.Select(d => d.id).ToHashSet(StringComparer.Ordinal);

			do
			{
				candidate = $"{prefix}({n:000})";
				n++;
			}
			while (existingIds.Contains(candidate));

			return candidate;
		}

		public static Definition GetDefinitionByStableId(string stableId)
		{
			if (string.IsNullOrEmpty(stableId)) return null;
			var match = database.definitions.FirstOrDefault(d => d.hashid == stableId);
			if (match != null) return match;
			return database.definitions.FirstOrDefault(d => d.GetStableId() == stableId);
		}

		public static int CountDefinitionsNeedingHashMigration()
		{
			return database?.definitions?.Count(d => string.IsNullOrEmpty(d.hashid) && !string.IsNullOrEmpty(d.id)) ?? 0;
		}

		public static void ApplyMapChanges(Map modifiedMap)
		{
			if (modifiedMap == null) return;
			if (_db?.maps != null) ReplaceInArray(_db.maps, modifiedMap);

			static void ReplaceInArray(Map[] array, Map updated)
			{
				for (int i = 0; i < array.Length; i++)
					if (array[i].name == updated.name)
					{ array[i] = updated; return; }
			}
		}

		public static void DeleteDefinitionAt(int index)
		{
			if (_db?.definitions == null || index < 0 || index >= _db.definitions.Length) return;

			var list = _db.definitions.ToList();
			list.RemoveAt(index);
			_db.definitions = list.ToArray();
		}

		public static void MoveDefinitionUp(int index)
		{
			if (_db?.definitions == null || index <= 0 || index >= _db.definitions.Length) return;

			var list = _db.definitions.ToList();
			(list[index - 1], list[index]) = (list[index], list[index - 1]);
			_db.definitions = list.ToArray();
		}

		public static void MoveDefinitionDown(int index)
		{
			if (_db?.definitions == null || index < 0 || index >= _db.definitions.Length - 1) return;

			var list = _db.definitions.ToList();
			(list[index + 1], list[index]) = (list[index], list[index + 1]);
			_db.definitions = list.ToArray();
		}

		public static string GetDefinitionIdAt(int index)
		{
			return index >= 0 && index < Definitions.Count ? Definitions[index].id : null;
		}

		public static int RenameDefinitionId(string oldId, string newId)
		{
			if (string.IsNullOrEmpty(oldId) || oldId == newId)
				return 0;

			if (Definitions.Any(d => string.Equals(d.id, newId, StringComparison.Ordinal)))
				return -1;

			int changeCount = 0;

			foreach (var map in Maps)
			{
				if (map?.table == null) continue;

				bool changed = false;
				for (int i = 0; i < map.table.Length; i++)
				{
					if (string.Equals(map.table[i], oldId, StringComparison.Ordinal))
					{
						map.table[i] = newId;
						changed = true;
						changeCount++;
					}
				}

				if (changed)
				{
					// table already updated
				}
			}

			var def = Definitions.FirstOrDefault(d => string.Equals(d.id, oldId, StringComparison.Ordinal));
			if (def != null)
			{
				def.id = newId;
			}

			return changeCount;
		}

		// IsDefinitionUsed — fixed (use table instead of StableId)
		public static bool IsDefinitionUsed(string stableId)
		{
			if (string.IsNullOrEmpty(stableId))
				return false;

			foreach (var map in Maps)
			{
				if (map == null || map.table == null) continue;

				if (map.table.Any(name =>
				{
					var def = GetDefinition(name);
					return def != null && string.Equals(def.hashid, stableId, StringComparison.OrdinalIgnoreCase);
				}))
				{
					return true;
				}
			}

			return false;
		}

		public static int DefinitionUsageCount(string stableId)
			=> string.IsNullOrEmpty(stableId) ? 0 : Maps.Count(m => m?.table?.Contains(stableId) == true);
	}
}