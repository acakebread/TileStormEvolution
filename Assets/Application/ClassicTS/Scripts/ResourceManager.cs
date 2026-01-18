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

		public static Definition GetDefinition(string id) => string.IsNullOrEmpty(id) ? null : Definitions.FirstOrDefault(d => d.id == id);
		public static TextureSequence GetTextureSequence(string id) => string.IsNullOrEmpty(id) ? null : TextureSequences.FirstOrDefault(ts => ts.id == id);

		// Data manipulation
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

		// ── Added for the refactor ─────────────────────────────────────────────

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

		/// <summary>
		/// Checks if the given definition ID is currently used in any map's tile table.
		/// </summary>
		public static bool IsDefinitionUsed(string defId)
		{
			if (string.IsNullOrEmpty(defId))
				return false;

			foreach (var map in Maps)
			{
				if (map?.table == null)
					continue;

				// Very fast check - most maps will fail here immediately
				if (Array.IndexOf(map.table, defId) >= 0)
					return true;
			}

			return false;
		}

		/// <summary>
		/// Generates the next available unique definition ID in the format "new_def_id(001)", "new_def_id(002)", etc.
		/// </summary>
		public static string GenerateUniqueNewDefinitionId(string prefix = "new_def_id")
		{
			int n = 1;
			string candidate;

			var existingIds = Definitions
				.Select(d => d.id)
				.ToHashSet(StringComparer.Ordinal); // Fast lookup

			do
			{
				candidate = $"{prefix}({n:000})";
				n++;
			}
			while (existingIds.Contains(candidate));

			return candidate;
		}
	}
}