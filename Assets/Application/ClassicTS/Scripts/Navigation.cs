namespace ClassicTilestorm
{
	public static class Navigation
	{
		// Public for access from MapManager.FindAdjacentConsole
		public static readonly int[] Directions = { TileData.North, TileData.South, TileData.East, TileData.West };
		public static float DirToAngle(int dir) => new float[] { 0f, 0f, 180f, 0f, 90f, 45f, 135f, 90f, -90f, -45f, -135f, -90f, 0f, 0f, 180f, 0f }[dir & 0xF];
		public static int GetOppositeDirection(int dir) => ((dir & TileData.North) << 1) | ((dir & TileData.South) >> 1) | ((dir & TileData.East) << 1) | ((dir & TileData.West) >> 1);

		//Classic TS legacy function - returns tile index in direction
		public static int LineOfSight(IMap map, int src, int dst, int dir)
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
		public static int NavToDest(IMap map, int src, int dst)
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
		public static int GetAdjacentTile(IMap map, int index, int dir)
		{
			var dx = ((dir & TileData.East) >> 2) - ((dir & TileData.West) >> 3);
			var dz = ((dir & TileData.North) >> 0) - ((dir & TileData.South) >> 1);
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