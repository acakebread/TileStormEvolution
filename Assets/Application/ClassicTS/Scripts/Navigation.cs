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

		public static void SetupWaypoints(DatabaseLoader.Map map, IMap imap)
		{
			waypoints = map.waypoints?.Where(w => w != null).ToArray();
			if (null == waypoints || 0 == waypoints.Length)
				waypoints = GenerateWaypoints(imap);

			static DatabaseLoader.Waypoint[] GenerateWaypoints(IMap map)
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

						var nextTile = map.GetTile(nextTileIndex);
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

		public static int GetStartTile(IMap map)
		{
			if (null != waypoints && 0 != waypoints.Length)
				return waypoints[0].nTile;

			for (var i = 0; i < map.Width * map.Height; ++i)
			{
				if (map.GetTile(i).IsStart)
					return i;

			}
			Debug.LogError("No start tile found!");
			return -1;
		}

		public static int GetEndTile(IMap map)
		{
			if (null != waypoints && 0 != waypoints.Length)
				return waypoints[waypoints.Length - 1].nTile;

			for (var i = 0; i < map.Width * map.Height; ++i)
			{
				if (map.GetTile(i).IsEnd)
					return i;
			}
			Debug.LogError("No end tile found!");
			return -1;
		}

		public static int FindAdjacentConsole(IMap map, int nTile)
		{
			if (nTile >= 0 && nTile < map.Width * map.Height)
			{
				foreach (var dirBit in Directions)
				{
					var consoleTileIndex = GetAdjacentTile(map, nTile, dirBit);
					if (-1 == consoleTileIndex)
						continue;

					var consoleTile = map.GetTile(consoleTileIndex);
					if (true != consoleTile.IsConsole)
						continue;

					if (dirBit == GetOppositeDirection(consoleTile.Nav))
						return consoleTileIndex;
				}
			}
			return -1;
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

		//Classic TS legacy function - returns Length in direction
		public static float LengthDir(IMap map, int nSrc, int nDst, int nDir)
		{
			var nNav = GetOppositeDirection(nDir);
			var fRet = 0f;
			while (0 != nDir)
			{
				nSrc = GetAdjacentTile(map, nSrc, nDir);
				var tile = map.GetTile(nSrc);
				if (0 == tile.Nav) break;
				var nNew = tile.Nav;
				nNav = nNav & nNew;
				if (0 == nNav) break;
				nDir = nDir & nNew;
				fRet += 1.0f;
				if (nSrc == nDst) break;
			}
			return fRet;
		}

		public static int LineOfSight(IMap map, int nSrc, int nDst, int nDir)
		{
			var nNav = GetOppositeDirection(nDir);
			while (0 != nDir)
			{
				nSrc = GetAdjacentTile(map, nSrc, nDir);
				var tile = map.GetTile(nSrc);
				if (0 == tile.Nav) break;
				var nNew = tile.Nav;
				nNav = nNav & nNew;
				if (0 == nNav) break;
				nDir = nDir & nNew;
				if (nSrc == nDst) break;
			}
			return nSrc;
		}

		private static int GetAdjacentTile(IMap map, int tileIndex, int dirBit)
		{
			var(dx, dz) = GetDirectionOffset(dirBit);
			return ((tileIndex / map.Width) + dz) * map.Width + (tileIndex % map.Width) + dx;
		}

		public static int GetTileOffsetToDirection(IMap map, int tileOffset) => GetOffsetDirection(tileOffset % map.Width, tileOffset / map.Width);

		public static float DirToAngle(int dir) => new float[] { 0f, 0f, 180f, 0f, 90f, 45f, 135f, 90f, -90f, -45f, -135f, -90f, 0f, 0f, 180f, 0f }[dir & 0xF];

		public static int GetOffsetDirection(int dx, int dz) => (dx > 0 ? Tile.East : 0) | (dx < 0 ? Tile.West : 0) | (dz > 0 ? Tile.North : 0) | (dz < 0 ? Tile.South : 0);

		public static (int dx, int dz) GetDirectionOffset(int dirBit) => (((dirBit & Tile.East) >> 2) - ((dirBit & Tile.West) >> 3), ((dirBit & Tile.North) >> 0) - ((dirBit & Tile.South) >> 1));

		public static int GetOppositeDirection(int dirBit) => ((dirBit & Tile.North) << 1) | ((dirBit & Tile.South) >> 1) | ((dirBit & Tile.East) << 1) | ((dirBit & Tile.West) >> 1);

		public static bool CanMoveBetweenTiles(Tile fromTile, Tile toTile, int dirBit) => (fromTile.Nav & dirBit) != 0 && (toTile.Nav & GetOppositeDirection(dirBit)) != 0;
	}
}
