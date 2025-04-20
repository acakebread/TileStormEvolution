using UnityEngine;
using System.Collections.Generic;

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
				return chain;
			}

			// Validate start tile index
			if (startTileIndex < 0 || startTileIndex >= mapManager.Tiles.Length)
			{
				return chain;
			}

			// Get start tile properties
			var startProps = mapManager.GetTilePropertiesAt(startTileIndex);
			if (startProps == null || !startProps.CanBeDragged)
			{
				return chain;
			}

			// Use GetAdjacentTile to find contiguous tiles
			int currentIndex = startTileIndex;
			int iteration = 1;

			while (true)
			{
				// Get the next tile in the direction
				int nextIndex = mapManager.GetAdjacentTile(currentIndex, dragDirectionBit);

				// Check if nextIndex is valid
				if (nextIndex == -1 || nextIndex < 0 || nextIndex >= mapManager.Tiles.Length)
				{
					break;
				}

				// Get next tile properties
				var nextProps = mapManager.GetTilePropertiesAt(nextIndex);
				if (nextProps == null)
				{
					break;
				}

				if (!nextProps.CanBeDragged)
				{
					break;
				}

				chain.TileIndices.Add(nextIndex);
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

			return chain;
		}

		public void HighlightChain(TileChain chain, bool highlight)
		{
			foreach (var tileIndex in chain.TileIndices)
			{
				if (tileIndex < 0 || tileIndex >= mapManager.Tiles.Length)
				{
					continue;
				}

				var tile = mapManager.Tiles[tileIndex];
				if (tile.GameObject == null)
				{
					continue;
				}

				var meshRenderer = tile.GameObject.GetComponentInChildren<MeshRenderer>();
				if (meshRenderer == null)
				{
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
				}
				else
				{
					// Restore original material
					if (tile.GameObject.TryGetComponent<OriginalMaterialHolder>(out var holder) && holder.originalMaterial != null)
					{
						meshRenderer.material = holder.originalMaterial;
					}
					else
					{
						// Fallback to MapManager's material
						var originalMaterial = mapManager.GetTileMeshMaterial(tileIndex);
						if (originalMaterial != null)
						{
							meshRenderer.material = originalMaterial;
						}
					}
				}
			}
		}

		public bool ValidateMove(int sourceIndex, int targetIndex)
		{
			if (targetIndex == sourceIndex || targetIndex < 0 || targetIndex >= mapManager.Tiles.Length) return false;
			var targetProps = mapManager.GetTilePropertiesAt(targetIndex);
			return targetProps != null && targetProps.Movable;
		}

		public bool TrySnapToGrid(int tileIndex, Vector3 currentPos, out int targetIndex)
		{
			var bounds = mapManager.GetMovementBounds(tileIndex, movementFlags);
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

	//public Vector3 CalculateOffset(int tileIndex, Vector3 currentPos, Vector3 offset)
	//{
	//	var dragPos = currentPos + offset;
	//	var bounds = GetMovementBounds(tileIndex);
	//	dragPos.x = Mathf.Clamp(dragPos.x, bounds.MinWest.X, bounds.MaxEast.X);
	//	dragPos.z = Mathf.Clamp(dragPos.z, bounds.MinSouth.Z, bounds.MaxNorth.Z);
	//	return dragPos;
	//}

	//// Original single-tile movement methods (unchanged)
	//public TileProperties.TileMovementBounds GetMovementBounds(int tileIndex, TileProperties.TileFlags flags)
	//{
	//	return new TileProperties.TileMovementBounds
	//	{
	//		MinWest = mapManager.GetTileCoordinatesForLast(tileIndex, TileProperties.West, flags),
	//		MaxEast = mapManager.GetTileCoordinatesForLast(tileIndex, TileProperties.East, flags),
	//		MinSouth = mapManager.GetTileCoordinatesForLast(tileIndex, TileProperties.South, flags),
	//		MaxNorth = mapManager.GetTileCoordinatesForLast(tileIndex, TileProperties.North, flags)
	//	};
	//}
}