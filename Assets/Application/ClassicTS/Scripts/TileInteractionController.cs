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

			gesture_direction = nDirection.none;
			tileStrip = default;
			dragIndex = tileIndex;
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

				var complete = false;
				var evaluation_direction = gesture_direction;
				var nesw = 0;//direction flag
				for (var i = 0; i < 2; ++i)
				{
					var dxyz = currentPos - initialPos;// gesture
					var absX = Mathf.Abs(dxyz.x);
					var absZ = Mathf.Abs(dxyz.z);

					// Compute the gesture
					if (nDirection.none == evaluation_direction) evaluation_direction = absZ > absX ? nDirection.north_south : 0 != absX ? nDirection.east_west : nDirection.none;

					nesw = 0;//direction flag

					// Compute the gesture
					if (nDirection.east_west == evaluation_direction)
					{
						if (absX >= gridSize)
						{
							var direction = dxyz.x > 0 ? 1 : -1;
							dxyz = new Vector3(direction, 0, 0);// quantised gesture
							if (dxyz.x == 1) nesw |= TileProperties.East;
							if (dxyz.x == -1) nesw |= TileProperties.West;
						}
					}
					else
					{
						if (absZ >= gridSize)
						{
							var direction = dxyz.z > 0 ? 1 : -1;
							dxyz = new Vector3(0, 0, direction);// quantised gesture
							if (dxyz.z == 1) nesw |= TileProperties.North;
							if (dxyz.z == -1) nesw |= TileProperties.South;
						}
					}

					// Process the gesture
					if (nesw != 0)// quantised gesture
					{
						tileStrip = mapManager.GetTileStrip(dragIndex, nesw);
						if (mapManager.RollStrip(tileStrip))
						{
							dragIndex += tileStrip.Stride;
							tileStrip = mapManager.GetTileStrip(dragIndex, nesw);
							gesture_direction = evaluation_direction;
							initialPos = currentPos;// consume the gesture
						}
						else
						{
							initialPos += dxyz;// consume partial gesture
							evaluation_direction = nDirection.none; //0 == i ? nDirection.north_south == evaluation_direction ? nDirection.east_west : nDirection.north_south : nDirection.none;
						}
					}
					else// partial gesture
					{
						tileStrip = new MapManager.TileStrip();

						var delta = Vector3.zero;
						switch (evaluation_direction)
						{
							case nDirection.north_south:
								tileStrip = mapManager.GetTileStrip(dragIndex, 0 == dxyz.z ? 0 : dxyz.z > 0f ? TileProperties.North : TileProperties.South);
								if (tileStrip.Count > 1) delta = new Vector3(0, 0, dxyz.z);
								else
								{
									//initialPos.z += dxyz.z;// consume the gesture
									evaluation_direction = nDirection.east_west;
								}
								break;
							case nDirection.east_west:
								tileStrip = mapManager.GetTileStrip(dragIndex, 0 == dxyz.x ? 0 : dxyz.x > 0f ? TileProperties.East : TileProperties.West);
								if (tileStrip.Count > 1) delta = new Vector3(dxyz.x, 0, 0);
								else
								{
									//initialPos.x += dxyz.x;// consume the gesture
									evaluation_direction = nDirection.north_south;
								}
								break;
						}

						// apply delta
						mapManager.TranslateStrip(tileStrip, delta);
						if (tileStrip.Count > 1 || i > 0)
						{
							complete = true;
							break;// this was a partial movement so break because it is the last
						}
					}
				}
				if (true == complete) break;// this was a partial movement so break because a valid partial drag has been detected
			}

			mapManager.HighlightStrip(tileStrip, tileStrip.Count > 1);//debug utility
		}

		private void OnEndDrag(Vector3 screenPos)
		{
			if (dragIndex == -1) return;

			if (tileStrip.Count > 1)
			{
				var currentPos = mapManager.ScreenToWorld(screenPos);
				var dxyz = currentPos - initialPos;// gesture
				var absX = Mathf.Abs(dxyz.x);
				var absZ = Mathf.Abs(dxyz.z);

				// Compute the gesture
				if (nDirection.none == gesture_direction) gesture_direction = absZ > absX ? nDirection.north_south : 0 != absX ? nDirection.east_west : nDirection.none;

				if ((nDirection.north_south == gesture_direction && absZ >= gridSize * 0.5f) || (nDirection.east_west == gesture_direction && absX >= gridSize * 0.5f))
					mapManager.RollStrip(tileStrip);
				mapManager.ResetStrip(tileStrip, mapManager.Width);
			}

			mapManager.HighlightStrip(tileStrip, false);//debug utility
			gesture_direction = nDirection.none;
			tileStrip = default;
			dragIndex = -1;
		}
	}
}
