using System;
using System.Linq;
using System.Collections.Generic;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	public static class HTB50Settings
	{
		public const int FixedLength = 6;
		//public const int Radix = 50;//no longer used
		//public const long Modulus = 15625000000L;  // 50^6//no longer used
	}

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

		public static bool HasDefinition(int id) => Definitions.Any(def => def.HashID == id);

		public static Definition GetDefinition(int id) => Definitions.FirstOrDefault(d => d.HashID == id);

		public static TextureSequence GetTextureSequence(string id)
			=> string.IsNullOrEmpty(id) ? null : TextureSequences.FirstOrDefault(ts => ts.id == id);

		// ── DEFINITION CREATION WITH OPTIONAL UNIQUENESS CHECK ────────────────
		public static Definition FindOrCreateDefaultTile()
		{
			var prototype = Definition.GetDefaultTile();
			int expectedHash = prototype.HashID;

			// Only hashid matters from now on
			var match = Definitions.FirstOrDefault(d => d.HashID == expectedHash);

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
			string _name = null,
			string model = "tile_flat",
			string texture = "Default",
			bool ensureUniqueHash = false)
		{
			var def = new Definition
			{
				name = _name ?? StringUtil.GenerateAssetId(),
				model = model,
				texture = texture
			};

			// Full-range 32-bit hash (no modulus), but still encode to fixed length 6
			int hash32 = RadixHash.GetStableHash32(def.name);
			def.HashID = hash32;

			if (ensureUniqueHash)
			{
				var existing = new HashSet<int>(
					Definitions.Where(d => d.HashID != 0 ).Select(d => d.HashID));

				int attempt = 1;
				while (existing.Contains(def.HashID))
				{
					hash32 = RadixHash.GetStableHash32(def.name + attempt);
					def.HashID = hash32;
					attempt++;
					UnityEngine.Debug.LogWarning($"Hash collision retry {attempt} for '{def.name}'");
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

		public static string GenerateUniqueNewDefinitionName(string prefix = "NAME_")
		{
			//int n = 1;
			string candidate;
			var existingIds = Definitions.Select(d => d.name).ToHashSet(StringComparer.Ordinal);
			do
			{
				//candidate = $"{prefix}({n:000})";
				//n++;
				candidate = $"<{prefix}{StringUtil.GenerateAssetId()}>";
			}
			while (existingIds.Contains(candidate));

			return candidate;
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

		//// Helper to get current hash set (used above if needed)
		//private static HashSet<int> GetCurrentHashIds()
		//{
		//	return new HashSet<int>(Definitions.Where(d => 0!=d.HashID).Select(d => d.HashID));
		//}

		// ── EXISTING INSERT METHODS (unchanged) ───────────────────────────────
		public static void InsertDefinitionAfter(int afterId, Definition newDef)
		{
			if (_db?.definitions == null) return;

			var list = _db.definitions.ToList();
			int index = list.Count;

			if (0 != afterId)
			{
				int found = list.FindIndex(d => d.HashID == afterId);
				if (found >= 0) index = found + 1;
			}

			list.Insert(index, newDef);
			_db.definitions = list.ToArray();
		}

		public static void InsertDefinitionAtIndex(int index, Definition newDef)
		{
			if (_db?.definitions == null || index < 0) return;

			var list = _db.definitions.ToList();
			if (index > list.Count) index = list.Count;

			list.Insert(index, newDef);
			_db.definitions = list.ToArray();
		}

		public static void DeleteDefinitionId(int id)
		{
			if (_db?.definitions == null) return;
			var list = _db.definitions.ToList();
			list.RemoveAll(d => d.HashID == id);
			_db.definitions = list.ToArray();
		}

		public static void MoveDefinitionIdUp(int id)
		{
			if (_db?.definitions == null) return;
			var list = _db.definitions.ToList();
			int idx = list.FindIndex(d => d.HashID == id);
			if (idx <= 0) return;
			(list[idx - 1], list[idx]) = (list[idx], list[idx - 1]);
			_db.definitions = list.ToArray();
		}

		public static void MoveDefinitionIdDown(int id)
		{
			if (_db?.definitions == null) return;
			var list = _db.definitions.ToList();
			int idx = list.FindIndex(d => d.HashID == id);
			if (idx < 0 || idx >= list.Count - 1) return;
			(list[idx + 1], list[idx]) = (list[idx], list[idx + 1]);
			_db.definitions = list.ToArray();
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

		public static int GetDefinitionIdAt(int index)
		{
			return index >= 0 && index < Definitions.Count ? Definitions[index].HashID : 0;
		}

		public static bool RenameDefinitionName(int hashId, string name)
		{
			if (false == HasDefinition(hashId))
				return false;

			var def = GetDefinition(hashId);
			def.name = name;
			return true;
		}

		public static bool IsDefinitionUsed(int hashId)
		{
			if (0 == hashId) return false;

			return Maps.Any(m => m?.hashes?.Contains(hashId) == true);
		}

		public static int DefinitionUsageCount(int hashId)
		{
			if (0 == hashId) return 0;

			return Maps.Sum(m => m?.hashes?.Count(h => h == hashId) ?? 0);
		}

		public static Definition ResolveDefinition(int hashId, out bool hadError)
		{
			hadError = false;

			if (0 == hashId)
			{
				hadError = true;
				DebugUtil.LogError("Attempted to resolve null or empty tile definition hash.");
				return FindOrCreateDefaultTile();
			}

			var def = GetDefinition(hashId);
			if (def != null)
			{
				return def;
			}

			hadError = true;
			DebugUtil.LogWarning($"Missing or invalid definition hash '{hashId}' — falling back to default tile.");
			return FindOrCreateDefaultTile();
		}

		// Convenience overload — no out, callers don't have to care
		public static Definition ResolveDefinition(int hashId)
		{
			// Discard the result — caller gets fallback + log automatically
			return ResolveDefinition(hashId, out _);
		}
	}
}