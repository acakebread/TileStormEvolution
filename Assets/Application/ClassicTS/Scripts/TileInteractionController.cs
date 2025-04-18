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
			if (x < 0 || x >= mapManager.Width || z < 0 || z >= mapManager.Height) return;
			int tileIndex = z * mapManager.Width + x;

			var properties = mapManager.Tiles[tileIndex]?.GetComponent<TileProperties>();
			if (properties == null || !properties.CanBeDragged) return;

			dragIndex = tileIndex;
			lastIndex = tileIndex;
		}

		private void HandleGesturesUpdated(List<(GestureSystem.GestureMode mode, int direction)> gestures)
		{
			if (dragIndex == -1 || lastIndex == -1) return;
			foreach (var gesture in gestures)
			{
				int stride = gesture.mode == GestureSystem.GestureMode.DraggingX ? 1 : mapManager.Width;
				int newIndex = lastIndex + stride * gesture.direction;
				if (ValidateMove(newIndex))
				{
					StartDragAtNewPosition(newIndex);
					gestureSystem.ConsumeGesture(gesture.mode, gesture.direction);
					lastIndex = newIndex;
				}
			}
			gestureSystem.ClearGestures();
		}

		private void UpdateTileVisualPosition()
		{
			if (dragIndex == -1 || mapManager.Tiles[dragIndex] == null) return;

			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			Plane mapPlane = new Plane(Vector3.up, Vector3.zero);
			if (!mapPlane.Raycast(ray, out float distance)) return;

			var currentPos = gestureSystem.GetCurrentPos();
			int gridX = dragIndex % mapManager.Width;
			int gridZ = dragIndex / mapManager.Width;
			Vector3 newPos = new Vector3(gridX, 0f, gridZ);
			int stride = gestureSystem.GetCurrentMode() == GestureSystem.GestureMode.DraggingX ? 1 : mapManager.Width;

			var (minIndex, maxIndex) = GetDragBoundsIndexes(dragIndex, stride);
			float minCoord = stride == 1 ? minIndex % mapManager.Width : minIndex / mapManager.Width;
			float maxCoord = stride == 1 ? maxIndex % mapManager.Width : maxIndex / mapManager.Width;

			if (stride == 1)
				newPos.x = Mathf.Clamp(currentPos.x, minCoord, maxCoord);
			else
				newPos.z = Mathf.Clamp(currentPos.z, minCoord, maxCoord);

			mapManager.Tiles[dragIndex].transform.position = newPos;
		}

		private void StartDragAtNewPosition(int newIndex)
		{
			if (newIndex < 0 || newIndex >= mapManager.Tiles.Length || dragIndex == -1) return;
			if (!ValidateMove(newIndex)) return;

			int oldX = dragIndex % mapManager.Width;
			int oldZ = dragIndex / mapManager.Width;
			int newX = newIndex % mapManager.Width;
			int newZ = newIndex / mapManager.Width;

			GameObject draggedTile = mapManager.Tiles[dragIndex];
			GameObject targetTile = mapManager.Tiles[newIndex];
			mapManager.Tiles[newIndex] = draggedTile;
			mapManager.Tiles[dragIndex] = targetTile;

			if (targetTile != null)
			{
				string tempName = draggedTile.name;
				draggedTile.name = targetTile.name;
				targetTile.name = tempName;
				targetTile.transform.position = new Vector3(oldX, 0f, oldZ);
			}

			mapManager.Tiles[newIndex].transform.position = new Vector3(newX, 0f, newZ);
			dragIndex = newIndex;
			lastIndex = newIndex;
		}

		private void HandleDragEnded(Vector3 finalPos)
		{
			if (dragIndex == -1 || mapManager.Tiles[dragIndex] == null) return;

			var (minIndexX, maxIndexX) = GetDragBoundsIndexes(dragIndex, 1);
			var (minIndexZ, maxIndexZ) = GetDragBoundsIndexes(dragIndex, mapManager.Width);
			float minX = minIndexX % mapManager.Width;
			float maxX = maxIndexX % mapManager.Width;
			float minZ = minIndexZ / mapManager.Width;
			float maxZ = maxIndexZ / mapManager.Width;

			Vector3 tilePos = mapManager.Tiles[dragIndex].transform.position;
			int x = Mathf.RoundToInt(Mathf.Clamp(tilePos.x, minX, maxX));
			int z = Mathf.RoundToInt(Mathf.Clamp(tilePos.z, minZ, maxZ));
			int targetIndex = z * mapManager.Width + x;

			if (ValidateMove(targetIndex))
			{
				int oldX = dragIndex % mapManager.Width;
				int oldZ = dragIndex / mapManager.Width;

				GameObject draggedTile = mapManager.Tiles[dragIndex];
				GameObject targetTile = mapManager.Tiles[targetIndex];
				mapManager.Tiles[targetIndex] = draggedTile;
				mapManager.Tiles[dragIndex] = targetTile;

				if (targetTile != null)
				{
					string tempName = draggedTile.name;
					draggedTile.name = targetTile.name;
					targetTile.name = tempName;
					targetTile.transform.position = new Vector3(oldX, 0f, oldZ);
				}

				mapManager.Tiles[targetIndex].transform.position = new Vector3(x, 0f, z);
			}
			else
			{
				mapManager.Tiles[dragIndex].transform.position = new Vector3(dragIndex % mapManager.Width, 0f, dragIndex / mapManager.Width);
			}

			ResetDrag();
		}

		private bool ValidateMove(int targetIndex)
		{
			if (targetIndex == dragIndex || targetIndex < 0 || targetIndex >= mapManager.Tiles.Length) return false;
			var targetTile = mapManager.Tiles[targetIndex];
			var targetProps = targetTile?.GetComponent<TileProperties>();
			return targetTile != null && targetProps != null && targetProps.DockOrRoll;
		}

		private void ResetDrag()
		{
			dragIndex = -1;
			lastIndex = -1;
		}

		private (int minIndex, int maxIndex) GetDragBoundsIndexes(int srcIndex, int stride)
		{
			return (WalkMap(srcIndex, -stride), WalkMap(srcIndex, stride));

			int WalkMap(int index, int stride)
			{
				int dx = stride % mapManager.Width; // 1 or -1 for X, 0 for Z
				int dz = stride / mapManager.Width; // 1 or -1 for Z, 0 for X
				int x = index % mapManager.Width;
				int z = index / mapManager.Width;
				while (x >= 0 && x < mapManager.Width && z >= 0 && z < mapManager.Height)
				{
					int nextIndex = index + stride;
					if (nextIndex < 0 || nextIndex >= mapManager.Tiles.Length) break;
					var props = mapManager.Tiles[nextIndex]?.GetComponent<TileProperties>();
					if (props == null || !props.DockOrRoll) break;
					index = nextIndex;
					x += dx;
					z += dz;
				}
				return index;
			}
		}
	}
}
