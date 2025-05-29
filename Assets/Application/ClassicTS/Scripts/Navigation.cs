using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace ClassicTilestorm
{
	public static class Navigation
	{
		private static readonly int[] Directions = { Tile.North, Tile.South, Tile.East, Tile.West };
		private static DatabaseLoader.Waypoint[] waypoints;
		public static DatabaseLoader.Waypoint[] Waypoints => waypoints;

		public static float DirToAngle(int dir) => new float[] { 0f, 0f, 180f, 0f, 90f, 45f, 135f, 90f, -90f, -45f, -135f, -90f, 0f, 0f, 180f, 0f }[dir & 0xF];
		public static int GetOppositeDirection(int dir) => ((dir & Tile.North) << 1) | ((dir & Tile.South) >> 1) | ((dir & Tile.East) << 1) | ((dir & Tile.West) >> 1);

		public static void SetupWaypoints(DatabaseLoader.Map map, IMapManager imap)
		{
			waypoints = map.waypoints?.Where(w => w != null).ToArray();
			if (null == waypoints || 0 == waypoints.Length)
				waypoints = GenerateWaypoints(imap);

			static DatabaseLoader.Waypoint[] GenerateWaypoints(IMapManager map)
			{
				var generatedWaypoints = new List<DatabaseLoader.Waypoint>();
				if (0 == map.Width * map.Height)
				{
					Debug.LogWarning("Cannot setup waypoints: invalid tile data");
					return generatedWaypoints.ToArray();
				}

				var startTile = GetStartTile(map);
				var endTile = GetEndTile(map);

				if (-1 == startTile || -1 == endTile)
				{
					Debug.LogWarning("Cannot setup waypoints: missing start or end tile");
					return generatedWaypoints.ToArray();
				}

				generatedWaypoints.Add(new DatabaseLoader.Waypoint { nTile = startTile });

				var currentTile = startTile;
				var currentDir = NavToDest(map, currentTile, endTile);
				if (0 != currentDir)
				{
					while (currentTile != endTile)
					{
						if (FindAdjacentConsole(map, currentTile) != -1)
							generatedWaypoints.Add(new DatabaseLoader.Waypoint { nTile = currentTile });

						var nextTileIndex = GetAdjacentTile(map, currentTile, currentDir);
						if (-1 == nextTileIndex || nextTileIndex == startTile) break;

						var nextTile = MapManager.GetTile(map, nextTileIndex);
						if (0 == nextTile.Nav) break;

						currentDir = CalculateNav(currentDir, nextTile.Nav);
						currentDir = CalculateNav(currentDir, nextTile.Nav);
						if (0 == currentDir) break;

						currentTile = nextTileIndex;
					}
				}

				generatedWaypoints.Add(new DatabaseLoader.Waypoint { nTile = endTile });

				Debug.Log($"Generated {generatedWaypoints.Count} waypoints: [{string.Join(", ", generatedWaypoints.Select(w => w.nTile))}]");
				return generatedWaypoints.ToArray();
			}
		}

		public static int GetStartTile(IMapManager map)
		{
			if (null != waypoints && 0 != waypoints.Length)
				return waypoints[0].nTile;

			for (var i = 0; i < map.Width * map.Height; ++i)
			{
				if (MapManager.GetTile(map, i).IsStart)
					return i;

			}
			Debug.LogError("No start tile found!");
			return -1;
		}

		public static int GetEndTile(IMapManager map)
		{
			if (null != waypoints && 0 != waypoints.Length)
				return waypoints[waypoints.Length - 1].nTile;

			for (var i = 0; i < map.Width * map.Height; ++i)
			{
				if (MapManager.GetTile(map, i).IsEnd)
					return i;
			}
			Debug.LogError("No end tile found!");
			return -1;
		}

		public static int FindAdjacentConsole(IMapManager map, int nTile)
		{
			if (nTile >= 0 && nTile < map.Width * map.Height)
			{
				foreach (var dirBit in Directions)
				{
					var consoleTileIndex = GetAdjacentTile(map, nTile, dirBit);
					if (-1 == consoleTileIndex)
						continue;

					var consoleTile = MapManager.GetTile(map, consoleTileIndex);
					if (true != consoleTile.IsConsole)
						continue;

					if (dirBit == GetOppositeDirection(consoleTile.Nav))
						return consoleTileIndex;
				}
			}
			return -1;
		}

		//Classic TS legacy function - returns tile index in direction
		public static int LineOfSight(IMapManager map, int src, int dst, int dir)
		{
			while (0 != dir)
			{
				if (src == dst) break;
				src = GetAdjacentTile(map, src, dir);
				dir &= MapManager.GetTile(map, src).Nav;
			}
			return src;
		}

		//Classic TS legacy function - returns direction
		public static int NavToDest(IMapManager map, int src, int dst)
		{
			if (src == dst || -1 == src || -1 == dst)
				return 0;

			foreach (var dirBit in Directions)
			{
				var currentTile = src;
				var currentNav = MapManager.GetTile(map, src).Nav & dirBit;

				while (currentNav != 0)
				{
					if (currentTile == dst)
						return dirBit; // Found destination, return initial direction

					var nextTileIndex = GetAdjacentTile(map, currentTile, currentNav);
					if (-1 == nextTileIndex || nextTileIndex == src) // Invalid tile or loop back
						break;

					var nextTile = MapManager.GetTile(map, nextTileIndex);
					if (0 == nextTile.Nav) break;

					currentNav = CalculateNav(currentNav, nextTile.Nav);
					currentTile = nextTileIndex;
				}
			}
			return 0; // No valid direction found
		}

		//Classic TS legacy function - returns direction to next tile
		private static int CalculateNav(int currentDir, int nextNav)
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
		private static int GetAdjacentTile(IMapManager map, int index, int dir)
		{
			var dx = ((dir & Tile.East) >> 2) - ((dir & Tile.West) >> 3);
			var dz = ((dir & Tile.North) >> 0) - ((dir & Tile.South) >> 1);
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
