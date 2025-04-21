using UnityEngine;
using System.Collections.Generic;

namespace GamePreviewNamespace
{
	public class TileMovementHandler
	{
		private readonly MapManager mapManager;
		public TileMovementHandler(MapManager mapManager) => this.mapManager = mapManager;

		public struct TileStrip
		{
			public List<int> TileIndices; // Ordered list of tile indices in the strip (head to tail)
			public int DirectionBit; // Drag direction (North, South, East, West)
		}

		public TileStrip GetTileStrip(int startTileIndex, int dragDirectionBit)
		{
			var strip = new TileStrip
			{
				TileIndices = new List<int> { startTileIndex },
				DirectionBit = dragDirectionBit
			};

			if (dragDirectionBit == 0)
				return strip;

			// Validate start tile index
			if (startTileIndex < 0 || startTileIndex >= mapManager.Tiles.Length)
				return strip;

			// Get start tile properties
			var startProps = mapManager.GetTilePropertiesAt(startTileIndex);
			if (startProps == null || !startProps.Interactive)
				return strip;

			// Use GetAdjacentTile to find contiguous tiles
			int currentIndex = startTileIndex;
			while (true)
			{
				// Get the next tile in the direction
				int nextIndex = mapManager.GetAdjacentTile(currentIndex, dragDirectionBit);

				// Get next tile properties
				var nextProps = mapManager.GetTilePropertiesAt(nextIndex);
				if (nextProps == null || false == nextProps.Interactive) break;

				strip.TileIndices.Add(nextIndex);
				currentIndex = nextIndex;
			}

			// Sort indices by position (ascending for positive direction, descending for negative)
			bool isHorizontal = (dragDirectionBit & (TileProperties.East | TileProperties.West)) != 0;
			var (dx, dz) = TileProperties.GetDirectionOffset(dragDirectionBit);
			strip.TileIndices.Sort((a, b) =>
			{
				var coordA = mapManager.GetTileCoordinates(a);
				var coordB = mapManager.GetTileCoordinates(b);
				int comparison = isHorizontal ? coordA.X.CompareTo(coordB.X) : coordA.Z.CompareTo(coordB.Z);
				bool isPositiveDir = (isHorizontal && dx > 0) || (!isHorizontal && dz > 0);
				return isPositiveDir ? comparison : -comparison;
			});

			return strip;
		}

		public void HighlightStrip(TileStrip strip, bool highlight)
		{
			foreach (var tileIndex in strip.TileIndices)
			{
				var tile = mapManager.Tiles[tileIndex].GameObject;
				var meshRenderer = tile.GetComponentInChildren<MeshRenderer>();
				if (meshRenderer == null) continue;

				if (highlight)
				{
					// Store original material
					if (!tile.TryGetComponent<OriginalMaterialHolder>(out var holder))
					{
						holder = tile.AddComponent<OriginalMaterialHolder>();
						holder.originalMaterial = meshRenderer.material;
					}

					// Assign red material
					meshRenderer.material = new Material(meshRenderer.material) { color = Color.red };
				}
				else
				{
					// Restore original material
					if (tile.TryGetComponent<OriginalMaterialHolder>(out var holder) && holder.originalMaterial != null)
					{
						meshRenderer.material = holder.originalMaterial;
					}
					else
					{
						// Fallback to MapManager's material
						var originalMaterial = mapManager.GetTileMeshMaterial(tileIndex);
						if (originalMaterial != null)
							meshRenderer.material = originalMaterial;
					}
				}
			}
		}

		public bool ValidateMove(int sourceIndex, int targetIndex)
		{
			if (targetIndex == sourceIndex || targetIndex < 0 || targetIndex >= mapManager.Tiles.Length) return false;
			var targetProps = mapManager.GetTilePropertiesAt(targetIndex);
			return targetProps != null && targetProps.CanMove;
		}

		public bool TrySnapToGrid(int tileIndex, Vector3 currentPos, out int targetIndex)
		{
			var bounds = mapManager.GetMovementBounds(tileIndex, TileProperties.TileFlags.Dock | TileProperties.TileFlags.Roll);
			int x = Mathf.RoundToInt(Mathf.Clamp(currentPos.x, bounds.MinWest.X, bounds.MaxEast.X));
			int z = Mathf.RoundToInt(Mathf.Clamp(currentPos.z, bounds.MinSouth.Z, bounds.MaxNorth.Z));
			var targetCoord = new GridCoord(x, z);
			targetIndex = mapManager.ToIndex(targetCoord);

			return ValidateMove(tileIndex, targetIndex);
		}

		public void SwapTiles(int sourceIndex, int targetIndex)
		{
			if (targetIndex < 0 || targetIndex >= mapManager.Tiles.Length || sourceIndex < 0 || !ValidateMove(sourceIndex, targetIndex)) return;

			var oldPosition = mapManager.GetTilePosition(sourceIndex);
			var newPosition = mapManager.GetTilePosition(targetIndex);

			var temp = mapManager.Tiles[sourceIndex];
			mapManager.Tiles[sourceIndex] = mapManager.Tiles[targetIndex];
			mapManager.Tiles[targetIndex] = temp;

			var draggedTile = mapManager.Tiles[targetIndex].GameObject;
			var targetTile = mapManager.Tiles[sourceIndex].GameObject;
			string tempName = draggedTile.name;
			draggedTile.name = targetTile.name;
			targetTile.name = tempName;
			draggedTile.transform.position = newPosition;
			targetTile.transform.position = oldPosition;
		}
	}

	public class OriginalMaterialHolder : MonoBehaviour
	{
		public Material originalMaterial;
	}
}