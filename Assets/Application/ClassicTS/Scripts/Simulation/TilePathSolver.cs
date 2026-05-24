using System;
using System.Collections.Generic;
using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	internal static class TilePathSolver
	{
		private const int MaxSearchDepth = 18;
		private const int CandidateLimit = 64;
		private const int PrimaryCandidateLimit = 44;
		private const int SecondaryCandidateLimit = 16;
		private const int FallbackCandidateLimit = 8;
		private const int NodeBudget = 180000;
		private const int QueuedStateBudget = 40000;
		private const int MaxSearchMilliseconds = 350;
		private const int RoutePlannerNodeBudget = 12000;
		private const int TimeoutCheckInterval = 64;

		public static string LastFailureReason { get; private set; }

		private readonly struct TileRule
		{
			public readonly bool Drag;
			public readonly bool Roll;
			public readonly bool Fold;
			public readonly bool Bake;
			public readonly int Nav;

			public TileRule(bool drag, bool roll, bool fold, bool bake, int nav)
			{
				Drag = drag;
				Roll = roll;
				Fold = fold;
				Bake = bake;
				Nav = nav;
			}
		}

		private sealed class SearchNode
		{
			public readonly int[] State;
			public readonly int Depth;
			public readonly TileStrip FirstMove;
			public readonly TileStrip LastMove;
			public readonly int Priority;
			public readonly int Sequence;

			public SearchNode(int[] state, int depth, TileStrip firstMove, TileStrip lastMove, int priority, int sequence)
			{
				State = state;
				Depth = depth;
				FirstMove = firstMove;
				LastMove = lastMove;
				Priority = priority;
				Sequence = sequence;
			}
		}

		private readonly struct ScoredStrip
		{
			public readonly TileStrip Strip;
			public readonly int Score;
			public readonly int Category;

			public ScoredStrip(TileStrip strip, int score, int category)
			{
				Strip = strip;
				Score = score;
				Category = category;
			}
		}

		private sealed class RoutePlan
		{
			public readonly bool Possible;
			public readonly int ExploredNodes;
			public readonly int[] Path;
			public readonly bool[] Cells;
			public readonly int[] Incoming;
			public readonly int[] Outgoing;
			public readonly bool[] SearchArea;
			public readonly int SearchAreaCount;
			public readonly int CulledCells;
			public readonly int NavTileBudget;

			public int Length => Path?.Length ?? 0;

			private RoutePlan(
				bool possible,
				int exploredNodes,
				int[] path,
				bool[] cells,
				int[] incoming,
				int[] outgoing,
				bool[] searchArea,
				int culledCells,
				int navTileBudget)
			{
				Possible = possible;
				ExploredNodes = exploredNodes;
				Path = path;
				Cells = cells;
				Incoming = incoming;
				Outgoing = outgoing;
				SearchArea = searchArea;
				SearchAreaCount = Count(searchArea);
				CulledCells = culledCells;
				NavTileBudget = navTileBudget;
			}

			public static RoutePlan Found(
				int exploredNodes,
				List<int> path,
				bool[] cells,
				int[] incoming,
				int[] outgoing,
				bool[] searchArea,
				int culledCells,
				int navTileBudget)
				=> new(
					true,
					exploredNodes,
					path.ToArray(),
					(bool[])cells.Clone(),
					(int[])incoming.Clone(),
					(int[])outgoing.Clone(),
					(bool[])searchArea.Clone(),
					culledCells,
					navTileBudget);

			public static RoutePlan Missing(int exploredNodes, bool[] searchArea = null, int culledCells = 0, int navTileBudget = 0)
				=> new(false, exploredNodes, Array.Empty<int>(), null, null, null, searchArea, culledCells, navTileBudget);
		}

		private sealed class SearchQueue
		{
			private readonly List<SearchNode> heap = new();

			public int Count => heap.Count;

			public void Enqueue(SearchNode node)
			{
				heap.Add(node);
				SiftUp(heap.Count - 1);
			}

			public SearchNode Dequeue()
			{
				var result = heap[0];
				var last = heap[^1];
				heap.RemoveAt(heap.Count - 1);

				if (heap.Count > 0)
				{
					heap[0] = last;
					SiftDown(0);
				}

				return result;
			}

			private void SiftUp(int index)
			{
				while (index > 0)
				{
					var parent = (index - 1) / 2;
					if (Compare(heap[parent], heap[index]) <= 0)
						return;

					(heap[parent], heap[index]) = (heap[index], heap[parent]);
					index = parent;
				}
			}

			private void SiftDown(int index)
			{
				while (true)
				{
					var left = index * 2 + 1;
					var right = left + 1;
					var best = index;

					if (left < heap.Count && Compare(heap[left], heap[best]) < 0)
						best = left;

					if (right < heap.Count && Compare(heap[right], heap[best]) < 0)
						best = right;

					if (best == index)
						return;

					(heap[index], heap[best]) = (heap[best], heap[index]);
					index = best;
				}
			}

			private static int Compare(SearchNode a, SearchNode b)
			{
				var priority = a.Priority.CompareTo(b.Priority);
				return priority != 0 ? priority : a.Sequence.CompareTo(b.Sequence);
			}
		}

		public static bool TrySolveNextStep(Map map, int sourceTile, int destinationTile, bool difficult, out TileStrip strip)
		{
			strip = default;
			LastFailureReason = null;

			if (map == null || map.tiles == null || map.State == null ||
				sourceTile < 0 || destinationTile < 0 ||
				sourceTile >= map.Count || destinationTile >= map.Count)
			{
				LastFailureReason = "invalid map or waypoint state.";
				return false;
			}

			var state = (int[])map.State.Clone();
			var rules = BuildRules(map);

			if (NavToDest(state, sourceTile, destinationTile, rules, map.Width, map.Height) != 0)
			{
				LastFailureReason = "the current path is already complete.";
				return false;
			}

			return TrySearch(state, sourceTile, destinationTile, rules, map.Width, map.Height, difficult, out strip);
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

				rules[i] = new TileRule(
					definition?.Drag ?? false,
					definition?.Roll ?? false,
					definition?.Fold ?? false,
					definition?.Bake ?? false,
					rotatedNav);
			}

			return rules;
		}

		private static bool TrySearch(
			int[] state,
			int sourceTile,
			int destinationTile,
			TileRule[] rules,
			int width,
			int height,
			bool difficult,
			out TileStrip strip)
		{
			strip = default;

			var rootPlayableArea = BuildPlayableArea(state, sourceTile, destinationTile, rules, width, height);
			var rootPlayableCount = Count(rootPlayableArea);
			var maxPlayableCount = rootPlayableCount;
			var routePlan = FindRoutePlan(state, sourceTile, destinationTile, rules, rootPlayableArea, width, height);
			var initialHash = HashState(state, sourceTile, destinationTile);
			var deadline = System.Diagnostics.Stopwatch.GetTimestamp()
						   + (long)(MaxSearchMilliseconds * (System.Diagnostics.Stopwatch.Frequency / 1000.0));
			var open = new SearchQueue();
			var visited = new HashSet<ulong> { initialHash };
			var sequence = 0;
			var nodes = 0;
			var maxDepthReached = 0;
			var timedOut = false;

			open.Enqueue(new SearchNode(state, 0, default, default, Estimate(state, sourceTile, destinationTile, rules, width, height) + RouteMismatch(state, routePlan, rules, width), sequence++));

			while (open.Count > 0 && nodes < NodeBudget)
			{
				if ((nodes & (TimeoutCheckInterval - 1)) == 0 && System.Diagnostics.Stopwatch.GetTimestamp() >= deadline)
				{
					timedOut = true;
					break;
				}

				nodes++;
				var node = open.Dequeue();
				maxDepthReached = Math.Max(maxDepthReached, node.Depth);
				if (node.Depth >= MaxSearchDepth)
					continue;

				var playableArea = node.Depth == 0
					? rootPlayableArea
					: BuildPlayableArea(node.State, sourceTile, destinationTile, rules, width, height);
				maxPlayableCount = Math.Max(maxPlayableCount, Count(playableArea));

				foreach (var candidate in GenerateCandidates(node.State, sourceTile, destinationTile, rules, playableArea, routePlan, width, height, difficult))
				{
					if (open.Count >= QueuedStateBudget)
						break;

					if (node.Depth > 0 && Reverses(candidate, node.LastMove))
						continue;

					var childState = (int[])node.State.Clone();
					ApplyMove(childState, candidate);

					if (!visited.Add(HashState(childState, sourceTile, destinationTile)))
						continue;

					var firstMove = node.Depth == 0 ? candidate : node.FirstMove;
					if (NavToDest(childState, sourceTile, destinationTile, rules, width, height) != 0)
					{
						strip = firstMove;
						return true;
					}

					var depth = node.Depth + 1;
					var childEstimate = Estimate(childState, sourceTile, destinationTile, rules, width, height);
					var priority = childEstimate * 96
								   + RouteMismatch(childState, routePlan, rules, width) * 48
								   + ScoreStrip(candidate, sourceTile, destinationTile, width) * 3
								   + depth;
					open.Enqueue(new SearchNode(childState, depth, firstMove, candidate, priority, sequence++));
				}
			}

			var routeSummary = routePlan.Possible
				? $"route length {routePlan.Length}, route nodes {routePlan.ExploredNodes}, route area {rootPlayableCount}->{routePlan.SearchAreaCount}, culled {routePlan.CulledCells}, nav budget {routePlan.NavTileBudget}"
				: $"no unordered route found, route nodes {routePlan.ExploredNodes}, route area {rootPlayableCount}->{routePlan.SearchAreaCount}, culled {routePlan.CulledCells}, nav budget {routePlan.NavTileBudget}";
			LastFailureReason = timedOut
				? $"no legal incremental move was found within {MaxSearchMilliseconds}ms ({nodes} nodes, depth {maxDepthReached}, playable area {rootPlayableCount}-{maxPlayableCount} tiles, {routeSummary})."
				: $"no legal incremental move was found within the solver budget ({nodes} nodes, depth {maxDepthReached}, playable area {rootPlayableCount}-{maxPlayableCount} tiles, {routeSummary}).";
			return false;
		}

		private static List<TileStrip> GenerateCandidates(
			int[] state,
			int sourceTile,
			int destinationTile,
			TileRule[] rules,
			bool[] playableArea,
			RoutePlan routePlan,
			int width,
			int height,
			bool difficult)
		{
			var scored = new List<ScoredStrip>();
			var seen = new HashSet<ulong>();
			var strides = new[] { 1, -1, width, -width };

			for (var index = 0; index < state.Length; index++)
			{
				foreach (var stride in strides)
				{
					if (!TryGetTileStrip(state, index, stride, difficult, rules, width, height, out var strip) || strip.Count <= 1)
						continue;

					if (!seen.Add(StripKey(strip)))
						continue;

					var candidateArea = routePlan?.SearchArea ?? playableArea;
					if (candidateArea != null && !IntersectsArea(strip, candidateArea))
						continue;

					var score = ScoreStrip(strip, sourceTile, destinationTile, width);
					score += ScoreRouteStrip(strip, routePlan);
					var containsNav = ContainsNav(strip, state, rules);
					var containsEndpoint = Contains(strip, sourceTile) || Contains(strip, destinationTile);
					var touchesNav = TouchesNav(strip, state, rules, width, height);
					var nearPath = IsBetween(strip, sourceTile, destinationTile, width);
					var category = containsNav || containsEndpoint ? 0 : touchesNav || nearPath ? 1 : 2;

					score += category * 32;
					if (containsNav)
						score -= 12;
					if (touchesNav)
						score -= 4;

					scored.Add(new ScoredStrip(strip, score, category));
				}
			}

			scored.Sort((a, b) =>
			{
				var cmp = a.Score.CompareTo(b.Score);
				if (cmp != 0) return cmp;
				cmp = a.Category.CompareTo(b.Category);
				if (cmp != 0) return cmp;
				cmp = b.Strip.Count.CompareTo(a.Strip.Count);
				if (cmp != 0) return cmp;
				return a.Strip.First.CompareTo(b.Strip.First);
			});

			var candidates = new List<TileStrip>(Math.Min(CandidateLimit, scored.Count));
			var primary = 0;
			var secondary = 0;
			var fallback = 0;
			var hasPrimary = scored.Exists(item => item.Category == 0);
			var selected = new HashSet<ulong>();

			foreach (var item in scored)
			{
				if (candidates.Count >= CandidateLimit)
					break;

				if (item.Category == 0 && primary >= PrimaryCandidateLimit)
					continue;

				if (item.Category == 1 && secondary >= (hasPrimary ? SecondaryCandidateLimit : CandidateLimit))
					continue;

				if (item.Category == 2 && fallback >= (hasPrimary ? FallbackCandidateLimit : SecondaryCandidateLimit))
					continue;

				if (!selected.Add(StripKey(item.Strip)))
					continue;

				candidates.Add(item.Strip);
				if (item.Category == 0) primary++;
				else if (item.Category == 1) secondary++;
				else fallback++;
			}

			foreach (var item in scored)
			{
				if (candidates.Count >= CandidateLimit)
					break;

				if (selected.Add(StripKey(item.Strip)))
					candidates.Add(item.Strip);
			}

			return candidates;
		}

		private static RoutePlan FindRoutePlan(int[] state, int sourceTile, int destinationTile, TileRule[] rules, bool[] playableArea, int width, int height)
		{
			var routeArea = playableArea != null ? (bool[])playableArea.Clone() : new bool[state.Length];
			if ((uint)sourceTile < routeArea.Length)
				routeArea[sourceTile] = true;
			if ((uint)destinationTile < routeArea.Length)
				routeArea[destinationTile] = true;

			routeArea = CullToReachableRouteArea(
				state,
				routeArea,
				sourceTile,
				destinationTile,
				rules,
				width,
				height,
				out var routeAreaCulled,
				out var navTileBudget);

			var navCounts = new int[16];
			var navCount = 0;
			for (var i = 0; i < routeArea.Length; i++)
			{
				if (!routeArea[i] || !TryGetRule(state, i, rules, out var rule))
					continue;

				var nav = rule.Nav & (int)DefinitionFlags.DirMask;
				if (nav == 0)
					continue;

				navCounts[nav]++;
				navCount++;
			}

			if (navCount == 0)
				return RoutePlan.Missing(0, routeArea, routeAreaCulled, navTileBudget);

			var visited = new bool[state.Length];
			var cells = new bool[state.Length];
			var incoming = new int[state.Length];
			var outgoing = new int[state.Length];
			var path = new List<int>();
			var explored = 0;

			foreach (var direction in OrderedDirections(sourceTile, destinationTile, width, height))
			{
				var next = GetAdjacentTile(sourceTile, direction, width, height);
				if (next < 0 || !routeArea[next])
					continue;

				for (var nav = 1; nav < navCounts.Length; nav++)
				{
					if (navCounts[nav] <= 0 || (nav & direction) == 0)
						continue;

					navCounts[nav]--;
					MarkRouteCell(sourceTile, 0, direction, visited, cells, incoming, outgoing, path);

					if (TryFindRouteFrom(
						next,
						direction,
						destinationTile,
						routeArea,
						navCounts,
						visited,
						cells,
						incoming,
						outgoing,
						path,
						width,
						height,
						ref explored))
					{
						return RoutePlan.Found(explored, path, cells, incoming, outgoing, routeArea, routeAreaCulled, navTileBudget);
					}

					UnmarkRouteCell(sourceTile, visited, cells, incoming, outgoing, path);
					navCounts[nav]++;
				}
			}

			return RoutePlan.Missing(explored, routeArea, routeAreaCulled, navTileBudget);
		}

		private static bool[] CullToReachableRouteArea(
			int[] state,
			bool[] routeArea,
			int sourceTile,
			int destinationTile,
			TileRule[] rules,
			int width,
			int height,
			out int culledCells,
			out int navTileBudget)
		{
			culledCells = 0;
			navTileBudget = 0;

			if (routeArea == null || routeArea.Length == 0)
				return routeArea;

			var result = (bool[])routeArea.Clone();
			var previousCount = -1;

			while (previousCount != Count(result))
			{
				previousCount = Count(result);
				navTileBudget = CountMovableNavTiles(state, result, rules);
				if (navTileBudget <= 0)
					break;

				var sourceDistance = BuildDistanceMap(result, sourceTile, width, height);
				var destinationDistance = BuildDistanceMap(result, destinationTile, width, height);
				var maxRouteCells = navTileBudget + 2; // two fixed blue/static anchors bracket the movable route.

				for (var i = 0; i < result.Length; i++)
				{
					if (!result[i] || i == sourceTile || i == destinationTile)
						continue;

					var canReachBothEnds = sourceDistance[i] >= 0 && destinationDistance[i] >= 0;
					var minimumRouteCells = canReachBothEnds
						? sourceDistance[i] + destinationDistance[i] + 1
						: int.MaxValue;

					if (canReachBothEnds && minimumRouteCells <= maxRouteCells)
						continue;

					result[i] = false;
					culledCells++;
				}
			}

			return result;
		}

		private static int CountMovableNavTiles(int[] state, bool[] area, TileRule[] rules)
		{
			var count = 0;
			for (var i = 0; i < area.Length; i++)
			{
				if (!area[i] || !TryGetRule(state, i, rules, out var rule))
					continue;

				if (!rule.Bake && rule.Nav != 0)
					count++;
			}

			return count;
		}

		private static int[] BuildDistanceMap(bool[] area, int source, int width, int height)
		{
			var distance = new int[area.Length];
			Array.Fill(distance, -1);

			if (source < 0 || source >= area.Length || !area[source])
				return distance;

			var queue = new Queue<int>();
			distance[source] = 0;
			queue.Enqueue(source);

			while (queue.Count > 0)
			{
				var current = queue.Dequeue();
				foreach (var direction in Navigation.Directions)
				{
					var next = GetAdjacentTile(current, direction, width, height);
					if (next < 0 || !area[next] || distance[next] >= 0)
						continue;

					distance[next] = distance[current] + 1;
					queue.Enqueue(next);
				}
			}

			return distance;
		}

		private static bool TryFindRouteFrom(
			int index,
			int incomingDirection,
			int destinationTile,
			bool[] routeArea,
			int[] navCounts,
			bool[] visited,
			bool[] cells,
			int[] incoming,
			int[] outgoing,
			List<int> path,
			int width,
			int height,
			ref int explored)
		{
			if (index < 0 || index >= routeArea.Length || !routeArea[index] || visited[index])
				return false;

			if (++explored > RoutePlannerNodeBudget)
				return false;

			if (index == destinationTile)
			{
				for (var nav = 1; nav < navCounts.Length; nav++)
				{
					if (navCounts[nav] <= 0 || Navigation.CalculateNav(incomingDirection, nav) == 0)
						continue;

					navCounts[nav]--;
					MarkRouteCell(index, incomingDirection, 0, visited, cells, incoming, outgoing, path);
					return true;
				}

				return false;
			}

			foreach (var nav in OrderedRouteTiles(navCounts, incomingDirection, index, destinationTile, width, height))
			{
				var nextDirection = Navigation.CalculateNav(incomingDirection, nav);
				if (!IsSingleDirection(nextDirection))
					continue;

				var next = GetAdjacentTile(index, nextDirection, width, height);
				if (next < 0 || !routeArea[next] || visited[next])
					continue;

				navCounts[nav]--;
				MarkRouteCell(index, incomingDirection, nextDirection, visited, cells, incoming, outgoing, path);

				if (TryFindRouteFrom(
					next,
					nextDirection,
					destinationTile,
					routeArea,
					navCounts,
					visited,
					cells,
					incoming,
					outgoing,
					path,
					width,
					height,
					ref explored))
				{
					return true;
				}

				UnmarkRouteCell(index, visited, cells, incoming, outgoing, path);
				navCounts[nav]++;
			}

			return false;
		}

		private static void MarkRouteCell(int index, int inDirection, int outDirection, bool[] visited, bool[] cells, int[] incoming, int[] outgoing, List<int> path)
		{
			visited[index] = true;
			cells[index] = true;
			incoming[index] = inDirection;
			outgoing[index] = outDirection;
			path.Add(index);
		}

		private static void UnmarkRouteCell(int index, bool[] visited, bool[] cells, int[] incoming, int[] outgoing, List<int> path)
		{
			visited[index] = false;
			cells[index] = false;
			incoming[index] = 0;
			outgoing[index] = 0;
			if (path.Count > 0 && path[^1] == index)
				path.RemoveAt(path.Count - 1);
		}

		private static int[] OrderedDirections(int index, int destinationTile, int width, int height)
		{
			var directions = (int[])Navigation.Directions.Clone();
			Array.Sort(directions, (a, b) =>
			{
				var nextA = GetAdjacentTile(index, a, width, height);
				var nextB = GetAdjacentTile(index, b, width, height);
				var distA = nextA < 0 ? int.MaxValue : Manhattan(nextA, destinationTile, width);
				var distB = nextB < 0 ? int.MaxValue : Manhattan(nextB, destinationTile, width);
				return distA.CompareTo(distB);
			});
			return directions;
		}

		private static List<int> OrderedRouteTiles(int[] navCounts, int incomingDirection, int index, int destinationTile, int width, int height)
		{
			var tiles = new List<int>();
			for (var nav = 1; nav < navCounts.Length; nav++)
			{
				if (navCounts[nav] > 0 && Navigation.CalculateNav(incomingDirection, nav) != 0)
					tiles.Add(nav);
			}

			tiles.Sort((a, b) =>
			{
				var nextA = GetAdjacentTile(index, Navigation.CalculateNav(incomingDirection, a), width, height);
				var nextB = GetAdjacentTile(index, Navigation.CalculateNav(incomingDirection, b), width, height);
				var distA = nextA < 0 ? int.MaxValue : Manhattan(nextA, destinationTile, width);
				var distB = nextB < 0 ? int.MaxValue : Manhattan(nextB, destinationTile, width);
				return distA.CompareTo(distB);
			});

			return tiles;
		}

		private static int RouteMismatch(int[] state, RoutePlan routePlan, TileRule[] rules, int width)
		{
			if (routePlan == null || !routePlan.Possible)
				return 0;

			var penalty = 0;
			foreach (var index in routePlan.Path)
			{
				if (TryGetRule(state, index, rules, out var rule) &&
					IsRouteCompatible(rule.Nav, routePlan.Incoming[index], routePlan.Outgoing[index]))
				{
					continue;
				}

				penalty += 4 + NearestCompatibleRouteTileDistance(state, index, routePlan.Incoming[index], routePlan.Outgoing[index], rules, width);
			}

			return penalty;
		}

		private static int NearestCompatibleRouteTileDistance(int[] state, int targetIndex, int incomingDirection, int outgoingDirection, TileRule[] rules, int width)
		{
			var best = int.MaxValue;
			for (var i = 0; i < state.Length; i++)
			{
				if (TryGetRule(state, i, rules, out var rule) && IsRouteCompatible(rule.Nav, incomingDirection, outgoingDirection))
					best = Math.Min(best, Manhattan(i, targetIndex, width));
			}

			return best == int.MaxValue ? 16 : best;
		}

		private static bool IsRouteCompatible(int nav, int incomingDirection, int outgoingDirection)
		{
			nav &= (int)DefinitionFlags.DirMask;
			if (nav == 0)
				return false;

			if (incomingDirection == 0)
				return outgoingDirection != 0 && (nav & outgoingDirection) != 0;

			if (outgoingDirection == 0)
				return Navigation.CalculateNav(incomingDirection, nav) != 0;

			return Navigation.CalculateNav(incomingDirection, nav) == outgoingDirection;
		}

		private static int ScoreRouteStrip(TileStrip strip, RoutePlan routePlan)
			=> routePlan != null && routePlan.Possible && IntersectsArea(strip, routePlan.Cells) ? -10 : 0;

		private static bool IsSingleDirection(int direction)
			=> direction is 1 or 2 or 4 or 8;

		private static bool[] BuildPlayableArea(int[] state, int sourceTile, int destinationTile, TileRule[] rules, int width, int height)
		{
			var area = new bool[state.Length];
			var queue = new Queue<int>();

			AddPlayableSeed(state, sourceTile, rules, width, height, area, queue, includeNeighbours: true);
			FloodPlayableArea(state, rules, width, height, area, queue);

			AddPlayableSeed(state, destinationTile, rules, width, height, area, queue, includeNeighbours: true);
			var count = FloodPlayableArea(state, rules, width, height, area, queue);
			count = Math.Max(count, Count(area));
			if (count > 1)
				return area;

			Array.Clear(area, 0, area.Length);
			AddPlayableSeed(state, sourceTile, rules, width, height, area, queue, includeNeighbours: true);
			FloodPlayableArea(state, rules, width, height, area, queue);

			AddPlayableSeed(state, destinationTile, rules, width, height, area, queue, includeNeighbours: true);
			count = FloodPlayableArea(state, rules, width, height, area, queue);
			count = Math.Max(count, Count(area));
			if (count > 1)
				return area;

			for (var i = 0; i < state.Length; i++)
				area[i] = IsPlayableAt(state, i, rules);

			return area;
		}

		private static void AddPlayableSeed(
			int[] state,
			int index,
			TileRule[] rules,
			int width,
			int height,
			bool[] area,
			Queue<int> queue,
			bool includeNeighbours)
		{
			queue.Clear();

			if (TryAddSeed(state, index, rules, area, queue) || !includeNeighbours)
				return;

			foreach (var dir in Navigation.Directions)
			{
				var next = GetAdjacentTile(index, dir, width, height);
				if (next >= 0)
					TryAddSeed(state, next, rules, area, queue);
			}
		}

		private static bool TryAddSeed(int[] state, int index, TileRule[] rules, bool[] area, Queue<int> queue)
		{
			if (index < 0 || index >= area.Length || area[index] || !IsPlayableAt(state, index, rules))
				return false;

			area[index] = true;
			queue.Enqueue(index);
			return true;
		}

		private static int FloodPlayableArea(int[] state, TileRule[] rules, int width, int height, bool[] area, Queue<int> queue)
		{
			var count = queue.Count;

			while (queue.Count > 0)
			{
				var current = queue.Dequeue();
				foreach (var dir in Navigation.Directions)
				{
					var next = GetAdjacentTile(current, dir, width, height);
					if (next < 0 || area[next] || !IsPlayableAt(state, next, rules))
						continue;

					area[next] = true;
					queue.Enqueue(next);
					count++;
				}
			}

			return count;
		}

		private static bool IsPlayableAt(int[] state, int index, TileRule[] rules)
			=> TryGetRule(state, index, rules, out var rule) && !rule.Bake;

		private static int Estimate(int[] state, int sourceTile, int destinationTile, TileRule[] rules, int width, int height)
		{
			if (NavToDest(state, sourceTile, destinationTile, rules, width, height) != 0)
				return 0;

			var sourceRule = GetRuleAt(state, sourceTile, rules);
			var best = Manhattan(sourceTile, destinationTile, width) * 2 + 8;

			if (sourceRule.Nav == 0)
				return Math.Max(1, NearestNavDistance(state, sourceTile, rules, width) + Manhattan(sourceTile, destinationTile, width));

			var visited = new bool[state.Length];
			var queue = new Queue<int>();
			visited[sourceTile] = true;
			queue.Enqueue(sourceTile);

			while (queue.Count > 0)
			{
				var current = queue.Dequeue();
				best = Math.Min(best, Manhattan(current, destinationTile, width));

				var currentRule = GetRuleAt(state, current, rules);
				foreach (var dir in Navigation.Directions)
				{
					if ((currentRule.Nav & dir) == 0)
						continue;

					var next = GetAdjacentTile(current, dir, width, height);
					if (next < 0 || visited[next])
						continue;

					var nextRule = GetRuleAt(state, next, rules);
					if ((nextRule.Nav & Navigation.GetOppositeDirection(dir)) == 0)
						continue;

					visited[next] = true;
					queue.Enqueue(next);
				}
			}

			return Math.Max(1, best);
		}

		private static int NearestNavDistance(int[] state, int index, TileRule[] rules, int width)
		{
			var best = int.MaxValue;
			for (var i = 0; i < state.Length; i++)
			{
				if (TryGetRule(state, i, rules, out var rule) && rule.Nav != 0)
					best = Math.Min(best, Manhattan(index, i, width));
			}

			return best == int.MaxValue ? state.Length : best;
		}

		private static int ScoreStrip(TileStrip strip, int sourceTile, int destinationTile, int width)
		{
			var score = DistanceToStrip(strip, sourceTile, width) + DistanceToStrip(strip, destinationTile, width);
			score -= strip.Count / 2;

			if (Contains(strip, sourceTile))
				score -= 8;

			if (Contains(strip, destinationTile))
				score -= 8;

			if (IsBetween(strip, sourceTile, destinationTile, width))
				score -= 4;

			return score;
		}

		private static int DistanceToStrip(TileStrip strip, int index, int width)
		{
			var best = int.MaxValue;
			for (var i = 0; i < strip.Count; i++)
			{
				var candidate = strip.First + strip.Stride * i;
				best = Math.Min(best, Manhattan(index, candidate, width));
			}
			return best == int.MaxValue ? 999 : best;
		}

		private static bool IsBetween(TileStrip strip, int a, int b, int width)
		{
			var minX = Math.Min(a % width, b % width) - 1;
			var maxX = Math.Max(a % width, b % width) + 1;
			var minY = Math.Min(a / width, b / width) - 1;
			var maxY = Math.Max(a / width, b / width) + 1;

			for (var i = 0; i < strip.Count; i++)
			{
				var index = strip.First + strip.Stride * i;
				var x = index % width;
				var y = index / width;

				if (x >= minX && x <= maxX && y >= minY && y <= maxY)
					return true;
			}

			return false;
		}

		private static int Manhattan(int a, int b, int width)
		{
			var ax = a % width;
			var ay = a / width;
			var bx = b % width;
			var by = b / width;
			return Math.Abs(ax - bx) + Math.Abs(ay - by);
		}

		private static bool Contains(TileStrip strip, int index)
		{
			for (var i = 0; i < strip.Count; i++)
			{
				if (strip.First + strip.Stride * i == index)
					return true;
			}
			return false;
		}

		private static bool IntersectsArea(TileStrip strip, bool[] area)
		{
			for (var i = 0; i < strip.Count; i++)
			{
				var index = strip.First + strip.Stride * i;
				if (index >= 0 && index < area.Length && area[index])
					return true;
			}

			return false;
		}

		private static bool ContainsNav(TileStrip strip, int[] state, TileRule[] rules)
		{
			for (var i = 0; i < strip.Count; i++)
			{
				var index = strip.First + strip.Stride * i;
				if (TryGetRule(state, index, rules, out var rule) && rule.Nav != 0)
					return true;
			}
			return false;
		}

		private static bool TouchesNav(TileStrip strip, int[] state, TileRule[] rules, int width, int height)
		{
			for (var i = 0; i < strip.Count; i++)
			{
				var index = strip.First + strip.Stride * i;

				foreach (var dir in Navigation.Directions)
				{
					var next = GetAdjacentTile(index, dir, width, height);
					if (next >= 0 && TryGetRule(state, next, rules, out var rule) && rule.Nav != 0)
						return true;
				}
			}

			return false;
		}

		private static int Count(bool[] values)
		{
			if (values == null)
				return 0;

			var count = 0;
			for (var i = 0; i < values.Length; i++)
				if (values[i])
					count++;
			return count;
		}

		private static bool Reverses(TileStrip current, TileStrip previous)
			=> previous.Count > 1
			   && current.Count == previous.Count
			   && current.Stride == -previous.Stride
			   && current.First == previous.Last;

		private static ulong StripKey(TileStrip strip)
		{
			unchecked
			{
				var key = 1469598103934665603UL;
				key = (key ^ (uint)strip.First) * 1099511628211UL;
				key = (key ^ (uint)strip.Count) * 1099511628211UL;
				key = (key ^ (uint)strip.Stride) * 1099511628211UL;
				return key;
			}
		}

		private static ulong HashState(int[] state, int sourceTile, int destinationTile)
		{
			unchecked
			{
				var hash = 1469598103934665603UL;
				hash = (hash ^ (uint)sourceTile) * 1099511628211UL;
				hash = (hash ^ (uint)destinationTile) * 1099511628211UL;

				for (var i = 0; i < state.Length; i++)
					hash = (hash ^ (uint)state[i]) * 1099511628211UL;

				return hash;
			}
		}

		private static void ApplyMove(int[] state, TileStrip strip)
			=> ArrayExtensions.RollArray(state, strip.First, strip.Count, 1, strip.Stride);

		private static void UndoMove(int[] state, TileStrip strip)
			=> ArrayExtensions.RollArray(state, strip.First, strip.Count, -1, strip.Stride);

		private static bool TryGetTileStrip(int[] state, int startIndex, int stride, bool difficult, TileRule[] rules, int width, int height, out TileStrip strip)
		{
			strip = default;

			if (stride == 0 || !TryGetRule(state, startIndex, rules, out var tile) || !tile.Drag)
				return false;

			strip.First = startIndex;
			strip.Count = 1;

			var lastIndex = startIndex;

			if (!difficult && tile.Roll)
			{
				while (TryGetNextRule(state, lastIndex, stride, width, height, rules, out var nextTile) && nextTile.Roll)
					lastIndex += stride;
			}

			while (TryGetNextRule(state, lastIndex, stride, width, height, rules, out tile))
			{
				if (!tile.Drag || (!difficult && tile.Roll))
					break;

				lastIndex += stride;
			}

			while (difficult)
			{
				if (!TryGetNextRule(state, lastIndex, stride, width, height, rules, out tile))
					break;

				if (!(tile.Drag || tile.Roll))
					break;

				lastIndex += stride;
			}

			while (TryGetNextRule(state, lastIndex, stride, width, height, rules, out tile))
			{
				if (!(tile.Fold || tile.Roll))
					break;

				lastIndex += stride;
			}

			var lastTile = GetRuleAt(state, lastIndex, rules);

			if (!(lastTile.Fold || lastTile.Roll))
				return false;

			if (!difficult && GetRuleAt(state, startIndex, rules).Roll)
			{
				while (lastIndex != strip.First && !lastTile.Roll)
				{
					lastIndex -= stride;
					lastTile = GetRuleAt(state, lastIndex, rules);
				}
			}

			if (!(lastTile.Fold || lastTile.Roll))
				return false;

			if (!difficult)
			{
				var allRoll = true;
				for (var index = strip.First; allRoll; index += stride)
				{
					if (!GetRuleAt(state, index, rules).Roll)
						allRoll = false;

					if (index == lastIndex)
						break;
				}

				if (allRoll)
				{
					while (TryGetNextRule(state, strip.First, -stride, width, height, rules, out tile))
					{
						if (tile.Roll)
						{
							strip.First -= stride;
							continue;
						}

						if (tile.Drag)
							strip.First -= stride;

						break;
					}
				}
			}

			while (TryGetNextRule(state, strip.First, -stride, width, height, rules, out tile))
			{
				if (!(tile.Fold || tile.Roll))
					break;

				strip.First -= stride;
			}

			var testHard = difficult && !lastTile.Fold;
			while (testHard)
			{
				if (!TryGetNextRule(state, strip.First, -stride, width, height, rules, out tile))
					break;

				if (tile.Bake)
					break;

				strip.First -= stride;
			}

			strip.Count = (lastIndex - strip.First) / stride + 1;
			strip.Stride = stride;
			return strip.Count > 1;
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

		private static bool TryGetNextRule(int[] state, int currentIndex, int delta, int width, int height, TileRule[] rules, out TileRule rule)
		{
			rule = default;

			var x = (currentIndex % width) + (delta % width);
			var y = (currentIndex / width) + (delta / width);

			if (x < 0 || x >= width || y < 0 || y >= height)
				return false;

			return TryGetRule(state, y * width + x, rules, out rule);
		}

		private static int NavToDest(int[] state, int src, int dst, TileRule[] rules, int width, int height)
		{
			if (src == dst || src < 0 || dst < 0)
				return 0;

			foreach (var dirBit in Navigation.Directions)
			{
				var currentTile = src;
				var currentNav = GetRuleAt(state, src, rules).Nav & dirBit;

				while (currentNav != 0)
				{
					if (currentTile == dst)
						return dirBit;

					var nextTileIndex = GetAdjacentTile(currentTile, currentNav, width, height);
					if (nextTileIndex == -1 || nextTileIndex == src)
						break;

					var nextTile = GetRuleAt(state, nextTileIndex, rules);
					if (nextTile.Nav == 0)
						break;

					currentNav = Navigation.CalculateNav(currentNav, nextTile.Nav);
					currentTile = nextTileIndex;
				}
			}

			return 0;
		}

		private static int GetAdjacentTile(int index, int dir, int width, int height)
		{
			var dx = ((dir & 4) >> 2) - ((dir & 8) >> 3);
			var dz = ((dir & 1) >> 0) - ((dir & 2) >> 1);

			var x = (index % width) + dx;
			var y = (index / width) + dz;

			if (x < 0 || x >= width || y < 0 || y >= height)
				return -1;

			return y * width + x;
		}
	}
}
