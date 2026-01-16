using UnityEngine;
using UnityEngine.UI;

namespace MassiveHadronLtd
{
	[ExecuteAlways]
	public class FlexibleGridLayoutGroup : LayoutGroup
	{
		public enum FitType
		{
			FixedColumns,     // classic - fixed column count, height grows
			FixedRows,        // fixed row count, width grows
			WidthControls,    // container width → columns, height grows with content
			HeightControls,   // container height → rows, width grows with content
			FitBoth           // both directions grow to fit content (simple square-ish heuristic)
		}

		[Header("Grid Settings")]
		public FitType fitType = FitType.FixedColumns;

		[Tooltip("Used when FitType = FixedColumns or WidthControls")]
		public int columns = 4;

		[Tooltip("Used when FitType = FixedRows or HeightControls")]
		public int rows = 3;

		[Header("Cell Spacing & Padding")]
		public Vector2 spacing = new Vector2(8, 8);
		public Vector2 cellPadding = Vector2.zero; // extra padding *inside* each cell

		[Header("Child Size Control")]
		[Tooltip("Respect each child's LayoutElement.preferredWidth (if present)")]
		public bool respectChildWidth = false;

		[Tooltip("Respect each child's LayoutElement.preferredHeight (if present)")]
		public bool respectChildHeight = false;

		public override void CalculateLayoutInputHorizontal()
		{
			base.CalculateLayoutInputHorizontal();
			Calculate();
		}

		public override void CalculateLayoutInputVertical()
		{
			Calculate();
		}

		public override void SetLayoutHorizontal() => Calculate();
		public override void SetLayoutVertical() => Calculate();

		private void Calculate()
		{
			if (rectChildren.Count == 0) return;

			int childCount = rectChildren.Count;

			// ── Determine actual rows & columns ─────────────────────────────────
			int actualCols = columns;
			int actualRows = rows;

			switch (fitType)
			{
				case FitType.FixedColumns:
					actualCols = Mathf.Max(1, columns);
					actualRows = Mathf.CeilToInt((float)childCount / actualCols);
					break;

				case FitType.FixedRows:
					actualRows = Mathf.Max(1, rows);
					actualCols = Mathf.CeilToInt((float)childCount / actualRows);
					break;

				case FitType.WidthControls:
					actualCols = Mathf.Max(1, columns);
					actualRows = Mathf.CeilToInt((float)childCount / actualCols);
					break;

				case FitType.HeightControls:
					actualRows = Mathf.Max(1, rows);
					actualCols = Mathf.CeilToInt((float)childCount / actualRows);
					break;

				case FitType.FitBoth:
					actualCols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(childCount)));
					actualRows = Mathf.CeilToInt((float)childCount / actualCols);
					break;
			}

			// ── Get cell sizes ──────────────────────────────────────────────────
			float availableWidth = rectTransform.rect.width - padding.left - padding.right;
			float availableHeight = rectTransform.rect.height - padding.top - padding.bottom;

			// Default uniform cell size
			float uniformCellWidth = (availableWidth - spacing.x * (actualCols - 1)) / actualCols;
			float uniformCellHeight = (availableHeight - spacing.y * (actualRows - 1)) / actualRows;

			float cellWidth = respectChildWidth ? GetMaxChildPreferredWidth() + cellPadding.x * 2 : uniformCellWidth;
			float cellHeight = respectChildHeight ? GetMaxChildPreferredHeight() + cellPadding.y * 2 : uniformCellHeight;

			// ── Position & size children ───────────────────────────────────────
			for (int i = 0; i < rectChildren.Count; i++)
			{
				RectTransform child = rectChildren[i];

				int col = i % actualCols;
				int row = i / actualCols;

				float x = padding.left + col * (cellWidth + spacing.x);
				float y = padding.top + row * (cellHeight + spacing.y);

				// Per-child size when respecting individual preferred sizes
				float finalWidth = respectChildWidth ? GetChildPreferredWidth(child) + cellPadding.x * 2 : cellWidth;
				float finalHeight = respectChildHeight ? GetChildPreferredHeight(child) + cellPadding.y * 2 : cellHeight;

				SetChildAlongAxis(child, 0, x, finalWidth);
				SetChildAlongAxis(child, 1, y, finalHeight);
			}

			// ── Optional: drive parent size when fitting content ───────────────
			if (fitType == FitType.WidthControls || fitType == FitType.FitBoth)
			{
				float neededWidth = padding.left + padding.right
								  + actualCols * cellWidth
								  + spacing.x * Mathf.Max(0, actualCols - 1);

				// min = neededWidth, preferred = neededWidth, flexible = 0 (or -1 to ignore)
				SetLayoutInputForAxis(neededWidth, neededWidth, 0f, 0);  // horizontal
			}

			if (fitType == FitType.HeightControls || fitType == FitType.FitBoth)
			{
				float neededHeight = padding.top + padding.bottom
								   + actualRows * cellHeight
								   + spacing.y * Mathf.Max(0, actualRows - 1);

				SetLayoutInputForAxis(neededHeight, neededHeight, 0f, 1);  // vertical
			}
		}

		private float GetMaxChildPreferredWidth()
		{
			float max = 0f;
			foreach (RectTransform child in rectChildren)
				max = Mathf.Max(max, GetChildPreferredWidth(child));
			return max;
		}

		private float GetMaxChildPreferredHeight()
		{
			float max = 0f;
			foreach (RectTransform child in rectChildren)
				max = Mathf.Max(max, GetChildPreferredHeight(child));
			return max;
		}

		private float GetChildPreferredWidth(RectTransform child)
		{
			var le = child.GetComponent<LayoutElement>();
			return le != null && le.preferredWidth > 0 ? le.preferredWidth : 0f;
		}

		private float GetChildPreferredHeight(RectTransform child)
		{
			var le = child.GetComponent<LayoutElement>();
			return le != null && le.preferredHeight > 0 ? le.preferredHeight : 0f;
		}
	}
}