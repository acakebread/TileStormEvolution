// ResourceManager.cs — FINAL FIXED VERSION (owns init, no circular calls)
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

		public static bool IsInitialized => _data != null;

		public static void Initialize()
		{
			if (_data != null) return;

			var data = DatabaseSerializer.LoadData();
			if (data == null)
			{
				Debug.LogError("ResourceManager failed to initialize: DatabaseSerializer returned null");
				return;
			}

			_data = data;
			Debug.Log($"ResourceManager initialized with {data.maps.Length} maps, {data.tiledefs.Length} tiledefs");
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

		public static void UpdateChanges() => DatabaseSerializer.UpdateDatabase(GetCurrentDatabaseData());
		public static void SaveToDisk() => DatabaseSerializer.SaveDatabase(GetCurrentDatabaseData());
		public static DatabaseSerializer.DatabaseData GetCurrentDatabaseData() => _data;
	}
}