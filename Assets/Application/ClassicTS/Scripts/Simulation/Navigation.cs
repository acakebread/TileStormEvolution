using System.Collections.Generic;
using UnityEngine;

namespace ClassicTilestorm
{
	public static class Navigation
	{
		// Constants now directly from DefinitionFlags (the single source of truth)
		private const int N = (int)DefinitionFlags.North;
		private const int S = (int)DefinitionFlags.South;
		private const int E = (int)DefinitionFlags.East;
		private const int W = (int)DefinitionFlags.West;

		// Mask for direction bits (bits 0–3)
		private const int DirMask = (int)DefinitionFlags.DirMask;

		public static readonly int[] Directions = { N, S, E, W };

		public static float DirToAngle(int dir) => new float[]
		{
			0f, 0f, 180f, 0f, 90f, 45f, 135f, 90f, -90f, -45f, -135f, -90f, 0f, 0f, 180f, 0f
		}[dir & 0xF];

		public static int GetOppositeDirection(int dir)
			=> ((dir & N) << 1) | ((dir & S) >> 1) | ((dir & E) << 1) | ((dir & W) >> 1);

		public static int Rotate(int flags, int degrees)
		{
			int masked = flags & DirMask;

			int turns = Mathf.RoundToInt(degrees / 90f);
			turns = (turns % 4 + 4) % 4;

			return turns switch
			{
				0 => masked,
				1 => (((masked & N) << 2) | ((masked & E) >> 1) | ((masked & S) << 2) | ((masked & W) >> 3)) & DirMask,
				2 => (((masked & N) << 1) | ((masked & E) << 1) | ((masked & S) >> 1) | ((masked & W) >> 1)) & DirMask,
				3 => (((masked & N) << 3) | ((masked & E) >> 2) | ((masked & S) << 1) | ((masked & W) >> 2)) & DirMask,
				_ => masked
			};
		}

		public static int LineOfSight(IMapPlay map, int src, int dst, int dir)
		{
			while (dir != 0)
			{
				if (src == dst) break;
				src = GetAdjacentTile(map, src, dir);
				dir &= map.GetTile(src).Nav;
			}
			return src;
		}

		public static int NavToDest(IMapPlay map, int src, int dst)
		{
			if (src == dst || src == -1 || dst == -1)
				return 0;

			foreach (var dirBit in Directions)
			{
				var currentTile = src;
				var currentNav = map.GetTile(src).Nav & dirBit;

				while (currentNav != 0)
				{
					if (currentTile == dst)
						return dirBit;

					var nextTileIndex = GetAdjacentTile(map, currentTile, currentNav);
					if (nextTileIndex == -1 || nextTileIndex == src)
						break;

					var nextTile = map.GetTile(nextTileIndex);
					if (nextTile.Nav == 0) break;

					currentNav = CalculateNav(currentNav, nextTile.Nav);
					currentTile = nextTileIndex;
				}
			}
			return 0;
		}

		public static int CalculateNav(int currentDir, int nextNav)
		{
			var oppositeDir = GetOppositeDirection(currentDir);
			if ((oppositeDir & nextNav) != 0)
			{
				if ((currentDir & nextNav) != 0)
					return currentDir;
				return nextNav & ~oppositeDir;
			}
			return 0;
		}

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

		//public static int[] GenerateWaypoints(IMapPlay map)
		//{
		//	var generated = new List<int>();
		//	var start = map.GetStartTile();
		//	var end = map.GetEndTile();

		//	if (start == -1 || end == -1)
		//		return generated.ToArray();

		//	generated.Add(start);

		//	var cur = start;
		//	var dir = NavToDest(map, cur, end);
		//	if (dir != 0)
		//	{
		//		while (cur != end)
		//		{
		//			if (map.FindAdjacentConsole(cur) != -1)
		//				generated.Add(cur);

		//			var next = GetAdjacentTile(map, cur, dir);
		//			if (next == -1 || next == start) break;

		//			var nextTile = map.GetTile(next);
		//			if (nextTile.Nav == 0) break;

		//			dir = CalculateNav(dir, nextTile.Nav);
		//			if (dir == 0) break;

		//			cur = next;
		//		}
		//	}

		//	generated.Add(end);
		//	return generated.ToArray();
		//}
	}
}