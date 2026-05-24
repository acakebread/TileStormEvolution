using System;
using System.Collections.Generic;
using UnityEngine;

namespace ClassicTilestorm
{
	internal static class TileRouteAssembler
	{
		private const int NodeBudget = 500000;
		private const int IslandDepthBudget = 8;

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

		private readonly struct IslandRouteConstraint
		{
			public readonly Anchor Source;
			public readonly Anchor Destination;

			public IslandRouteConstraint(Anchor source, Anchor destination)
			{
				Source = source;
				Destination = destination;
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
			var sourceAnchors = FindAnchors(BuildScratchGrid(state, rules), sourceTile, null, map.Width, map.Height);
			var attempts = 0;
			var lastSummary = "no source anchors.";

			foreach (var sourceAnchor in sourceAnchors)
			{
				if (TrySolveIslandChain(
					state,
					sourceAnchor,
					sourceTile,
					destinationTile,
					rules,
					map.Width,
					map.Height,
					new Dictionary<string, HashSet<string>>(),
					new Dictionary<string, List<IslandRouteConstraint>>(),
					0,
					ref attempts,
					out var assembledState,
					out var summary))
				{
					result = new Result
					{
						State = assembledState,
						Summary = summary
					};
					return true;
				}

				lastSummary = summary;
			}

			result = new Result
			{
				State = null,
				Summary = $"no route assembled after {attempts} anchor attempt(s); last attempt: {lastSummary}"
			};
			return false;
		}

		private static bool TrySolveIslandChain(
			int[] state,
			Anchor sourceAnchor,
			int originalSourceTile,
			int destinationTile,
			TileRule[] rules,
			int width,
			int height,
			Dictionary<string, HashSet<string>> consumedIslandAnchors,
			Dictionary<string, List<IslandRouteConstraint>> islandConstraints,
			int depth,
			ref int attempts,
			out int[] assembledState,
			out string summary)
		{
			assembledState = null;
			summary = null;

			if (depth > IslandDepthBudget)
			{
				summary = $"island chain exceeded depth budget {IslandDepthBudget}.";
				return false;
			}

			var baseGrid = BuildScratchGrid(state, rules);
			var destinationAnchors = new List<Anchor>();
			var rawPlayable = FloodPlayableArea(baseGrid, sourceAnchor.PlayableCell, sourceAnchor, destinationAnchors, width, height);
			var islandKey = BuildIslandKey(rawPlayable);
			if (IsAnchorConsumed(consumedIslandAnchors, islandKey, sourceAnchor))
			{
				summary = $"island {islandKey} source anchor {DescribeAnchor(sourceAnchor)} was already consumed in this chain.";
				return false;
			}

			FilterReachableExits(baseGrid, rawPlayable, destinationAnchors, destinationTile, width, height);
			RemoveConsumedAnchors(destinationAnchors, consumedIslandAnchors, islandKey);
			var navTiles = CollectMovableNavTiles(state, rawPlayable, rules);
			var navBudget = CountNavTiles(navTiles);
			var reusableCrossroads = CountReusableCrossroads(navTiles);
			var rawCount = Count(rawPlayable);
			var existingConstraints = GetIslandConstraints(islandConstraints, islandKey);

			foreach (var destinationAnchor in destinationAnchors)
			{
				attempts++;
				if (!TrySolveSingleIsland(
					state,
					baseGrid,
					sourceAnchor,
					destinationAnchor,
					rawPlayable,
					navTiles,
					navBudget,
					reusableCrossroads,
					existingConstraints,
					rules,
					width,
					height,
					out var islandState,
					out var islandSummary))
				{
					summary = islandSummary;
					continue;
				}

				if (HasFlexibleRouteToDest(islandState, originalSourceTile, destinationTile, rules, width, height))
				{
					assembledState = islandState;
					summary = $"assembled {depth + 1} island(s); {islandSummary}";
					return true;
				}

				var islandGrid = BuildScratchGrid(islandState, rules);
				if (!TryTraceExitContinuation(islandGrid, rawPlayable, destinationAnchor, destinationTile, width, height, out var continuation))
				{
					summary = $"{islandSummary}; exit did not reach destination or another island.";
					continue;
				}

				if (continuation.ReachedDestination)
				{
					assembledState = islandState;
					summary = $"assembled {depth + 1} island(s); {islandSummary}";
					return true;
				}

				var nextConsumedIslandAnchors = CloneConsumedAnchors(consumedIslandAnchors);
				AddConsumedAnchor(nextConsumedIslandAnchors, islandKey, sourceAnchor);
				AddConsumedAnchor(nextConsumedIslandAnchors, islandKey, destinationAnchor);
				var nextIslandConstraints = CloneIslandConstraints(islandConstraints);
				AddIslandConstraint(nextIslandConstraints, islandKey, new IslandRouteConstraint(sourceAnchor, destinationAnchor));

				if (TrySolveIslandChain(
					islandState,
					continuation.NextIslandAnchor,
					originalSourceTile,
					destinationTile,
					rules,
					width,
					height,
					nextConsumedIslandAnchors,
					nextIslandConstraints,
					depth + 1,
					ref attempts,
					out assembledState,
					out var chainSummary))
				{
					summary = $"{islandSummary}; {chainSummary}";
					return true;
				}

				summary = chainSummary;
			}

			if (destinationAnchors.Count == 0)
				summary = $"source {sourceAnchor.StaticCell}->{sourceAnchor.PlayableCell}, area {rawCount}, pool {DescribeNavTiles(navTiles)}, no static-nav exit in raw flood.";

			return false;
		}

		private static bool TrySolveSingleIsland(
			int[] state,
			CellBits[] baseGrid,
			Anchor sourceAnchor,
			Anchor destinationAnchor,
			bool[] rawPlayable,
			Dictionary<int, Queue<int>> navTiles,
			int navBudget,
			int reusableCrossroads,
			List<IslandRouteConstraint> existingConstraints,
			TileRule[] rules,
			int width,
			int height,
			out int[] assembledState,
			out string summary)
		{
			assembledState = null;
			var rawCount = Count(rawPlayable);
			var solutionArea = CullSolutionArea(
				baseGrid,
				rawPlayable,
				sourceAnchor.PlayableCell,
				destinationAnchor.PlayableCell,
				navBudget,
				reusableCrossroads,
				width,
				height);

			var searchGrid = (CellBits[])baseGrid.Clone();
			var route = new List<RouteCell>();
			var counts = CloneCounts(navTiles);
			var visited = new bool[state.Length];
			var navVisits = new HashSet<int>();
			var nodes = 0;
			var searchBudgetExhausted = false;
			summary = $"source {sourceAnchor.StaticCell}->{sourceAnchor.PlayableCell}, destination {destinationAnchor.StaticCell}<-{destinationAnchor.PlayableCell}, area {rawCount}->{Count(solutionArea)}, pool {DescribeNavTiles(navTiles)}, nodes {nodes}.";

			if (!solutionArea[sourceAnchor.PlayableCell] || !solutionArea[destinationAnchor.PlayableCell])
			{
				summary += " endpoint was culled.";
				return false;
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
				width,
				height,
				ref nodes,
				ref searchBudgetExhausted))
			{
				var reason = searchBudgetExhausted
					? $"search budget exhausted at {nodes}/{NodeBudget} nodes"
					: "no compatible chain";
				summary = $"source {sourceAnchor.StaticCell}->{sourceAnchor.PlayableCell}, destination {destinationAnchor.StaticCell}<-{destinationAnchor.PlayableCell}, area {rawCount}->{Count(solutionArea)}, pool {DescribeNavTiles(navTiles)}, nodes {nodes}, {reason}.";
				return false;
			}

			if (!TryBuildState(state, rawPlayable, route, navTiles, out assembledState))
			{
				summary = $"found route of {route.Count} tile(s), but failed to build state.";
				return false;
			}

			var constraints = BuildConstraintsWithCurrent(existingConstraints, sourceAnchor, destinationAnchor);
			if (!ValidateIslandConstraints(assembledState, constraints, rules, width, height))
			{
				if (!TryRepairIslandBySwapping(assembledState, rawPlayable, constraints, rules, width, height, out assembledState))
				{
					summary = $"assembled route with {route.Count} tile(s), but existing island constraints could not be preserved.";
					return false;
				}

				summary = $"assembled route with {route.Count} movable tile(s), repaired locked island constraints by swapping, area {rawCount}->{Count(solutionArea)}, pool {DescribeNavTiles(navTiles)}, anchors {sourceAnchor.StaticCell}->{sourceAnchor.PlayableCell} and {destinationAnchor.StaticCell}<-{destinationAnchor.PlayableCell}, nodes {nodes}.";
				return true;
			}

			summary = $"assembled route with {route.Count} movable tile(s), area {rawCount}->{Count(solutionArea)}, pool {DescribeNavTiles(navTiles)}, anchors {sourceAnchor.StaticCell}->{sourceAnchor.PlayableCell} and {destinationAnchor.StaticCell}<-{destinationAnchor.PlayableCell}, nodes {nodes}.";
			return true;
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
				if (IsSameAnchor(existing, anchor))
				{
					return;
				}
			}

			anchors.Add(anchor);
		}

		private static bool IsSameAnchor(Anchor a, Anchor b)
			=> a.StaticCell == b.StaticCell &&
				a.PlayableCell == b.PlayableCell &&
				a.Direction == b.Direction;

		private static bool[] FloodPlayableArea(CellBits[] grid, int source, Anchor ignoredEntrance, List<Anchor> exits, int width, int height)
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
					if (next < 0 || area[next])
						continue;

					if (IsPlayableAt(grid, next))
					{
						area[next] = true;
						queue.Enqueue(next);
						continue;
					}

					if (!IsStaticNavAt(grid, next))
						continue;

					var staticToPlayableDirection = Navigation.GetOppositeDirection(direction);
					if ((grid[next].Nav & staticToPlayableDirection) == 0)
						continue;

					var exit = new Anchor(next, current, staticToPlayableDirection);
					if (IsSameAnchor(exit, ignoredEntrance))
						continue;

					AddAnchor(exits, exit);
				}
			}

			return area;
		}

		private static void FilterReachableExits(CellBits[] grid, bool[] playableArea, List<Anchor> exits, int destinationTile, int width, int height)
		{
			if (grid == null || playableArea == null || exits == null)
				return;

			for (var i = exits.Count - 1; i >= 0; i--)
			{
				if (!ExitCanReachDestinationOrNewIsland(grid, playableArea, exits[i], destinationTile, width, height))
					exits.RemoveAt(i);
			}
		}

		private readonly struct ExitContinuation
		{
			public readonly bool ReachedDestination;
			public readonly Anchor NextIslandAnchor;

			public ExitContinuation(bool reachedDestination, Anchor nextIslandAnchor)
			{
				ReachedDestination = reachedDestination;
				NextIslandAnchor = nextIslandAnchor;
			}
		}

		private readonly struct TraceDirectionState
		{
			public readonly int Cell;
			public readonly int IncomingDirection;

			public TraceDirectionState(int cell, int incomingDirection)
			{
				Cell = cell;
				IncomingDirection = incomingDirection;
			}
		}

		private static bool ExitCanReachDestinationOrNewIsland(CellBits[] grid, bool[] playableArea, Anchor exit, int destinationTile, int width, int height)
			=> TryTraceExitContinuation(grid, playableArea, exit, destinationTile, width, height, out _);

		private static bool TryTraceExitContinuation(CellBits[] grid, bool[] playableArea, Anchor exit, int destinationTile, int width, int height, out ExitContinuation continuation)
		{
			continuation = default;
			if (exit.StaticCell == destinationTile)
			{
				continuation = new ExitContinuation(true, default);
				return true;
			}

			var queue = new Queue<TraceDirectionState>();
			var visited = new HashSet<int>();
			var initialIncoming = Navigation.GetOppositeDirection(exit.Direction);
			queue.Enqueue(new TraceDirectionState(exit.StaticCell, initialIncoming));

			while (queue.Count > 0 && visited.Count < NodeBudget)
			{
				var currentState = queue.Dequeue();
				var current = currentState.Cell;
				var incoming = currentState.IncomingDirection;
				var visitKey = current * 16 + incoming;
				if (!visited.Add(visitKey))
					continue;

				var outgoing = Navigation.CalculateNav(incoming, grid[current].Nav);
				if (outgoing == 0)
					continue;

				var next = GetAdjacentTile(current, outgoing, width, height);
				if (next < 0 || next >= grid.Length)
					continue;

				if (next == destinationTile)
				{
					continuation = new ExitContinuation(true, default);
					return true;
				}

				if (IsPlayableAt(grid, next))
				{
					if (!playableArea[next])
					{
						continuation = new ExitContinuation(false, new Anchor(current, next, outgoing));
						return true;
					}

					continue;
				}

				if (!IsStaticNavAt(grid, next))
					continue;

				queue.Enqueue(new TraceDirectionState(next, outgoing));
			}

			return false;
		}

		private static string BuildIslandKey(bool[] playableArea)
		{
			if (playableArea == null)
				return "<null>";

			var cells = new List<int>();
			for (var i = 0; i < playableArea.Length; i++)
			{
				if (playableArea[i])
					cells.Add(i);
			}

			return string.Join(",", cells);
		}

		private static Dictionary<string, HashSet<string>> CloneConsumedAnchors(Dictionary<string, HashSet<string>> consumedAnchors)
		{
			var clone = new Dictionary<string, HashSet<string>>();
			if (consumedAnchors == null)
				return clone;

			foreach (var pair in consumedAnchors)
				clone[pair.Key] = new HashSet<string>(pair.Value);

			return clone;
		}

		private static void AddConsumedAnchor(Dictionary<string, HashSet<string>> consumedAnchors, string islandKey, Anchor anchor)
		{
			if (consumedAnchors == null || string.IsNullOrEmpty(islandKey))
				return;

			if (!consumedAnchors.TryGetValue(islandKey, out var anchors))
			{
				anchors = new HashSet<string>();
				consumedAnchors[islandKey] = anchors;
			}

			anchors.Add(BuildAnchorKey(anchor));
		}

		private static bool IsAnchorConsumed(Dictionary<string, HashSet<string>> consumedAnchors, string islandKey, Anchor anchor)
			=> consumedAnchors != null &&
				consumedAnchors.TryGetValue(islandKey, out var anchors) &&
				anchors.Contains(BuildAnchorKey(anchor));

		private static void RemoveConsumedAnchors(List<Anchor> anchors, Dictionary<string, HashSet<string>> consumedAnchors, string islandKey)
		{
			if (anchors == null || consumedAnchors == null || !consumedAnchors.TryGetValue(islandKey, out var consumed))
				return;

			for (var i = anchors.Count - 1; i >= 0; i--)
			{
				if (consumed.Contains(BuildAnchorKey(anchors[i])))
					anchors.RemoveAt(i);
			}
		}

		private static string BuildAnchorKey(Anchor anchor)
			=> $"{anchor.StaticCell}:{anchor.PlayableCell}:{anchor.Direction}";

		private static string DescribeAnchor(Anchor anchor)
			=> $"{anchor.StaticCell}->{anchor.PlayableCell}/{DescribeNav(anchor.Direction)}";

		private static Dictionary<string, List<IslandRouteConstraint>> CloneIslandConstraints(Dictionary<string, List<IslandRouteConstraint>> constraints)
		{
			var clone = new Dictionary<string, List<IslandRouteConstraint>>();
			if (constraints == null)
				return clone;

			foreach (var pair in constraints)
				clone[pair.Key] = new List<IslandRouteConstraint>(pair.Value);

			return clone;
		}

		private static List<IslandRouteConstraint> GetIslandConstraints(Dictionary<string, List<IslandRouteConstraint>> constraints, string islandKey)
		{
			if (constraints == null || !constraints.TryGetValue(islandKey, out var result))
				return new List<IslandRouteConstraint>();

			return result;
		}

		private static void AddIslandConstraint(Dictionary<string, List<IslandRouteConstraint>> constraints, string islandKey, IslandRouteConstraint constraint)
		{
			if (constraints == null || string.IsNullOrEmpty(islandKey))
				return;

			if (!constraints.TryGetValue(islandKey, out var islandConstraints))
			{
				islandConstraints = new List<IslandRouteConstraint>();
				constraints[islandKey] = islandConstraints;
			}

			islandConstraints.Add(constraint);
		}

		private static List<IslandRouteConstraint> BuildConstraintsWithCurrent(List<IslandRouteConstraint> existingConstraints, Anchor sourceAnchor, Anchor destinationAnchor)
		{
			var constraints = existingConstraints != null
				? new List<IslandRouteConstraint>(existingConstraints)
				: new List<IslandRouteConstraint>();

			constraints.Add(new IslandRouteConstraint(sourceAnchor, destinationAnchor));
			return constraints;
		}

		private static bool TryRepairIslandBySwapping(
			int[] state,
			bool[] islandCells,
			List<IslandRouteConstraint> constraints,
			TileRule[] rules,
			int width,
			int height,
			out int[] repairedState)
		{
			repairedState = null;
			if (state == null || islandCells == null || constraints == null)
				return false;

			var cells = new List<int>();
			for (var i = 0; i < islandCells.Length; i++)
			{
				if (islandCells[i])
					cells.Add(i);
			}

			for (var a = 0; a < cells.Count; a++)
			{
				for (var b = a + 1; b < cells.Count; b++)
				{
					var first = cells[a];
					var second = cells[b];
					var firstNav = GetRuleAt(state, first, rules).Nav;
					var secondNav = GetRuleAt(state, second, rules).Nav;
					if (firstNav == secondNav)
						continue;

					var candidate = (int[])state.Clone();
					(candidate[first], candidate[second]) = (candidate[second], candidate[first]);

					if (!ValidateIslandConstraints(candidate, constraints, rules, width, height))
						continue;

					repairedState = candidate;
					return true;
				}
			}

			return false;
		}

		private static bool ValidateIslandConstraints(int[] state, List<IslandRouteConstraint> constraints, TileRule[] rules, int width, int height)
		{
			if (constraints == null)
				return true;

			foreach (var constraint in constraints)
			{
				if (!HasRouteBetweenAnchors(state, constraint.Source, constraint.Destination, rules, width, height))
					return false;
			}

			return true;
		}

		private static bool HasRouteBetweenAnchors(int[] state, Anchor source, Anchor destination, TileRule[] rules, int width, int height)
		{
			if (state == null || rules == null)
				return false;

			var destinationEntryDirection = Navigation.GetOppositeDirection(destination.Direction);
			var current = source.StaticCell;
			var direction = source.Direction;
			var visited = new HashSet<int>();

			while (direction != 0)
			{
				var next = GetAdjacentTile(current, direction, width, height);
				if (next < 0)
					return false;

				if (next == destination.StaticCell)
					return direction == destinationEntryDirection;

				var visitKey = next * 16 + direction;
				if (!visited.Add(visitKey))
					return false;

				var nextNav = GetRuleAt(state, next, rules).Nav;
				if (nextNav == 0)
					return false;

				direction = Navigation.CalculateNav(direction, nextNav);
				current = next;
			}

			return false;
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
			ref int nodes,
			ref bool searchBudgetExhausted)
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
					searchBudgetExhausted = true;
					navVisits.Remove(visitKey);
					return false;
				}

				if (current == destinationCell && (outgoing & exitDirection) != 0)
					return true;

				foreach (var direction in OrderedOutputDirections(outgoing, current, destinationCell, width, height))
				{
					if (TryExtendRoute(grid, GetAdjacentTile(current, direction, width, height), direction, destinationCell, exitDirection, solutionArea, navCounts, visited, navVisits, route, width, height, ref nodes, ref searchBudgetExhausted))
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

				foreach (var direction in OrderedOutputDirections(outgoing, current, destinationCell, width, height))
				{
					var next = GetAdjacentTile(current, direction, width, height);
					if (!CanAcceptIncoming(grid, next, direction, destinationCell, exitDirection, solutionArea, navCounts))
						continue;

					if (TryExtendRoute(grid, next, direction, destinationCell, exitDirection, solutionArea, navCounts, visited, navVisits, route, width, height, ref nodes, ref searchBudgetExhausted))
						return true;
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
				return outgoing != 0;
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
