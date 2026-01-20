using System;
using System.Linq;

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

		public static System.Collections.Generic.IList<Map> Maps => _db?.maps ?? Array.Empty<Map>();
		public static System.Collections.Generic.IList<Definition> Definitions => _db?.definitions ?? Array.Empty<Definition>();
		public static System.Collections.Generic.IList<TextureSequence> TextureSequences => _db?.textures ?? Array.Empty<TextureSequence>();
		public static System.Collections.Generic.IList<Legacy.Button> Buttons => _db?.buttons ?? Array.Empty<Legacy.Button>();

		public static Definition GetDefinition(string id)
					=> string.IsNullOrEmpty(id) ? null : Definitions.FirstOrDefault(d => d.id == id);

		public static TextureSequence GetTextureSequence(string id)
			=> string.IsNullOrEmpty(id) ? null : TextureSequences.FirstOrDefault(ts => ts.id == id);

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

		public static bool IsDefinitionUsed(string defId)
		{
			if (string.IsNullOrEmpty(defId))
				return false;

			foreach (var map in Maps)
			{
				if (map?.table == null)
					continue;

				if (Array.IndexOf(map.table, defId) >= 0)
					return true;
			}

			return false;
		}

		public static int DefinitionUsageCount(string defId) =>
			string.IsNullOrEmpty(defId) ? 0 : Maps.Count(m => m?.table?.Contains(defId) == true);

		public static string GenerateUniqueNewDefinitionId(string prefix = "new_def_id")
		{
			int n = 1;
			string candidate;

			var existingIds = Definitions
				.Select(d => d.id)
				.ToHashSet(StringComparer.Ordinal);

			do
			{
				candidate = $"{prefix}({n:000})";
				n++;
			}
			while (existingIds.Contains(candidate));

			return candidate;
		}

		// ── NEW: Index-based methods (for DefinitionEditorPanel) ──────────────

		public static void InsertDefinitionAt(int index, Definition newDef)
		{
			if (_db?.definitions == null || index < 0) return;

			var list = _db.definitions.ToList();
			if (index > list.Count) index = list.Count;

			list.Insert(index, newDef);
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

		public static string GetDefinitionIdAt(int index)
		{
			return index >= 0 && index < Definitions.Count ? Definitions[index].id : null;
		}

		/// <summary>
		/// Renames a definition ID across the entire database.
		/// Returns number of map cells changed, or -1 if newId already exists.
		/// </summary>
		public static int RenameDefinitionId(string oldId, string newId)
		{
			if (string.IsNullOrEmpty(oldId) || oldId == newId)
				return 0;

			// Prevent duplicate IDs in the global definitions list
			if (Definitions.Any(d => string.Equals(d.id, newId, StringComparison.Ordinal)))
				return -1;

			int changeCount = 0;

			foreach (var map in Maps)
			{
				if (map == null || map.tiles == null || map.width <= 0 || map.height <= 0)
					continue;

				int size = map.width * map.height;
				if (map.tiles.Length != size) continue; // skip corrupted maps

				for (int i = 0; i < size; i++)
				{
					string currentDefId = map.GetDefinitionIdAt(i);

					if (currentDefId != null && string.Equals(currentDefId, oldId, StringComparison.Ordinal))
					{
						bool success = map.SetDefinitionIdAt(i, newId);
						if (success)
							changeCount++;
						// else → log warning if you want (should rarely happen)
					}
				}
			}

			// Rename the definition definition itself
			var def = Definitions.FirstOrDefault(d => string.Equals(d.id, oldId, StringComparison.Ordinal));
			if (def != null)
			{
				def.id = newId;
			}

			return changeCount;
		}

		public static Definition GetDefinitionByStableId(string stableId)
		{
			if (string.IsNullOrEmpty(stableId)) return null;

			// Fast path: most records will eventually have hashid
			var match = database.definitions.FirstOrDefault(d => d.hashid == stableId);
			if (match != null) return match;

			// Migration fallback: check computed value
			return database.definitions.FirstOrDefault(d => d.GetStableId() == stableId);
		}

		// Optional: report how many still need migration
		public static int CountDefinitionsNeedingHashMigration()
		{
			return database?.definitions?.Count(d => string.IsNullOrEmpty(d.hashid) && !string.IsNullOrEmpty(d.id)) ?? 0;
		}
	}
}