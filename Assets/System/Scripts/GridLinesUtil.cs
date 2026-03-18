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
		public static bool Enabled 
		{ 
			get => enabled;
			set
			{
				var changed = enabled != value;
				enabled = value;
				if (changed || (null == currentGrid && value))
				{
					if (value)
						Show();
					else
						Hide();
				}
			}
		}

		public static void Initialise(Transform parent, int width = -1, int height = -1, Vector3 offset = default)
		{
			currentParent = parent;
			currentOffset = offset;
		}

		public static void Update(int width = 32, int height = 32, Vector3 offset = default)
		{
			if (currentWidth == width && currentHeight == height && currentOffset == offset)
				return;

			var parent = currentParent;
			Destroy();
			//cache settings ready for reinstantiation
			currentParent = parent;
			currentWidth = width;
			currentHeight = height;
			currentOffset = offset;
			Show();
		}

		public static void UpdateSize(int width, int height)
		{
			if (width != currentWidth || height != currentHeight)
				Update(width, height, currentOffset);
		}

		public static void UpdateOffset(Vector3 value)
		{
			if (value != currentOffset)
				Update(currentWidth, currentHeight, value);
		}

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

		private static void Show()
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
		private static void Hide() => currentGrid?.SetActive(false);


		//public static void SetVisible(bool visible)
		//{
		//	if (currentGrid != null)
		//		currentGrid.SetActive(visible);
		//}

		//public static bool IsVisible => currentGrid != null && currentGrid.activeSelf;
	}
}