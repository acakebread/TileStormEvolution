#define TILE_ROUTE_VISUAL_DEBUG

using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClassicTilestorm
{
#if TILE_ROUTE_VISUAL_DEBUG
	internal static class TileDebugVisualizer
	{
		public static bool Enabled => true;

		private static readonly Color PlayableTint = new(0.15f, 0.8f, 0.2f, 1f);
		private static readonly Color EndpointTint = new(0.2f, 0.35f, 1f, 1f);
		private static readonly Color NavPlayableTint = new(1f, 0.9f, 0.2f, 1f);
		private static readonly Color EliminatedTint = new(0.9f, 0.15f, 0.1f, 1f);
		private const int RouteNodeBudget = 50000;

		private readonly struct ConnectionProbe
		{
			public readonly int StaticTile;
			public readonly int PlayableTile;
			public readonly int Direction;
			public readonly bool Found;

			public ConnectionProbe(int staticTile, int playableTile, int direction, bool found)
			{
				StaticTile = staticTile;
				PlayableTile = playableTile;
				Direction = direction;
				Found = found;
			}
		}

		private readonly struct RouteCell
		{
			public readonly int Index;
			public readonly int Nav;

			public RouteCell(int index, int nav)
			{
				Index = index;
				Nav = nav;
			}
		}

		public static string Visualize(Map map, int sourceTile, int destinationTile)
		{
			if (map == null)
				return "Debug visualize skipped: no active map.";

			TileStripHelper.ClearSpareTile();
			DebugVisualizationHelper.ClearMapTints(map);

			var sourceProbes = ResolveSourceConnections(map, sourceTile);
			var sourceProbe = new ConnectionProbe(-1, -1, 0, false);
			var destinationProbe = new ConnectionProbe(-1, -1, 0, false);
			var playableArea = new bool[map.Count];

			foreach (var candidateSourceProbe in sourceProbes)
			{
				var candidateDestinationProbes = new List<ConnectionProbe>();
				var candidatePlayableArea = BuildPlayableArea(map, candidateSourceProbe.PlayableTile, candidateSourceProbe.StaticTile, candidateDestinationProbes);
				FilterReachableExits(map, candidatePlayableArea, candidateDestinationProbes, destinationTile);
				var candidateDestinationProbe = candidateDestinationProbes.Count > 0
					? candidateDestinationProbes[0]
					: new ConnectionProbe(-1, -1, 0, false);

				if (!sourceProbe.Found)
				{
					sourceProbe = candidateSourceProbe;
					playableArea = candidatePlayableArea;
					destinationProbe = candidateDestinationProbe;
				}

				if (candidateDestinationProbes.Count == 0)
					continue;

				sourceProbe = candidateSourceProbe;
				playableArea = candidatePlayableArea;
				destinationProbe = candidateDestinationProbe;
				break;
			}

			var sourceConnection = sourceProbe.Found ? sourceProbe.StaticTile : -1;
			var destinationConnection = destinationProbe.Found ? destinationProbe.StaticTile : -1;
			var rawPlayableCount = Count(playableArea);
			var rawNavPlayableCount = CountNavTiles(map, playableArea);
			var solutionArea = CullToReachableSolutionArea(
				map,
				playableArea,
				sourceProbe.PlayableTile,
				destinationProbe.PlayableTile,
				out var navTileBudget,
				out var culledTiles);
			var playableCount = 0;
			var navPlayableCount = 0;

			for (var i = 0; i < playableArea.Length; i++)
			{
				if (!playableArea[i])
					continue;

				if (!solutionArea[i])
				{
					DebugVisualizationHelper.TintTile(map.GetTile(i).gameObject, EliminatedTint);
					continue;
				}

				playableCount++;
				var tile = map.GetTile(i);
				if (tile.Nav != 0)
				{
					navPlayableCount++;
					DebugVisualizationHelper.TintTile(tile.gameObject, NavPlayableTint);
				}
				else
				{
					DebugVisualizationHelper.TintTile(tile.gameObject, PlayableTint);
				}
			}

			playableArea = solutionArea;
			var sourceResolved = TintEndpoint(map, sourceConnection);
			var destinationResolved = TintEndpoint(map, destinationConnection);

			var routeFound = TryFindSpatialRoute(
				map,
				playableArea,
				sourceProbe,
				destinationProbe,
				out var route,
				out var routeReason);

			var routeText = routeFound
				? $"spatial route {route.Count} tile(s)"
				: $"no route ({routeReason})";
			var sourceText = sourceProbe.Found ? $"{DescribeTile(map, sourceResolved)} -> {sourceProbe.PlayableTile}" : "<not found>";
			var destinationText = destinationProbe.Found ? $"{DescribeTile(map, destinationResolved)} <- {destinationProbe.PlayableTile}" : "<not found>";

			return $"Debug visualize applied: playable area {rawPlayableCount}->{playableCount} tile(s), culled {culledTiles}, nav-capable {rawNavPlayableCount}->{navPlayableCount}/{navTileBudget}, source candidates {sourceProbes.Count}, source {sourceText}, destination {destinationText}, {routeText}.";
		}

		private static bool TryFindSpatialRoute(
			Map map,
			bool[] playableArea,
			ConnectionProbe sourceProbe,
			ConnectionProbe destinationProbe,
			out List<RouteCell> route,
			out string reason)
		{
			route = new List<RouteCell>();
			reason = null;

			if (map == null || playableArea == null)
			{
				reason = "invalid map or playable area";
				return false;
			}

			if (!sourceProbe.Found || !destinationProbe.Found)
			{
				reason = "one or both blue anchors were not found";
				return false;
			}

			if (sourceProbe.PlayableTile < 0 || destinationProbe.PlayableTile < 0 ||
				sourceProbe.PlayableTile >= map.Count || destinationProbe.PlayableTile >= map.Count)
			{
				reason = "one or both playable entry tiles were invalid";
				return false;
			}

			var navCounts = CollectPlayableNavCounts(map, playableArea);
			if (CountNavTiles(navCounts) == 0)
			{
				reason = "no yellow nav-capable tiles in the flooded area";
				return false;
			}

			var visited = new bool[playableArea.Length];
			var nodes = 0;
			var destinationExitDirection = Navigation.GetOppositeDirection(destinationProbe.Direction);

			if (TryFindSpatialRoute(
				map,
				sourceProbe.PlayableTile,
				sourceProbe.Direction,
				destinationProbe.PlayableTile,
				destinationExitDirection,
				playableArea,
				navCounts,
				visited,
				route,
				ref nodes))
			{
				return true;
			}

			reason = $"no spatial route through the flooded cells using the yellow nav pool ({nodes} node(s))";
			return false;
		}

		private static bool TryFindSpatialRoute(
			Map map,
			int index,
			int incomingDirection,
			int destinationCell,
			int exitDirection,
			bool[] playableArea,
			Dictionary<int, int> navCounts,
			bool[] visited,
			List<RouteCell> route,
			ref int nodes)
		{
			if (index < 0 || index >= playableArea.Length || !playableArea[index] || visited[index])
				return false;

			if (++nodes > RouteNodeBudget)
				return false;

			visited[index] = true;

			foreach (var nav in OrderedNavMasks(navCounts, incomingDirection, index, destinationCell, map.Width, map.Height))
			{
				var outgoingDirections = GetOutgoingDirections(incomingDirection, nav);
				if (outgoingDirections == 0)
					continue;

				navCounts[nav]--;
				route.Add(new RouteCell(index, nav));

				if (index == destinationCell)
				{
					if ((outgoingDirections & exitDirection) != 0)
						return true;
				}

				foreach (var nextDirection in OrderedOutputDirections(outgoingDirections, index, destinationCell, map.Width, map.Height))
				{
					var next = GetAdjacentTile(index, nextDirection, map.Width, map.Height);
					if (TryFindSpatialRoute(
						map,
						next,
						nextDirection,
						destinationCell,
						exitDirection,
						playableArea,
						navCounts,
						visited,
						route,
						ref nodes))
					{
						return true;
					}
				}

				route.RemoveAt(route.Count - 1);
				navCounts[nav]++;
			}

			visited[index] = false;
			return false;
		}

		private static List<ConnectionProbe> ResolveSourceConnections(Map map, int origin)
		{
			var probes = new List<ConnectionProbe>();
			if (map == null || origin < 0 || origin >= map.Count)
				return probes;

			if (!IsStaticNav(map, origin))
				return probes;

			var queue = new Queue<int>();
			var visited = new HashSet<int>();
			queue.Enqueue(origin);

			while (queue.Count > 0 && visited.Count < RouteNodeBudget)
			{
				var current = queue.Dequeue();
				if (!visited.Add(current))
					continue;

				var currentTile = map.GetTile(current);
				foreach (var direction in Navigation.Directions)
				{
					if ((currentTile.Nav & direction) == 0)
						continue;

					var next = GetAdjacentTile(current, direction, map.Width, map.Height);
					if (next < 0)
						continue;

					if (IsPlayable(map, next))
					{
						AddUniqueProbe(probes, new ConnectionProbe(current, next, direction, true));
						continue;
					}

					if (HasStaticNavConnection(map, current, next, direction))
						queue.Enqueue(next);
				}
			}

			return probes;
		}

		private static ConnectionProbe ResolveDestinationConnection(Map map, int origin, bool[] playableArea)
		{
			if (map == null || origin < 0 || origin >= map.Count ||
				playableArea == null || playableArea.Length != map.Count)
			{
				return new ConnectionProbe(-1, -1, 0, false);
			}

			return TraceStaticTrackToPlayable(map, origin, playableArea);
		}

		private static ConnectionProbe TraceStaticTrackToPlayable(Map map, int origin, bool[] targetPlayableArea)
		{
			if (!IsStaticNav(map, origin))
				return new ConnectionProbe(-1, -1, 0, false);

			var queue = new Queue<int>();
			var visited = new HashSet<int>();
			queue.Enqueue(origin);

			while (queue.Count > 0 && visited.Count < RouteNodeBudget)
			{
				var current = queue.Dequeue();
				if (!visited.Add(current))
					continue;

				var currentTile = map.GetTile(current);
				foreach (var direction in Navigation.Directions)
				{
					if ((currentTile.Nav & direction) == 0)
						continue;

					var next = GetAdjacentTile(current, direction, map.Width, map.Height);
					if (next < 0)
						continue;

					if (targetPlayableArea != null ? targetPlayableArea[next] : IsPlayable(map, next))
						return new ConnectionProbe(current, next, direction, true);

					if (HasStaticNavConnection(map, current, next, direction))
						queue.Enqueue(next);
				}
			}

			return new ConnectionProbe(-1, -1, 0, false);
		}

		private static void AddUniqueProbe(List<ConnectionProbe> probes, ConnectionProbe probe)
		{
			foreach (var existing in probes)
			{
				if (existing.StaticTile == probe.StaticTile &&
					existing.PlayableTile == probe.PlayableTile &&
					existing.Direction == probe.Direction)
				{
					return;
				}
			}

			probes.Add(probe);
		}

		private static bool HasStaticNavConnection(Map map, int current, int next, int direction)
		{
			if (!IsStaticNav(map, current) || !IsStaticNav(map, next))
				return false;

			var currentNav = map.GetTile(current).Nav;
			var nextNav = map.GetTile(next).Nav;
			return (currentNav & direction) != 0 &&
				(nextNav & Navigation.GetOppositeDirection(direction)) != 0;
		}

		private static Dictionary<int, int> CollectPlayableNavCounts(Map map, bool[] playableArea)
		{
			var counts = new Dictionary<int, int>();

			for (var index = 0; index < playableArea.Length; index++)
			{
				if (!playableArea[index])
					continue;

				var nav = map.GetTile(index).Nav;
				if (nav == 0)
					continue;

				if (!counts.ContainsKey(nav))
					counts[nav] = 0;

				counts[nav]++;
			}

			return counts;
		}

		private static int CountNavTiles(Dictionary<int, int> navCounts)
		{
			var count = 0;
			foreach (var pair in navCounts)
				count += pair.Value;
			return count;
		}

		private static bool[] CullToReachableSolutionArea(
			Map map,
			bool[] playableArea,
			int sourceEntry,
			int destinationEntry,
			out int navTileBudget,
			out int culledTiles)
		{
			navTileBudget = CountNavTiles(map, playableArea);
			var reusableCrossroads = CountReusableCrossroads(map, playableArea);
			culledTiles = 0;

			if (map == null || playableArea == null || playableArea.Length != map.Count ||
				sourceEntry < 0 || destinationEntry < 0 ||
				sourceEntry >= playableArea.Length || destinationEntry >= playableArea.Length)
			{
				return playableArea ?? Array.Empty<bool>();
			}

			var solutionArea = (bool[])playableArea.Clone();
			var previousCount = -1;

			while (previousCount != Count(solutionArea))
			{
				previousCount = Count(solutionArea);

				var sourceDistance = BuildDistanceMap(map, solutionArea, sourceEntry);
				var destinationDistance = BuildDistanceMap(map, solutionArea, destinationEntry);

				for (var i = 0; i < solutionArea.Length; i++)
				{
					if (!solutionArea[i])
						continue;

					var canReachBothEnds = sourceDistance[i] >= 0 && destinationDistance[i] >= 0;
					var fillValue = canReachBothEnds
						? AdjustRouteLengthForReusableCrossroads(sourceDistance[i], destinationDistance[i], reusableCrossroads)
						: int.MaxValue;

					if (canReachBothEnds && fillValue <= navTileBudget)
						continue;

					solutionArea[i] = false;
					culledTiles++;
				}
			}

			return solutionArea;
		}

		private static int AdjustRouteLengthForReusableCrossroads(int sourceDistance, int destinationDistance, int reusableCrossroads)
		{
			var routeLength = sourceDistance + destinationDistance + 1;
			if (reusableCrossroads <= 0 || sourceDistance <= 0 || destinationDistance <= 0)
				return routeLength;

			var doubleChargeCredit = Math.Min(reusableCrossroads, Math.Min(sourceDistance, destinationDistance));
			return routeLength - doubleChargeCredit;
		}

		private static int CountNavTiles(Map map, bool[] area)
		{
			var count = 0;
			if (map == null || area == null)
				return count;

			for (var i = 0; i < area.Length; i++)
			{
				if (area[i] && map.GetTile(i).Nav != 0)
					count++;
			}

			return count;
		}

		private static int CountReusableCrossroads(Map map, bool[] area)
		{
			var count = 0;
			if (map == null || area == null)
				return count;

			for (var i = 0; i < area.Length; i++)
			{
				var tile = map.GetTile(i);
				if (area[i] && !tile.IsBake && tile.Nav == (int)DefinitionFlags.DirMask)
					count++;
			}

			return count;
		}

		private static int Count(bool[] area)
		{
			if (area == null)
				return 0;

			var count = 0;
			for (var i = 0; i < area.Length; i++)
				if (area[i])
					count++;
			return count;
		}

		private static int[] BuildDistanceMap(Map map, bool[] area, int source)
		{
			var distance = new int[area.Length];
			Array.Fill(distance, -1);

			if (map == null || source < 0 || source >= area.Length || !area[source])
				return distance;

			var queue = new LinkedList<int>();
			distance[source] = 0;
			queue.AddLast(source);

			while (queue.Count > 0)
			{
				var current = queue.First.Value;
				queue.RemoveFirst();

				foreach (var direction in Navigation.Directions)
				{
					var next = GetAdjacentTile(current, direction, map.Width, map.Height);
					if (next < 0 || !TryGetFillStepCost(map, area, current, next, direction, out var stepCost))
						continue;

					var nextDistance = distance[current] + stepCost;
					if (distance[next] >= 0 && distance[next] <= nextDistance)
						continue;

					distance[next] = nextDistance;
					if (stepCost == 0)
						queue.AddFirst(next);
					else
						queue.AddLast(next);
				}
			}

			return distance;
		}

		private static bool TryGetFillStepCost(Map map, bool[] playableArea, int current, int next, int direction, out int stepCost)
		{
			stepCost = 0;

			var currentPlayable = current >= 0 && current < playableArea.Length && playableArea[current];
			var nextPlayable = next >= 0 && next < playableArea.Length && playableArea[next];
			var currentStatic = IsStaticNav(map, current);
			var nextStatic = IsStaticNav(map, next);

			if (nextPlayable)
			{
				if (currentPlayable)
				{
					stepCost = 1;
					return true;
				}

				if (currentStatic && (map.GetTile(current).Nav & direction) != 0)
				{
					stepCost = 1;
					return true;
				}
			}

			if (!nextStatic)
				return false;

			if (currentPlayable && (map.GetTile(next).Nav & Navigation.GetOppositeDirection(direction)) != 0)
				return true;

			return currentStatic && HasStaticNavConnection(map, current, next, direction);
		}

		private static List<int> OrderedNavMasks(
			Dictionary<int, int> navCounts,
			int incomingDirection,
			int index,
			int destinationCell,
			int width,
			int height)
		{
			var masks = new List<int>();
			foreach (var pair in navCounts)
			{
				if (pair.Value > 0 && GetOutgoingDirections(incomingDirection, pair.Key) != 0)
					masks.Add(pair.Key);
			}

			masks.Sort((a, b) =>
			{
				var distA = BestOutputDistance(index, GetOutgoingDirections(incomingDirection, a), destinationCell, width, height);
				var distB = BestOutputDistance(index, GetOutgoingDirections(incomingDirection, b), destinationCell, width, height);
				return distA.CompareTo(distB);
			});

			return masks;
		}

		private static List<int> OrderedOutputDirections(
			int directions,
			int index,
			int destinationCell,
			int width,
			int height)
		{
			var result = new List<int>();
			foreach (var direction in Navigation.Directions)
			{
				if ((directions & direction) != 0)
					result.Add(direction);
			}

			result.Sort((a, b) =>
			{
				var nextA = GetAdjacentTile(index, a, width, height);
				var nextB = GetAdjacentTile(index, b, width, height);
				var distA = nextA < 0 ? int.MaxValue : Manhattan(nextA, destinationCell, width);
				var distB = nextB < 0 ? int.MaxValue : Manhattan(nextB, destinationCell, width);
				return distA.CompareTo(distB);
			});

			return result;
		}

		private static int BestOutputDistance(int index, int directions, int destinationCell, int width, int height)
		{
			var best = int.MaxValue;
			foreach (var direction in Navigation.Directions)
			{
				if ((directions & direction) == 0)
					continue;

				var next = GetAdjacentTile(index, direction, width, height);
				if (next >= 0)
					best = Math.Min(best, Manhattan(next, destinationCell, width));
			}

			return best;
		}

		private static int GetOutgoingDirections(int incomingDirection, int nav)
			=> Navigation.CalculateNav(incomingDirection, nav);

		private static int Manhattan(int a, int b, int width)
		{
			var ax = a % width;
			var ay = a / width;
			var bx = b % width;
			var by = b / width;
			return Math.Abs(ax - bx) + Math.Abs(ay - by);
		}

		private static int TintEndpoint(Map map, int tileIndex)
		{
			if (map == null || tileIndex < 0 || tileIndex >= map.Count)
				return -1;

			var tile = map.GetTile(tileIndex);
			if (tile.gameObject == null)
				return -1;

			DebugVisualizationHelper.TintTile(tile.gameObject, EndpointTint);
			return tileIndex;
		}

		private static string DescribeTile(Map map, int index)
		{
			if (map == null || index < 0 || index >= map.Count)
				return "<none>";

			var tile = map.GetTile(index);
			var tileName = tile.gameObject != null ? tile.gameObject.name : "missing";
			return $"{index} ({tileName})";
		}

		private static bool[] BuildPlayableArea(Map map, int sourceTile, int ignoredStaticExit, List<ConnectionProbe> exits)
		{
			var area = new bool[map.Count];
			var queue = new Queue<int>();

			TrySeedPlayable(map, sourceTile, area, queue);

			while (queue.Count > 0)
			{
				var current = queue.Dequeue();
				foreach (var direction in Navigation.Directions)
				{
					var next = GetAdjacentTile(current, direction, map.Width, map.Height);
					if (next < 0 || next >= map.Count || area[next])
						continue;

					if (IsPlayable(map, next))
					{
						area[next] = true;
						queue.Enqueue(next);
						continue;
					}

					if (next == ignoredStaticExit || !IsStaticNav(map, next))
						continue;

					var staticToPlayableDirection = Navigation.GetOppositeDirection(direction);
					if ((map.GetTile(next).Nav & staticToPlayableDirection) != 0)
						AddUniqueProbe(exits, new ConnectionProbe(next, current, staticToPlayableDirection, true));
				}
			}

			return area;
		}

		private static void FilterReachableExits(Map map, bool[] playableArea, List<ConnectionProbe> exits, int destinationTile)
		{
			if (map == null || playableArea == null || exits == null)
				return;

			for (var i = exits.Count - 1; i >= 0; i--)
			{
				if (!ExitCanReachDestinationOrNewIsland(map, playableArea, exits[i], destinationTile))
					exits.RemoveAt(i);
			}
		}

		private static bool ExitCanReachDestinationOrNewIsland(Map map, bool[] playableArea, ConnectionProbe exit, int destinationTile)
		{
			if (exit.StaticTile == destinationTile)
				return true;

			var queue = new Queue<(int Tile, int IncomingDirection)>();
			var visited = new HashSet<int>();
			var initialIncoming = Navigation.GetOppositeDirection(exit.Direction);
			queue.Enqueue((exit.StaticTile, initialIncoming));

			while (queue.Count > 0 && visited.Count < RouteNodeBudget)
			{
				var currentState = queue.Dequeue();
				var visitKey = currentState.Tile * 16 + currentState.IncomingDirection;
				if (!visited.Add(visitKey))
					continue;

				var outgoing = Navigation.CalculateNav(currentState.IncomingDirection, map.GetTile(currentState.Tile).Nav);
				if (outgoing == 0)
					continue;

				var next = GetAdjacentTile(currentState.Tile, outgoing, map.Width, map.Height);
				if (next < 0 || next >= map.Count)
					continue;

				if (next == destinationTile)
					return true;

				if (IsPlayable(map, next))
				{
					if (!playableArea[next])
						return true;

					continue;
				}

				if (!IsStaticNav(map, next))
					continue;

				queue.Enqueue((next, outgoing));
			}

			return false;
		}

		private static void TrySeedPlayable(Map map, int index, bool[] area, Queue<int> queue)
		{
			if (index < 0 || index >= map.Count || area[index] || !IsPlayable(map, index))
				return;

			area[index] = true;
			queue.Enqueue(index);
		}

		private static bool IsPlayable(Map map, int index)
		{
			if (map == null || index < 0 || index >= map.Count)
				return false;

			var tile = map.GetTile(index);
			return tile.gameObject != null && !tile.IsBake;
		}

		private static bool IsStaticNav(Map map, int index)
		{
			if (map == null || index < 0 || index >= map.Count)
				return false;

			var tile = map.GetTile(index);
			return tile.gameObject != null && tile.IsBake && tile.Nav != 0;
		}

		private static int GetAdjacentTile(int index, int direction, int width, int height)
		{
			var dx = ((direction & 4) >> 2) - ((direction & 8) >> 3);
			var dy = ((direction & 1) >> 0) - ((direction & 2) >> 1);

			var x = (index % width) + dx;
			var y = (index / width) + dy;

			if (x < 0 || x >= width || y < 0 || y >= height)
				return -1;

			return y * width + x;
		}
	}
#else
	internal static class TileDebugVisualizer
	{
		public static bool Enabled => false;

		public static string Visualize(Map map, int sourceTile, int destinationTile)
			=> "Visual debugging is disabled.";
	}
#endif
}
