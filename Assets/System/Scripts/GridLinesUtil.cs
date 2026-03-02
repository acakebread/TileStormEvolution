using UnityEngine;

namespace MassiveHadronLtd
{
	public static class GridLinesUtil
	{
		private static GameObject currentGrid;
		private static int currentWidth = -1;
		private static int currentHeight = -1;

		private const int Extension = 16;

		private static bool enabled = true;
		public static bool Enabled { get => enabled; set { enabled = value; currentGrid?.SetActive(value); } }

		/// <summary>
		/// Shows, updates, or hides the grid. If visible = false, hides without recreating.
		/// </summary>
		public static void Show(Transform parent, int width, int height, Vector3 offset = default)
		{
			// If turning off: just hide existing
			if (currentGrid != null && !enabled)
			{
				currentGrid.SetActive(false);
				return;
			}

			// If turning on but already correct size and visible: early exit
			if (enabled &&
				currentGrid != null &&
				currentWidth == width &&
				currentHeight == height &&
				currentGrid.activeSelf)
			{
				return;
			}

			// Otherwise: full recreate
			Hide();

			if (parent == null || width <= 0 || height <= 0 || !enabled)
				return;

			currentGrid = GridLinesHelper.CreateGridLines(
				parent,
				width,
				height,
				extension: Extension
			);

			if (currentGrid != null)
			{
				currentGrid.transform.SetLayer(LayerMask.NameToLayer("Editor"));
				currentGrid.transform.localPosition = offset;// Map.tile_origin + new Vector3(-0.5f, 0f, -0.5f);
				currentGrid.SetActive(true);
			}

			currentWidth = width;
			currentHeight = height;
		}

		public static void UpdateSize(int width, int height)
		{
			if (false == enabled || null == currentGrid || (width == currentWidth && height == currentHeight))
				return;

			var parent = currentGrid.transform.parent;
			Show(parent, width, height, currentGrid.transform.localPosition);
		}

		public static void SetVisible(bool visible)
		{
			if (currentGrid != null)
				currentGrid.SetActive(visible);
		}

		public static bool IsVisible => currentGrid != null && currentGrid.activeSelf;

		public static void Hide()
		{
			if (currentGrid != null)
			{
				if (Application.isPlaying)
					Object.Destroy(currentGrid);
				else
					Object.DestroyImmediate(currentGrid);

				currentGrid = null;
			}

			currentWidth = -1;
			currentHeight = -1;
		}
	}
}