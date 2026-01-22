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

			// Only hashid lookup — no legacy id fallback
			return Definitions.FirstOrDefault(d => string.Equals(d.hashid, idOrHash, StringComparison.Ordinal));
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
			bool ensureUniqueHash = false)
		{
			var def = new Definition
			{
				id = name ?? StringUtil.GenerateAssetId(),
				model = model,
				texture = texture
			};

			// Full-range 32-bit hash (no modulus), but still encode to fixed length 6
			int hash32 = RadixHash.GetStableHash32(def.id);
			def.hashid = HTB50.EncodeFixed(hash32, HTB50Settings.FixedLength, padChar: '0', appendFlavor: false);

			if (ensureUniqueHash)
			{
				var existing = new HashSet<string>(
					Definitions.Where(d => !string.IsNullOrEmpty(d.hashid))
							   .Select(d => d.hashid),
					StringComparer.Ordinal
				);

				int attempt = 1;
				while (existing.Contains(def.hashid))
				{
					hash32 = RadixHash.GetStableHash32(def.id + attempt);
					def.hashid = HTB50.EncodeFixed(hash32, HTB50Settings.FixedLength, padChar: '0', appendFlavor: false);
					attempt++;
					UnityEngine.Debug.LogWarning($"Hash collision retry {attempt} for '{def.id}'");
				}
			}

			return def;
		}

		private static string _defaultTileHash;
		public static string DefaultTileHash
		{
			get
			{
				if (_defaultTileHash == null)
				{
					const string legacyName = "tile_empty";

					// Full-range 32-bit hash, but keep fixed-length 6 encoding as before
					int hash32 = RadixHash.GetStableHash32(legacyName);
					_defaultTileHash = HTB50.EncodeFixed(hash32, HTB50Settings.FixedLength, padChar: '0', appendFlavor: false);
				}
				return _defaultTileHash;
			}
		}

		// ── EXISTING INSERT METHODS (unchanged) ───────────────────────────────
		public static void InsertDefinitionAfter(string afterId, Definition newDef)
		{
			if (_db?.definitions == null) return;

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

		// ── OTHER METHODS (unchanged) ────────────────────────────────────────
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
			if (string.IsNullOrEmpty(oldId) || oldId == newId) return 0;

			if (Definitions.Any(d => string.Equals(d.id, newId, StringComparison.Ordinal)))
				return -1;

			int changeCount = 0;

			var def = Definitions.FirstOrDefault(d => string.Equals(d.id, oldId, StringComparison.Ordinal));
			if (def != null)
			{
				def.id = newId;
				changeCount = 1;
			}

			return changeCount;
		}

		public static bool IsDefinitionUsed(string hashId)
		{
			if (string.IsNullOrEmpty(hashId)) return false;

			return Maps.Any(m => m?.table?.Contains(hashId) == true);
		}

		public static int DefinitionUsageCount(string hashId)
		{
			if (string.IsNullOrEmpty(hashId)) return 0;

			return Maps.Sum(m => m?.table?.Count(h => h == hashId) ?? 0);
		}

		public static class HTB50Settings
		{
			public const int Radix = 50;
			public const int FixedLength = 6;
			public const long Modulus = 15625000000L;  // 50^6
		}
	}
}