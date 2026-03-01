//using MassiveHadronLtd;

//namespace ClassicTilestorm
//{
//	public static class DebugUtilExtensions
//	{
//		public static void LogTile(this MapManager manager, string message, int? tileIndex = null)
//		{
//			string loc = BuildLocation(MainController.CurrentMap?.name, tileIndex);
//			DebugUtil.LogContext(loc, message, manager);
//		}

//		public static void WarnTile(this MapManager manager, string message, int? tileIndex = null)
//		{
//			string loc = BuildLocation(MainController.CurrentMap?.name, tileIndex);
//			DebugUtil.WarnContext(loc, message, manager);
//		}

//		// You can add more overloads / helpers as needed
//		// e.g. LogAttachment, WarnNav, etc.

//		private static string BuildLocation(string mapName, int? tile)
//		{
//			if (string.IsNullOrEmpty(mapName) && !tile.HasValue)
//				return null;

//			string parts = "";

//			if (!string.IsNullOrEmpty(mapName))
//				parts += $"map '{mapName}'";

//			if (tile.HasValue)
//			{
//				if (parts.Length > 0) parts += " ";
//				parts += $"tile {tile.Value}";
//			}

//			return parts;
//		}
//	}
//}