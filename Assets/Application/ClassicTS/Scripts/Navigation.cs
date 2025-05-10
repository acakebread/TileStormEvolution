using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ClassicTilestorm
{
	public static class Navigation
	{
		public static int GetStartTile(IMap map)
		{
			if (map.Waypoints != null && map.Waypoints.Count != 0)
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
			if (map.Waypoints != null && map.Waypoints.Count != 0)
				return map.Waypoints[map.Waypoints.Count - 1].nTile;

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

					if (dirBit == TileProperties.GetOppositeDirection(consoleProps.Nav))
						return consoleTile;
				}
			}
			return -1;
		}

		public static List<DatabaseLoader.Waypoint> SetupWaypoints(IMap map)
		{
			var generatedWaypoints = new List<DatabaseLoader.Waypoint>();
			if (map.Width * map.Height == 0)
			{
				Debug.LogWarning("Cannot setup waypoints: invalid tile data");
				return generatedWaypoints;
			}

			var startTile = GetStartTile(map);
			var endTile = GetEndTile(map);

			if (startTile == -1 || endTile == -1)
			{
				Debug.LogWarning("Cannot setup waypoints: missing start or end tile");
				return generatedWaypoints;
			}

			generatedWaypoints.Add(new DatabaseLoader.Waypoint { nTile = startTile });
			var path = FindPath(map, startTile, endTile);

			if (path != null)
			{
				foreach (int tile in path)
				{
					if (tile == startTile)
						continue;
					if (FindAdjacentConsole(map, tile) != -1 || tile == endTile)
					{
						generatedWaypoints.Add(new DatabaseLoader.Waypoint { nTile = tile });
					}
				}
			}
			else
			{
				generatedWaypoints.Add(new DatabaseLoader.Waypoint { nTile = endTile });
				Debug.LogWarning("Failed to find path from start to end for waypoint setup");
			}

			Debug.Log($"Generated {generatedWaypoints.Count} waypoints: [{string.Join(", ", generatedWaypoints.Select(w => w.nTile))}]");
			return generatedWaypoints;
		}

		//public static bool CheckPathBetweenWaypoints(IMap map, int fromWaypointIndex, int toWaypointIndex, out List<int> path)
		//{
		//	if (fromWaypointIndex < 0 || fromWaypointIndex >= map.Waypoints.Count || toWaypointIndex < 0 || toWaypointIndex >= map.Waypoints.Count)
		//	{
		//		path = null;
		//		return false;
		//	}
		//	int fromTile = map.Waypoints[fromWaypointIndex].nTile;
		//	int toTile = map.Waypoints[toWaypointIndex].nTile;

		//	path = FindPath(map, fromTile, toTile);
		//	return path != null;
		//}

		//Classic TS legacy function - returns direction - ToDo rewrite correctly
		public static int NavToDest(IMap map, int src, int dst)
		{
			var path = FindPath(map, src, dst);
			if (null == path || path.Count < 2) return 0;
			return GetTileOffsetToDirection(map, path[1] - path[0]);
		}

		//Classic TS legacy function - returns Length in direction - ToDo rewrite correctly
		public static float LengthDir(IMap map, int nSrc, int nDst, int nDir)
		{
			int nNav = TileProperties.GetOppositeDirection(nDir);
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

		public static List<int> FindPath(IMap map, int startTile, int targetTile)
		{
			var startProps = map.GetTileProperties(startTile);
			if (startProps == null)
				return null;

			foreach (int dirBit in TileProperties.Directions)
			{
				if ((startProps.Nav & dirBit) == 0)
					continue;
				var path = FindPathRecursive(map, startTile, targetTile, dirBit);
				if (path != null)
					return path;
			}
			return null;
		}

		private static List<int> FindPathRecursive(IMap map, int currentTile, int targetTile, int currentDirBit, List<int> path = null)
		{
			path ??= new List<int>();
			path.Add(currentTile);

			if (currentTile == targetTile)
				return path;

			var currentProps = map.GetTileProperties(currentTile);
			if (currentProps == null)
			{
				path.RemoveAt(path.Count - 1);
				return null;
			}

			var tryDirections = GetTryDirections(currentProps.Nav, currentDirBit);
			for (int i = 0; i < tryDirections.Length; i++)
			{
				var dirBit = tryDirections[i];
				var nextTile = GetAdjacentTile(map, currentTile, dirBit);
				if (nextTile == -1)
					continue;

				var nextProps = map.GetTileProperties(nextTile);
				if (!TileProperties.CanMoveBetweenTiles(currentProps, nextProps, dirBit))
					continue;

				var result = FindPathRecursive(map, nextTile, targetTile, dirBit, path);
				if (result != null)
					return result;
			}

			path.RemoveAt(path.Count - 1);
			return null;

			static int[] GetTryDirections(int nav, int currentDirBit)
			{
				if ((TileProperties.GetOppositeDirection(nav) & nav) == nav)
					return new[] { currentDirBit };
				if (currentDirBit != 0)
					return new[] { nav & ~(currentDirBit | TileProperties.GetOppositeDirection(currentDirBit)) };
				return TileProperties.Directions;
			}
		}

		private static int GetAdjacentTile(IMap map, int tileIndex, int dirBit)
		{
			var (dx, dz) = TileProperties.GetDirectionOffset(dirBit);
			var newCoord = map.GetTileCoordinates(tileIndex).Add(dx, dz);
			if (newCoord.X < 0 || newCoord.X >= map.Width || newCoord.Z < 0 || newCoord.Z >= map.Height) return -1;
			return map.ToIndex(newCoord);
		}

		public static int GetTileOffsetToDirection(IMap map, int tileOffset) => TileProperties.GetOffsetDirection(tileOffset % map.Width, tileOffset / map.Width);
	}
}

//public static int GetTileOffsetToDirection(IMap map, int tileOffset)
//{
//	if (tileOffset == 1) return TileProperties.East;
//	if (tileOffset == -1) return TileProperties.West;
//	if (tileOffset == map.Width) return TileProperties.North;
//	if (tileOffset == -map.Width) return TileProperties.South;
//	return 0;
//}

//public static int GetDirectionFlag(Vector3 direction)
//{
//	if (direction.sqrMagnitude < 0.01f)
//		return 0;
//	var angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
//	if (Mathf.Abs(angle) <= 45f) return 1; // North (positive Z)
//	if (Mathf.Abs(angle - 180) <= 45f || Mathf.Abs(angle + 180) <= 45f) return 2; // South (negative Z)
//	if (Mathf.Abs(angle - 90) <= 45f) return 4; // East (positive X)
//	if (Mathf.Abs(angle + 90) <= 45f) return 8; // West (negative X)
//	return 0;
//}