using UnityEngine;
using static GameDatabase.DatabaseLoader;

namespace GamePreviewNamespace
{
	public class TileProperties : MonoBehaviour
	{
		public TileDef tileDef;

		// Returns navigation bitfield: North=1, South=2, East=4, West=8
		// If flipped, returns inverse: North→South, South→North, East→West, West→East
		public int GetNav(bool flipped)
		{
			int nav = 0;
			if (flipped)
			{
				if (tileDef.bNorth) nav |= 2; // North→South
				if (tileDef.bSouth) nav |= 1; // South→North
				if (tileDef.bEast) nav |= 8;  // East→West
				if (tileDef.bWest) nav |= 4;  // West→East
			}
			else
			{
				if (tileDef.bNorth) nav |= 1;
				if (tileDef.bSouth) nav |= 2;
				if (tileDef.bEast) nav |= 4;
				if (tileDef.bWest) nav |= 8;
			}
			return nav;
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