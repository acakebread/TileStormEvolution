using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClassicTilestorm
{
	internal static class TileRouteAssembler
	{
		private const int RouteNodeBudget = 50000;

		public sealed class Result
		{
			public int[] State;
			public string Summary;
		}

		private readonly struct TileRule
		{
			public readonly bool Bake;
			public readonly int Nav;

			public TileRule(bool bake, int nav)
			{
				Bake = bake;
				Nav = nav;
			}
		}

		private readonly struct Anchor
		{
			public readonly int Cell;
			public readonly int Direction;

			public Anchor(int cell, int direction)
			{
				Cell = cell;
				Direction = direction;
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

		private readonly struct TraceState
		{
			public readonly int Index;
			public readonly int Direction;

			public TraceState(int index, int direction)
			{
				Index = index;
				Direction = direction;
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
			var rawPlayableArea = BuildPlayableArea(state, sourceTile, destinationTile, rules, map.Width, map.Height);
			var sourceAnchors = FindAnchors(state, sourceTile, rawPlayableArea, rules, map.Width, map.Height, fromSource: true);
			var destinationAnchors = FindAnchors(state, destinationTile, rawPlayableArea, rules, map.Width, map.Height, fromSource: false);
			var attemptedRoutes = 0;

			foreach (var sourceAnchor in sourceAnchors)
			{
				foreach (var destinationAnchor in destinationAnchors)
				{
					var rawAnchorPlayableArea = BuildPlayableArea(state, sourceAnchor.Cell, destinationAnchor.Cell, rules, map.Width, map.Height);
					var rawPlayableCount = Count(rawAnchorPlayableArea);
					var navTiles = CollectPlayableTilesByNav(state, rawAnchorPlayableArea, rules);
					var solutionArea = CullToReachableSolutionArea(
						state,
						rawAnchorPlayableArea,
						sourceAnchor.Cell,
						destinationAnchor.Cell,
						rules,
						map.Width,
						map.Height,
						out var navTileBudget);
					var solutionAreaCount = Count(solutionArea);

					var counts = CloneCounts(navTiles);
					var route = new List<RouteCell>();
					var visited = new bool[state.Length];
					var nodes = 0;

					if (!TryFindRouteIncludingStatic(
						state,
						sourceAnchor.Cell,
						sourceAnchor.Direction,
						destinationAnchor.Cell,
						destinationAnchor.Direction,
						solutionArea,
						counts,
						visited,
						route,
						rules,
						map.Width,
						map.Height,
						ref nodes))
					{
						continue;
					}

					attemptedRoutes++;
					if (!TryBuildState(state, rawAnchorPlayableArea, route, navTiles, rules, out var assembledState))
						continue;

					if (!IsPermutationOf(state, assembledState, out var permutationError))
					{
						Debug.LogWarning($"Route assembly rejected: {permutationError}");
						continue;
					}

					if (NavToDest(assembledState, sourceTile, destinationTile, rules, map.Width, map.Height) == 0)
						continue;

					result = new Result
					{
						State = assembledState,
						Summary = $"assembled unordered route with {route.Count} movable tile(s) from {navTileBudget} raw candidate nav tile(s), playable area {rawPlayableCount}->{solutionAreaCount} tile(s), using {sourceAnchors.Count} source anchor(s) and {destinationAnchors.Count} destination anchor(s)."
					};
					return true;
				}
			}

			result = new Result
			{
				State = null,
				Summary = $"no validated unordered route found with {sourceAnchors.Count} source anchor(s), {destinationAnchors.Count} destination anchor(s), and {attemptedRoutes} candidate route(s)."
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

				rules[i] = new TileRule(definition?.Bake ?? false, rotatedNav);
			}

			return rules;
		}

		private static bool[] BuildPlayableArea(int[] state, int sourceTile, int destinationTile, TileRule[] rules, int width, int height)
		{
			var area = new bool[state.Length];
			var queue = new Queue<int>();

			AddPlayableSeedsAround(state, sourceTile, rules, width, height, area, queue);
			FloodPlayableArea(state, rules, width, height, area, queue);

			AddPlayableSeedsAround(state, destinationTile, rules, width, height, area, queue);
			FloodPlayableArea(state, rules, width, height, area, queue);

			if (Count(area) > 0)
				return area;

			for (var i = 0; i < state.Length; i++)
				area[i] = IsPlayableAt(state, i, rules);

			return area;
		}

		private static void AddPlayableSeedsAround(int[] state, int index, TileRule[] rules, int width, int height, bool[] area, Queue<int> queue)
		{
			TryAddPlayableSeed(state, index, rules, area, queue);

			foreach (var direction in Navigation.Directions)
			{
				var next = GetAdjacentTile(index, direction, width, height);
				if (next >= 0)
					TryAddPlayableSeed(state, next, rules, area, queue);
			}
		}

		private static bool TryAddPlayableSeed(int[] state, int index, TileRule[] rules, bool[] area, Queue<int> queue)
		{
			if (index < 0 || index >= area.Length || area[index] || !IsPlayableAt(state, index, rules))
				return false;

			area[index] = true;
			queue.Enqueue(index);
			return true;
		}

		private static void FloodPlayableArea(int[] state, TileRule[] rules, int width, int height, bool[] area, Queue<int> queue)
		{
			while (queue.Count > 0)
			{
				var current = queue.Dequeue();
				foreach (var direction in Navigation.Directions)
				{
					var next = GetAdjacentTile(current, direction, width, height);
					if (next < 0 || area[next] || !IsPlayableAt(state, next, rules))
						continue;

					area[next] = true;
					queue.Enqueue(next);
				}
			}
		}

		private static bool IsPlayableAt(int[] state, int index, TileRule[] rules)
			=> TryGetRule(state, index, rules, out var rule) && !rule.Bake;

		private static List<Anchor> FindAnchors(int[] state, int endpoint, bool[] playableArea, TileRule[] rules, int width, int height, bool fromSource)
		{
			var anchors = new List<Anchor>();

			if (endpoint >= 0 && endpoint < playableArea.Length && playableArea[endpoint])
			{
				foreach (var direction in Navigation.Directions)
					anchors.Add(new Anchor(endpoint, direction));
			}

			var endpointRule = GetRuleAt(state, endpoint, rules);
			var queue = new Queue<TraceState>();
			var visited = new HashSet<int>();

			foreach (var direction in Navigation.Directions)
			{
				if ((endpointRule.Nav & direction) != 0)
					queue.Enqueue(new TraceState(endpoint, direction));
			}

			while (queue.Count > 0 && visited.Count < RouteNodeBudget)
			{
				var current = queue.Dequeue();
				var visitKey = current.Index * 16 + current.Direction;
				if (!visited.Add(visitKey))
					continue;

				var next = GetAdjacentTile(current.Index, current.Direction, width, height);
				if (next < 0)
					continue;

				if (playableArea[next])
				{
					var routeDirection = fromSource
						? current.Direction
						: Navigation.GetOppositeDirection(current.Direction);
					AddAnchor(anchors, new Anchor(next, routeDirection));
					continue;
				}

				var nextRule = GetRuleAt(state, next, rules);
				var nextDirections = Navigation.CalculateNav(current.Direction, nextRule.Nav);
				foreach (var direction in Navigation.Directions)
				{
					if ((nextDirections & direction) != 0)
						queue.Enqueue(new TraceState(next, direction));
				}
			}

			return anchors;
		}

		private static void AddBoundaryAnchors(List<Anchor> anchors, bool[] playableArea, int width, int height, bool fromSource)
		{
			for (var index = 0; index < playableArea.Length; index++)
			{
				if (!playableArea[index])
					continue;

				foreach (var direction in Navigation.Directions)
				{
					var outside = fromSource
						? GetAdjacentTile(index, Navigation.GetOppositeDirection(direction), width, height)
						: GetAdjacentTile(index, direction, width, height);

					if (outside >= 0 && playableArea[outside])
						continue;

					AddAnchor(anchors, new Anchor(index, direction));
				}
			}
		}

		private static void AddAnchor(List<Anchor> anchors, Anchor anchor)
		{
			foreach (var existing in anchors)
				if (existing.Cell == anchor.Cell && existing.Direction == anchor.Direction)
					return;

			anchors.Add(anchor);
		}

		private static Dictionary<int, Queue<int>> CollectPlayableTilesByNav(int[] state, bool[] playableArea, TileRule[] rules)
		{
			var result = new Dictionary<int, Queue<int>>();

			for (var index = 0; index < playableArea.Length; index++)
			{
				if (!playableArea[index])
					continue;

				var logicalIndex = state[index];
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

		private static Dictionary<int, int> CloneCounts(Dictionary<int, Queue<int>> navTiles)
		{
			var counts = new Dictionary<int, int>();
			foreach (var pair in navTiles)
				counts[pair.Key] = pair.Value.Count;
			return counts;
		}

		private static bool[] CullToReachableSolutionArea(
			int[] state,
			bool[] playableArea,
			int sourceEntry,
			int destinationEntry,
			TileRule[] rules,
			int width,
			int height,
			out int navTileBudget)
		{
			navTileBudget = CountNavTiles(state, playableArea, rules);
			if (playableArea == null || sourceEntry < 0 || destinationEntry < 0 ||
				sourceEntry >= playableArea.Length || destinationEntry >= playableArea.Length)
			{
				return playableArea ?? Array.Empty<bool>();
			}

			var solutionArea = (bool[])playableArea.Clone();
			if (navTileBudget <= 0)
				return solutionArea;

			var previousCount = -1;

			while (previousCount != Count(solutionArea))
			{
				previousCount = Count(solutionArea);

				var sourceDistance = BuildDistanceMap(state, solutionArea, sourceEntry, rules, width, height);
				var destinationDistance = BuildDistanceMap(state, solutionArea, destinationEntry, rules, width, height);

				for (var i = 0; i < solutionArea.Length; i++)
				{
					if (!solutionArea[i])
						continue;

					var canReachBothEnds = sourceDistance[i] >= 0 && destinationDistance[i] >= 0;
					var fillValue = canReachBothEnds
						? sourceDistance[i] + destinationDistance[i] + 1
						: int.MaxValue;

					if (canReachBothEnds && fillValue <= navTileBudget)
						continue;

					solutionArea[i] = false;
				}
			}

			return solutionArea;
		}

		private static int[] BuildDistanceMap(int[] state, bool[] playableArea, int source, TileRule[] rules, int width, int height)
		{
			var distance = new int[playableArea.Length];
			Array.Fill(distance, -1);

			if (source < 0 || source >= playableArea.Length || !playableArea[source])
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
					if (next < 0 || !TryGetFillStepCost(state, playableArea, current, next, direction, rules, out var stepCost))
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

		private static bool TryGetFillStepCost(
			int[] state,
			bool[] playableArea,
			int current,
			int next,
			int direction,
			TileRule[] rules,
			out int stepCost)
		{
			stepCost = 0;

			var currentPlayable = current >= 0 && current < playableArea.Length && playableArea[current];
			var nextPlayable = next >= 0 && next < playableArea.Length && playableArea[next];
			var currentStatic = IsStaticNavAt(state, current, rules);
			var nextStatic = IsStaticNavAt(state, next, rules);

			if (nextPlayable)
			{
				if (currentPlayable)
				{
					stepCost = 1;
					return true;
				}

				if (currentStatic && (GetRuleAt(state, current, rules).Nav & direction) != 0)
				{
					stepCost = 1;
					return true;
				}
			}

			if (!nextStatic)
				return false;

			if (currentPlayable && (GetRuleAt(state, next, rules).Nav & Navigation.GetOppositeDirection(direction)) != 0)
				return true;

			return currentStatic && HasStaticNavConnection(state, current, next, direction, rules);
		}

		private static bool TryFindRouteIncludingStatic(
			int[] state,
			int index,
			int incomingDirection,
			int destinationCell,
			int exitDirection,
			bool[] playableArea,
			Dictionary<int, int> navCounts,
			bool[] visited,
			List<RouteCell> route,
			TileRule[] rules,
			int width,
			int height,
			ref int nodes)
		{
			if (index < 0 || index >= playableArea.Length || visited[index])
				return false;

			var isPlayable = playableArea[index];
			var isStatic = IsStaticNavAt(state, index, rules);
			if (!isPlayable && !isStatic)
				return false;

			if (++nodes > RouteNodeBudget)
				return false;

			visited[index] = true;

			if (isStatic)
			{
				var nextDirections = Navigation.CalculateNav(incomingDirection, GetRuleAt(state, index, rules).Nav);
				if (index == destinationCell && (nextDirections & exitDirection) != 0)
					return true;

				foreach (var nextDirection in OrderedOutputDirections(nextDirections, index, destinationCell, width, height))
				{
					var next = GetAdjacentTile(index, nextDirection, width, height);
					if (TryFindRouteIncludingStatic(state, next, nextDirection, destinationCell, exitDirection, playableArea, navCounts, visited, route, rules, width, height, ref nodes))
						return true;
				}

				visited[index] = false;
				return false;
			}

			foreach (var nav in OrderedNavMasks(navCounts, incomingDirection, index, destinationCell, width, height))
			{
				var nextDirections = Navigation.CalculateNav(incomingDirection, nav);
				if (nextDirections == 0)
					continue;

				navCounts[nav]--;
				route.Add(new RouteCell(index, nav));

				if (index == destinationCell)
				{
					if ((nextDirections & exitDirection) != 0)
						return true;
				}
				else
				{
					foreach (var nextDirection in OrderedOutputDirections(nextDirections, index, destinationCell, width, height))
					{
						var next = GetAdjacentTile(index, nextDirection, width, height);
						if (TryFindRouteIncludingStatic(state, next, nextDirection, destinationCell, exitDirection, playableArea, navCounts, visited, route, rules, width, height, ref nodes))
							return true;
					}
				}

				route.RemoveAt(route.Count - 1);
				navCounts[nav]++;
			}

			visited[index] = false;
			return false;
		}

		private static bool TryFindRoute(
			int index,
			int incomingDirection,
			int destinationCell,
			int exitDirection,
			bool[] playableArea,
			Dictionary<int, int> navCounts,
			bool[] visited,
			List<RouteCell> route,
			int width,
			int height,
			ref int nodes)
		{
			if (index < 0 || index >= playableArea.Length || !playableArea[index] || visited[index])
				return false;

			if (++nodes > RouteNodeBudget)
				return false;

			visited[index] = true;

			foreach (var nav in OrderedNavMasks(navCounts, incomingDirection, index, destinationCell, width, height))
			{
				var nextDirections = Navigation.CalculateNav(incomingDirection, nav);
				if (nextDirections == 0)
					continue;

				navCounts[nav]--;
				route.Add(new RouteCell(index, nav));

				if (index == destinationCell)
				{
					if ((nextDirections & exitDirection) != 0)
						return true;
				}
				else
				{
					foreach (var nextDirection in OrderedOutputDirections(nextDirections, index, destinationCell, width, height))
					{
						var next = GetAdjacentTile(index, nextDirection, width, height);
						if (TryFindRoute(next, nextDirection, destinationCell, exitDirection, playableArea, navCounts, visited, route, width, height, ref nodes))
							return true;
					}
				}

				route.RemoveAt(route.Count - 1);
				navCounts[nav]++;
			}

			visited[index] = false;
			return false;
		}

		private static List<int> OrderedNavMasks(Dictionary<int, int> navCounts, int incomingDirection, int index, int destinationCell, int width, int height)
		{
			var masks = new List<int>();
			foreach (var pair in navCounts)
			{
				if (pair.Value > 0 && Navigation.CalculateNav(incomingDirection, pair.Key) != 0)
					masks.Add(pair.Key);
			}

			masks.Sort((a, b) =>
			{
				var distA = BestOutputDistance(index, Navigation.CalculateNav(incomingDirection, a), destinationCell, width, height);
				var distB = BestOutputDistance(index, Navigation.CalculateNav(incomingDirection, b), destinationCell, width, height);
				return distA.CompareTo(distB);
			});

			return masks;
		}

		private static List<int> OrderedOutputDirections(int directions, int index, int destinationCell, int width, int height)
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

		private static bool TryBuildState(
			int[] state,
			bool[] playableArea,
			List<RouteCell> route,
			Dictionary<int, Queue<int>> navTiles,
			TileRule[] rules,
			out int[] assembledState)
		{
			assembledState = (int[])state.Clone();
			var available = new List<int>();
			var routeCells = new HashSet<int>();
			var used = new HashSet<int>();
			var navQueues = new Dictionary<int, Queue<int>>();

			foreach (var pair in navTiles)
				navQueues[pair.Key] = new Queue<int>(pair.Value);

			for (var index = 0; index < playableArea.Length; index++)
			{
				if (!playableArea[index])
					continue;

				var logicalIndex = state[index];
				if (logicalIndex >= 0 && logicalIndex < rules.Length)
					available.Add(logicalIndex);
			}

			foreach (var cell in route)
			{
				routeCells.Add(cell.Index);
				if (!navQueues.TryGetValue(cell.Nav, out var queue) || queue.Count == 0)
					return false;

				var logicalRouteTile = queue.Dequeue();
				assembledState[cell.Index] = logicalRouteTile;
				used.Add(logicalRouteTile);
			}

			var remaining = new Queue<int>();
			foreach (var logicalIndex in available)
				if (!used.Contains(logicalIndex))
					remaining.Enqueue(logicalIndex);

			for (var index = 0; index < playableArea.Length; index++)
			{
				if (!playableArea[index] || routeCells.Contains(index) || remaining.Count == 0)
					continue;

				assembledState[index] = remaining.Dequeue();
			}

			return true;
		}

		private static bool TryGetRule(int[] state, int index, TileRule[] rules, out TileRule rule)
		{
			rule = default;
			if (state == null || rules == null || index < 0 || index >= state.Length)
				return false;

			var logicalIndex = state[index];
			if (logicalIndex < 0 || logicalIndex >= rules.Length)
				return false;

			rule = rules[logicalIndex];
			return true;
		}

		private static TileRule GetRuleAt(int[] state, int index, TileRule[] rules)
			=> TryGetRule(state, index, rules, out var rule) ? rule : default;

		private static bool IsStaticNavAt(int[] state, int index, TileRule[] rules)
			=> TryGetRule(state, index, rules, out var rule) && rule.Bake && rule.Nav != 0;

		private static bool HasStaticNavConnection(int[] state, int current, int next, int direction, TileRule[] rules)
		{
			if (!IsStaticNavAt(state, current, rules) || !IsStaticNavAt(state, next, rules))
				return false;

			var currentNav = GetRuleAt(state, current, rules).Nav;
			var nextNav = GetRuleAt(state, next, rules).Nav;
			return (currentNav & direction) != 0 &&
				(nextNav & Navigation.GetOppositeDirection(direction)) != 0;
		}

		private static int NavToDest(int[] state, int sourceTile, int destinationTile, TileRule[] rules, int width, int height)
		{
			if (sourceTile == destinationTile || sourceTile < 0 || destinationTile < 0)
				return 0;

			foreach (var direction in Navigation.Directions)
			{
				var currentTile = sourceTile;
				var currentNav = GetRuleAt(state, sourceTile, rules).Nav & direction;

				while (currentNav != 0)
				{
					if (currentTile == destinationTile)
						return direction;

					var nextTile = GetAdjacentTile(currentTile, currentNav, width, height);
					if (nextTile < 0 || nextTile == sourceTile)
						break;

					var nextRule = GetRuleAt(state, nextTile, rules);
					if (nextRule.Nav == 0)
						break;

					currentNav = Navigation.CalculateNav(currentNav, nextRule.Nav);
					currentTile = nextTile;
				}
			}

			return 0;
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

		private static bool IsSingleDirection(int direction)
			=> direction is 1 or 2 or 4 or 8;

		private static int Manhattan(int a, int b, int width)
		{
			var ax = a % width;
			var ay = a / width;
			var bx = b % width;
			var by = b / width;
			return Math.Abs(ax - bx) + Math.Abs(ay - by);
		}

		private static int Count(bool[] area)
		{
			var count = 0;
			for (var i = 0; i < area.Length; i++)
				if (area[i])
					count++;
			return count;
		}

		private static int CountNavTiles(Dictionary<int, Queue<int>> navTiles)
		{
			var count = 0;
			foreach (var pair in navTiles)
				count += pair.Value.Count;
			return count;
		}

		private static int CountNavTiles(int[] state, bool[] area, TileRule[] rules)
		{
			var count = 0;
			if (state == null || area == null || rules == null)
				return count;

			for (var i = 0; i < area.Length; i++)
			{
				if (area[i] && TryGetRule(state, i, rules, out var rule) && !rule.Bake && rule.Nav != 0)
					count++;
			}

			return count;
		}

		private static bool IsPermutationOf(int[] expected, int[] actual, out string error)
		{
			error = null;

			if (expected == null || actual == null)
			{
				error = "assembled state was null.";
				return false;
			}

			if (expected.Length != actual.Length)
			{
				error = $"assembled state length {actual.Length} did not match expected length {expected.Length}.";
				return false;
			}

			var seenExpected = new int[expected.Length];
			var seenActual = new int[actual.Length];

			for (var i = 0; i < expected.Length; i++)
			{
				var expectedIndex = expected[i];
				var actualIndex = actual[i];

				if (expectedIndex < 0 || expectedIndex >= expected.Length)
				{
					error = $"expected state contained out-of-range tile index {expectedIndex} at position {i}.";
					return false;
				}

				if (actualIndex < 0 || actualIndex >= actual.Length)
				{
					error = $"assembled state contained out-of-range tile index {actualIndex} at position {i}.";
					return false;
				}

				seenExpected[expectedIndex]++;
				seenActual[actualIndex]++;
			}

			for (var i = 0; i < expected.Length; i++)
			{
				if (seenExpected[i] != 1)
				{
					error = $"expected state is not a permutation: tile index {i} appears {seenExpected[i]} times.";
					return false;
				}

				if (seenActual[i] != 1)
				{
					error = $"assembled state is not a permutation: tile index {i} appears {seenActual[i]} times.";
					return false;
				}
			}

			return true;
		}
	}
}
