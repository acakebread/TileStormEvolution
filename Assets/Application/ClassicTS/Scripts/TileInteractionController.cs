using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace GamePreviewNamespace
{
	public class TileInteractionController : MonoBehaviour
	{
		private GestureSystem gestureSystem => GestureSystem.instance;
		private MapManager mapManager;
		private TileMovementHandler movementHandler;
		private int dragIndex = -1;
		private TileMovementHandler.TileStrip tileStrip;

		public void Start()
		{
			if (null == gestureSystem) return;
			gestureSystem.OnDragStart += OnDragStart;
			gestureSystem.OnDragging += OnDragging;
			gestureSystem.OnDragEnd += OnDragEnd;
		}

		private void OnDestroy()
		{
			if (null == gestureSystem) return;
			gestureSystem.OnDragStart -= OnDragStart;
			gestureSystem.OnDragEnd -= OnDragEnd;
			gestureSystem.OnDragging -= OnDragging;

			// Clear highlights on destroy
			if (tileStrip.TileIndices != null && tileStrip.TileIndices.Count > 0)
				movementHandler.HighlightStrip(tileStrip, false);
		}

		public void Initialize(MapManager manager)
		{
			mapManager = manager;
			movementHandler = new TileMovementHandler(mapManager);
			dragIndex = -1;
			tileStrip = default;
		}

		private bool ValidateMove(int targetIndex)
		{
			if (targetIndex == dragIndex || targetIndex < 0 || targetIndex >= mapManager.Tiles.Length) return false;
			var targetProps = mapManager.GetTilePropertiesAt(targetIndex);
			return targetProps != null && targetProps.DockOrRoll;//TileProperties.TileFlags.Dock | TileProperties.TileFlags.Roll
		}

		private void OnDragStart(Vector3 hitPos)
		{
			int x = Mathf.RoundToInt(hitPos.x);
			int z = Mathf.RoundToInt(hitPos.z);
			var coord = new GridCoord(x, z);
			if (coord.X < 0 || coord.X >= mapManager.Width || coord.Z < 0 || coord.Z >= mapManager.Height) return;
			int tileIndex = mapManager.ToIndex(coord);

			var properties = mapManager.GetTilePropertiesAt(tileIndex);
			if (properties == null || !properties.Interactive)
			{
				Debug.LogWarning($"Cannot drag tile at index {tileIndex}: {(properties == null ? "Empty" : "Not draggable")}");
				return;
			}

			dragIndex = tileIndex;
			tileStrip = movementHandler.GetTileStrip(tileIndex, 0); // No direction yet
			movementHandler.HighlightStrip(tileStrip, true);
		}

		private void OnDragging(List<Vector3> gestures)
		{
			if (dragIndex == -1) return;
			foreach (var gesture in gestures)
			{
				int dirBit = 0;
				if (gesture.z == 1) dirBit |= TileProperties.North;
				if (gesture.z == -1) dirBit |= TileProperties.South;
				if (gesture.x == 1) dirBit |= TileProperties.East;
				if (gesture.x == -1) dirBit |= TileProperties.West;

				// Update strip for visualization only
				var previousStrip = tileStrip;
				if (dirBit != 0)
				{
					tileStrip = movementHandler.GetTileStrip(dragIndex, dirBit);
				}
				else
				{
					if (Mathf.Abs(gesture.x) > Mathf.Abs(gesture.z))
						tileStrip = movementHandler.GetTileStrip(dragIndex, gesture.x > 0 ? TileProperties.East : gesture.x < 0 ? TileProperties.West : 0);
					else
						tileStrip = movementHandler.GetTileStrip(dragIndex, gesture.z > 0 ? TileProperties.North : gesture.z < 0 ? TileProperties.South : 0);
				}

				// Update highlights if strip changed
				if (!tileStrip.TileIndices.SequenceEqual(previousStrip.TileIndices))
				{
					movementHandler.HighlightStrip(previousStrip, false); // Restore previous
					movementHandler.HighlightStrip(tileStrip, true); // Highlight new
				}

				if (dirBit != 0)
				{
					int newIndex = mapManager.GetAdjacentTile(dragIndex, dirBit);
					if (newIndex != -1 && ValidateMove(newIndex))
					{
						movementHandler.SwapTiles(dragIndex, newIndex);
						gestureSystem.ConsumeGesture(gesture);
						dragIndex = newIndex;
					}
				}
				else
				{
					// Remainder is partial drag position
					var currentPos = mapManager.GetTilePosition(dragIndex);

					var bounds = mapManager.GetMovementBounds(dragIndex, TileProperties.TileFlags.Dock | TileProperties.TileFlags.Roll);
					if (Mathf.Abs(gesture.x) > Mathf.Abs(gesture.z))
					{
						var dragPos = currentPos + new Vector3(gesture.x, 0f, 0f);
						currentPos.x = Mathf.Clamp(dragPos.x, bounds.MinWest.X, bounds.MaxEast.X);
					}
					else
					{
						var dragPos = currentPos + new Vector3(0f, 0f, gesture.z);
						currentPos.z = Mathf.Clamp(dragPos.z, bounds.MinSouth.Z, bounds.MaxNorth.Z);
					}

					mapManager.Tiles[dragIndex].GameObject.transform.position = currentPos;
				}
			}
		}

		private void OnDragEnd()
		{
			if (dragIndex == -1) return;

			var currentPos = mapManager.Tiles[dragIndex].GameObject.transform.position;

			// Check bounds for both X and Z directions
			var bounds = mapManager.GetMovementBounds(dragIndex, TileProperties.TileFlags.Dock | TileProperties.TileFlags.Roll);
			int x = Mathf.RoundToInt(Mathf.Clamp(currentPos.x, bounds.MinWest.X, bounds.MaxEast.X));
			int z = Mathf.RoundToInt(Mathf.Clamp(currentPos.z, bounds.MinSouth.Z, bounds.MaxNorth.Z));
			var targetCoord = new GridCoord(x, z);
			int targetIndex = mapManager.ToIndex(targetCoord);

			if (ValidateMove(targetIndex))
				movementHandler.SwapTiles(dragIndex, targetIndex);
			else
				mapManager.Tiles[dragIndex].GameObject.transform.position = mapManager.GetTilePosition(dragIndex);

			// Clear highlights for current strip
			if (tileStrip.TileIndices != null && tileStrip.TileIndices.Count > 0)
				movementHandler.HighlightStrip(tileStrip, false);

			// Explicitly clear highlight for the dragged tile's final index
			if (dragIndex >= 0 && dragIndex < mapManager.Tiles.Length)
			{
				var singleTileStrip = new TileMovementHandler.TileStrip
				{
					TileIndices = new List<int> { dragIndex },
					DirectionBit = 0
				};
				movementHandler.HighlightStrip(singleTileStrip, false);
			}

			dragIndex = -1;
			tileStrip = default;
		}
	}
}