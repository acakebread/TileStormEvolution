using System;
using System.Linq;
using System.Collections.Generic;
using MassiveHadronLtd;
using UnityEngine;

namespace ClassicTilestorm
{
	public static class HTB50Settings
	{
		public const int FixedLength = 6;
		public static string ToString(int value, int length = -1, bool appendFlavor = false, char padChar = '0') => HTB50.EncodeFixed(value, length != -1 ? length : FixedLength, appendFlavor, padChar);
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

		public static Definition GetDefinition(HashId id) => Definitions.FirstOrDefault(d => d.HashID == id);

		public static TextureSequence GetTextureSequence(string id)
			=> string.IsNullOrEmpty(id) ? null : TextureSequences.FirstOrDefault(ts => ts.id == id);

		// ── DEFINITION CREATION WITH OPTIONAL UNIQUENESS CHECK ────────────────

		public static HashId DefaultHash => FindOrCreateDefaultDefinition().HashID;

		public static Definition FindOrCreateDefaultDefinition()
		{
			var prototype = Definition.GetDefault();
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

			// Full-range 32-bit hash (no modulus)
			HashId hash32 = RadixHash.GetStableHash32(def.name);
			def.HashID = hash32;

			if (ensureUniqueHash)
			{
				var existing = new HashSet<HashId>(
					Definitions.Where(d => d.HashID != 0 ).Select(d => d.HashID));

				int attempt = 1;
				while (existing.Contains(def.HashID))
				{
					hash32 = RadixHash.GetStableHash32(def.name + attempt);
					def.HashID = hash32;
					attempt++;
					Debug.LogWarning($"Hash collision retry {attempt} for '{def.name}'");
				}
			}

			return def;
		}

		public static string GenerateUniqueNewDefinitionName(string prefix = "NAME_")
		{
			string candidate;
			var existingIds = Definitions.Select(d => d.name).ToHashSet(StringComparer.Ordinal);
			do candidate = $"<{prefix}{StringUtil.GenerateAssetId()}>";
			while (existingIds.Contains(candidate));
			return candidate;
		}

		public static void ApplyMapChanges(Map modifiedMap)
		{
			if (null == modifiedMap) return;
			if (null != _db?.maps)
			{
				for (int i = 0; i < _db.maps.Length; i++)
					if (_db.maps[i].name == modifiedMap.name)
					{ _db.maps[i] = modifiedMap; return; }
			}
		}

		// ── EXISTING INSERT METHODS (unchanged) ───────────────────────────────
		public static void InsertDefinitionAfter(HashId afterId, Definition newDef)
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
			OnDefininionsModified?.Invoke();
		}

		public static void InsertDefinitionAtIndex(int index, Definition newDef)
		{
			if (_db?.definitions == null || index < 0) return;

			var list = _db.definitions.ToList();
			if (index > list.Count) index = list.Count;

			list.Insert(index, newDef);
			_db.definitions = list.ToArray();
			OnDefininionsModified?.Invoke();
		}

		public static void DeleteDefinitionId(HashId id)
		{
			if (_db?.definitions == null) return;
			var list = _db.definitions.ToList();
			list.RemoveAll(d => d.HashID == id);
			_db.definitions = list.ToArray();
			OnDefininionsModified?.Invoke();
		}

		public static void MoveDefinitionIdUp(HashId id)
		{
			if (_db?.definitions == null) return;
			var list = _db.definitions.ToList();
			int idx = list.FindIndex(d => d.HashID == id);
			if (idx <= 0) return;
			(list[idx - 1], list[idx]) = (list[idx], list[idx - 1]);
			_db.definitions = list.ToArray();
			OnDefininionsModified?.Invoke();
		}

		public static void MoveDefinitionIdDown(HashId id)
		{
			if (_db?.definitions == null) return;
			var list = _db.definitions.ToList();
			int idx = list.FindIndex(d => d.HashID == id);
			if (idx < 0 || idx >= list.Count - 1) return;
			(list[idx + 1], list[idx]) = (list[idx], list[idx + 1]);
			_db.definitions = list.ToArray();
			OnDefininionsModified?.Invoke();
		}

		public static void DeleteDefinitionAt(int index)
		{
			if (_db?.definitions == null || index < 0 || index >= _db.definitions.Length) return;
			var list = _db.definitions.ToList();
			list.RemoveAt(index);
			_db.definitions = list.ToArray();
			OnDefininionsModified?.Invoke();
		}

		public static void MoveDefinitionUp(int index)
		{
			if (_db?.definitions == null || index <= 0 || index >= _db.definitions.Length) return;
			var list = _db.definitions.ToList();
			(list[index - 1], list[index]) = (list[index], list[index - 1]);
			_db.definitions = list.ToArray();
			OnDefininionsModified?.Invoke();
		}

		public static void MoveDefinitionDown(int index)
		{
			if (_db?.definitions == null || index < 0 || index >= _db.definitions.Length - 1) return;
			var list = _db.definitions.ToList();
			(list[index + 1], list[index]) = (list[index], list[index + 1]);
			_db.definitions = list.ToArray();
			OnDefininionsModified?.Invoke();
		}

		public static bool RenameMapName(int index, string value)
		{
			if (index >= 0 && _db?.maps.Length > index)
			{
				_db.maps[index].name = value;
				return true;
			}
			return false;
		}

		public static bool RenameMapName(Map map, string value)
		{
			var index = Array.IndexOf(_db?.maps, map);
			return RenameMapName(index, value);
		}

		public static bool RenameDefinitionName(HashId hashId, string name)
		{
			if (false == HasDefinition(hashId))
				return false;

			var def = GetDefinition(hashId);
			def.name = name;
			return true;
		}

		public static bool IsDefinitionUsed(HashId hashId)
		{
			if (hashId == 0) return false;

			// Any map uses it at least once?
			return Maps.Any(m => m != null && m.IsDefinitionUsed(hashId));
		}

		public static int DefinitionUsageCount(HashId hashId)
		{
			if (hashId == 0) return 0;

			// Total usage across ALL maps
			return Maps.Sum(m => m?.DefinitionUsageCount(hashId) ?? 0);
		}

		public static Definition ResolveDefinition(HashId hashId, out bool hadError)
		{
			hadError = false;

			if (0 == hashId)
			{
				hadError = true;
				Debug.LogError("Attempted to resolve null or empty tile definition hash.");
				return FindOrCreateDefaultDefinition();
			}

			var def = GetDefinition(hashId);
			if (def != null)
			{
				return def;
			}

			hadError = true;
			Debug.LogWarning($"Missing or invalid definition hash '{hashId}' — falling back to default tile.");
			return FindOrCreateDefaultDefinition();
		}

		public static Action OnDefininionsModified;
	}
}
