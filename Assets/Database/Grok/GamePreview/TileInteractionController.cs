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
			draggedTile = null;
			lastMousePos = Vector3.zero;
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
			GetDragBounds(tileIndex);
			startGridX = x;
			startGridZ = z;
			lastMousePos = hitPos;
		}

		private void HandleGesturesUpdated(List<(GestureSystem.GestureMode mode, int direction)> gestures)
		{
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
				if (targetTile != null && (targetProps == null || !targetProps.tileDef.bDock))
				{
					return false;
				}
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

			int targetIndex = tileIndex;
			if (targetIndex != dragIndex && targetIndex >= 0 && targetIndex < mapManager.Tiles.Length)
			{
				GameObject targetTile = mapManager.Tiles[targetIndex];
				var targetProps = targetTile?.GetComponent<TileProperties>();
				if (targetTile != null && (targetProps == null || !targetProps.tileDef.bDock))
				{
					return;
				}
			}

			Vector3 newTilePos = new Vector3(gridX, 0f, gridZ);
			if (targetIndex != dragIndex && targetIndex >= 0 && targetIndex < mapManager.Tiles.Length)
			{
				GameObject targetTile = mapManager.Tiles[targetIndex];
				var targetProps = targetTile?.GetComponent<TileProperties>();
				if (targetTile == null || (targetProps != null && targetProps.tileDef.bDock))
				{
					mapManager.Tiles[targetIndex] = draggedTile;
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
				else
				{
					return;
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
			GetDragBounds(dragIndex);
		}

		private void HandleDragEnded(Vector3 finalPos)
		{
			if (!isDragging) return;

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

		private void GetDragBounds(int tileIndex)
		{
			int x = tileIndex % mapManager.Width;
			int z = tileIndex / mapManager.Width;

			minX = 0f;
			maxX = mapManager.Width - 1;
			minZ = 0f;
			maxZ = mapManager.Height - 1;

			for (int i = x - 1; i >= -1; i--)
			{
				if (i == -1) { minX = 0f; break; }
				int checkIndex = z * mapManager.Width + i;
				var props = mapManager.Tiles[checkIndex]?.GetComponent<TileProperties>();
				if (props == null || !props.tileDef.bDock) { minX = i + 1; break; }
			}

			for (int i = x + 1; i <= mapManager.Width; i++)
			{
				if (i == mapManager.Width) { maxX = mapManager.Width - 1; break; }
				int checkIndex = z * mapManager.Width + i;
				var props = mapManager.Tiles[checkIndex]?.GetComponent<TileProperties>();
				if (props == null || !props.tileDef.bDock) { maxX = i - 1; break; }
			}

			for (int j = z - 1; j >= -1; j--)
			{
				if (j == -1) { minZ = 0f; break; }
				int checkIndex = j * mapManager.Width + x;
				var props = mapManager.Tiles[checkIndex]?.GetComponent<TileProperties>();
				if (props == null || !props.tileDef.bDock) { minZ = j + 1; break; }
			}

			for (int j = z + 1; j <= mapManager.Height; j++)
			{
				if (j == mapManager.Height) { maxZ = mapManager.Height - 1; break; }
				int checkIndex = j * mapManager.Width + x;
				var props = mapManager.Tiles[checkIndex]?.GetComponent<TileProperties>();
				if (props == null || !props.tileDef.bDock) { maxZ = j - 1; break; }
			}
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



//using UnityEngine;

//namespace GamePreviewNamespace
//{
//	public class TileInteractionController : MonoBehaviour
//	{
//		private MapManager mapManager;
//		private GameObject draggedTile;
//		private Vector3 originalPos;
//		private int dragIndex = -1;
//		private Vector3 dragOffset;
//		private bool isDragging;
//		private float minX, maxX, minZ, maxZ;
//		private int startGridX, startGridZ;
//		private GestureSystem gestureSystem;
//		private Vector3 lastPos;
//		private (Vector2Int start, Vector2Int delta) lastMovementVector;

//		public void Initialize(MapManager manager, GestureSystem gesture)
//		{
//			mapManager = manager;
//			gestureSystem = gesture;
//			isDragging = false;
//			dragIndex = -1;
//			draggedTile = null;
//			lastPos = Vector3.zero;
//			lastMovementVector = (Vector2Int.zero, Vector2Int.zero);
//			gestureSystem.OnDragStarted += HandleDragStarted;
//			gestureSystem.OnDragEnded += HandleDragEnded;
//			gestureSystem.OnModeChanged += HandleModeChanged;
//		}

//		private void OnDestroy()
//		{
//			if (gestureSystem != null)
//			{
//				gestureSystem.OnDragStarted -= HandleDragStarted;
//				gestureSystem.OnDragEnded -= HandleDragEnded;
//				gestureSystem.OnModeChanged -= HandleModeChanged;
//			}
//		}

//		private void Update()
//		{
//			if (isDragging)
//			{
//				UpdateTilePosition();
//			}
//		}

//		private void HandleDragStarted(Vector3 hitPos)
//		{
//			int x = Mathf.RoundToInt(hitPos.x);
//			int z = Mathf.RoundToInt(hitPos.z);
//			int tileIndex = z * mapManager.Width + x;

//			if (tileIndex < 0 || tileIndex >= mapManager.Tiles.Length || mapManager.Tiles[tileIndex] == null)
//			{
//				return;
//			}

//			var properties = mapManager.Tiles[tileIndex].GetComponent<TileProperties>();
//			if (properties == null || !properties.CanBeDragged)
//			{
//				return;
//			}

//			isDragging = true;
//			dragIndex = tileIndex;
//			draggedTile = mapManager.Tiles[tileIndex];
//			originalPos = draggedTile.transform.position;
//			dragOffset = hitPos - originalPos;
//			GetDragBounds(tileIndex);
//			startGridX = x;
//			startGridZ = z;
//			lastPos = hitPos;
//			lastMovementVector = (new Vector2Int(x, z), Vector2Int.zero);
//		}

//		private void UpdateTilePosition()
//		{
//			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
//			Plane mapPlane = new Plane(Vector3.up, Vector3.zero);
//			if (!mapPlane.Raycast(ray, out float distance))
//			{
//				return;
//			}

//			Vector3 currentPos = ray.GetPoint(distance);
//			Vector3 newPos = originalPos;
//			bool isValidMove = true;

//			GestureSystem.GestureMode mode = gestureSystem.GetCurrentMode();

//			// Interpolate grid crossings
//			int currentGridX = startGridX;
//			int currentGridZ = startGridZ;
//			Vector2Int lastGrid = new Vector2Int(currentGridX, currentGridZ);

//			if (mode == GestureSystem.GestureMode.DraggingX)
//			{
//				float lastTileX = lastPos.x - dragOffset.x;
//				float currentTileX = currentPos.x - dragOffset.x;
//				float startX = Mathf.Min(lastTileX, currentTileX);
//				float endX = Mathf.Max(lastTileX, currentTileX);
//				int direction = lastTileX <= currentTileX ? 1 : -1;

//				for (float gridX = Mathf.Ceil(startX); gridX <= Mathf.Floor(endX); gridX += 1f)
//				{
//					if (gridX >= minX && gridX <= maxX)
//					{
//						currentGridX = Mathf.RoundToInt(gridX);
//						newPos.x = gridX;
//						newPos.z = originalPos.z;
//						isValidMove = ValidateMove(currentGridX, currentGridZ);
//						if (isValidMove)
//						{
//							draggedTile.transform.position = newPos;
//							Vector2Int newGrid = new Vector2Int(currentGridX, currentGridZ);
//							if (newGrid != lastGrid)
//							{
//								lastMovementVector = (lastGrid, newGrid - lastGrid);
//								// Interpolate mouse position at grid center
//								float t = (gridX - lastTileX) / (currentTileX - lastTileX);
//								Vector3 interpolatedPos = Vector3.Lerp(lastPos, currentPos, t);
//								gestureSystem.SignalDeadZone(interpolatedPos);
//								StartDragAtNewPosition(interpolatedPos, currentGridX, currentGridZ);
//								lastGrid = newGrid;
//							}
//						}
//						else
//						{
//							newPos = originalPos;
//							break;
//						}
//					}
//				}
//				if (isValidMove && currentTileX >= minX && currentTileX <= maxX)
//				{
//					newPos.x = currentTileX;
//					newPos.z = originalPos.z;
//					draggedTile.transform.position = newPos;
//				}
//				else
//				{
//					draggedTile.transform.position = originalPos;
//				}
//			}
//			else if (mode == GestureSystem.GestureMode.DraggingZ)
//			{
//				float lastTileZ = lastPos.z - dragOffset.z;
//				float currentTileZ = currentPos.z - dragOffset.z;
//				float startZ = Mathf.Min(lastTileZ, currentTileZ);
//				float endZ = Mathf.Max(lastTileZ, currentTileZ);
//				int direction = lastTileZ <= currentTileZ ? 1 : -1;

//				for (float gridZ = Mathf.Ceil(startZ); gridZ <= Mathf.Floor(endZ); gridZ += 1f)
//				{
//					if (gridZ >= minZ && gridZ <= maxZ)
//					{
//						currentGridZ = Mathf.RoundToInt(gridZ);
//						newPos.x = originalPos.x;
//						newPos.z = gridZ;
//						isValidMove = ValidateMove(currentGridX, currentGridZ);
//						if (isValidMove)
//						{
//							draggedTile.transform.position = newPos;
//							Vector2Int newGrid = new Vector2Int(currentGridX, currentGridZ);
//							if (newGrid != lastGrid)
//							{
//								lastMovementVector = (lastGrid, newGrid - lastGrid);
//								// Interpolate mouse position at grid center
//								float t = (gridZ - lastTileZ) / (currentTileZ - lastTileZ);
//								Vector3 interpolatedPos = Vector3.Lerp(lastPos, currentPos, t);
//								gestureSystem.SignalDeadZone(interpolatedPos);
//								StartDragAtNewPosition(interpolatedPos, currentGridX, currentGridZ);
//								lastGrid = newGrid;
//							}
//						}
//						else
//						{
//							newPos = originalPos;
//							break;
//						}
//					}
//				}
//				if (isValidMove && currentTileZ >= minZ && currentTileZ <= maxZ)
//				{
//					newPos.x = originalPos.x;
//					newPos.z = currentTileZ;
//					draggedTile.transform.position = newPos;
//				}
//				else
//				{
//					draggedTile.transform.position = originalPos;
//				}
//			}

//			lastPos = currentPos;
//		}

//		private bool ValidateMove(int gridX, int gridZ)
//		{
//			int targetIndex = gridZ * mapManager.Width + gridX;
//			if (targetIndex != dragIndex && targetIndex >= 0 && targetIndex < mapManager.Tiles.Length)
//			{
//				GameObject targetTile = mapManager.Tiles[targetIndex];
//				var targetProps = targetTile?.GetComponent<TileProperties>();
//				if (targetTile != null && (targetProps == null || !targetProps.tileDef.bDock))
//				{
//					return false;
//				}
//			}
//			return true;
//		}

//		private void StartDragAtNewPosition(Vector3 hitPos, int gridX, int gridZ)
//		{
//			int tileIndex = gridZ * mapManager.Width + gridX;
//			if (tileIndex < 0 || tileIndex >= mapManager.Tiles.Length)
//			{
//				return;
//			}

//			int targetIndex = tileIndex;
//			if (targetIndex != dragIndex && targetIndex >= 0 && targetIndex < mapManager.Tiles.Length)
//			{
//				GameObject targetTile = mapManager.Tiles[targetIndex];
//				var targetProps = targetTile?.GetComponent<TileProperties>();
//				if (targetTile != null && (targetProps == null || !targetProps.tileDef.bDock))
//				{
//					return;
//				}
//			}

//			Vector3 newTilePos = new Vector3(gridX, 0f, gridZ);
//			if (targetIndex != dragIndex && targetIndex >= 0 && targetIndex < mapManager.Tiles.Length)
//			{
//				GameObject targetTile = mapManager.Tiles[targetIndex];
//				var targetProps = targetTile?.GetComponent<TileProperties>();
//				if (targetTile == null || (targetProps != null && targetProps.tileDef.bDock))
//				{
//					mapManager.Tiles[targetIndex] = draggedTile;
//					mapManager.Tiles[dragIndex] = targetTile;

//					UpdateTileName(draggedTile, gridX, gridZ);

//					if (targetTile != null)
//					{
//						int origX = dragIndex % mapManager.Width;
//						int origZ = dragIndex / mapManager.Width;
//						targetTile.transform.position = new Vector3(origX, 0f, origZ);
//						UpdateTileName(targetTile, origX, origZ);
//					}
//				}
//				else
//				{
//					return;
//				}
//			}

//			var properties = draggedTile.GetComponent<TileProperties>();
//			if (properties == null || !properties.CanBeDragged)
//			{
//				return;
//			}

//			dragIndex = tileIndex;
//			originalPos = newTilePos;
//			dragOffset = hitPos - newTilePos;
//			GetDragBounds(dragIndex);
//			startGridX = gridX;
//			startGridZ = gridZ;
//		}

//		private void HandleModeChanged(GestureSystem.GestureMode newMode)
//		{
//			Vector3 hitPos = lastPos;
//			dragOffset = hitPos - draggedTile.transform.position;
//			if (newMode == GestureSystem.GestureMode.DraggingX)
//			{
//				dragOffset.z = 0f;
//			}
//			else if (newMode == GestureSystem.GestureMode.DraggingZ)
//			{
//				dragOffset.x = 0f;
//			}
//			GetDragBounds(dragIndex);
//		}

//		private void HandleDragEnded(Vector3 finalPos)
//		{
//			if (!isDragging) return;

//			Vector3 tilePos = draggedTile.transform.position;
//			int x = Mathf.RoundToInt(Mathf.Clamp(tilePos.x, minX, maxX));
//			int z = Mathf.RoundToInt(Mathf.Clamp(tilePos.z, minZ, maxZ));
//			int targetIndex = z * mapManager.Width + x;

//			if (targetIndex != dragIndex && targetIndex >= 0 && targetIndex < mapManager.Tiles.Length)
//			{
//				GameObject targetTile = mapManager.Tiles[targetIndex];
//				var targetProps = targetTile?.GetComponent<TileProperties>();
//				if (targetTile == null || (targetProps != null && targetProps.tileDef.bDock))
//				{
//					mapManager.Tiles[targetIndex] = draggedTile;
//					mapManager.Tiles[dragIndex] = targetTile;

//					draggedTile.transform.position = new Vector3(x, 0f, z);
//					UpdateTileName(draggedTile, x, z);

//					if (targetTile != null)
//					{
//						int origX = dragIndex % mapManager.Width;
//						int origZ = dragIndex / mapManager.Width;
//						targetTile.transform.position = new Vector3(origX, 0f, origZ);
//						UpdateTileName(targetTile, origX, origZ);
//					}
//				}
//				else
//				{
//					draggedTile.transform.position = originalPos;
//				}
//			}
//			else
//			{
//				draggedTile.transform.position = originalPos;
//			}

//			ResetDrag();
//		}

//		private void GetDragBounds(int tileIndex)
//		{
//			int x = tileIndex % mapManager.Width;
//			int z = tileIndex / mapManager.Width;

//			minX = 0f;
//			maxX = mapManager.Width - 1;
//			minZ = 0f;
//			maxZ = mapManager.Height - 1;

//			for (int i = x - 1; i >= -1; i--)
//			{
//				if (i == -1) { minX = 0f; break; }
//				int checkIndex = z * mapManager.Width + i;
//				var props = mapManager.Tiles[checkIndex]?.GetComponent<TileProperties>();
//				if (props == null || !props.tileDef.bDock) { minX = i + 1; break; }
//			}

//			for (int i = x + 1; i <= mapManager.Width; i++)
//			{
//				if (i == mapManager.Width) { maxX = mapManager.Width - 1; break; }
//				int checkIndex = z * mapManager.Width + i;
//				var props = mapManager.Tiles[checkIndex]?.GetComponent<TileProperties>();
//				if (props == null || !props.tileDef.bDock) { maxX = i - 1; break; }
//			}

//			for (int j = z - 1; j >= -1; j--)
//			{
//				if (j == -1) { minZ = 0f; break; }
//				int checkIndex = j * mapManager.Width + x;
//				var props = mapManager.Tiles[checkIndex]?.GetComponent<TileProperties>();
//				if (props == null || !props.tileDef.bDock) { minZ = j + 1; break; }
//			}

//			for (int j = z + 1; j <= mapManager.Height; j++)
//			{
//				if (j == mapManager.Height) { maxZ = mapManager.Height - 1; break; }
//				int checkIndex = j * mapManager.Width + x;
//				var props = mapManager.Tiles[checkIndex]?.GetComponent<TileProperties>();
//				if (props == null || !props.tileDef.bDock) { maxZ = j - 1; break; }
//			}
//		}

//		private void UpdateTileName(GameObject tile, int x, int z)
//		{
//			string[] nameParts = tile.name.Split('_');
//			if (nameParts.Length >= 3)
//				tile.name = $"{nameParts[0]}_{x}_{z}";
//		}

//		private void ResetDrag()
//		{
//			isDragging = false;
//			dragIndex = -1;
//			draggedTile = null;
//			dragOffset = Vector3.zero;
//			lastPos = Vector3.zero;
//			lastMovementVector = (Vector2Int.zero, Vector2Int.zero);
//		}
//	}
//}