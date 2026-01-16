using UnityEngine;
using UnityEngine.UI;

namespace MassiveHadronLtd
{
	[ExecuteAlways]
	public class AutoGridLayoutGroup : LayoutGroup
	{
		public int columns = 4;
		public Vector2 spacing = Vector2.zero;

		public override void CalculateLayoutInputHorizontal()
		{
			base.CalculateLayoutInputHorizontal();
			Calculate();
		}

		public override void CalculateLayoutInputVertical()
		{
			Calculate();
		}

		public override void SetLayoutHorizontal()
		{
			SetCells();
		}

		public override void SetLayoutVertical()
		{
			SetCells();
		}

		private void Calculate()
		{
			float width = rectTransform.rect.width
				- padding.left - padding.right
				- spacing.x * (columns - 1);

			float cellWidth = width / columns;
			int rows = Mathf.CeilToInt(rectChildren.Count / (float)columns);

			float height = rectTransform.rect.height
				- padding.top - padding.bottom
				- spacing.y * (rows - 1);

			float cellHeight = height / rows;

			for (int i = 0; i < rectChildren.Count; i++)
			{
				int row = i / columns;
				int col = i % columns;

				float x = padding.left + (cellWidth + spacing.x) * col;
				float y = padding.top + (cellHeight + spacing.y) * row;

				SetChildAlongAxis(rectChildren[i], 0, x, cellWidth);
				SetChildAlongAxis(rectChildren[i], 1, y, cellHeight);
			}
		}

		private void SetCells() { }
	}
}