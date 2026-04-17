using UnityEngine;
using MassiveHadronLtd;

namespace ClassicTilestorm
{
	[RequireComponent(typeof(GestureSystem))]
	public class GestureController : MonoBehaviour
	{
		private Camera targetCamera;
		private IMapPlay imap;
		private TileStrip tileStrip;
		private Vector3 last;
		private Vector3 delta;
		private int dragIndex = -1;
		private const float gridSize = 1.0f;
		public event System.Action<IMapPlay> OnMapUpdated;

		public void Initialise(Camera camera, IMapPlay imap)
		{
			this.targetCamera = camera;
			this.imap = imap;
			tileStrip = default;
			dragIndex = -1;
		}

		public void Start()
		{
			var gestureSystem = gameObject.GetComponent<GestureSystem>();
			gestureSystem.OnBeginDrag += OnBeginDrag;
			gestureSystem.OnDrag += OnDrag;
			gestureSystem.OnEndDrag += OnEndDrag;
		}

		private void OnEnable() => gameObject.GetComponent<GestureSystem>().enabled = true;
		private void OnDisable() { EndDrag(Vector3.zero); gameObject.GetComponent<GestureSystem>().enabled = false; }

		private void OnDestroy()
		{
			DebugVisualizationHelper.HighlightStrip(imap, tileStrip, false);
			var gestureSystem = gameObject.GetComponent<GestureSystem>();
			if (null == gestureSystem) return;
			gestureSystem.OnBeginDrag -= OnBeginDrag;
			gestureSystem.OnDrag -= OnDrag;
			gestureSystem.OnEndDrag -= OnEndDrag;
			Destroy(gestureSystem);
		}

		private void OnBeginDrag(Vector3 screenPos)
		{
			var vert = Map.ScreenToWorld(targetCamera, screenPos);
			var index = imap.VectorToIndex(vert);
			var tile = imap.GetTile(index);
			if (false == tile.IsDrag) return;

			last = vert;
			delta = Vector3.zero;
			dragIndex = index;
			tileStrip = default;
		}

		private void OnDrag(Vector3 screenPos)
		{
			if (-1 == dragIndex) return;

			DebugVisualizationHelper.HighlightStrip(imap, tileStrip, false);

			var vert = Map.ScreenToWorld(targetCamera, screenPos);
			TryDrag(vert - last);
			last = vert;

			DebugVisualizationHelper.HighlightStrip(imap, tileStrip, tileStrip.Count > 1);
		}

		private void OnEndDrag(Vector3 screenPos) => EndDrag(Map.ScreenToWorld(targetCamera, screenPos) - last);

		private void EndDrag(Vector3 offset)
		{
			if (-1 == dragIndex) return;

			DebugVisualizationHelper.HighlightStrip(imap, tileStrip, false);

			TryDrag(offset, true);

			dragIndex = -1;
		}

		private void TryDrag(Vector3 offset, bool snap = false)
		{
			delta += offset;
			var slide = Vector3.zero;
			var isX = Mathf.Abs(delta.x) > Mathf.Abs(delta.z);

			for (var axis = 0; axis < 2; ++axis)
			{
				TileStripHelper.ResetStrip(imap, tileStrip);
				tileStrip = default;

				var val = isX ? delta.x : delta.z;
				if (Mathf.Approximately(val, 0f))
					break;

				var stride = val > 0f ? (isX ? 1 : imap.Width) : (isX ? -1 : -imap.Width);

				tileStrip = TileStripHelper.GetTileStrip(imap, dragIndex, stride, ApplicationSettings.Difficulty);
				if (tileStrip.Count <= 1)
				{
					isX = !isX;
					continue;
				}

				var count = (int)((Mathf.Abs(val) + (snap ? gridSize * 0.5f : 0f)) / gridSize);
				for (var i = 0; i < count; ++i)
				{
					if (!TileStripHelper.RollStrip(imap, tileStrip)) break;
					OnMapUpdated?.Invoke(imap);
					dragIndex += tileStrip.Stride;
					tileStrip = TileStripHelper.GetTileStrip(imap, dragIndex, stride, ApplicationSettings.Difficulty);
				}

				var mod = val % gridSize;
				if (isX)
				{
					delta = new Vector3(mod, 0, delta.z);
					slide = new Vector3(mod, 0, 0);
				}
				else
				{
					delta = new Vector3(delta.x, 0, mod);
					slide = new Vector3(0, 0, mod);
				}
				break;
			}

			if (snap)
				TileStripHelper.ResetStrip(imap, tileStrip);
			else
				TileStripHelper.TranslateStrip(imap, tileStrip, slide);
		}
	}
}