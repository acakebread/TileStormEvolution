using UnityEngine;

namespace GamePreviewNamespace
{
	public class TileInteractionController : MonoBehaviour
	{
		private GestureSystem gestureSystem => GestureSystem.instance;
		private MapManager mapManager;
		private MapManager.TileStrip tileStrip;//working selection
		private Vector3 initialPos;//world ground plane position
		private int dragIndex = -1;//selected tile
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
			if (initialPos.x < 0 || initialPos.x >= mapManager.Width || initialPos.z < 0 || initialPos.z >= mapManager.Height) return;
			var tileIndex = mapManager.ToIndex(new GridCoord(initialPos));

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
				mapManager.ResetStrip(tileStrip, mapManager.Width);

				var dxyz = currentPos - workingPos;// gesture
				var absX = Mathf.Abs(dxyz.x);
				var absZ = Mathf.Abs(dxyz.z);
				var nesw = 0;//direction flag

				// Compute the gesture
				if (absX > absZ && absX >= gridSize)
				{
					var direction = dxyz.x > 0 ? 1 : -1;
					dxyz = new Vector3(direction, 0, 0);// quantised gesture
					workingPos.x += direction * gridSize;
					if (dxyz.x == 1) nesw |= TileProperties.East;
					if (dxyz.x == -1) nesw |= TileProperties.West;
				}
				else if (absZ >= gridSize)
				{
					var direction = dxyz.z > 0 ? 1 : -1;
					dxyz = new Vector3(0, 0, direction);// quantised gesture
					workingPos.z += direction * gridSize;
					if (dxyz.z == 1) nesw |= TileProperties.North;
					if (dxyz.z == -1) nesw |= TileProperties.South;
				}

				// Process the gesture
				var delta = Vector3.zero;
				if (nesw != 0)// quantised gesture
				{
					tileStrip = mapManager.GetTileStrip(dragIndex, nesw);
					if (mapManager.RollStrip(tileStrip))
						dragIndex += tileStrip.Stride;
					tileStrip = mapManager.GetTileStrip(dragIndex, nesw);
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

					// apply delta
					mapManager.TranslateStrip(tileStrip, delta);
				}

				mapManager.UpdateSpareTile(tileStrip, delta, delta != Vector3.zero);

				if (nesw == 0) break;// Break if this was a partial movement
			}

			mapManager.HighlightStrip(tileStrip, tileStrip.Count > 1);//debug utility
		}

		private void OnEndDrag(Vector3 screenPos)
		{
			if (dragIndex == -1) return;

			if (tileStrip.Count > 1)
			{
				var currentPos = mapManager.ScreenToWorld(screenPos);
				var targetIndex = mapManager.ToIndex(new GridCoord(currentPos));

				if (targetIndex != dragIndex)
					mapManager.RollStrip(tileStrip);

				mapManager.ResetStrip(tileStrip, mapManager.Width);
			}

			dragIndex = -1;
			mapManager.UpdateSpareTile(tileStrip, Vector3.zero, false);// Deactivate spare tile
			mapManager.HighlightStrip(tileStrip, false);//debug utility
			tileStrip = default;
		}
	}
}
