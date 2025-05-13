using UnityEngine;

namespace ClassicTilestorm
{
	public class GestureController : MonoBehaviour
	{
		private GestureSystem gestureSystem => GestureSystem.instance;
		private MapManager mapManager => GamePreview.mapManager;
		private TileStripHelper.TileStrip tileStrip;
		private Vector3 last;
		private Vector3 delta;
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

			DebugVisualizationHelper.HighlightStrip(mapManager, tileStrip, false);
		}

		public void Initialize()
		{
			tileStrip = default;
			dragIndex = -1;
		}

		private void OnBeginDrag(Vector3 screenPos)
		{
			var tileIndex = mapManager.ScreenToMapIndex(screenPos);
			var properties = mapManager.GetTileProperties(tileIndex);
			if (properties == null || !properties.Interactive) return;

			last = mapManager.ScreenToWorld(screenPos);
			delta = Vector3.zero;
			dragIndex = tileIndex;
			tileStrip = default;
		}

		private void OnDrag(Vector3 screenPos)
		{
			if (dragIndex == -1) return;

			DebugVisualizationHelper.HighlightStrip(mapManager, tileStrip, false);

			var vert = mapManager.ScreenToWorld(screenPos);
			TryDrag(vert - last);
			last = vert;

			DebugVisualizationHelper.HighlightStrip(mapManager, tileStrip, tileStrip.Count > 1);
		}

		private void OnEndDrag(Vector3 screenPos)
		{
			if (dragIndex == -1) return;

			DebugVisualizationHelper.HighlightStrip(mapManager, tileStrip, false);

			TryDrag(mapManager.ScreenToWorld(screenPos) - last, true);

			dragIndex = -1;
		}

		private void TryDrag(Vector3 offset, bool snap = false)
		{
			delta += offset;

			for (var axis = 0; axis < 2; ++axis)
			{
				TileStripHelper.ResetStrip(mapManager, tileStrip);
				tileStrip = default;

				bool isX = Mathf.Abs(delta.x) > Mathf.Abs(delta.z);
				float val = isX ? delta.x : delta.z;
				if (Mathf.Approximately(val, 0f))
					break;

				int direction = val > 0f ? (isX ? TileProperties.East : TileProperties.North) : (isX ? TileProperties.West : TileProperties.South);

				tileStrip = TileStripHelper.GetTileStrip(mapManager, dragIndex, direction);
				if (tileStrip.Count <= 1)
				{
					if (isX) delta.x = 0;
					else delta.z = 0;
					continue;
				}

				int count = (int)((Mathf.Abs(val) + (snap ? gridSize * 0.5f : 0f)) / gridSize);
				for (int i = 0; i < count; ++i)
				{
					if (!TileStripHelper.RollStrip(mapManager, tileStrip)) break;
					dragIndex += tileStrip.Stride;
					tileStrip = TileStripHelper.GetTileStrip(mapManager, dragIndex, direction);
				}

				if (isX)
					delta = new Vector3(val % gridSize, 0, 0);
				else
					delta = new Vector3(0, 0, val % gridSize);
				break;
			}

			if (snap)
				TileStripHelper.ResetStrip(mapManager, tileStrip);
			else
				TileStripHelper.TranslateStrip(mapManager, tileStrip, delta);
		}
	}
}