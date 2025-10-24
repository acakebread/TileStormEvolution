using UnityEngine;

namespace MassiveHadronLtd
{
	public static class GridLinesHelper
	{
		public static GameObject CreateGridLines(Transform parentTransform, int width, int height, float y = 0f, float offset = 0f, Color? color = null)
		{
			// Destroy existing grid lines if they exist
			var existingGridLines = parentTransform.Find("GridLines");
			if (existingGridLines != null)
			{
				Object.Destroy(existingGridLines.gameObject);
			}

			// Create new grid lines object
			var gridLinesObject = new GameObject("GridLines");
			gridLinesObject.transform.SetParent(parentTransform, false);

			// Use faint grey as default, allow override
			var gridColor = color ?? new Color(0.5f, 0.5f, 0.5f, 0.3f);
			var gridMaterial = MaterialUtils.CreateTransparentUnlitMaterial(gridColor);
			if (gridMaterial == null)
			{
				Debug.LogError("GridLinesHelper: Failed to create grid line material.");
			}

			// Create vertical lines (along X)
			for (int x = 0; x <= width; x++)
			{
				float xPos = x + offset;
				var lineObj = new GameObject($"VerticalLine_{x}");
				lineObj.transform.SetParent(gridLinesObject.transform, false);
				var lr = lineObj.AddComponent<LineRenderer>();
				lr.material = gridMaterial;
				lr.startWidth = 0.02f;
				lr.endWidth = 0.02f;
				lr.useWorldSpace = true;
				lr.positionCount = 2;
				lr.SetPosition(0, new Vector3(xPos, y, 0 + offset));
				lr.SetPosition(1, new Vector3(xPos, y, height + offset));
			}

			// Create horizontal lines (along Z)
			for (int z = 0; z <= height; z++)
			{
				float zPos = z + offset;
				var lineObj = new GameObject($"HorizontalLine_{z}");
				lineObj.transform.SetParent(gridLinesObject.transform, false);
				var lr = lineObj.AddComponent<LineRenderer>();
				lr.material = gridMaterial;
				lr.startWidth = 0.02f;
				lr.endWidth = 0.02f;
				lr.useWorldSpace = true;
				lr.positionCount = 2;
				lr.SetPosition(0, new Vector3(0 + offset, y, zPos));
				lr.SetPosition(1, new Vector3(width + offset, y, zPos));
			}
			return gridLinesObject;
		}
	}
}