using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace GamePreviewNamespace
{
	public class TileMovementHandler
	{
		private readonly MapManager mapManager;
		private readonly TileProperties.TileFlags movementFlags;

		public TileMovementHandler(MapManager mapManager, TileProperties.TileFlags movementFlags = TileProperties.TileFlags.Dock | TileProperties.TileFlags.Roll)
		{
			this.mapManager = mapManager;
			this.movementFlags = movementFlags;
		}

		public struct TileMovementBounds
		{
			public GridCoord MinWest;
			public GridCoord MaxEast;
			public GridCoord MinSouth;
			public GridCoord MaxNorth;
		}

		public struct TileChain
		{
			public List<int> TileIndices; // Ordered list of tile indices in the chain (head to tail)
			public int DirectionBit; // Drag direction (North, South, East, West)
		}

		public TileChain GetTileChain(int startTileIndex, int dragDirectionBit)
		{
			var chain = new TileChain
			{
				TileIndices = new List<int> { startTileIndex },
				DirectionBit = dragDirectionBit
			};

			if (dragDirectionBit == 0)
			{
				Debug.Log($"No direction specified for chain at index {startTileIndex}. Returning single tile chain.");
				return chain;
			}

			// Validate start tile index
			if (startTileIndex < 0 || startTileIndex >= mapManager.Tiles.Length)
			{
				Debug.LogWarning($"Invalid start tile index {startTileIndex}. Map size: {mapManager.Tiles.Length}");
				return chain;
			}

			// Get start tile properties
			var startProps = mapManager.GetTilePropertiesAt(startTileIndex);
			if (startProps == null || !startProps.CanBeDragged)
			{
				Debug.LogWarning($"Tile at index {startTileIndex} is not draggable: {(startProps == null ? "Empty" : $"CanBeDragged={startProps.CanBeDragged}")}");
				return chain;
			}
			Debug.Log($"Start tile {startTileIndex}: CanBeDragged={startProps.CanBeDragged}, DockOrRoll={startProps.DockOrRoll}, Coord=({mapManager.GetTileCoordinates(startTileIndex).X}, {mapManager.GetTileCoordinates(startTileIndex).Z})");

			// Log map dimensions
			Debug.Log($"Map dimensions: Width={mapManager.Width}, Height={mapManager.Height}, Total Tiles={mapManager.Tiles.Length}");

			// Use GetAdjacentTile to find contiguous tiles
			int currentIndex = startTileIndex;
			int iteration = 1;

			while (true)
			{
				// Get the next tile in the direction
				int nextIndex = mapManager.GetAdjacentTile(currentIndex, dragDirectionBit);
				Debug.Log($"Iteration {iteration}: Checking next tile from {currentIndex} in direction {dragDirectionBit}. Next index: {nextIndex}");

				// Check if nextIndex is valid
				if (nextIndex == -1 || nextIndex < 0 || nextIndex >= mapManager.Tiles.Length)
				{
					Debug.Log($"Stopped chain: Invalid next index {nextIndex} (boundary or out of bounds).");
					break;
				}

				// Get next tile properties
				var nextProps = mapManager.GetTilePropertiesAt(nextIndex);
				if (nextProps == null)
				{
					Debug.Log($"Stopped chain at index {nextIndex}: Empty tile");
					break;
				}

				Debug.Log($"Tile {nextIndex}: CanBeDragged={nextProps.CanBeDragged}, DockOrRoll={nextProps.DockOrRoll}, Coord=({mapManager.GetTileCoordinates(nextIndex).X}, {mapManager.GetTileCoordinates(nextIndex).Z})");
				// Include tiles that are either DockOrRoll or CanBeDragged
				if (!nextProps.DockOrRoll && !nextProps.CanBeDragged)
				{
					Debug.Log($"Stopped chain at index {nextIndex}: Non-DockOrRoll and Non-CanBeDragged tile");
					break;
				}

				chain.TileIndices.Add(nextIndex);
				Debug.Log($"Added tile {nextIndex} to chain.");
				currentIndex = nextIndex;
				iteration++;
			}

			// Sort indices by position (ascending for positive direction, descending for negative)
			bool isHorizontal = (dragDirectionBit & (TileProperties.East | TileProperties.West)) != 0;
			var (dx, dz) = TileProperties.GetDirectionOffset(dragDirectionBit);
			chain.TileIndices.Sort((a, b) =>
			{
				var coordA = mapManager.GetTileCoordinates(a);
				var coordB = mapManager.GetTileCoordinates(b);
				int comparison = isHorizontal ? coordA.X.CompareTo(coordB.X) : coordA.Z.CompareTo(coordB.Z);
				bool isPositiveDir = (isHorizontal && dx > 0) || (!isHorizontal && dz > 0);
				return isPositiveDir ? comparison : -comparison;
			});

			Debug.Log($"Chain detected: [{string.Join(", ", chain.TileIndices)}] for direction {dragDirectionBit}");
			return chain;
		}

		public void HighlightChain(TileChain chain, bool highlight)
		{
			foreach (var tileIndex in chain.TileIndices)
			{
				if (tileIndex < 0 || tileIndex >= mapManager.Tiles.Length)
				{
					Debug.LogWarning($"Invalid tile index {tileIndex}.");
					continue;
				}

				var tile = mapManager.Tiles[tileIndex];
				if (tile.GameObject == null)
				{
					Debug.LogWarning($"Tile at index {tileIndex} has no GameObject.");
					continue;
				}

				var meshRenderer = tile.GameObject.GetComponentInChildren<MeshRenderer>();
				if (meshRenderer == null)
				{
					Debug.LogWarning($"Tile at index {tileIndex} has no MeshRenderer.");
					continue;
				}

				if (highlight)
				{
					// Store original material
					if (!tile.GameObject.TryGetComponent<OriginalMaterialHolder>(out var holder))
					{
						holder = tile.GameObject.AddComponent<OriginalMaterialHolder>();
						holder.originalMaterial = meshRenderer.material;
					}

					// Create or reuse red material
					var redMaterial = new Material(meshRenderer.material) { color = Color.red };
					meshRenderer.material = redMaterial;
					Debug.Log($"Highlighted tile {tileIndex} in red.");
				}
				else
				{
					// Restore original material
					if (tile.GameObject.TryGetComponent<OriginalMaterialHolder>(out var holder) && holder.originalMaterial != null)
					{
						meshRenderer.material = holder.originalMaterial;
						Debug.Log($"Restored material for tile {tileIndex}.");
					}
					else
					{
						// Fallback to MapManager's material
						var originalMaterial = mapManager.GetTileMeshMaterial(tileIndex);
						if (originalMaterial != null)
						{
							meshRenderer.material = originalMaterial;
							Debug.Log($"Restored material for tile {tileIndex} from MapManager.");
						}
						else
							Debug.LogWarning($"Failed to restore material for tile {tileIndex}.");
					}
				}
			}
		}

		// Original single-tile movement methods (unchanged)
		public TileMovementBounds GetMovementBounds(int tileIndex)
		{
			return new TileMovementBounds
			{
				MinWest = mapManager.GetTileCoordinatesForLast(tileIndex, TileProperties.West, movementFlags),
				MaxEast = mapManager.GetTileCoordinatesForLast(tileIndex, TileProperties.East, movementFlags),
				MinSouth = mapManager.GetTileCoordinatesForLast(tileIndex, TileProperties.South, movementFlags),
				MaxNorth = mapManager.GetTileCoordinatesForLast(tileIndex, TileProperties.North, movementFlags)
			};
		}

		public bool ValidateMove(int sourceIndex, int targetIndex)
		{
			if (targetIndex == sourceIndex || targetIndex < 0 || targetIndex >= mapManager.Tiles.Length) return false;
			var targetProps = mapManager.GetTilePropertiesAt(targetIndex);
			return targetProps != null && targetProps.Movable;
		}

		public Vector3 ApplyDragOffset(int tileIndex, Vector3 currentPos, Vector3 offset)
		{
			var bounds = GetMovementBounds(tileIndex);
			var dragPos = currentPos + offset;
			dragPos.x = Mathf.Clamp(dragPos.x, bounds.MinWest.X, bounds.MaxEast.X);
			dragPos.z = Mathf.Clamp(dragPos.z, bounds.MinSouth.Z, bounds.MaxNorth.Z);
			return dragPos;
		}

		public bool TrySnapToGrid(int tileIndex, Vector3 currentPos, out int targetIndex)
		{
			targetIndex = -1;
			var bounds = GetMovementBounds(tileIndex);
			int x = Mathf.RoundToInt(Mathf.Clamp(currentPos.x, bounds.MinWest.X, bounds.MaxEast.X));
			int z = Mathf.RoundToInt(Mathf.Clamp(currentPos.z, bounds.MinSouth.Z, bounds.MaxNorth.Z));
			var targetCoord = new GridCoord(x, z);
			targetIndex = targetCoord.ToIndex(mapManager.Width);

			return ValidateMove(tileIndex, targetIndex);
		}

		public void SwapTiles(int sourceIndex, int targetIndex, bool updatePositions = true)
		{
			if (targetIndex < 0 || targetIndex >= mapManager.Tiles.Length || sourceIndex < 0 || !ValidateMove(sourceIndex, targetIndex))
			{
				Debug.LogWarning($"Invalid swap: source={sourceIndex}, target={targetIndex}");
				return;
			}

			var oldPosition = mapManager.GetTilePosition(sourceIndex);
			var newPosition = mapManager.GetTilePosition(targetIndex);

			var temp = mapManager.Tiles[sourceIndex];
			mapManager.Tiles[sourceIndex] = mapManager.Tiles[targetIndex];
			mapManager.Tiles[targetIndex] = temp;

			if (updatePositions)
			{
				var draggedTile = mapManager.Tiles[targetIndex].GameObject;
				var targetTile = mapManager.Tiles[sourceIndex].GameObject;
				if (targetTile != null)
				{
					string tempName = draggedTile.name;
					draggedTile.name = targetTile.name;
					targetTile.name = tempName;
					targetTile.transform.position = oldPosition;
				}
				if (draggedTile != null)
					draggedTile.transform.position = newPosition;
			}
		}
	}

	public class OriginalMaterialHolder : MonoBehaviour
	{
		public Material originalMaterial;
	}
}