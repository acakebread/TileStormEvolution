using MassiveHadronLtd;
using System;
using System.Linq;
using UnityEngine;

namespace ClassicTilestorm
{
	public static class MapUtils
	{
		public static Variant NextVariantOnMap(IMapEdit map, Vector3 worldPos, Variant variant, bool cycleHeight = false)
		{
			var current = map.GetVariantAt(worldPos);
			if (current.hash == variant.hash)
			{
				float[] angles = { 0f, 90f, 180f, 270f };
				float[] deltas = { 0f, 0.25f, 0.5f, 0.75f, 1f };

				int dIdx = Array.IndexOf(deltas, current.delta.y); if (dIdx < 0) dIdx = 0;
				int aIdx = Array.IndexOf(angles, current.angle); if (aIdx < 0) aIdx = 0;

				aIdx = (aIdx + 1) % angles.Length;
				if (aIdx == 0) dIdx = (dIdx + 1) % deltas.Length;

				variant.delta = cycleHeight ? new Vector3(current.delta.x, deltas[dIdx], current.delta.z) : current.delta;
				variant.angle = angles[aIdx];
			}

			return variant;
		}

		internal static void RebuildMarkers(IMapEdit iMap, ISelectable[] selection)
		{
			var tiles = iMap?.GetAttachments()?.Select(a => a.tile)?.Distinct()?.ToArray() ?? Array.Empty<int>();
			var positions = new Vector3[tiles.Length];
			var colors = new Color[tiles.Length];
			var isWaypointMode = selection != null && selection.Length == 1 && selection[0] is Waypoint;

			for (var i = 0; i < tiles.Length; i++)
			{
				var tile = tiles[i];
				positions[i] = iMap.TileRenderPosition(tile);
				colors[i] = isWaypointMode && iMap.HasAttachmentOfType<View>(tile) ? new(0f, 1f, 1f, 0.5f) : new(0f, 0.7f, 1f, 0.7f);
			}

			var selectedTile = (selection != null && selection.Length > 0 && selection[0] is MapAttachment ma) ? ma.tile : -1;
			var selectedIndex = Array.IndexOf(tiles, selectedTile);
			EditorMarkerUtil.ShowMarkers(positions, colors, selectedIndex);
		}

		public static RectInt GetContentBounds(this Map map)
		{
			if (map.tiles == null || map.tiles.Length == 0 || map.width <= 0 || map.height <= 0)
				return new RectInt(0, 0, 0, 0);

			int minX = map.width;
			int minZ = map.height;
			int maxX = -1;
			int maxZ = -1;

			for (int i = 0; i < map.tiles.Length; i++)
			{
				int t = map.tiles[i];
				if (t < 0) continue;

				int hash = (t < map.variants.Length) ? map.variants[t].hash : 0;
				if (hash == 0) continue;

				var def = ResourceManager.GetDefinition(hash);
				if (def == null || def.IsDefault()) continue;

				int x = i % map.width;
				int z = i / map.width;

				minX = Math.Min(minX, x);
				maxX = Math.Max(maxX, x);
				minZ = Math.Min(minZ, z);
				maxZ = Math.Max(maxZ, z);
			}

			return maxX >= 0 ? new RectInt(minX, minZ, maxX - minX + 1, maxZ - minZ + 1) : new RectInt(0, 0, 0, 0);
		}

		public static Map Clone(Map map, Transform parent)
		{
			if (map.width <= 0 || map.height <= 0 || map.tiles == null || map.variants == null)
				return null;

			// CRITICAL: Work on a CLONE so we don't corrupt the original map's runtime state
			var previewMap = map.Clone();
			previewMap.parent = parent;
			if (!previewMap.InitialiseGraph())
			{
				Debug.LogWarning("Preview graph creation failed on clone");
				return null;
			}

			previewMap.UpdateLighting();

			previewMap.Preset();
			previewMap.RefreshAttachments(previewMap.GetAttachments());

			PreviewRenderLayers.SetLayerRecursively(parent.gameObject, PreviewRenderLayers.LAYER_PREVIEW);
			var particleControllers = parent.gameObject.GetComponentsInChildren<ParticleController>(true);
			foreach (var particleController in particleControllers) particleController.gameObject.layer = PreviewRenderLayers.previewTransparentLayer;
			PreviewRenderLayers.SetPreviewLayersToChildLights(parent);
			return previewMap;
		}

		public static bool Optimise(this Map map)
		{
			if (map.tiles == null || map.tiles.Length == 0 || map.variants == null || map.variants.Length == 0)
				return false;

			// Group by full Variant identity (hash + angle + delta)
			var grouped = map.tiles
				.Select(idx => idx >= 0 && idx < map.variants.Length ? map.variants[idx] : new Variant(0))
				.GroupBy(v => (v.hash, v.angle, v.delta))   // tuple key = full identity
				.Select(g => new
				{
					Variant = g.Key,
					Count = g.Count(),
				})
				.OrderByDescending(g => g.Count)
				.ThenBy(g => g.Variant.hash)// stable secondary sort
				.ThenBy(g => g.Variant.angle)
				.ThenBy(g => g.Variant.delta, Vector3LexComparer.Instance)
				.ToList();

			var newVariants = grouped
				.Select(g => new Variant(g.Variant.hash, g.Variant.delta, g.Variant.angle))
				.ToArray();

			// Build lookup: old key → new index
			var oldToNew = new System.Collections.Generic.Dictionary<(HashId, float, Vector3), int>(grouped.Count);
			for (int i = 0; i < newVariants.Length; i++)
			{
				var v = newVariants[i];
				oldToNew[(v.hash, v.angle, v.delta)] = i;
			}

			// Remap tiles to new indices
			var newTiles = new int[map.tiles.Length];
			for (int i = 0; i < map.tiles.Length; i++)
			{
				int oldIdx = map.tiles[i];
				var oldVariant = oldIdx >= 0 && oldIdx < map.variants.Length ? map.variants[oldIdx] : new Variant(0);
				var key = (oldVariant.hash, oldVariant.angle, oldVariant.delta);
				newTiles[i] = oldToNew[key];
			}

			// Detect what actually changed
			bool sizeChanged = newVariants.Length != map.variants.Length;

			bool orderChanged = !sizeChanged &&
				!map.variants.Select(v => (v.hash, v.angle, v.delta))
						 .SequenceEqual(newVariants.Select(v => (v.hash, v.angle, v.delta)));

			bool anythingChanged = sizeChanged || orderChanged;

			if (anythingChanged)
			{
				map.variants = newVariants;
				map.tiles = newTiles;

				if (sizeChanged)
				{
					string direction = newVariants.Length > map.variants.Length ? "increased" : "reduced";
					Debug.Log($"{map.name} consolidated: table size {direction} from {map.variants.Length} → {newVariants.Length}");
				}
				else if (orderChanged)
				{
					Debug.Log($"{map.name} consolidated: table order changed (size remains {newVariants.Length})");
				}

				// Invalidate caches
				map.InvalidateGraphCache();
			}

			return anythingChanged;
		}

		/// <summary>
		/// Finds or creates an exact matching variant (hash + normalized delta + angle).
		/// Mutates map.variants only when necessary.
		/// Returns the index in map.variants.
		/// Does NOT invalidate the graph — caller must do it if needed.
		/// </summary>
		public static int GetOrCreateVariantIndex(this Map map, HashId hash, Vector3 delta = new Vector3(), float angle = 0f)
		{
			//// Normalize delta (same rule as UpdateTileAt)
			//delta = new Vector3(
			//	((delta.x % 1f) + 1f) % 1f,
			//	delta.y,
			//	((delta.z % 1f) + 1f) % 1f
			//);

			if (map.variants == null)
			{
				map.variants = Array.Empty<Variant>();
			}

			// Search (identical to original)
			for (int i = 0; i < map.variants.Length; i++)
			{
				var v = map.variants[i];
				if (v.hash == hash &&
					Mathf.Approximately(v.angle, angle) &&
					Vector3LexComparer.ApproximatelyEqual(v.delta, delta))
				{
					return i;
				}
			}

			// Append new variant (using Array.Resize for simplicity & clarity)
			var newVariant = new Variant(hash, delta, angle);

			int newIndex = map.variants.Length;
			Array.Resize(ref map.variants, newIndex + 1);
			map.variants[newIndex] = newVariant;

			return newIndex;
		}

		public static int[] GenerateWaypoints(this Map map)
		{
			var generated = new System.Collections.Generic.List<int>();
			var start = map.GetStartTile();
			var end = map.GetEndTile();

			if (start == -1 || end == -1)
				return generated.ToArray();

			generated.Add(start);

			var cur = start;
			var dir = Navigation.NavToDest(map, cur, end);
			if (dir != 0)
			{
				while (cur != end)
				{
					if (map.FindAdjacentConsole(cur) != -1)
						generated.Add(cur);

					var next = Navigation.GetAdjacentTile(map, cur, dir);
					if (next == -1 || next == start) break;

					var nextTile = map.GetTile(next);
					if (nextTile.Nav == 0) break;

					dir = Navigation.CalculateNav(dir, nextTile.Nav);
					if (dir == 0) break;

					cur = next;
				}
			}

			generated.Add(end);
			return generated.ToArray();
		}
	}

	public static class MapExtensions
	{
		public static T GetAttachmentOfType<T>(this IMapPlay map, int tile) where T : MapAttachment
		{
			return map.GetAttachments(tileIndex: tile, filterTypes: new[] { typeof(T) })
					  .OfType<T>()
					  .FirstOrDefault();
		}

		public static bool HasAttachmentOfType<T>(this IMapPlay map, int tile) where T : MapAttachment
		{
			return map.GetAttachments(tileIndex: tile, filterTypes: new[] { typeof(T) })
					  .Length > 0;
		}

		public static Waypoint GetWaypoint(this IMapPlay map, int waypointIndex)
		{
			return map.GetAttachments(filterTypes: new[] { typeof(Waypoint) })
					  .Cast<Waypoint>()
					  .FirstOrDefault(w => w.waypointIndex == waypointIndex);
		}

		public static Waypoint[] GetWaypoints(this IMapPlay map)
		{
			return map.GetAttachments(filterTypes: new[] { typeof(Waypoint) })
					  .Cast<Waypoint>()
					  .OrderBy(w => w.waypointIndex)
					  .ToArray();
		}
	}
}