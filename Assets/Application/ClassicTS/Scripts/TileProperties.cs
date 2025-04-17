using UnityEngine;
using static GameDatabase.DatabaseLoader;

namespace GamePreviewNamespace
{
	public class TileProperties : MonoBehaviour
	{
		public TileDef tileDef;

		// Return the opposite direction flag for each direction (North=1, South=2, East=4, West=8)
		public static int GetOppositeDirection(int dirBit) => ((dirBit & 0b0010) >> 1) | ((dirBit & 0b0001) << 1) | ((dirBit & 0b1000) >> 1) | ((dirBit & 0b0100) << 1);

		// Returns navigation bitfield: North=1, South=2, East=4, West=8
		// If flipped, returns inverse: North→South, South→North, East→West, West→East
		public int GetNav(bool flipped)
		{
			int nav = (tileDef.bNorth ? 1 : 0) | (tileDef.bSouth ? 2 : 0) | (tileDef.bEast ? 4 : 0) | (tileDef.bWest ? 8 : 0);
			int flippedNav = ((nav & 0b0010) >> 1) | ((nav & 0b0001) << 1) | ((nav & 0b1000) >> 1) | ((nav & 0b0100) << 1);
			int mask = flipped ? 0xF : 0;
			return (flippedNav & mask) | (nav & ~mask);
		}

		public bool hasNav => GetNav(false) != 0;
		public bool movable => hasNav && (tileDef.bSlide || tileDef.bRoll);
		public bool DockOrRoll => tileDef.bDock || tileDef.bRoll;
		public bool CanBeDragged => tileDef != null && !(tileDef.bDock || tileDef.bRoll) && tileDef.bSlide;
		public bool IsSlidableTarget => tileDef != null && tileDef.bSlide;

		// Checks if this tile can move to a target tile in the given direction
		public bool CanMoveTo(TileProperties target, int direction)
		{
			if (tileDef == null) return false;

			// Allow moves to empty spots or bDock/bRoll targets
			if (target == null || (target.tileDef.bDock || target.tileDef.bRoll))
				return true;

			// For slide tiles, check navigation compatibility
			if (tileDef.bSlide)
			{
				if ((GetNav(false) & direction) == 0) return false;
				return (target.GetNav(true) & direction) != 0;
			}

			return true; // Non-slide tiles rely on bounds
		}

		// Checks if movement is possible between two tiles in the given direction
		public static bool CanMoveBetweenTiles(TileProperties fromTile, TileProperties toTile, int dirBit, int oppositeBit)
		{
			if (fromTile == null || toTile == null) return false;

			int fromNav = fromTile.GetNav(false);
			int toNav = toTile.GetNav(false);
			return (fromNav & dirBit) != 0 && (toNav & oppositeBit) != 0;
		}
	}
}


//using UnityEngine;
//using static GameDatabase.DatabaseLoader;

//namespace GamePreviewNamespace
//{
//	public class TileProperties : MonoBehaviour
//	{
//		public TileDef tileDef;

//		// Return the opposite direction flag for each direction
//		public static int GetOppositeDirection(int dirBit)
//		{
//			switch (dirBit)
//			{
//				case 1: return 2; // North's opposite is South
//				case 2: return 1; // South's opposite is North
//				case 4: return 8; // East's opposite is West
//				case 8: return 4; // West's opposite is East
//				default: return 0;
//			}
//		}

//		// Returns navigation bitfield: North=1, South=2, East=4, West=8
//		// If flipped, returns inverse: North→South, South→North, East→West, West→East
//		public int GetNav(bool flipped)
//		{
//			int nav = 0;
//			if (flipped)
//			{
//				if (tileDef.bNorth) nav |= 2; // North→South
//				if (tileDef.bSouth) nav |= 1; // South→North
//				if (tileDef.bEast) nav |= 8;  // East→West
//				if (tileDef.bWest) nav |= 4;  // West→East
//			}
//			else
//			{
//				if (tileDef.bNorth) nav |= 1;
//				if (tileDef.bSouth) nav |= 2;
//				if (tileDef.bEast) nav |= 4;
//				if (tileDef.bWest) nav |= 8;
//			}
//			return nav;
//		}

//		public bool hasNav => GetNav(false) != 0;
//		public bool movable => hasNav && (tileDef.bSlide || tileDef.bRoll);
//		public bool DockOrRoll => tileDef.bDock || tileDef.bRoll;
//		public bool CanBeDragged => tileDef != null && !(tileDef.bDock || tileDef.bRoll) && tileDef.bSlide;
//		public bool IsSlidableTarget => tileDef != null && tileDef.bSlide;

//		// Checks if this tile can move to a target tile in the given direction
//		public bool CanMoveTo(TileProperties target, int direction)
//		{
//			if (tileDef == null) return false;

//			// Allow moves to empty spots or bDock/bRoll targets
//			if (target == null || (target.tileDef.bDock || target.tileDef.bRoll))
//				return true;

//			// For slide tiles, check navigation compatibility
//			if (tileDef.bSlide)
//			{
//				if ((GetNav(false) & direction) == 0) return false;
//				return (target.GetNav(true) & direction) != 0;
//			}

//			return true; // Non-slide tiles rely on bounds
//		}

//		// Checks if movement is possible between two tiles in the given direction
//		public static bool CanMoveBetweenTiles(TileProperties fromTile, TileProperties toTile, int dirBit, int oppositeBit)
//		{
//			if (fromTile == null || toTile == null) return false;

//			int fromNav = fromTile.GetNav(false);
//			int toNav = toTile.GetNav(false);
//			return (fromNav & dirBit) != 0 && (toNav & oppositeBit) != 0;
//		}
//	}
//}