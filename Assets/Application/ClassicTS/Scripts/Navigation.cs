using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using static ClassicTilestorm.TileDirectionFlags;

namespace ClassicTilestorm
{
	public static class Navigation
	{
		public static int GetStartTile(IMap map)
		{
			if (null != map.Waypoints && 0 != map.Waypoints.Length)
				return map.Waypoints[0].nTile;

			for (var i = 0; i < map.Width * map.Height; i++)
			{
				var props = map.GetTileProperties(i);
				if (props != null && props.IsStart)
					return i;
			}
			Debug.LogError("No start tile found!");
			return -1;
		}

		public static int GetEndTile(IMap map)
		{
			if (null != map.Waypoints&& 0 != map.Waypoints.Length)
				return map.Waypoints[map.Waypoints.Length - 1].nTile;

			for (var i = 0; i < map.Width * map.Height; i++)
			{
				var props = map.GetTileProperties(i);
				if (props != null && props.IsEnd)
					return i;
			}
			Debug.LogError("No end tile found!");
			return -1;
		}

		public static int FindAdjacentConsole(IMap map, int nTile)
		{
			if (map.IsValidTileIndex(nTile))
			{
				foreach (var dirBit in TileProperties.Directions)
				{
					var consoleTile = GetAdjacentTile(map, nTile, dirBit);
					if (consoleTile == -1)
						continue;

					var consoleProps = map.GetTileProperties(consoleTile);
					if (consoleProps?.IsConsole != true)
						continue;

					if (dirBit == GetOppositeDirection(consoleProps.Nav))
						return consoleTile;
				}
			}
			return -1;
		}

		public static DatabaseLoader.Waypoint[] SetupWaypoints(IMap map)
		{
			var generatedWaypoints = new List<DatabaseLoader.Waypoint>();
			if (map.Width * map.Height == 0)
			{
				Debug.LogWarning("Cannot setup waypoints: invalid tile data");
				return generatedWaypoints.ToArray();
			}

			var startTile = GetStartTile(map);
			var endTile = GetEndTile(map);

			if (startTile == -1 || endTile == -1)
			{
				Debug.LogWarning("Cannot setup waypoints: missing start or end tile");
				return generatedWaypoints.ToArray();
			}

			generatedWaypoints.Add(new DatabaseLoader.Waypoint { nTile = startTile });

			int currentTile = startTile;
			int currentDir = NavToDest(map, currentTile, endTile);
			if (currentDir != 0)
			{
				while (currentTile != endTile)
				{
					if (FindAdjacentConsole(map, currentTile) != -1)
						generatedWaypoints.Add(new DatabaseLoader.Waypoint { nTile = currentTile });

					int nextTile = GetAdjacentTile(map, currentTile, currentDir);
					if (nextTile == -1 || nextTile == startTile)
						break;

					var nextProps = map.GetTileProperties(nextTile);
					if (nextProps == null)
						break;

					currentDir = CalculateNav(currentDir, nextProps.Nav);
					if (currentDir == 0)
						break;

					currentTile = nextTile;
				}
			}

			generatedWaypoints.Add(new DatabaseLoader.Waypoint { nTile = endTile });

			Debug.Log($"Generated {generatedWaypoints.Count} waypoints: [{string.Join(", ", generatedWaypoints.Select(w => w.nTile))}]");
			return generatedWaypoints.ToArray();
		}

		//Classic TS legacy function - returns direction to next tile
		private static int CalculateNav(int currentDir, int nextNav)
		{
			int oppositeDir = GetOppositeDirection(currentDir);
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
			if (src == dst || src == -1 || dst == -1)
				return 0;

			foreach (var dirBit in TileProperties.Directions)
			{
				int currentTile = src;
				int currentNav = map.GetTileProperties(src)?.Nav & dirBit ?? 0;

				while (currentNav != 0)
				{
					if (currentTile == dst)
						return dirBit; // Found destination, return initial direction

					int nextTile = GetAdjacentTile(map, currentTile, currentNav);
					if (nextTile == -1 || nextTile == src) // Invalid tile or loop back
						break;

					var nextProps = map.GetTileProperties(nextTile);
					if (nextProps == null)
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
			int nNav = GetOppositeDirection(nDir);
			float fRet = 0.0f;
			while (0 != nDir)
			{
				nSrc = GetAdjacentTile(map, nSrc, nDir);

				var props = map.GetTileProperties(nSrc);
				if (null == props) break;

				int nNew = props.Nav;
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
			int nNav = GetOppositeDirection(nDir);
			while (0 != nDir)
			{
				nSrc = GetAdjacentTile(map, nSrc, nDir);

				var props = map.GetTileProperties(nSrc);
				if (null == props) break;

				int nNew = props.Nav;
				nNav = nNav & nNew;
				if (0 == nNav) break;
				nDir = nDir & nNew;
				if (nSrc == nDst) break;
			}
			return nSrc;
		}

		private static int GetAdjacentTile(IMap map, int tileIndex, int dirBit)
		{
			var (dx, dz) = GetDirectionOffset(dirBit);
			var newCoord = map.GetTileCoordinates(tileIndex).Add(dx, dz);
			if (newCoord.X < 0 || newCoord.X >= map.Width || newCoord.Z < 0 || newCoord.Z >= map.Height) return -1;
			return map.ToIndex(newCoord);
		}

		public static int GetTileOffsetToDirection(IMap map, int tileOffset) => GetOffsetDirection(tileOffset % map.Width, tileOffset / map.Width);

		public static float DirToAngle(int dir) => new float[] { 0f, 0f, 180f, 0f, 90f, 45f, 135f, 90f, -90f, -45f, -135f, -90f, 0f, 0f, 180f, 0f }[dir & 0xF];

		public static int GetOffsetDirection(int dx, int dz) => (dx > 0 ? East : 0) | (dx < 0 ? West : 0) | (dz > 0 ? North : 0) | (dz < 0 ? South : 0);
		public static (int dx, int dz) GetDirectionOffset(int dirBit) => (((dirBit & East) >> 2) - ((dirBit & West) >> 3), (dirBit & North) - ((dirBit & South) >> 1));
		public static int GetOppositeDirection(int dirBit) => ((dirBit & North) << 1) | ((dirBit & South) >> 1) | ((dirBit & East) << 1) | ((dirBit & West) >> 1);
		public static bool CanMoveBetweenTiles(TileProperties fromTile, TileProperties toTile, int dirBit) => ((fromTile?.Nav ?? 0) & dirBit) != 0 && ((toTile?.Nav ?? 0) & GetOppositeDirection(dirBit)) != 0;

	}
}
