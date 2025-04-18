using UnityEngine;

namespace GamePreviewNamespace
{
	public class TileProperties : MonoBehaviour
	{
		// Named direction constants
		public const int North = 1;  // 0b0001
		public const int South = 2;  // 0b0010
		public const int East = 4;   // 0b0100
		public const int West = 8;   // 0b1000

		private GameDatabase.DatabaseLoader.TileDef tileDef;
		private int nav; // North=1, South=2, East=4, West=8

		// Public setter for tileDef, builds navBits
		public GameDatabase.DatabaseLoader.TileDef TileDef
		{
			set
			{
				tileDef = value;
				nav = (byte)((tileDef.bNorth ? 1 : 0) | (tileDef.bSouth ? 2 : 0) | (tileDef.bEast ? 4 : 0) | (tileDef.bWest ? 8 : 0));
			}
		}

		// Getters for MapManager
		public bool IsStart => tileDef.bStart;
		public bool IsEnd => tileDef.bEnd;
		public bool IsConsole => tileDef.bConsole;
		public bool IsDock => tileDef.bDock;
		public bool IsRoll => tileDef.bRoll;
		public string Type => tileDef.szType;
		public string Geom => tileDef.szGeom;
		public string Theme => tileDef.szTheme;
		public int Nav => nav;

		public bool Movable => (nav != 0) && (tileDef.bSlide || tileDef.bRoll);
		public bool DockOrRoll => tileDef.bDock || tileDef.bRoll;
		public bool CanBeDragged => tileDef != null && !(tileDef.bDock || tileDef.bRoll) && tileDef.bSlide;
		public bool IsSlidableTarget => tileDef != null && tileDef.bSlide;

		// Return the opposite direction flag for each direction (North=1, South=2, East=4, West=8)
		public static int GetOppositeDirection(int dirBit) => ((dirBit & 0b0010) >> 1) | ((dirBit & 0b0001) << 1) | ((dirBit & 0b1000) >> 1) | ((dirBit & 0b0100) << 1);

		// Checks if movement is possible between two tiles in the given direction
		public static bool CanMoveBetweenTiles(TileProperties fromTile, TileProperties toTile, int dirBit) => ((fromTile != null ? fromTile.nav : 0) & dirBit) != 0 && (null != toTile? toTile.nav & GetOppositeDirection(dirBit): 0) != 0;
	}
}
