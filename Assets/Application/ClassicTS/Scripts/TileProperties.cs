namespace GamePreviewNamespace
{
	public class TileProperties
	{
		public const int North = 1;  // 0b0001
		public const int South = 2;  // 0b0010
		public const int East = 4;   // 0b0100
		public const int West = 8;   // 0b1000

		public static readonly int[] Directions = { North, South, East, West };

		public readonly GameDatabase.DatabaseLoader.TileDef tileDef;
		public TileProperties(GameDatabase.DatabaseLoader.TileDef def)
		{
			tileDef = def;
			nav = (byte)((def.bNorth ? North : 0) | (def.bSouth ? South : 0) | (def.bEast ? East : 0) | (def.bWest ? West : 0));
		}

		public bool IsStart => tileDef.bStart;
		public bool IsEnd => tileDef.bEnd;
		public bool IsConsole => tileDef.bConsole;
		public bool IsDock => tileDef.bDock;
		public bool IsRoll => tileDef.bRoll;
		public string Type => tileDef.szType;
		public string Geom => tileDef.szGeom;
		public string Theme => tileDef.szTheme;

		private readonly int nav;
		public int Nav => nav;

		public bool Movable => tileDef.bSlide || tileDef.bRoll;
		public bool DockOrRoll => tileDef.bDock || tileDef.bRoll;
		public bool CanBeDragged => !(tileDef.bDock || tileDef.bRoll) && tileDef.bSlide;
		public bool IsSlidableTarget => tileDef.bSlide;

		public static int GetOppositeDirection(int dirBit) => ((dirBit & North) << 1) | ((dirBit & South) >> 1) | ((dirBit & East) << 1) | ((dirBit & West) >> 1);
		public static bool CanMoveBetweenTiles(TileProperties fromTile, TileProperties toTile, int dirBit) => ((fromTile?.nav ?? 0) & dirBit) != 0 && ((toTile?.nav ?? 0) & GetOppositeDirection(dirBit)) != 0;
	}
}