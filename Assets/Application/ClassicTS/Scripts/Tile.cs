using UnityEngine;
using System.Linq;

namespace ClassicTilestorm
{
	[System.Flags]
	public enum TileFlags
	{
		None = 0,
		North = 1 << 0,   // 0b0000000001
		South = 1 << 1,   // 0b0000000010
		East = 1 << 2,    // 0b0000000100
		West = 1 << 3,    // 0b0000001000
		Dock = 1 << 4,	  // 0b0000010000
		Roll = 1 << 5,	  // 0b0000100000
		Slide = 1 << 6,	  // 0b0001000000
		Start = 1 << 7,	  // 0b0010000000
		End = 1 << 8,	  // 0b0100000000
		Console = 1 << 9  // 0b1000000000 
	}

	public struct Tile
	{
		private readonly TileFlags flags;

		//public Tile(DatabaseLoader.TileDef def)
		public Tile(string szType, string szTheme)
		{
			var def = DatabaseLoader.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
			flags = null == def ? 0 :
				(def.bNorth ? TileFlags.North : 0) |
				(def.bSouth ? TileFlags.South : 0) |
				(def.bEast ? TileFlags.East : 0) |
				(def.bWest ? TileFlags.West : 0) |
				(def.bDock ? TileFlags.Dock : 0) |
				(def.bRoll ? TileFlags.Roll : 0) |
				(def.bSlide ? TileFlags.Slide : 0) |
				(def.bStart ? TileFlags.Start : 0) |
				(def.bEnd ? TileFlags.End : 0) |
				(def.bConsole ? TileFlags.Console : 0);
			GameObject = null;
		}

		public readonly bool IsStart => 0 != (flags & TileFlags.Start);
		public readonly bool IsEnd => 0 != (flags & TileFlags.End);
		public readonly bool IsConsole => 0 != (flags & TileFlags.Console);
		public readonly bool IsDock => 0 != (flags & TileFlags.Dock);
		public readonly bool IsRoll => 0 != (flags & TileFlags.Roll);
		public readonly bool IsSlide => 0 != (flags & TileFlags.Slide);
		public readonly bool Interactive => !(IsDock || IsRoll) && IsSlide;

		public readonly int Nav => (int)(flags & (TileFlags.North | TileFlags.South | TileFlags.East | TileFlags.West));

		public GameObject GameObject;
		//public Vector3 position { get => null != GameObject ? GameObject.transform.position : Vector3.zero; set { if (null != GameObject) GameObject.transform.position = value; } }
	}
}