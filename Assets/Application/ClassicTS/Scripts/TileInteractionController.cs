using UnityEngine;

namespace GamePreviewNamespace
{
	public class TileInteractionController : MonoBehaviour
	{
		private GestureSystem gestureSystem => GestureSystem.instance;
		private MapManager mapManager;
		private MapManager.TileStrip tileStrip;//working selection
		private Vector3 last;//last world ground plane position
		private Vector3 delta;//delta remainder
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

			last = mapManager.ScreenToWorld(screenPos);
			tileStrip = default;
			dragIndex = tileIndex;
			delta = Vector3.zero;
		}

		private void OnDrag(Vector3 screenPos)
		{
			if (dragIndex == -1) return;

			mapManager.HighlightStrip(tileStrip, false);//debug utility

			var vert = mapManager.ScreenToWorld(screenPos);
			TryDrag(vert - last);
			last = vert;

			if (tileStrip.Count > 1)
				mapManager.TranslateStrip(tileStrip, delta);// apply remainder

			mapManager.HighlightStrip(tileStrip, tileStrip.Count > 1);//debug utility
		}

		private void OnEndDrag(Vector3 screenPos)
		{
			if (dragIndex == -1) return;

			mapManager.HighlightStrip(tileStrip, false);//debug utility

			if (tileStrip.Count > 1)
			{
				var vert = mapManager.ScreenToWorld(screenPos);
				TryDrag(vert - last, f => Mathf.Abs(f) >= 0.5f ? (int)Mathf.Sign(f) : 0);
				mapManager.ResetStrip(tileStrip, mapManager.Width);
			}

			tileStrip = default;
			dragIndex = -1;
		}

		private void TryDrag(Vector3 offset, System.Func<float, int> roundFunc = null)
		{
			delta += offset;
			roundFunc ??= (val => (int)(val / gridSize)); // default truncation

			for (var n = 0; n < 2; ++n)
			{
				mapManager.ResetStrip(tileStrip, mapManager.Width);
				tileStrip = default;

				if (Mathf.Abs(delta.x) > Mathf.Abs(delta.z))
				{
					var direction = delta.x == 0f ? 0 : delta.x > 0f ? TileProperties.East : TileProperties.West;
					tileStrip = mapManager.GetTileStrip(dragIndex, direction);
					if (tileStrip.Count > 1)
					{
						var count = roundFunc(delta.x);
						if (count != 0)
						{
							for (var i = 0; i < Mathf.Abs(count); ++i)
							{
								if (mapManager.RollStrip(tileStrip))
								{
									dragIndex += tileStrip.Stride;
									tileStrip = mapManager.GetTileStrip(dragIndex, direction);
								}
							}
							delta -= new Vector3(count, 0, 0);
						}
						delta.z = 0;
						break;
					}
					else
						delta.x = 0;
				}
				else
				{
					var direction = delta.z == 0f ? 0 : delta.z > 0f ? TileProperties.North : TileProperties.South;
					tileStrip = mapManager.GetTileStrip(dragIndex, direction);
					if (tileStrip.Count > 1)
					{
						var count = roundFunc(delta.z);
						if (count != 0)
						{
							for (var i = 0; i < Mathf.Abs(count); ++i)
							{
								if (mapManager.RollStrip(tileStrip))
								{
									dragIndex += tileStrip.Stride;
									tileStrip = mapManager.GetTileStrip(dragIndex, direction);
								}
							}
							delta -= new Vector3(0, 0, count);
						}
						delta.x = 0;
						break;
					}
					else
						delta.z = 0;
				}
			}
		}
	}
}
