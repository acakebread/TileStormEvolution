using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClassicTilestorm
{
	internal static class TileRouteAssembler
	{
		private const int NodeBudget = 100000;

		public sealed class Result
		{
			public int[] State;
			public string Summary;
		}

		private readonly struct TileRule
		{
			public readonly bool Static;
			public readonly int Nav;

			public TileRule(bool isStatic, int nav)
			{
				Static = isStatic;
				Nav = nav;
			}
		}

		private struct CellBits
		{
			public bool Static;
			public int Nav;
		}

		private readonly struct Anchor
		{
			public readonly int StaticCell;
			public readonly int PlayableCell;
			public readonly int Direction;

			public Anchor(int staticCell, int playableCell, int direction)
			{
				StaticCell = staticCell;
				PlayableCell = playableCell;
				Direction = direction;
			}
		}

		private readonly struct TraceState
		{
			public readonly int Cell;

			public TraceState(int cell)
			{
				Cell = cell;
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

		public static bool TryAssemble(Map map, int sourceTile, int destinationTile, out Result result)
		{
			result = null;

			if (map == null || map.State == null || sourceTile < 0 || destinationTile < 0 ||
				sourceTile >= map.Count || destinationTile >= map.Count)
			{
				return false;
			}

			var state = (int[])map.State.Clone();
			var rules = BuildRules(map);
			var baseGrid = BuildScratchGrid(state, rules);
			var sourceAnchors = FindAnchors(baseGrid, sourceTile, null, map.Width, map.Height);
			var attempts = 0;
			var lastSummary = "no source anchors.";

			foreach (var sourceAnchor in sourceAnchors)
			{
				var rawPlayable = FloodPlayableArea(baseGrid, sourceAnchor.PlayableCell, map.Width, map.Height);
				var navTiles = CollectMovableNavTiles(state, rawPlayable, rules);
				var navBudget = CountNavTiles(navTiles);
				var reusableCrossroads = CountReusableCrossroads(navTiles);
				var rawCount = Count(rawPlayable);
				var destinationAnchors = FindAnchors(baseGrid, destinationTile, rawPlayable, map.Width, map.Height);

				foreach (var destinationAnchor in destinationAnchors)
				{
					attempts++;
					var solutionArea = CullSolutionArea(
						baseGrid,
						rawPlayable,
						sourceAnchor.PlayableCell,
						destinationAnchor.PlayableCell,
						navBudget,
						reusableCrossroads,
						map.Width,
						map.Height);

					var searchGrid = (CellBits[])baseGrid.Clone();
					var route = new List<RouteCell>();
					var counts = CloneCounts(navTiles);
					var visited = new bool[state.Length];
					var navVisits = new HashSet<int>();
					var nodes = 0;
					lastSummary = $"source {sourceAnchor.StaticCell}->{sourceAnchor.PlayableCell}, destination {destinationAnchor.StaticCell}<-{destinationAnchor.PlayableCell}, area {rawCount}->{Count(solutionArea)}, pool {DescribeNavTiles(navTiles)}, nodes {nodes}.";

					if (!solutionArea[sourceAnchor.PlayableCell] || !solutionArea[destinationAnchor.PlayableCell])
					{
						lastSummary += " endpoint was culled.";
						continue;
					}

					if (!TryExtendRoute(
						searchGrid,
						sourceAnchor.PlayableCell,
						sourceAnchor.Direction,
						destinationAnchor.PlayableCell,
						Navigation.GetOppositeDirection(destinationAnchor.Direction),
						solutionArea,
						counts,
						visited,
						navVisits,
						route,
						map.Width,
						map.Height,
						ref nodes))
					{
						lastSummary = $"source {sourceAnchor.StaticCell}->{sourceAnchor.PlayableCell}, destination {destinationAnchor.StaticCell}<-{destinationAnchor.PlayableCell}, area {rawCount}->{Count(solutionArea)}, pool {DescribeNavTiles(navTiles)}, nodes {nodes}, no compatible chain.";
						continue;
					}

					if (!TryBuildState(state, rawPlayable, route, navTiles, out var assembledState))
					{
						lastSummary = $"found route of {route.Count} tile(s), but failed to build state.";
						continue;
					}

					if (!HasFlexibleRouteToDest(assembledState, sourceTile, destinationTile, rules, map.Width, map.Height))
					{
						lastSummary = $"built state from route of {route.Count} tile(s), but final route did not validate.";
						continue;
					}

					result = new Result
					{
						State = assembledState,
						Summary = $"assembled route with {route.Count} movable tile(s), area {rawCount}->{Count(solutionArea)}, pool {DescribeNavTiles(navTiles)}, anchors {sourceAnchor.StaticCell}->{sourceAnchor.PlayableCell} and {destinationAnchor.StaticCell}<-{destinationAnchor.PlayableCell}, nodes {nodes}."
					};
					return true;
				}

				if (destinationAnchors.Count == 0)
					lastSummary = $"source {sourceAnchor.StaticCell}->{sourceAnchor.PlayableCell}, area {rawCount}, pool {DescribeNavTiles(navTiles)}, no destination anchor in raw flood.";
			}

			result = new Result
			{
				State = null,
				Summary = $"no route assembled after {attempts} anchor attempt(s); last attempt: {lastSummary}"
			};
			return false;
		}

		private static TileRule[] BuildRules(Map map)
		{
			var rules = new TileRule[map.Count];

			for (var i = 0; i < rules.Length; i++)
			{
				var variantIndex = map.tiles[i];
				var variant = variantIndex >= 0 && map.variants != null && variantIndex < map.variants.Length
					? map.variants[variantIndex]
					: default;

				var definition = ResourceManager.ResolveDefinition(variant.hash, out _);
				var baseFlags = ((IFlagAccess)definition)?.Flags ?? 0;
				var rotatedNav = Navigation.Rotate(
					baseFlags & (int)DefinitionFlags.DirMask,
					Mathf.RoundToInt(variant.angle));

				rules[i] = new TileRule(definition?.Bake ?? true, rotatedNav);
			}

			return rules;
		}

		private static CellBits[] BuildScratchGrid(int[] state, TileRule[] rules)
		{
			var grid = new CellBits[state.Length];
			for (var i = 0; i < state.Length; i++)
			{
				if (!TryGetRule(state, i, rules, out var rule))
					continue;

				grid[i] = new CellBits
				{
					Static = rule.Static,
					Nav = rule.Static ? rule.Nav : 0
				};
			}

			return grid;
		}

		private static List<Anchor> FindAnchors(
			CellBits[] grid,
			int origin,
			bool[] targetPlayableArea,
			int width,
			int height)
		{
			var anchors = new List<Anchor>();
			if (origin < 0 || origin >= grid.Length)
				return anchors;

			if (!IsStaticNavAt(grid, origin))
				return anchors;

			var queue = new Queue<TraceState>();
			var visited = new HashSet<int>();
			queue.Enqueue(new TraceState(origin));

			while (queue.Count > 0 && visited.Count < NodeBudget)
			{
				var current = queue.Dequeue().Cell;
				if (!visited.Add(current))
					continue;

				var currentNav = grid[current].Nav;
				foreach (var direction in Navigation.Directions)
				{
					if ((currentNav & direction) == 0)
						continue;

					var next = GetAdjacentTile(current, direction, width, height);
					if (next < 0)
						continue;

					if (IsPlayableAt(grid, next))
					{
						if (targetPlayableArea == null || targetPlayableArea[next])
							AddAnchor(anchors, new Anchor(current, next, direction));
						continue;
					}

					if (HasStaticConnection(grid, current, next, direction))
						queue.Enqueue(new TraceState(next));
				}
			}

			return anchors;
		}

		private static void AddAnchor(List<Anchor> anchors, Anchor anchor)
		{
			foreach (var existing in anchors)
			{
				if (existing.StaticCell == anchor.StaticCell &&
					existing.PlayableCell == anchor.PlayableCell &&
					existing.Direction == anchor.Direction)
				{
					return;
				}
			}

			anchors.Add(anchor);
		}

		private static bool[] FloodPlayableArea(CellBits[] grid, int source, int width, int height)
		{
			var area = new bool[grid.Length];
			if (source < 0 || source >= grid.Length || !IsPlayableAt(grid, source))
				return area;

			var queue = new Queue<int>();
			area[source] = true;
			queue.Enqueue(source);

			while (queue.Count > 0)
			{
				var current = queue.Dequeue();
				foreach (var direction in Navigation.Directions)
				{
					var next = GetAdjacentTile(current, direction, width, height);
					if (next < 0 || area[next] || !IsPlayableAt(grid, next))
						continue;

					area[next] = true;
					queue.Enqueue(next);
				}
			}

			return area;
		}

		private static Dictionary<int, Queue<int>> CollectMovableNavTiles(int[] state, bool[] rawPlayable, TileRule[] rules)
		{
			var result = new Dictionary<int, Queue<int>>();
			for (var i = 0; i < rawPlayable.Length; i++)
			{
				if (!rawPlayable[i])
					continue;

				var logicalIndex = state[i];
				if (logicalIndex < 0 || logicalIndex >= rules.Length)
					continue;

				var nav = rules[logicalIndex].Nav & (int)DefinitionFlags.DirMask;
				if (nav == 0)
					continue;

				if (!result.TryGetValue(nav, out var queue))
				{
					queue = new Queue<int>();
					result[nav] = queue;
				}

				queue.Enqueue(logicalIndex);
			}

			return result;
		}

		private static bool[] CullSolutionArea(
			CellBits[] grid,
			bool[] rawPlayable,
			int sourceEntry,
			int destinationEntry,
			int navBudget,
			int reusableCrossroads,
			int width,
			int height)
		{
			var solutionArea = (bool[])rawPlayable.Clone();
			if (navBudget <= 0)
				return solutionArea;

			var previousCount = -1;
			while (previousCount != Count(solutionArea))
			{
				previousCount = Count(solutionArea);
				var sourceDistance = BuildDistanceMap(grid, solutionArea, sourceEntry, width, height);
				var destinationDistance = BuildDistanceMap(grid, solutionArea, destinationEntry, width, height);

				for (var i = 0; i < solutionArea.Length; i++)
				{
					if (!solutionArea[i])
						continue;

					var reachable = sourceDistance[i] >= 0 && destinationDistance[i] >= 0;
					var routeLength = reachable
						? AdjustRouteLengthForReusableCrossroads(sourceDistance[i], destinationDistance[i], reusableCrossroads)
						: int.MaxValue;
					if (reachable && routeLength <= navBudget)
						continue;

					solutionArea[i] = false;
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

		private static int[] BuildDistanceMap(CellBits[] grid, bool[] solutionArea, int source, int width, int height)
		{
			var distance = new int[solutionArea.Length];
			Array.Fill(distance, -1);

			if (source < 0 || source >= solutionArea.Length || !solutionArea[source])
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
					var next = GetAdjacentTile(current, direction, width, height);
					if (next < 0 || !TryGetFillStepCost(grid, solutionArea, current, next, direction, out var cost))
						continue;

					var nextDistance = distance[current] + cost;
					if (distance[next] >= 0 && distance[next] <= nextDistance)
						continue;

					distance[next] = nextDistance;
					if (cost == 0)
						queue.AddFirst(next);
					else
						queue.AddLast(next);
				}
			}

			return distance;
		}

		private static bool TryGetFillStepCost(CellBits[] grid, bool[] solutionArea, int current, int next, int direction, out int cost)
		{
			cost = 0;
			var currentPlayable = current >= 0 && current < solutionArea.Length && solutionArea[current];
			var nextPlayable = next >= 0 && next < solutionArea.Length && solutionArea[next];
			var currentStatic = IsStaticNavAt(grid, current);
			var nextStatic = IsStaticNavAt(grid, next);

			if (nextPlayable)
			{
				if (currentPlayable)
				{
					cost = 1;
					return true;
				}

				if (currentStatic && (grid[current].Nav & direction) != 0)
				{
					cost = 1;
					return true;
				}
			}

			if (!nextStatic)
				return false;

			if (currentPlayable && (grid[next].Nav & Navigation.GetOppositeDirection(direction)) != 0)
				return true;

			return currentStatic && HasStaticConnection(grid, current, next, direction);
		}

		private static bool TryExtendRoute(
			CellBits[] grid,
			int current,
			int incomingDirection,
			int destinationCell,
			int exitDirection,
			bool[] solutionArea,
			Dictionary<int, int> navCounts,
			bool[] visited,
			HashSet<int> navVisits,
			List<RouteCell> route,
			int width,
			int height,
			ref int nodes)
		{
			if (current < 0 || current >= solutionArea.Length)
				return false;

			if (grid[current].Nav != 0)
			{
				var visitKey = current * 16 + incomingDirection;
				if (!navVisits.Add(visitKey))
					return false;

				var outgoing = GetOutgoingDirections(incomingDirection, grid[current].Nav);
				if (outgoing == 0)
				{
					navVisits.Remove(visitKey);
					return false;
				}

				if (++nodes > NodeBudget)
				{
					navVisits.Remove(visitKey);
					return false;
				}

				if (current == destinationCell && (outgoing & exitDirection) != 0)
					return true;

				foreach (var direction in OrderedOutputDirections(outgoing, current, destinationCell, width, height))
				{
					if (TryExtendRoute(grid, GetAdjacentTile(current, direction, width, height), direction, destinationCell, exitDirection, solutionArea, navCounts, visited, navVisits, route, width, height, ref nodes))
						return true;
				}

				navVisits.Remove(visitKey);
				return false;
			}

			if (!solutionArea[current] || visited[current])
				return false;

			foreach (var nav in OrderedAvailableNavMasks(navCounts, incomingDirection, current, destinationCell, width, height))
			{
				var outgoing = GetOutgoingDirections(incomingDirection, nav);
				if (outgoing == 0)
					continue;

				navCounts[nav]--;
				var previousNav = grid[current].Nav;
				grid[current].Nav = nav;
				visited[current] = true;
				route.Add(new RouteCell(current, nav));

				if (current == destinationCell)
				{
					if ((outgoing & exitDirection) != 0)
						return true;
				}
				else
				{
					foreach (var direction in OrderedOutputDirections(outgoing, current, destinationCell, width, height))
					{
						var next = GetAdjacentTile(current, direction, width, height);
						if (!CanAcceptIncoming(grid, next, direction, destinationCell, exitDirection, solutionArea, navCounts))
							continue;

						if (TryExtendRoute(grid, next, direction, destinationCell, exitDirection, solutionArea, navCounts, visited, navVisits, route, width, height, ref nodes))
							return true;
					}
				}

				route.RemoveAt(route.Count - 1);
				visited[current] = false;
				grid[current].Nav = previousNav;
				navCounts[nav]++;
			}
			return false;
		}

		private static bool CanAcceptIncoming(
			CellBits[] grid,
			int cell,
			int incomingDirection,
			int destinationCell,
			int exitDirection,
			bool[] solutionArea,
			Dictionary<int, int> navCounts)
		{
			if (cell < 0 || cell >= solutionArea.Length)
				return false;

			if (grid[cell].Nav != 0)
			{
				var outgoing = GetOutgoingDirections(incomingDirection, grid[cell].Nav);
				return cell == destinationCell ? (outgoing & exitDirection) != 0 : outgoing != 0;
			}

			if (!solutionArea[cell])
				return false;

			foreach (var pair in navCounts)
			{
				if (pair.Value <= 0)
					continue;

				var outgoing = GetOutgoingDirections(incomingDirection, pair.Key);
				if (outgoing == 0)
					continue;

				if (cell != destinationCell || (outgoing & exitDirection) != 0)
					return true;
			}

			return false;
		}

		private static bool TryBuildState(int[] state, bool[] rawPlayable, List<RouteCell> route, Dictionary<int, Queue<int>> navTiles, out int[] assembledState)
		{
			assembledState = (int[])state.Clone();
			var used = new HashSet<int>();
			var routeCells = new HashSet<int>();
			var navQueues = new Dictionary<int, Queue<int>>();

			foreach (var pair in navTiles)
				navQueues[pair.Key] = new Queue<int>(pair.Value);

			foreach (var cell in route)
			{
				routeCells.Add(cell.Index);
				if (!navQueues.TryGetValue(cell.Nav, out var queue) || queue.Count == 0)
					return false;

				var logical = queue.Dequeue();
				assembledState[cell.Index] = logical;
				used.Add(logical);
			}

			var remaining = new Queue<int>();
			for (var i = 0; i < rawPlayable.Length; i++)
			{
				if (!rawPlayable[i] || used.Contains(state[i]))
					continue;

				remaining.Enqueue(state[i]);
			}

			for (var i = 0; i < rawPlayable.Length; i++)
			{
				if (!rawPlayable[i] || routeCells.Contains(i) || remaining.Count == 0)
					continue;

				assembledState[i] = remaining.Dequeue();
			}

			return true;
		}

		private static Dictionary<int, int> CloneCounts(Dictionary<int, Queue<int>> navTiles)
		{
			var counts = new Dictionary<int, int>();
			foreach (var pair in navTiles)
				counts[pair.Key] = pair.Value.Count;
			return counts;
		}

		private static List<int> OrderedAvailableNavMasks(Dictionary<int, int> navCounts, int incomingDirection, int current, int destinationCell, int width, int height)
		{
			var masks = new List<int>();
			foreach (var pair in navCounts)
			{
				if (pair.Value > 0 && GetOutgoingDirections(incomingDirection, pair.Key) != 0)
					masks.Add(pair.Key);
			}

			masks.Sort((a, b) =>
			{
				var distA = BestOutputDistance(current, GetOutgoingDirections(incomingDirection, a), destinationCell, width, height);
				var distB = BestOutputDistance(current, GetOutgoingDirections(incomingDirection, b), destinationCell, width, height);
				return distA.CompareTo(distB);
			});
			return masks;
		}

		private static List<int> OrderedOutputDirections(int directions, int current, int destinationCell, int width, int height)
		{
			var result = new List<int>();
			foreach (var direction in Navigation.Directions)
			{
				if ((directions & direction) != 0)
					result.Add(direction);
			}

			result.Sort((a, b) =>
			{
				var nextA = GetAdjacentTile(current, a, width, height);
				var nextB = GetAdjacentTile(current, b, width, height);
				var distA = nextA < 0 ? int.MaxValue : Manhattan(nextA, destinationCell, width);
				var distB = nextB < 0 ? int.MaxValue : Manhattan(nextB, destinationCell, width);
				return distA.CompareTo(distB);
			});
			return result;
		}

		private static int BestOutputDistance(int current, int directions, int destinationCell, int width, int height)
		{
			var best = int.MaxValue;
			foreach (var direction in Navigation.Directions)
			{
				if ((directions & direction) == 0)
					continue;

				var next = GetAdjacentTile(current, direction, width, height);
				if (next >= 0)
					best = Math.Min(best, Manhattan(next, destinationCell, width));
			}

			return best;
		}

		private static int GetOutgoingDirections(int incomingDirection, int nav)
			=> Navigation.CalculateNav(incomingDirection, nav);

		private static bool HasFlexibleRouteToDest(int[] state, int sourceTile, int destinationTile, TileRule[] rules, int width, int height)
		{
			if (sourceTile == destinationTile || sourceTile < 0 || destinationTile < 0)
				return false;

			var sourceNav = GetRuleAt(state, sourceTile, rules).Nav;
			foreach (var direction in Navigation.Directions)
			{
				if ((sourceNav & direction) == 0)
					continue;

				var visited = new HashSet<int>();
				if (TryFollowFlexibleRoute(state, sourceTile, direction, destinationTile, rules, width, height, visited))
					return true;
			}

			return false;
		}

		private static bool TryFollowFlexibleRoute(int[] state, int current, int direction, int destinationTile, TileRule[] rules, int width, int height, HashSet<int> visited)
		{
			var next = GetAdjacentTile(current, direction, width, height);
			if (next < 0)
				return false;

			var nextNav = GetRuleAt(state, next, rules).Nav;
			if ((nextNav & Navigation.GetOppositeDirection(direction)) == 0)
				return false;

			if (next == destinationTile)
				return true;

			var key = next * 16 + direction;
			if (!visited.Add(key))
				return false;

			var outgoing = GetOutgoingDirections(direction, nextNav);
			foreach (var nextDirection in Navigation.Directions)
			{
				if ((outgoing & nextDirection) == 0)
					continue;

				if (TryFollowFlexibleRoute(state, next, nextDirection, destinationTile, rules, width, height, visited))
					return true;
			}

			return false;
		}

		private static bool IsPlayableAt(CellBits[] grid, int cell)
			=> grid != null && cell >= 0 && cell < grid.Length && !grid[cell].Static;

		private static bool IsStaticNavAt(CellBits[] grid, int cell)
			=> grid != null && cell >= 0 && cell < grid.Length && grid[cell].Static && grid[cell].Nav != 0;

		private static bool HasStaticConnection(CellBits[] grid, int current, int next, int direction)
		{
			if (!IsStaticNavAt(grid, current) || !IsStaticNavAt(grid, next))
				return false;

			return (grid[current].Nav & direction) != 0 &&
				(grid[next].Nav & Navigation.GetOppositeDirection(direction)) != 0;
		}

		private static bool IsPlayableAt(int[] state, int cell, TileRule[] rules)
			=> TryGetRule(state, cell, rules, out var rule) && !rule.Static;

		private static bool IsStaticNavAt(int[] state, int cell, TileRule[] rules)
			=> TryGetRule(state, cell, rules, out var rule) && rule.Static && rule.Nav != 0;

		private static bool HasStaticConnection(int[] state, int current, int next, int direction, TileRule[] rules)
		{
			if (!IsStaticNavAt(state, current, rules) || !IsStaticNavAt(state, next, rules))
				return false;

			var currentNav = GetRuleAt(state, current, rules).Nav;
			var nextNav = GetRuleAt(state, next, rules).Nav;
			return (currentNav & direction) != 0 &&
				(nextNav & Navigation.GetOppositeDirection(direction)) != 0;
		}

		private static bool TryGetRule(int[] state, int cell, TileRule[] rules, out TileRule rule)
		{
			rule = default;
			if (state == null || rules == null || cell < 0 || cell >= state.Length)
				return false;

			var logical = state[cell];
			if (logical < 0 || logical >= rules.Length)
				return false;

			rule = rules[logical];
			return true;
		}

		private static TileRule GetRuleAt(int[] state, int cell, TileRule[] rules)
			=> TryGetRule(state, cell, rules, out var rule) ? rule : default;

		private static int CountNavTiles(Dictionary<int, Queue<int>> navTiles)
		{
			var count = 0;
			foreach (var pair in navTiles)
				count += pair.Value.Count;
			return count;
		}

		private static int CountReusableCrossroads(Dictionary<int, Queue<int>> navTiles)
			=> navTiles != null && navTiles.TryGetValue((int)DefinitionFlags.DirMask, out var crossroads) ? crossroads.Count : 0;

		private static int Count(bool[] area)
		{
			var count = 0;
			if (area == null)
				return 0;

			for (var i = 0; i < area.Length; i++)
				if (area[i])
					count++;
			return count;
		}

		private static string DescribeNavTiles(Dictionary<int, Queue<int>> navTiles)
		{
			if (navTiles == null || navTiles.Count == 0)
				return "<none>";

			var parts = new List<string>();
			foreach (var pair in navTiles)
				parts.Add($"{DescribeNav(pair.Key)}x{pair.Value.Count}");

			parts.Sort(StringComparer.Ordinal);
			return string.Join(",", parts);
		}

		private static string DescribeNav(int nav)
		{
			var result = "";
			if ((nav & (int)DefinitionFlags.North) != 0) result += "N";
			if ((nav & (int)DefinitionFlags.South) != 0) result += "S";
			if ((nav & (int)DefinitionFlags.East) != 0) result += "E";
			if ((nav & (int)DefinitionFlags.West) != 0) result += "W";
			return string.IsNullOrEmpty(result) ? "-" : result;
		}

		private static int Manhattan(int a, int b, int width)
		{
			var ax = a % width;
			var ay = a / width;
			var bx = b % width;
			var by = b / width;
			return Math.Abs(ax - bx) + Math.Abs(ay - by);
		}

		private static int GetAdjacentTile(int cell, int direction, int width, int height)
		{
			var dx = ((direction & (int)DefinitionFlags.East) >> 2) - ((direction & (int)DefinitionFlags.West) >> 3);
			var dy = ((direction & (int)DefinitionFlags.North) >> 0) - ((direction & (int)DefinitionFlags.South) >> 1);
			var x = cell % width + dx;
			var y = cell / width + dy;

			if (x < 0 || x >= width || y < 0 || y >= height)
				return -1;

			return y * width + x;
		}
	}
}
