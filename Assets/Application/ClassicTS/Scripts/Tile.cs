using UnityEngine;
using System.Linq;
using ClassicTilestorm;

public struct Tile
{
	public const int North = 1 << 0;   // 0b0000000001
	public const int South = 1 << 1;   // 0b0000000010
	public const int East = 1 << 2;    // 0b0000000100
	public const int West = 1 << 3;    // 0b0000001000
	public const int Dock = 1 << 4;    // 0b0000010000
	public const int Roll = 1 << 5;    // 0b0000100000
	public const int Slide = 1 << 6;   // 0b0001000000
	public const int Start = 1 << 7;   // 0b0010000000
	public const int End = 1 << 8;     // 0b0100000000
	public const int Console = 1 << 9; // 0b1000000000

	private readonly int flags;
	private static readonly int navMask = North | South | East | West;

	public Tile(string szType, string szTheme)
	{
		var def = DatabaseLoader.TileDefs.FirstOrDefault(td => td.szType == szType && td.szTheme == szTheme);
		flags = 0;
		if (def != null)
		{
			if (def.bNorth) flags |= North;
			if (def.bSouth) flags |= South;
			if (def.bEast) flags |= East;
			if (def.bWest) flags |= West;
			if (def.bDock) flags |= Dock;
			if (def.bRoll) flags |= Roll;
			if (def.bSlide) flags |= Slide;
			if (def.bStart) flags |= Start;
			if (def.bEnd) flags |= End;
			if (def.bConsole) flags |= Console;
		}
		GameObject = null;
	}

	public readonly bool IsStart => (flags & Start) != 0;
	public readonly bool IsEnd => (flags & End) != 0;
	public readonly bool IsConsole => (flags & Console) != 0;
	public readonly bool IsDock => (flags & Dock) != 0;
	public readonly bool IsRoll => (flags & Roll) != 0;
	public readonly bool IsSlide => (flags & Slide) != 0;
	public readonly bool Interactive => !(IsDock || IsRoll) && IsSlide;
	public readonly int Nav => flags & navMask;

	public GameObject GameObject;
	//public Vector3 position { get => null != GameObject ? GameObject.transform.position : Vector3.zero; set { if (null != GameObject) GameObject.transform.position = value; } }
}