using static ClassicTilestorm.TileDirectionFlags;

namespace ClassicTilestorm
{
	public class TileProperties
	{
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

		public readonly DatabaseLoader.TileDef tileDef;
		private readonly int nav;
		private readonly TileFlags flags;

		public TileProperties(DatabaseLoader.TileDef def)
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
		public bool IsSlide => tileDef.bSlide;
		public string Type => tileDef.szType;
		public string Geom => tileDef.szGeom;
		public string Theme => tileDef.szTheme;
		public bool Interactive => !(tileDef.bDock || tileDef.bRoll) && tileDef.bSlide;

		public int Nav => nav;
	}
}
