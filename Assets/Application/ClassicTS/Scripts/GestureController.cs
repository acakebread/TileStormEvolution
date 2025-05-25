using UnityEngine;

namespace ClassicTilestorm
{
	public class GestureController : MonoBehaviour
	{
		private GestureSystem gestureSystem => GestureSystem.instance;
		private MapManager mapManager => GameController.mapManager;

		private TileStripHelper.TileStrip tileStrip;
		private Vector3 last;
		private Vector3 delta;
		private int dragIndex = -1;
		private const float gridSize = 1.0f;

		public static GestureController instance;
		private void Awake() { instance = this; Reset(); }

		public void Reset()
		{
			tileStrip = default;
			dragIndex = -1;
		}

		public void Start()
		{
			if (null == gestureSystem) return;
			gestureSystem.OnBeginDrag += OnBeginDrag;
			gestureSystem.OnDrag += OnDrag;
			gestureSystem.OnEndDrag += OnEndDrag;
		}

		private void OnDestroy()
		{
			instance = null;
			DebugVisualizationHelper.HighlightStrip(mapManager, tileStrip, false);
			if (null == gestureSystem) return;
			gestureSystem.OnBeginDrag -= OnBeginDrag;
			gestureSystem.OnDrag -= OnDrag;
			gestureSystem.OnEndDrag -= OnEndDrag;
		}

		private void OnBeginDrag(Vector3 screenPos)
		{
			var tileIndex = mapManager.ScreenToMapIndex(screenPos);
			var properties = mapManager.GetTileProperties(tileIndex);
			if (null == properties || !properties.Interactive) return;

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

				var isX = Mathf.Abs(delta.x) > Mathf.Abs(delta.z);
				var val = isX ? delta.x : delta.z;
				if (Mathf.Approximately(val, 0f))
					break;

				var stride = val > 0f ? (isX ? 1 : mapManager.Width) : (isX ? -1 : -mapManager.Width);

				tileStrip = TileStripHelper.GetTileStrip(mapManager, dragIndex, stride, PreviewSettings.Difficulty);
				if (tileStrip.Count <= 1)
				{
					if (isX) delta.x = 0;
					else delta.z = 0;
					continue;
				}

				var count = (int)((Mathf.Abs(val) + (snap ? gridSize * 0.5f : 0f)) / gridSize);
				for (var i = 0; i < count; ++i)
				{
					if (!TileStripHelper.RollStrip(mapManager, tileStrip)) break;
					dragIndex += tileStrip.Stride;
					tileStrip = TileStripHelper.GetTileStrip(mapManager, dragIndex, stride, PreviewSettings.Difficulty);
				}

				delta = isX ? new Vector3(val % gridSize, 0, 0) : new Vector3(0, 0, val % gridSize);
				break;
			}

			if (snap)
				TileStripHelper.ResetStrip(mapManager, tileStrip);
			else
				TileStripHelper.TranslateStrip(mapManager, tileStrip, delta);
		}
	}
}