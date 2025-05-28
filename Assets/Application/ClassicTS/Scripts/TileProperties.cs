using static ClassicTilestorm.TileDirectionFlags;

namespace ClassicTilestorm
{
	public readonly struct TileProperties
	{
		[System.Flags]
		private enum TileFlags
		{
			None = 0,
			North = 1 << 0,   // 0b0000000001
			South = 1 << 1,   // 0b0000000010
			East = 1 << 2,    // 0b0000000100
			West = 1 << 3,    // 0b0000001000
			Dock = 1 << 4,    // 0b0000010000
			Roll = 1 << 5,    // 0b0000100000
			Slide = 1 << 6,   // 0b0001000000
			Start = 1 << 7,   // 0b0010000000
			End = 1 << 8,     // 0b0100000000
			Console = 1 << 9  // 0b1000000000 
		}

		private readonly TileFlags flags;

		public TileProperties(DatabaseLoader.TileDef def)
		{
			//Type = def.szType;//for debugging
			flags = (def.bNorth ? TileFlags.North : 0) |
					(def.bSouth ? TileFlags.South : 0) |
					(def.bEast ? TileFlags.East : 0) |
					(def.bWest ? TileFlags.West : 0) |
					(def.bDock ? TileFlags.Dock : 0) |
					(def.bRoll ? TileFlags.Roll : 0) |
					(def.bSlide ? TileFlags.Slide : 0) |
					(def.bStart ? TileFlags.Start : 0) |
					(def.bEnd ? TileFlags.End : 0) |
					(def.bConsole ? TileFlags.Console : 0);
		}

		public readonly bool IsStart => 0 != (flags & TileFlags.Start);
		public readonly bool IsEnd => 0 != (flags & TileFlags.End);
		public readonly bool IsConsole => 0 != (flags & TileFlags.Console);
		public readonly bool IsDock => 0 != (flags & TileFlags.Dock);
		public readonly bool IsRoll => 0 != (flags & TileFlags.Roll);
		public readonly bool IsSlide => 0 != (flags & TileFlags.Slide);
		public readonly bool Interactive => !(IsDock || IsRoll) && IsSlide;

		public readonly int Nav => (byte)flags & (North | South | East | West);//(byte)((IsNorth ? North : 0) | (IsSouth ? South : 0) | (IsEast ? East : 0) | (IsWest ? West : 0));

		//public readonly string Type;// => tileDef.szType;
		//public readonly string Geom => tileDef.szGeom;
		//public readonly string Theme => tileDef.szTheme;
		//private readonly bool IsNorth => 0 != (flags & TileFlags.North);
		//private readonly bool IsSouth => 0 != (flags & TileFlags.South);
		//private readonly bool IsEast => 0 != (flags & TileFlags.East);
		//private readonly bool IsWest => 0 != (flags & TileFlags.West);
	}
}



//using static ClassicTilestorm.TileDirectionFlags;

//namespace ClassicTilestorm
//{
//	public struct TileProperties
//	{
//		[System.Flags]
//		public enum TileFlags
//		{
//			None = 0,
//			North = 1 << 0,   // 0b0000000001
//			South = 1 << 1,	  // 0b0000000010
//			East = 1 << 2,	  // 0b0000000100
//			West = 1 << 3,    // 0b0000001000
//			Dock = 1 << 4,    // 0b0000010000
//			Roll = 1 << 5,	  // 0b0000100000
//			Slide = 1 << 6,   // 0b0001000000
//			Start = 1 << 7,   // 0b0010000000
//			End = 1 << 8,     // 0b0100000000
//			Console = 1 << 9  // 0b1000000000 
//		}

//		public readonly DatabaseLoader.TileDef tileDef;
//		//nav = (byte)((def.bNorth ? North : 0) | (def.bSouth ? South : 0) | (def.bEast ? East : 0) | (def.bWest ? West : 0));
//		private readonly TileFlags flags;

//		public TileProperties(DatabaseLoader.TileDef def)
//		{
//			tileDef = def;
//			//nav = (byte)((def.bNorth ? North : 0) | (def.bSouth ? South : 0) | (def.bEast ? East : 0) | (def.bWest ? West : 0));
//			flags =	(def.bNorth ? TileFlags.North : 0) |
//					(def.bSouth ? TileFlags.South : 0) |
//					(def.bEast ? TileFlags.East : 0) |
//					(def.bWest ? TileFlags.West : 0) |
//					(def.bDock ? TileFlags.Dock : 0) |
//					(def.bRoll ? TileFlags.Roll : 0) |
//					(def.bSlide ? TileFlags.Slide : 0) |
//					(def.bStart ? TileFlags.Start : 0) |
//					(def.bEnd ? TileFlags.End : 0) |
//					(def.bConsole ? TileFlags.Console : 0);
//		}

//		//public TileFlags Flags => flags;
//		//public string Type => tileDef.szType;
//		//public string Geom => tileDef.szGeom;
//		//public string Theme => tileDef.szTheme;
//		//public bool IsStart => tileDef.bStart;
//		//public bool IsEnd => tileDef.bEnd;
//		//public bool IsConsole => tileDef.bConsole;
//		//public bool IsDock => tileDef.bDock;
//		//public bool IsRoll => tileDef.bRoll;
//		//public bool IsSlide => tileDef.bSlide;
//		//public bool Interactive => null != tileDef && (!(tileDef.bDock || tileDef.bRoll) && tileDef.bSlide);
//		//public int Nav => nav;

//		//public string Type => tileDef.szType;
//		//public string Geom => tileDef.szGeom;
//		//public string Theme => tileDef.szTheme;

//		public bool IsStart => 0 != (flags & TileFlags.Start);
//		public bool IsEnd => 0 != (flags & TileFlags.End);
//		public bool IsConsole => 0 != (flags & TileFlags.Console);
//		public bool IsDock => 0 != (flags & TileFlags.Dock);
//		public bool IsRoll => 0 != (flags & TileFlags.Roll);
//		public bool IsSlide => 0 != (flags & TileFlags.Slide);
//		public bool Interactive => !(IsDock || IsRoll) && IsSlide;


//		private bool IsNorth => 0 != (flags & TileFlags.North);
//		private bool IsSouth => 0 != (flags & TileFlags.South);
//		private bool IsEast => 0 != (flags & TileFlags.East);
//		private bool IsWest => 0 != (flags & TileFlags.West);

//		public int Nav => (byte)((IsNorth ? North : 0) | (IsSouth ? South : 0) | (IsEast ? East : 0) | (IsWest ? West : 0));
//	}
//}
