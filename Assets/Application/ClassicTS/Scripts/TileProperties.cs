namespace GamePreviewNamespace
{
	public class TileProperties
	{
		public const int North = 1;  // 0b0001
		public const int South = 2;  // 0b0010
		public const int East = 4;   // 0b0100
		public const int West = 8;   // 0b1000

		public static readonly int[] Directions = { North, South, East, West };

		[System.Flags]
		public enum TileFlags
		{
			None = 0,
			Dock = 1 << 0,   // 0b0001
			Roll = 1 << 1,   // 0b0010
			Slide = 1 << 2,  // 0b0100
			Start = 1 << 3,  // 0b1000
			End = 1 << 4,    // 0b10000
			Console = 1 << 5 // 0b100000
		}

		public struct TileMovementBounds
		{
			public GridCoord MinWest;
			public GridCoord MaxEast;
			public GridCoord MinSouth;
			public GridCoord MaxNorth;
		}

		public readonly GameDatabase.DatabaseLoader.TileDef tileDef;
		private readonly int nav;
		private readonly TileFlags flags;

		public TileProperties(GameDatabase.DatabaseLoader.TileDef def)
		{
			tileDef = def;
			nav = (byte)((def.bNorth ? North : 0) | (def.bSouth ? South : 0) | (def.bEast ? East : 0) | (def.bWest ? West : 0));
			flags = (def.bDock ? TileFlags.Dock : 0) |
					(def.bRoll ? TileFlags.Roll : 0) |
					(def.bSlide ? TileFlags.Slide : 0) |
					(def.bStart ? TileFlags.Start : 0) |
					(def.bEnd ? TileFlags.End : 0) |
					(def.bConsole ? TileFlags.Console : 0);
		}

		public TileFlags Flags => flags;

		public bool IsStart => tileDef.bStart;
		public bool IsEnd => tileDef.bEnd;
		public bool IsConsole => tileDef.bConsole;
		public bool IsDock => tileDef.bDock;
		public bool IsRoll => tileDef.bRoll;
		public string Type => tileDef.szType;
		public string Geom => tileDef.szGeom;
		public string Theme => tileDef.szTheme;
		public int Nav => nav;
		public bool IsSlidableTarget => tileDef.bSlide;
		public bool Movable => tileDef.bSlide || tileDef.bRoll;
		public bool DockOrRoll => tileDef.bDock || tileDef.bRoll;
		public bool CanBeDragged => !(tileDef.bDock || tileDef.bRoll) && tileDef.bSlide;

		public static (int dx, int dz) GetDirectionOffset(int dirBit) => (((dirBit & East) >> 2) - ((dirBit & West) >> 3), (dirBit & North) - ((dirBit & South) >> 1));
		public static int GetOppositeDirection(int dirBit) => ((dirBit & North) << 1) | ((dirBit & South) >> 1) | ((dirBit & East) << 1) | ((dirBit & West) >> 1);
		public static bool CanMoveBetweenTiles(TileProperties fromTile, TileProperties toTile, int dirBit) => ((fromTile?.nav ?? 0) & dirBit) != 0 && ((toTile?.nav ?? 0) & GetOppositeDirection(dirBit)) != 0;
	}
}