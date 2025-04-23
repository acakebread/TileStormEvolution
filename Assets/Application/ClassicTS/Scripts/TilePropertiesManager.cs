using System.Collections.Generic;
using System.Linq;
using GameDatabase;

namespace GamePreviewNamespace
{
	public static class TilePropertiesManager
	{
		private static readonly Dictionary<(string Type, string Theme), TileProperties> propertiesCache = new();

		public static TileProperties GetOrCreateTileProperties(string szType, string szTheme)
		{
			var key = (szType, szTheme);
			if (propertiesCache.TryGetValue(key, out var properties))
			{
				return properties;
			}

			var tileDef = DatabaseLoader.instance.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
			if (tileDef == null)
			{
				return null;
			}

			properties = new TileProperties(tileDef);
			propertiesCache[key] = properties;
			return properties;
		}
	}
}