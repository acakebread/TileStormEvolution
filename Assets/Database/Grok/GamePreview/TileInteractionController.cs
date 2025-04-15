using UnityEngine;
using System.Collections.Generic;

namespace GamePreviewNamespace
{
	public class TileInteractionController : MonoBehaviour
	{
		private MapManager mapManager;
		private GameObject draggedTile;
		private Vector3 originalPos;
		private int dragIndex = -1;
		private Vector3 dragOffset;
		private bool isDragging;
		private float minX, maxX, minZ, maxZ;
		private int startGridX, startGridZ;
		private GestureSystem gestureSystem;
		private Vector3 lastMousePos;

		public void Initialize(MapManager manager, GestureSystem gesture)
		{
			mapManager = manager;
			gestureSystem = gesture;
			isDragging = false;
			dragIndex = -1;
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
			if (isDragging)
			{
				UpdateTileVisualPosition();
			}
		}

		private void HandleDragStarted(Vector3 hitPos)
		{
			int x = Mathf.RoundToInt(hitPos.x);
			int z = Mathf.RoundToInt(hitPos.z);
			int tileIndex = z * mapManager.Width + x;

			if (tileIndex < 0 || tileIndex >= mapManager.Tiles.Length || mapManager.Tiles[tileIndex] == null)
			{
				return;
			}

			var properties = mapManager.Tiles[tileIndex].GetComponent<TileProperties>();
			if (properties == null || !properties.CanBeDragged)
			{
				return;
			}

			isDragging = true;
			dragIndex = tileIndex;
			draggedTile = mapManager.Tiles[tileIndex];
			originalPos = draggedTile.transform.position;
			dragOffset = hitPos - originalPos;
			GetDragBounds(x, z);
			startGridX = x;
			startGridZ = z;
			lastMousePos = hitPos;
		}

		private void HandleGesturesUpdated(List<(GestureSystem.GestureMode mode, int direction)> gestures)
		{
			if (-1 == dragIndex) return;
			foreach (var gesture in gestures)
			{
				int newGridX = startGridX;
				int newGridZ = startGridZ;

				if (gesture.mode == GestureSystem.GestureMode.DraggingX)
				{
					newGridX += gesture.direction;
				}
				else if (gesture.mode == GestureSystem.GestureMode.DraggingZ)
				{
					newGridZ += gesture.direction;
				}

				if (ValidateMove(newGridX, newGridZ))
				{
					StartDragAtNewPosition(lastMousePos, newGridX, newGridZ);
					gestureSystem.ConsumeGesture(gesture.mode, gesture.direction);
					dragOffset = lastMousePos - originalPos;
					startGridX = newGridX;
					startGridZ = newGridZ;
				}
			}
			gestureSystem.ClearGestures();
		}

		private void UpdateTileVisualPosition()
		{
			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			Plane mapPlane = new Plane(Vector3.up, Vector3.zero);
			if (!mapPlane.Raycast(ray, out float distance))
			{
				return;
			}

			Vector3 currentPos = ray.GetPoint(distance);
			Vector3 newPos = originalPos;
			GestureSystem.GestureMode mode = gestureSystem.GetCurrentMode();

			if (mode == GestureSystem.GestureMode.DraggingX)
			{
				newPos.x = Mathf.Clamp(currentPos.x - dragOffset.x, minX, maxX);
				newPos.z = originalPos.z;
			}
			else if (mode == GestureSystem.GestureMode.DraggingZ)
			{
				newPos.z = Mathf.Clamp(currentPos.z - dragOffset.z, minZ, maxZ);
				newPos.x = originalPos.x;
			}
			else
			{
				newPos = originalPos;
			}

			draggedTile.transform.position = newPos;
			lastMousePos = currentPos;
		}

		private bool ValidateMove(int gridX, int gridZ)
		{
			if (gridX < minX || gridX > maxX || gridZ < minZ || gridZ > maxZ)
			{
				return false;
			}

			int targetIndex = gridZ * mapManager.Width + gridX;
			if (targetIndex != dragIndex && targetIndex >= 0 && targetIndex < mapManager.Tiles.Length)
			{
				GameObject targetTile = mapManager.Tiles[targetIndex];
				var targetProps = targetTile?.GetComponent<TileProperties>();
				return targetTile == null || (targetProps != null && targetProps.tileDef.bDock);
			}
			return true;
		}

		private void StartDragAtNewPosition(Vector3 hitPos, int gridX, int gridZ)
		{
			int tileIndex = gridZ * mapManager.Width + gridX;
			if (tileIndex < 0 || tileIndex >= mapManager.Tiles.Length)
			{
				return;
			}

			Vector3 newTilePos = new Vector3(gridX, 0f, gridZ);
			if (tileIndex != dragIndex)
			{
				GameObject targetTile = mapManager.Tiles[tileIndex];
				var targetProps = targetTile?.GetComponent<TileProperties>();
				if (targetTile != null && (targetProps == null || !targetProps.tileDef.bDock))
				{
					return;
				}

				mapManager.Tiles[tileIndex] = draggedTile;
				mapManager.Tiles[dragIndex] = targetTile;

				UpdateTileName(draggedTile, gridX, gridZ);

				if (targetTile != null)
				{
					int origX = dragIndex % mapManager.Width;
					int origZ = dragIndex / mapManager.Width;
					targetTile.transform.position = new Vector3(origX, 0f, origZ);
					UpdateTileName(targetTile, origX, origZ);
				}
			}

			var properties = draggedTile.GetComponent<TileProperties>();
			if (properties == null || !properties.CanBeDragged)
			{
				return;
			}

			dragIndex = tileIndex;
			originalPos = newTilePos;
			dragOffset = hitPos - newTilePos;
			GetDragBounds(gridX, gridZ);
		}

		private void HandleDragEnded(Vector3 finalPos)
		{
			if (!isDragging) return;
			if (-1 == dragIndex) return;

			Vector3 tilePos = draggedTile.transform.position;
			int x = Mathf.RoundToInt(Mathf.Clamp(tilePos.x, minX, maxX));
			int z = Mathf.RoundToInt(Mathf.Clamp(tilePos.z, minZ, maxZ));
			int targetIndex = z * mapManager.Width + x;

			if (targetIndex != dragIndex && targetIndex >= 0 && targetIndex < mapManager.Tiles.Length)
			{
				GameObject targetTile = mapManager.Tiles[targetIndex];
				var targetProps = targetTile?.GetComponent<TileProperties>();
				if (targetTile == null || (targetProps != null && targetProps.tileDef.bDock))
				{
					mapManager.Tiles[targetIndex] = draggedTile;
					mapManager.Tiles[dragIndex] = targetTile;

					draggedTile.transform.position = new Vector3(x, 0f, z);
					UpdateTileName(draggedTile, x, z);

					if (targetTile != null)
					{
						int origX = dragIndex % mapManager.Width;
						int origZ = dragIndex / mapManager.Width;
						targetTile.transform.position = new Vector3(origX, 0f, origZ);
						UpdateTileName(targetTile, origX, origZ);
					}
				}
				else
				{
					draggedTile.transform.position = originalPos;
				}
			}
			else
			{
				draggedTile.transform.position = originalPos;
			}

			ResetDrag();
		}

		private void GetDragBounds(int x, int z)
		{
			minX = 0f;
			maxX = mapManager.Width - 1;
			minZ = 0f;
			maxZ = mapManager.Height - 1;

			minX = FindBoundary(x - 1, -1, -1, i => z * mapManager.Width + i, i => i + 1, 0f);
			maxX = FindBoundary(x + 1, mapManager.Width, 1, i => z * mapManager.Width + i, i => i - 1, mapManager.Width - 1);
			minZ = FindBoundary(z - 1, -1, -1, j => j * mapManager.Width + x, j => j + 1, 0f);
			maxZ = FindBoundary(z + 1, mapManager.Height, 1, j => j * mapManager.Width + x, j => j - 1, mapManager.Height - 1);
		}

		private float FindBoundary(int start, int limit, int step, System.Func<int, int> getIndex, System.Func<int, float> getBound, float defaultBound)
		{
			for (int i = start; step > 0 ? i <= limit : i >= limit; i += step)
			{
				if (i == limit)
				{
					return defaultBound;
				}
				int checkIndex = getIndex(i);
				var props = mapManager.Tiles[checkIndex]?.GetComponent<TileProperties>();
				if (props == null || !props.tileDef.bDock)
				{
					return getBound(i);
				}
			}
			return defaultBound;
		}

		private void UpdateTileName(GameObject tile, int x, int z)
		{
			string[] nameParts = tile.name.Split('_');
			if (nameParts.Length >= 3)
				tile.name = $"{nameParts[0]}_{x}_{z}";
		}

		private void ResetDrag()
		{
			isDragging = false;
			dragIndex = -1;
			draggedTile = null;
			dragOffset = Vector3.zero;
			lastMousePos = Vector3.zero;
		}
	}
}
