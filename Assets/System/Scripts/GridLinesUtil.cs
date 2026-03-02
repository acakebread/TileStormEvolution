using UnityEngine;

namespace MassiveHadronLtd
{
	public static class GridLinesUtil
	{
		private static GameObject currentGrid;
		private static int currentWidth = -1;
		private static int currentHeight = -1;
		private static Transform currentParent = null;
		private static Vector3 currentOffset = Vector3.zero;
		private const int Extension = 16;

		private static bool enabled = true;
		public static bool Enabled { get => enabled; set => enabled = value; }

		public static void Update(Transform parent, int width, int height, Vector3 offset = default)
		{
			if (currentGrid != null && currentWidth == width && currentHeight == height && currentParent == parent && currentOffset == offset)
				return;

			Destroy();
			//cache settings ready for reinstantiation
			currentWidth = width;
			currentHeight = height;
			currentParent = parent;
			currentOffset = offset;
		}

		public static void UpdateSize(int width, int height)
		{
			if (false == enabled || null == currentGrid || (width == currentWidth && height == currentHeight))
				return;

			Update(currentGrid.transform.parent, width, height, currentGrid.transform.localPosition);
			Show();
		}

		public static void SetVisible(bool visible)
		{
			if (currentGrid != null)
				currentGrid.SetActive(visible);
		}

		public static bool IsVisible => currentGrid != null && currentGrid.activeSelf;

		public static void Destroy()
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
			currentParent = null;
		}

		public static void Show()
		{
			if (false == enabled || currentParent == null || currentWidth <= 0 || currentHeight <= 0)
				return;

			if (null == currentGrid)
			{
				currentGrid = GridLinesHelper.CreateGridLines(currentParent, currentWidth, currentHeight, extension: Extension);
				currentGrid.transform.SetLayer(LayerMask.NameToLayer("Editor"));
				currentGrid.transform.localPosition = currentOffset;
			}

			if (null != currentGrid)
				currentGrid.SetActive(true);
		}
		public static void Hide() => currentGrid?.SetActive(false);
	}
}