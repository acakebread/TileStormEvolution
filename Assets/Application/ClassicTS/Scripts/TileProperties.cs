using UnityEngine;

namespace GamePreviewNamespace
{
	public class TileProperties : MonoBehaviour
	{
		public const int North = 1;  // 0b0001
		public const int South = 2;  // 0b0010
		public const int East = 4;   // 0b0100
		public const int West = 8;   // 0b1000

		public static readonly int[] Directions = { North, South, East, West };

		private GameDatabase.DatabaseLoader.TileDef tileDef;
		private int nav;

		public GameDatabase.DatabaseLoader.TileDef TileDef
		{
			set
			{
				tileDef = value;
				nav = (byte)((value.bNorth ? North : 0) | (value.bSouth ? South : 0) |
							 (value.bEast ? East : 0) | (value.bWest ? West : 0));
			}
		}

		public bool IsStart => tileDef.bStart;
		public bool IsEnd => tileDef.bEnd;
		public bool IsConsole => tileDef.bConsole;
		public bool IsDock => tileDef.bDock;
		public bool IsRoll => tileDef.bRoll;
		public string Type => tileDef.szType;
		public string Geom => tileDef.szGeom;
		public string Theme => tileDef.szTheme;
		public int Nav => nav;

		public bool Movable => tileDef.bSlide || tileDef.bRoll;
		public bool DockOrRoll => tileDef.bDock || tileDef.bRoll;
		public bool CanBeDragged => !DockOrRoll && tileDef.bSlide;
		public bool IsSlidableTarget => tileDef.bSlide;

		public static int GetOppositeDirection(int dirBit) =>
			((dirBit & South) >> 1) | ((dirBit & North) << 1) | ((dirBit & West) >> 1) | ((dirBit & East) << 1);

		public static bool CanMoveBetweenTiles(TileProperties fromTile, TileProperties toTile, int dirBit) =>
			((fromTile?.nav ?? 0) & dirBit) != 0 && ((toTile?.nav ?? 0) & GetOppositeDirection(dirBit)) != 0;
	}
}