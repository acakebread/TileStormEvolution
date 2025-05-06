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
			tileStrip = default;
			dragIndex = -1;
		}

		private void OnBeginDrag(Vector3 screenPos)
		{
			var tileIndex = mapManager.ScreenToMapIndex(screenPos);
			var properties = mapManager.GetTileProperties(tileIndex);
			if (properties == null || !properties.Interactive) return;

			initialPos = mapManager.ScreenToWorld(screenPos);
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
				var dxyz = currentPos - initialPos;// gesture
				var absX = Mathf.Abs(dxyz.x);
				var absZ = Mathf.Abs(dxyz.z);
				var nesw = 0;//direction flag

				// Compute the gesture
				if (absX > absZ && absX >= gridSize)
				{
					var direction = dxyz.x > 0 ? gridSize : -gridSize;
					dxyz = new Vector3(direction, 0, 0);// quantised gesture
					if (dxyz.x == gridSize) nesw |= TileProperties.East;
					if (dxyz.x == -gridSize) nesw |= TileProperties.West;
				}
				else if (absZ >= gridSize)
				{
					var direction = dxyz.z > 0 ? gridSize : -gridSize;
					dxyz = new Vector3(0, 0, direction);// quantised gesture
					if (dxyz.z == gridSize) nesw |= TileProperties.North;
					if (dxyz.z == -gridSize) nesw |= TileProperties.South;
				}

				// Reset tile positions
				mapManager.ResetStrip(tileStrip, mapManager.Width);

				// Process the gesture
				if (nesw != 0)// quantised gesture
				{
					tileStrip = mapManager.GetTileStrip(dragIndex, nesw);
					if (mapManager.RollStrip(tileStrip))
						dragIndex += tileStrip.Stride;
					tileStrip = mapManager.GetTileStrip(dragIndex, nesw);
					initialPos += dxyz;// consume the gesture
				}
				else// partial gesture
				{
					tileStrip = new MapManager.TileStrip();

					var delta = Vector3.zero;
					if (Mathf.Abs(dxyz.x) > Mathf.Abs(dxyz.z))
					{
						tileStrip = mapManager.GetTileStrip(dragIndex, 0 == dxyz.x ? 0 : dxyz.x > 0f ? TileProperties.East : TileProperties.West);
						if (tileStrip.Count > 1)
							delta = new Vector3(dxyz.x, 0, 0);
					}
					else
					{
						tileStrip = mapManager.GetTileStrip(dragIndex, 0 == dxyz.z ? 0 : dxyz.z > 0f ? TileProperties.North : TileProperties.South);
						if (tileStrip.Count > 1)
							delta = new Vector3(0, 0, dxyz.z);
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
