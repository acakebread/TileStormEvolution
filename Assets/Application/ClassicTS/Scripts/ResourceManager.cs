// ResourceManager.cs — FINAL CLEAN VERSION
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ClassicTilestorm
{
	public static class ResourceManager
	{
		private static DatabaseSerializer.DatabaseData _data;

		public static IList<Map> Maps => _data?.maps ?? new Map[0];
		public static IList<TileDef> TileDefs => _data?.tiledefs ?? new TileDef[0];
		public static IList<Theme> Themes => _data?.themes ?? new Theme[0];
		public static IList<TextureSet> TextureSets => _data?.texture_set ?? new TextureSet[0];

		internal static void Initialize(DatabaseSerializer.DatabaseData data)
		{
			_data = data;
		}

		public static TileDef GetTileDef(string szType) =>
			string.IsNullOrEmpty(szType) ? null :
				_data?.tiledefs.FirstOrDefault(td => td?.szType == szType);

		public static Theme GetTheme(string themeName) =>
			string.IsNullOrEmpty(themeName) ? null :
				_data?.themes.FirstOrDefault(t => t?.name == themeName);

		public static TextureSet GetTextureSet(string name) =>
			string.IsNullOrEmpty(name) ? null :
				_data?.texture_set.FirstOrDefault(ts => ts?.name == name);

		// MapManager calls this when it has changes
		public static void ApplyMapChanges(Map updatedMap)
		{
			if (_data == null || updatedMap == null) return;

			for (int i = 0; i < _data.maps.Length; i++)
			{
				if (_data.maps[i].name == updatedMap.name)
				{
					_data.maps[i] = updatedMap;
					return;
				}
			}
		}

		// NEW: These are the only two methods MapManager should call
		public static void UpdateChanges()
		{
			DatabaseSerializer.UpdateDatabase(GetCurrentDatabaseData());
			Debug.Log("Map changes updated in memory (via ResourceManager).");
		}

		public static void SaveToDisk()
		{
			DatabaseSerializer.SaveDatabase(GetCurrentDatabaseData());
			Debug.Log("Database saved to disk (via ResourceManager).");
		}

		public static DatabaseSerializer.DatabaseData GetCurrentDatabaseData() => _data;
	}
}