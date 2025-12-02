using UnityEngine;

namespace MassiveHadronLtd
{
	public static class GridLinesHelper
	{
		public static GameObject CreateGridLines(
			Transform parentTransform,
			int width,
			int height,
			int extension = 0,
			Color? color = null,
			Color? extendedColor = null)
		{
			var existing = parentTransform.Find("GridLines");
			if (existing != null)
				Object.Destroy(existing.gameObject);

			var gridLinesObject = new GameObject("GridLines");
			var gridTransform = gridLinesObject.transform;
			gridTransform.SetParent(parentTransform, false);
			gridTransform.localPosition = Vector3.zero;
			gridTransform.localRotation = Quaternion.identity;

			// Materials
			var mainColor = color ?? new Color(0.5f, 0.5f, 0.5f, 0.3f);
			var mainMaterial = MaterialUtils.CreateTransparentUnlitMaterial(mainColor);

			Material extendedMaterial = null;
			if (extension > 0)
			{
				var extColor = extendedColor ?? new Color(0.4f, 0.4f, 0.9f, 0.3f); // ← Debug blue
				extendedMaterial = MaterialUtils.CreateTransparentUnlitMaterial(extColor);
			}

			if (mainMaterial == null)
			{
				Debug.LogError("GridLinesHelper: Failed to create main grid material.");
				return gridLinesObject;
			}

			int totalSizeX = width + 2 * extension;
			int totalSizeZ = height + 2 * extension;

			float offsetX = -extension;
			float offsetZ = -extension;

			// ====================================================================
			// 1. VERTICAL LINES (constant X)
			// ====================================================================
			for (int x = 0; x <= totalSizeX; x++)
			{
				float posX = x + offsetX;
				bool isMainX = x >= extension && x <= width + extension;

				if (isMainX)
				{
					// Main vertical line: only main height
					CreateLine(gridTransform, mainMaterial,
						new Vector3(posX, 0f, 0f),
						new Vector3(posX, 0f, height),
						$"V_Main_{x}");

					// Add extended TOP and BOTTOM segments on main X lines
					if (extension > 0)
					{
						// Top extension
						CreateLine(gridTransform, extendedMaterial,
							new Vector3(posX, 0f, height),
							new Vector3(posX, 0f, height + extension),
							$"V_Ext_Top_{x}");

						// Bottom extension
						CreateLine(gridTransform, extendedMaterial,
							new Vector3(posX, 0f, -extension),
							new Vector3(posX, 0f, 0f),
							$"V_Ext_Bottom_{x}");
					}
				}
				else if (extension > 0)
				{
					// Fully extended vertical lines (left/right borders)
					CreateLine(gridTransform, extendedMaterial,
						new Vector3(posX, 0f, offsetZ),
						new Vector3(posX, 0f, offsetZ + totalSizeZ),
						$"V_Ext_Full_{x}");
				}
			}

			// ====================================================================
			// 2. HORIZONTAL LINES (constant Z)
			// ====================================================================
			for (int z = 0; z <= totalSizeZ; z++)
			{
				float posZ = z + offsetZ;
				bool isMainZ = z >= extension && z <= height + extension;

				if (isMainZ)
				{
					// Main horizontal line: only main width
					CreateLine(gridTransform, mainMaterial,
						new Vector3(0f, 0f, posZ),
						new Vector3(width, 0f, posZ),
						$"H_Main_{z}");

					// Add extended LEFT and RIGHT segments on main Z lines
					if (extension > 0)
					{
						// Left extension
						CreateLine(gridTransform, extendedMaterial,
							new Vector3(-extension, 0f, posZ),
							new Vector3(0f, 0f, posZ),
							$"H_Ext_Left_{z}");

						// Right extension
						CreateLine(gridTransform, extendedMaterial,
							new Vector3(width, 0f, posZ),
							new Vector3(width + extension, 0f, posZ),
							$"H_Ext_Right_{z}");
					}
				}
				else if (extension > 0)
				{
					// Fully extended horizontal lines (top/bottom borders)
					CreateLine(gridTransform, extendedMaterial,
						new Vector3(offsetX, 0f, posZ),
						new Vector3(offsetX + totalSizeX, 0f, posZ),
						$"H_Ext_Full_{z}");
				}
			}

			return gridLinesObject;
		}

		private static void CreateLine(Transform parent, Material mat, Vector3 start, Vector3 end, string name)
		{
			if (mat == null) return;

			var obj = new GameObject(name);
			obj.transform.SetParent(parent, false);

			var lr = obj.AddComponent<LineRenderer>();
			lr.material = mat;
			lr.startWidth = lr.endWidth = 0.02f;
			lr.positionCount = 2;
			lr.useWorldSpace = false;
			lr.SetPosition(0, start);
			lr.SetPosition(1, end);
		}
	}
}