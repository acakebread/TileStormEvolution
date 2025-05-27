using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using static ClassicTilestorm.TileDirectionFlags;

namespace ClassicTilestorm
{
	public static class Navigation
	{
		private static readonly int[] Directions = { North, South, East, West };
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

						var nextTile = GetAdjacentTile(map, currentTile, currentDir);
						if (-1 == nextTile || nextTile == startTile)
							break;

						var nextProps = map.GetTile(nextTile).Properties;
						if (null == nextProps)
							break;

						currentDir = CalculateNav(currentDir, nextProps.Nav);
						if (0 == currentDir)
							break;

						currentTile = nextTile;
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
				var props = map.GetTile(i).Properties;
				if (null != props && props.IsStart)
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
				var props = map.GetTile(i).Properties;
				if (null != props && props.IsEnd)
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
					var consoleTile = GetAdjacentTile(map, nTile, dirBit);
					if (-1 == consoleTile)
						continue;

					var consoleProps = map.GetTile(consoleTile).Properties;
					if (consoleProps?.IsConsole != true)
						continue;

					if (dirBit == GetOppositeDirection(consoleProps.Nav))
						return consoleTile;
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
				var currentNav = map.GetTile(src).Properties?.Nav & dirBit ?? 0;

				while (currentNav != 0)
				{
					if (currentTile == dst)
						return dirBit; // Found destination, return initial direction

					var nextTile = GetAdjacentTile(map, currentTile, currentNav);
					if (-1 == nextTile || nextTile == src) // Invalid tile or loop back
						break;

					var nextProps = map.GetTile(nextTile).Properties;
					if (null == nextProps)
						break;

					currentNav = CalculateNav(currentNav, nextProps.Nav);
					currentTile = nextTile;
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

				var props = map.GetTile(nSrc).Properties;
				if (null == props) break;

				var nNew = props.Nav;
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

				var props = map.GetTile(nSrc).Properties;
				if (null == props) break;

				var nNew = props.Nav;
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

		public static int GetOffsetDirection(int dx, int dz) => (dx > 0 ? East : 0) | (dx < 0 ? West : 0) | (dz > 0 ? North : 0) | (dz < 0 ? South : 0);
		public static (int dx, int dz) GetDirectionOffset(int dirBit) => (((dirBit & East) >> 2) - ((dirBit & West) >> 3), (dirBit & North) - ((dirBit & South) >> 1));
		public static int GetOppositeDirection(int dirBit) => ((dirBit & North) << 1) | ((dirBit & South) >> 1) | ((dirBit & East) << 1) | ((dirBit & West) >> 1);
		public static bool CanMoveBetweenTiles(TileProperties fromTile, TileProperties toTile, int dirBit) => ((fromTile?.Nav ?? 0) & dirBit) != 0 && ((toTile?.Nav ?? 0) & GetOppositeDirection(dirBit)) != 0;
	}
}
