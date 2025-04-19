using UnityEngine;
using System.Collections.Generic;

namespace GamePreviewNamespace
{
	public class TileInteractionController : MonoBehaviour
	{
		private MapManager mapManager;
		private int dragIndex = -1;
		private int lastIndex = -1;
		private GestureSystem gestureSystem => GestureSystem.instance;

		public void Initialize(MapManager manager)
		{
			mapManager = manager;
			dragIndex = -1;
			lastIndex = -1;
			gestureSystem.OnDragStarted += HandleDragStarted;
			gestureSystem.OnDragEnded += HandleDragEnded;
			gestureSystem.OnGesturesUpdated += HandleGesturesUpdated;
		}

		private void OnDestroy()
		{
			if (gestureSystem != null)
			{
				gestureSystem.OnDragStarted -= HandleDragStarted;
				gestureSystem.OnDragEnded -= HandleDragEnded;
				gestureSystem.OnGesturesUpdated -= HandleGesturesUpdated;
			}
		}

		private void Update()
		{
			if (gestureSystem.isDragging) UpdateTileVisualPosition();
		}

		private void HandleDragStarted(Vector3 hitPos)
		{
			int x = Mathf.RoundToInt(hitPos.x);
			int z = Mathf.RoundToInt(hitPos.z);
			var coord = new GridCoord(x, z);
			if (coord.X < 0 || coord.X >= mapManager.Width || coord.Z < 0 || coord.Z >= mapManager.Height) return;
			int tileIndex = coord.ToIndex(mapManager.Width);

			var properties = mapManager.GetTilePropertiesAt(tileIndex);
			if (properties == null || !properties.CanBeDragged) return;

			dragIndex = tileIndex;
			lastIndex = tileIndex;
		}

		private void HandleGesturesUpdated(List<(GestureSystem.GestureMode mode, int direction)> gestures)
		{
			if (dragIndex == -1 || lastIndex == -1) return;
			foreach (var gesture in gestures)
			{
				int dirBit = GetDirectionBit(gesture.mode, gesture.direction);
				int newIndex = mapManager.GetAdjacentTile(lastIndex, dirBit);
				if (newIndex != -1 && ValidateMove(newIndex))
				{
					StartDragAtNewPosition(newIndex);
					gestureSystem.ConsumeGesture(gesture.mode, gesture.direction);
					lastIndex = newIndex;
				}
			}
			gestureSystem.ClearGestures();
		}

		private int GetDirectionBit(GestureSystem.GestureMode mode, int direction)
		{
			if (mode == GestureSystem.GestureMode.DraggingX)
				return direction > 0 ? TileProperties.East : TileProperties.West;
			return direction > 0 ? TileProperties.North : TileProperties.South;
		}

		private void UpdateTileVisualPosition()
		{
			if (dragIndex == -1 || mapManager.Tiles[dragIndex].GameObject == null) return;

			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			Plane mapPlane = new Plane(Vector3.up, Vector3.zero);
			if (!mapPlane.Raycast(ray, out float distance)) return;

			var currentPos = gestureSystem.GetCurrentPos();
			Vector3 newPos = mapManager.GetTilePosition(dragIndex);
			var mode = gestureSystem.GetCurrentMode();

			int dirBitMin = mode == GestureSystem.GestureMode.DraggingX ? TileProperties.West : TileProperties.South;
			int dirBitMax = mode == GestureSystem.GestureMode.DraggingX ? TileProperties.East : TileProperties.North;
			var minCoord = mapManager.GetTileCoordinatesForLast(dragIndex, dirBitMin, TileProperties.TileFlags.Dock | TileProperties.TileFlags.Roll);
			var maxCoord = mapManager.GetTileCoordinatesForLast(dragIndex, dirBitMax, TileProperties.TileFlags.Dock | TileProperties.TileFlags.Roll);
			float minValue = mode == GestureSystem.GestureMode.DraggingX ? minCoord.X : minCoord.Z;
			float maxValue = mode == GestureSystem.GestureMode.DraggingX ? maxCoord.X : maxCoord.Z;

			if (mode == GestureSystem.GestureMode.DraggingX)
				newPos.x = Mathf.Clamp(currentPos.x, minValue, maxValue);
			else
				newPos.z = Mathf.Clamp(currentPos.z, minValue, maxValue);

			mapManager.Tiles[dragIndex].GameObject.transform.position = newPos;
		}

		private void StartDragAtNewPosition(int newIndex)
		{
			if (newIndex < 0 || newIndex >= mapManager.Tiles.Length || dragIndex == -1) return;
			if (!ValidateMove(newIndex)) return;

			var oldPosition = mapManager.GetTilePosition(dragIndex);
			var newPosition = mapManager.GetTilePosition(newIndex);

			// Swap entire TileData
			var temp = mapManager.Tiles[dragIndex];
			mapManager.Tiles[dragIndex] = mapManager.Tiles[newIndex];
			mapManager.Tiles[newIndex] = temp;

			// Update GameObject names and positions
			var draggedTile = mapManager.Tiles[newIndex].GameObject;
			var targetTile = mapManager.Tiles[dragIndex].GameObject;
			if (targetTile != null)
			{
				string tempName = draggedTile.name;
				draggedTile.name = targetTile.name;
				targetTile.name = tempName;
				targetTile.transform.position = oldPosition;
			}

			draggedTile.transform.position = newPosition;
			dragIndex = newIndex;
			lastIndex = newIndex;
		}

		private void HandleDragEnded(Vector3 finalPos)
		{
			if (dragIndex == -1 || mapManager.Tiles[dragIndex].GameObject == null) return;

			// Check bounds for both X and Z directions
			var minCoordX = mapManager.GetTileCoordinatesForLast(dragIndex, TileProperties.West, TileProperties.TileFlags.Dock | TileProperties.TileFlags.Roll);
			var maxCoordX = mapManager.GetTileCoordinatesForLast(dragIndex, TileProperties.East, TileProperties.TileFlags.Dock | TileProperties.TileFlags.Roll);
			var minCoordZ = mapManager.GetTileCoordinatesForLast(dragIndex, TileProperties.South, TileProperties.TileFlags.Dock | TileProperties.TileFlags.Roll);
			var maxCoordZ = mapManager.GetTileCoordinatesForLast(dragIndex, TileProperties.North, TileProperties.TileFlags.Dock | TileProperties.TileFlags.Roll);

			Vector3 tilePos = mapManager.Tiles[dragIndex].GameObject.transform.position;
			int x = Mathf.RoundToInt(Mathf.Clamp(tilePos.x, minCoordX.X, maxCoordX.X));
			int z = Mathf.RoundToInt(Mathf.Clamp(tilePos.z, minCoordZ.Z, maxCoordZ.Z));
			var targetCoord = new GridCoord(x, z);
			int targetIndex = targetCoord.ToIndex(mapManager.Width);

			if (ValidateMove(targetIndex))
			{
				var oldCoord = mapManager.GetTileCoordinates(dragIndex);

				// Swap entire TileData
				var temp = mapManager.Tiles[dragIndex];
				mapManager.Tiles[dragIndex] = mapManager.Tiles[targetIndex];
				mapManager.Tiles[targetIndex] = temp;

				// Update GameObject names and positions
				var draggedTile = mapManager.Tiles[targetIndex].GameObject;
				var targetTile = mapManager.Tiles[dragIndex].GameObject;
				if (targetTile != null)
				{
					string tempName = draggedTile.name;
					draggedTile.name = targetTile.name;
					targetTile.name = tempName;
					targetTile.transform.position = oldCoord.ToPosition();
				}

				draggedTile.transform.position = targetCoord.ToPosition();
			}
			else
			{
				mapManager.Tiles[dragIndex].GameObject.transform.position = mapManager.GetTilePosition(dragIndex);
			}

			ResetDrag();
		}

		private bool ValidateMove(int targetIndex)
		{
			if (targetIndex == dragIndex || targetIndex < 0 || targetIndex >= mapManager.Tiles.Length) return false;
			var targetProps = mapManager.GetTilePropertiesAt(targetIndex);
			return targetProps != null && targetProps.DockOrRoll;
		}

		private void ResetDrag()
		{
			dragIndex = -1;
			lastIndex = -1;
		}
	}
}