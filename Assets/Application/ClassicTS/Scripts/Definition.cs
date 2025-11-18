using System;

namespace ClassicTilestorm
{
	[Serializable]
	public class Definition
	{
		public string szBank;
		public string szType;
		public string szGeom;
		public bool bSlide;
		public bool bRoll;
		public bool bDock;
		public bool bConsole;
		public bool bDoor;
		public bool bStart;
		public bool bEnd;
		public int nPickup;
		public bool bPuzzleBlock;
		public bool bNorth;
		public bool bSouth;
		public bool bEast;
		public bool bWest;
	}
}