using UnityEngine;

namespace GamePreviewNamespace
{
	public class TileInteractionController : MonoBehaviour
	{
		private GestureSystem gestureSystem => GestureSystem.instance;
		private MapManager mapManager;
		private MapManager.TileStrip tileStrip;
		private int dragIndex = -1;
		private Vector3 startWorldPos;
		private const float gridSize = 1.0f;

		public void Start()
		{
			if (null == gestureSystem) return;
			gestureSystem.OnBeginDrag += OnBeginDrag;
			gestureSystem.OnDrag += OnDrag;
			gestureSystem.OnEndDrag += OnEndDrag;
		}

		private void OnDestroy()
		{
			if (null == gestureSystem) return;
			gestureSystem.OnBeginDrag -= OnBeginDrag;
			gestureSystem.OnDrag -= OnDrag;
			gestureSystem.OnEndDrag -= OnEndDrag;

			mapManager.HighlightStrip(tileStrip, false);
		}

		public void Initialize(MapManager manager)
		{
			mapManager = manager;
			dragIndex = -1;
			tileStrip = default;
		}

		private void OnBeginDrag(Vector3 screenPos)
		{
			Vector3 worldPos = mapManager.ScreenToWorld(screenPos);
			int x = Mathf.RoundToInt(worldPos.x);
			int z = Mathf.RoundToInt(worldPos.z);
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
			startWorldPos = worldPos;
			tileStrip = mapManager.GetTileStrip(tileIndex, 0);
			mapManager.HighlightStrip(tileStrip, true);
		}

		private void OnDrag(Vector3 screenPos)
		{
			mapManager.HighlightStrip(tileStrip, false);

			if (dragIndex == -1) return;

			var currentPos = mapManager.ScreenToWorld(screenPos);
			var workingPos = startWorldPos;

			while (true)
			{
				var dxyz = currentPos - workingPos;
				var absX = Mathf.Abs(dxyz.x);
				var absZ = Mathf.Abs(dxyz.z);

				Vector3 gesture;
				int dirBit = 0;
				var delta = Vector3.zero;

				// Compute the gesture
				if (absX > absZ && absX >= gridSize)
				{
					var direction = dxyz.x > 0 ? 1 : -1;
					gesture = new Vector3(direction, 0, 0);
					workingPos.x += direction * gridSize;
					if (gesture.x == 1) dirBit |= TileProperties.East;
					if (gesture.x == -1) dirBit |= TileProperties.West;
				}
				else if (absZ >= gridSize)
				{
					var direction = dxyz.z > 0 ? 1 : -1;
					gesture = new Vector3(0, 0, direction);
					workingPos.z += direction * gridSize;
					if (gesture.z == 1) dirBit |= TileProperties.North;
					if (gesture.z == -1) dirBit |= TileProperties.South;
				}
				else
				{
					gesture = dxyz;
				}

				// Reset tile positions
				if (tileStrip.Count > 1)
				{
					foreach (var tileIndex in tileStrip.Indices)
						mapManager.Tiles[tileIndex].GameObject.transform.position = mapManager.GetTilePosition(tileIndex);
				}

				// Process the gesture
				if (dirBit != 0)
				{
					tileStrip = mapManager.GetTileStrip(dragIndex, dirBit);
					if (mapManager.RollStrip(tileStrip))
						dragIndex += tileStrip.Stride;
					tileStrip = mapManager.GetTileStrip(dragIndex, dirBit);
					startWorldPos += gesture;
				}
				else
				{
					tileStrip = new MapManager.TileStrip();
					if (Mathf.Abs(gesture.x) > Mathf.Abs(gesture.z))
					{
						tileStrip = mapManager.GetTileStrip(dragIndex, gesture.x > 0f ? TileProperties.East : TileProperties.West);
						if (tileStrip.Count > 1)
							delta = new Vector3(gesture.x, 0, 0);
					}
					else
					{
						tileStrip = mapManager.GetTileStrip(dragIndex, gesture.z > 0f ? TileProperties.North : TileProperties.South);
						if (tileStrip.Count > 1)
							delta = new Vector3(0, 0, gesture.z);
					}

					if (tileStrip.Count > 1)
					{
						foreach (var tileIndex in tileStrip.Indices)
							mapManager.Tiles[tileIndex].GameObject.transform.position += delta;
					}
				}

				mapManager.UpdateSpareTile(tileStrip, delta, delta != Vector3.zero);

				// Break if this was a partial movement
				if (dirBit == 0)
					break;
			}

			mapManager.HighlightStrip(tileStrip, tileStrip.Count > 1);
		}

		private void OnEndDrag(Vector3 screenPos)
		{
			if (dragIndex == -1) return;
			mapManager.HighlightStrip(tileStrip, false);
			
			mapManager.UpdateSpareTile(tileStrip, Vector3.zero, false);// Deactivate spare tile

			if (tileStrip.Count > 1)
			{
				var currentPos = mapManager.Tiles[dragIndex].GameObject.transform.position;
				int x = Mathf.RoundToInt(currentPos.x);
				int z = Mathf.RoundToInt(currentPos.z);
				var targetCoord = new GridCoord(x, z);
				int targetIndex = mapManager.ToIndex(targetCoord);

				if (targetIndex != dragIndex)
					mapManager.RollStrip(tileStrip);

				foreach (var tileIndex in tileStrip.Indices)
					mapManager.Tiles[tileIndex].GameObject.transform.position = mapManager.GetTilePosition(tileIndex);
			}

			dragIndex = -1;
			tileStrip = default;
		}
	}
}
