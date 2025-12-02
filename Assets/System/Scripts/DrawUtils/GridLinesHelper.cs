using UnityEngine;

namespace MassiveHadronLtd
{
	public static class GridLinesHelper
	{
		public static GameObject CreateGridLines(Transform parentTransform, int width, int height, Color? color = null)
		{
			// Destroy existing grid lines
			var existing = parentTransform.Find("GridLines");
			if (existing != null)
				Object.Destroy(existing.gameObject);

			// Create container (will be positioned by the caller)
			var gridLinesObject = new GameObject("GridLines");
			var gridTransform = gridLinesObject.transform;
			gridTransform.SetParent(parentTransform, false);
			gridTransform.localPosition = Vector3.zero;   // Explicitly clean
			gridTransform.localRotation = Quaternion.identity;

			// Material
			var gridColor = color ?? new Color(0.5f, 0.5f, 0.5f, 0.3f);
			var gridMaterial = MaterialUtils.CreateTransparentUnlitMaterial(gridColor);

			if (gridMaterial == null)
			{
				Debug.LogError("GridLinesHelper: Failed to create grid line material.");
				return gridLinesObject;
			}

			// === Vertical lines (constant X) ===
			for (int x = 0; x <= width; x++)
			{
				var lineObj = new GameObject($"VerticalLine_{x}");
				lineObj.transform.SetParent(gridTransform, false);

				var lr = lineObj.AddComponent<LineRenderer>();
				lr.material = gridMaterial;
				lr.startWidth = lr.endWidth = 0.02f;
				lr.positionCount = 2;
				lr.useWorldSpace = false;

				lr.SetPosition(0, new Vector3(x, 0f, 0f));
				lr.SetPosition(1, new Vector3(x, 0f, height));
			}

			// === Horizontal lines (constant Z) ===
			for (int z = 0; z <= height; z++)
			{
				var lineObj = new GameObject($"HorizontalLine_{z}");
				lineObj.transform.SetParent(gridTransform, false);

				var lr = lineObj.AddComponent<LineRenderer>();
				lr.material = gridMaterial;
				lr.startWidth = lr.endWidth = 0.02f;
				lr.positionCount = 2;
				lr.useWorldSpace = false;

				lr.SetPosition(0, new Vector3(0f, 0f, z));
				lr.SetPosition(1, new Vector3(width, 0f, z));
			}

			return gridLinesObject;
		}
	}
}