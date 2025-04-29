using UnityEngine;

namespace GamePreviewNamespace
{
	public class TileInteractionController : MonoBehaviour
	{
		enum nDirection
		{
			none,
			north_south,
			east_west
		}

		private GestureSystem gestureSystem => GestureSystem.instance;
		private MapManager mapManager;
		private MapManager.TileStrip tileStrip;//working selection
		private Vector3 initialPos;//world ground plane position
		private int dragIndex = -1;//selected tile
		private const float gridSize = 1.0f;
		private nDirection gesture_direction = nDirection.none;

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
			tileStrip = default;
			dragIndex = -1;
		}

		private void OnBeginDrag(Vector3 screenPos)
		{
			initialPos = mapManager.ScreenToWorld(screenPos);
			if (initialPos.x < 0 || initialPos.x >= mapManager.Width || initialPos.z < 0 || initialPos.z >= mapManager.Height) return;
			var tileIndex = mapManager.ToIndex(new GridCoord(initialPos));

			var properties = mapManager.GetTileProperties(tileIndex);
			if (properties == null || !properties.Interactive) return;//Debug.LogWarning($"Cannot drag tile at index {tileIndex}: {(properties == null ? "Empty" : "Not draggable")}");

			tileStrip = default;
			dragIndex = tileIndex;
			gesture_direction = nDirection.none;
		}

		private void OnDrag(Vector3 screenPos)
		{
			if (dragIndex == -1) return;

			mapManager.HighlightStrip(tileStrip, false);//debug utility

			var currentPos = mapManager.ScreenToWorld(screenPos);

			while (true)
			{
				// Reset tile positions
				mapManager.ResetStrip(tileStrip, mapManager.Width);

				var dxyz = currentPos - initialPos;// gesture
				var absX = Mathf.Abs(dxyz.x);
				var absZ = Mathf.Abs(dxyz.z);
				var nesw = 0;//direction flag

				// Compute the gesture
				if (absX > absZ && absX >= gridSize)
				{
					var direction = dxyz.x > 0 ? 1 : -1;
					dxyz = new Vector3(direction, 0, 0);// quantised gesture
					if (dxyz.x == 1) nesw |= TileProperties.East;
					if (dxyz.x == -1) nesw |= TileProperties.West;
				}
				else if (absZ >= gridSize)
				{
					var direction = dxyz.z > 0 ? 1 : -1;
					dxyz = new Vector3(0, 0, direction);// quantised gesture
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
					initialPos += dxyz;// consume the gesture
					gesture_direction = absZ > absX ? nDirection.north_south : nDirection.east_west;
				}
				else// partial gesture
				{
					if (nDirection.none == gesture_direction && 0 != absX && 0 != absZ)
						gesture_direction = absZ > absX ? nDirection.north_south : nDirection.east_west;

					tileStrip = new MapManager.TileStrip();

					var evaluation_direction = gesture_direction;
					var count = 0;
					while (count < 2 && tileStrip.Count <= 1 && nDirection.none != evaluation_direction)
					{
						switch (evaluation_direction)
						{
							case nDirection.north_south:
								evaluation_direction = nDirection.east_west;
								tileStrip = mapManager.GetTileStrip(dragIndex, 0 == dxyz.z ? 0 : dxyz.z > 0f ? TileProperties.North : TileProperties.South);
								if (tileStrip.Count > 1) delta = new Vector3(0, 0, dxyz.z);
								break;
							case nDirection.east_west:
								evaluation_direction = nDirection.north_south;
								tileStrip = mapManager.GetTileStrip(dragIndex, 0 == dxyz.x ? 0 : dxyz.x > 0f ? TileProperties.East : TileProperties.West);
								if (tileStrip.Count > 1) delta = new Vector3(dxyz.x, 0, 0);
								break;
						}
						++count;
					}

					// apply delta
					mapManager.TranslateStrip(tileStrip, delta);
					break;// this was a partial movement so break because it is the last
				}
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

			mapManager.HighlightStrip(tileStrip, false);//debug utility
			tileStrip = default;
			dragIndex = -1;
		}
	}
}
