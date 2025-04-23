using UnityEngine;

namespace GamePreviewNamespace
{
	public class TileInteractionController : MonoBehaviour
	{
		private GestureSystem gestureSystem => GestureSystem.instance;
		private MapManager mapManager;
		private MapManager.TileStrip tileStrip;
		private Vector3 initialPos;//world ground plane position
		private int dragIndex = -1;
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

			mapManager.HighlightStrip(tileStrip, false);//debug utility
		}

		public void Initialize(MapManager manager)
		{
			mapManager = manager;
			dragIndex = -1;
			tileStrip = default;
		}

		private void OnBeginDrag(Vector3 screenPos)
		{
			initialPos = mapManager.ScreenToWorld(screenPos);
			var x = Mathf.RoundToInt(initialPos.x);
			var z = Mathf.RoundToInt(initialPos.z);
			var coord = new GridCoord(x, z);
			if (coord.X < 0 || coord.X >= mapManager.Width || coord.Z < 0 || coord.Z >= mapManager.Height) return;
			var tileIndex = mapManager.ToIndex(coord);

			var properties = mapManager.GetTilePropertiesAt(tileIndex);
			if (properties == null || !properties.Interactive) return;//Debug.LogWarning($"Cannot drag tile at index {tileIndex}: {(properties == null ? "Empty" : "Not draggable")}");

			dragIndex = tileIndex;
			tileStrip = default;
		}

		private void OnDrag(Vector3 screenPos)
		{
			if (dragIndex == -1) return;

			mapManager.HighlightStrip(tileStrip, false);//debug utility

			var currentPos = mapManager.ScreenToWorld(screenPos);
			var workingPos = initialPos;

			while (true)
			{
				// Reset tile positions
				if (tileStrip.Count > 1)
				{
					foreach (var tileIndex in tileStrip.Indices)
						mapManager.Tiles[tileIndex].GameObject.transform.position = mapManager.GetTilePosition(tileIndex);
				}

				var dxyz = currentPos - workingPos;// gesture
				var absX = Mathf.Abs(dxyz.x);
				var absZ = Mathf.Abs(dxyz.z);
				var dirBit = 0;

				// Compute the gesture
				if (absX > absZ && absX >= gridSize)
				{
					var direction = dxyz.x > 0 ? 1 : -1;
					dxyz = new Vector3(direction, 0, 0);// quantised gesture
					workingPos.x += direction * gridSize;
					if (dxyz.x == 1) dirBit |= TileProperties.East;
					if (dxyz.x == -1) dirBit |= TileProperties.West;
				}
				else if (absZ >= gridSize)
				{
					var direction = dxyz.z > 0 ? 1 : -1;
					dxyz = new Vector3(0, 0, direction);// quantised gesture
					workingPos.z += direction * gridSize;
					if (dxyz.z == 1) dirBit |= TileProperties.North;
					if (dxyz.z == -1) dirBit |= TileProperties.South;
				}

				// Process the gesture
				var delta = Vector3.zero;
				if (dirBit != 0)// quantised gesture
				{
					tileStrip = mapManager.GetTileStrip(dragIndex, dirBit);
					if (mapManager.RollStrip(tileStrip))
						dragIndex += tileStrip.Stride;
					tileStrip = mapManager.GetTileStrip(dragIndex, dirBit);
					initialPos += dxyz;
				}
				else// partial gesture
				{
					tileStrip = new MapManager.TileStrip();
					if (Mathf.Abs(dxyz.x) > Mathf.Abs(dxyz.z))
					{
						tileStrip = mapManager.GetTileStrip(dragIndex, dxyz.x > 0f ? TileProperties.East : TileProperties.West);
						if (tileStrip.Count > 1)
							delta = new Vector3(dxyz.x, 0, 0);
					}
					else
					{
						tileStrip = mapManager.GetTileStrip(dragIndex, dxyz.z > 0f ? TileProperties.North : TileProperties.South);
						if (tileStrip.Count > 1)
							delta = new Vector3(0, 0, dxyz.z);
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

			mapManager.HighlightStrip(tileStrip, tileStrip.Count > 1);//debug utility
		}

		private void OnEndDrag(Vector3 screenPos)
		{
			if (dragIndex == -1) return;
			
			if (tileStrip.Count > 1)
			{
				var currentPos = mapManager.Tiles[dragIndex].GameObject.transform.position;
				var x = Mathf.RoundToInt(currentPos.x);
				var z = Mathf.RoundToInt(currentPos.z);
				var targetCoord = new GridCoord(x, z);
				var targetIndex = mapManager.ToIndex(targetCoord);

				if (targetIndex != dragIndex)
					mapManager.RollStrip(tileStrip);

				foreach (var tileIndex in tileStrip.Indices)
					mapManager.Tiles[tileIndex].GameObject.transform.position = mapManager.GetTilePosition(tileIndex);
			}

			dragIndex = -1;
			mapManager.UpdateSpareTile(tileStrip, Vector3.zero, false);// Deactivate spare tile
			mapManager.HighlightStrip(tileStrip, false);//debug utility
			tileStrip = default;
		}
	}
}
