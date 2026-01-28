using UnityEngine;

namespace ClassicTilestorm
{
	[System.Flags]
	public enum DirectionFlags : int
	{
		None = 0,

		North = 1 << 0,  // RESERVED – do not reuse this bit anywhere else
		South = 1 << 1,  // RESERVED
		East = 1 << 2,   // RESERVED
		West = 1 << 3,   // RESERVED
		//ToDo Add NorthEast = 1 << 4
		//ToDo Add NorthWest = 1 << 5
		//ToDo Add SouthEast = 1 << 6
		//ToDo Add SouthEast = 1 << 7
		Directions = North | South | East | West

		// You are **not** allowed to add any other values here
		// All gameplay flags belong in DefinitionFlags
	}

	public static class Navigation
	{
		private const int N = (int)DirectionFlags.North;
		private const int S = (int)DirectionFlags.South;
		private const int E = (int)DirectionFlags.East;
		private const int W = (int)DirectionFlags.West;
		private const int DirMask = (int)DirectionFlags.Directions;

		public static readonly int[] Directions = { N, S, E, W };
		public static float DirToAngle(int dir) => new float[] { 0f, 0f, 180f, 0f, 90f, 45f, 135f, 90f, -90f, -45f, -135f, -90f, 0f, 0f, 180f, 0f }[dir & 0xF];
		public static int GetOppositeDirection(int dir) => ((dir & N) << 1) | ((dir & S) >> 1) | ((dir & E) << 1) | ((dir & W) >> 1);

		public static int Rotate(int flags, int degrees)
		{
			int masked = flags & DirMask;

			// Convert degrees → normalized 0..3 turns (positive = clockwise)
			int turns = Mathf.RoundToInt(degrees / 90f);
			turns = (turns % 4 + 4) % 4;           // this is the standard positive mod idiom

			return turns switch
			{
				0 => masked,
				1 => (((masked & N) << 2) | ((masked & E) >> 1) | ((masked & S) << 2) | ((masked & W) >> 3)) & (int)DirectionFlags.Directions,
				2 => (((masked & N) << 1) | ((masked & E) << 1) | ((masked & S) >> 1) | ((masked & W) >> 1)) & (int)DirectionFlags.Directions,
				3 => (((masked & N) << 3) | ((masked & E) >> 2) | ((masked & S) << 1) | ((masked & W) >> 2)) & (int)DirectionFlags.Directions,
				_ => masked
			};
		}

		//Classic TS legacy function - returns tile index in direction
		public static int LineOfSight(IMapPlay map, int src, int dst, int dir)
		{
			while (0 != dir)
			{
				if (src == dst) break;
				src = GetAdjacentTile(map, src, dir);
				dir &= map.GetTile(src).Nav;
			}
			return src;
		}

		//Classic TS legacy function - returns direction
		public static int NavToDest(IMapPlay map, int src, int dst)
		{
			if (src == dst || -1 == src || -1 == dst)
				return 0;

			foreach (var dirBit in Directions)
			{
				var currentTile = src;
				var currentNav = map.GetTile(src).Nav & dirBit;

				while (currentNav != 0)
				{
					if (currentTile == dst)
						return dirBit; // Found destination, return initial direction

					var nextTileIndex = GetAdjacentTile(map, currentTile, currentNav);
					if (-1 == nextTileIndex || nextTileIndex == src) // Invalid tile or loop back
						break;

					var nextTile = map.GetTile(nextTileIndex);
					if (0 == nextTile.Nav) break;

					currentNav = CalculateNav(currentNav, nextTile.Nav);
					currentTile = nextTileIndex;
				}
			}
			return 0; // No valid direction found
		}

		//Classic TS legacy function - returns direction to next tile
		public static int CalculateNav(int currentDir, int nextNav)
		{
			var oppositeDir = GetOppositeDirection(currentDir);
			if ((oppositeDir & nextNav) != 0) // Next tile allows entering from current direction
			{
				if ((currentDir & nextNav) != 0)
					return currentDir; // Continue straight (crossroad or straight path)
				return nextNav & ~oppositeDir; // Turn at bend (use next tile's nav, exclude opposite)
			}
			return 0; // Direction not supported
		}

		//Classic TS legacy function - returns index of adjacent tile
		public static int GetAdjacentTile(IMapPlay map, int index, int dir)
		{
			var dx = ((dir & E) >> 2) - ((dir & W) >> 3);
			var dz = ((dir & N) >> 0) - ((dir & S) >> 1);
			return ((index / map.Width) + dz) * map.Width + (index % map.Width) + dx;
		}

		//public static float LengthDir(IMap map, int src, int dst, int dir)
		//{
		//	float length = 0f;
		//	while (0 != dir)
		//	{
		//		src = GetAdjacentTile(map, src, dir);
		//		var tile = MapManager.GetTile(map, src);
		//		if (tile.Nav == 0) break;
		//		dir &= tile.Nav;
		//		length += 1f;
		//		if (src == dst) break;
		//	}
		//	return length;
		//}
	}
}