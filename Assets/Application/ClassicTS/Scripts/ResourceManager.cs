using System.Linq;

namespace ClassicTilestorm
{
	[System.Serializable]
	public class DatabaseData
	{
		public Map[] maps;
		public Definition[] definitions;
		public TextureSequence[] textures;
		public Button[] buttons;
	}

	public static class ResourceManager
	{
		private static DatabaseData _db;
		public static DatabaseData database { get => _db; set => _db = value; }

		public static System.Collections.Generic.IList<Map> Maps => _db?.maps ?? System.Array.Empty<Map>();
		public static System.Collections.Generic.IList<Definition> Definitions => _db?.definitions ?? System.Array.Empty<Definition>();
		public static System.Collections.Generic.IList<TextureSequence> TextureSets => _db?.textures ?? System.Array.Empty<TextureSequence>();
		public static System.Collections.Generic.IList<Button> Buttons => _db?.buttons ?? System.Array.Empty<Button>();

		public static Definition GetDefinition(string id) => string.IsNullOrEmpty(id) ? null : Definitions.FirstOrDefault(d => d.id == id);
		public static TextureSequence GetTextureSequence(string id) => string.IsNullOrEmpty(id) ? null : TextureSets.FirstOrDefault(ts => ts.id == id);

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

		public static System.Collections.Generic.List<string> DefinitionNavGroup(string referenceDef)
		{
			var singleDirections = new[] { " n", " e", " s", " w" };
			var doubleLinear = new[] { " we", " ns", " ew", " sn" };
			var doubleDiagonal = new[] { " nw", " ne", " se", " sw" };
			var selectedGroup = singleDirections;

			var baseId = referenceDef;
			foreach (var suffix in singleDirections.Concat(doubleLinear).Concat(doubleDiagonal))
			{
				if (referenceDef.EndsWith(suffix))
				{
					baseId = referenceDef.Substring(0, referenceDef.Length - suffix.Length);
					if (doubleLinear.Any(s => referenceDef.EndsWith(s)))
						selectedGroup = doubleLinear;
					else if (doubleDiagonal.Any(s => referenceDef.EndsWith(s)))
						selectedGroup = doubleDiagonal;
					break;
				}
			}

			var cycleList = new System.Collections.Generic.List<string>();

			if (Definitions.Any(d => d.id == baseId))
				cycleList.Add(baseId);

			foreach (var suffix in selectedGroup)
			{
				var candidate = baseId + suffix;
				if (Definitions.Any(d => d.id == candidate))
					cycleList.Add(candidate);
			}

			if (0 == cycleList.Count)
				cycleList.Add(referenceDef);

			return cycleList;
		}
	}
}