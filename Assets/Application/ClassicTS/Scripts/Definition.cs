using System;

namespace ClassicTilestorm
{
	[Serializable]
	public class Definition
	{
		public string szType;//id - probably should be 'name'
		public string szGeom;//geometry
		public string szBank;//texture bank
		public bool bConsole;
		public bool bStart;
		public bool bEnd;
		public bool bSlide;
		public bool bRoll;
		public bool bDock;
		public bool bDoor;
		public bool bNorth;
		public bool bSouth;
		public bool bEast;
		public bool bWest;
		public int nPickup;
		public bool bPuzzleBlock;
	}
}