using UnityEngine;
using UnityEngine.UI;

namespace MassiveHadronLtd.UI
{
	public static class ScrollViewUtil
	{
		/// <summary>
		/// Scrolls ScrollRect so that the target Selectable is fully visible.
		/// Aligns to top/bottom if needed, similar to original DefinitionEditorPanel behaviour.
		/// </summary>
		public static void ScrollToVisible(this ScrollRect scrollRect, Selectable target)
		{
			if (scrollRect == null || target == null) return;

			var itemRt = target.GetComponent<RectTransform>();
			if (itemRt == null) return;

			Canvas.ForceUpdateCanvases();

			var viewport = scrollRect.viewport;
			var content = scrollRect.content as RectTransform;
			if (viewport == null || content == null) return;

			// Transform item position into content-local space
			Vector2 localPoint = content.InverseTransformPoint(itemRt.position);

			float itemTop = -localPoint.y;
			float itemBottom = itemTop - itemRt.rect.height;

			float viewportTop = 0f;
			float viewportBottom = -viewport.rect.height;

			float contentY = content.anchoredPosition.y;

			// If item is above viewport → scroll down
			if (itemTop > viewportTop)
			{
				contentY += itemTop - viewportTop;
			}
			// If item is below viewport → scroll up
			else if (itemBottom < viewportBottom)
			{
				contentY += itemBottom - viewportBottom;
			}

			// Clamp to content scrollable range
			float minY = 0f;
			float maxY = Mathf.Max(0f, content.rect.height - viewport.rect.height);
			contentY = Mathf.Clamp(contentY, minY, maxY);

			content.anchoredPosition = new Vector2(content.anchoredPosition.x, contentY);
		}

		/// <summary>
		/// Convenience overload - finds first Selectable in hierarchy
		/// </summary>
		public static void ScrollToVisible(this ScrollRect scrollRect, GameObject targetGo)
		{
			if (targetGo == null) return;
			var sel = targetGo.GetComponentInChildren<Selectable>(true)
				?? targetGo.GetComponentInParent<Selectable>();
			if (sel != null)
				ScrollToVisible(scrollRect, sel);
		}

		/// <summary>
		/// Safe coroutine version - waits a few frames before scrolling
		/// </summary>
		public static System.Collections.IEnumerator ScrollToVisibleAfterLayout(
			ScrollRect scrollRect,
			Selectable target,
			int framesToWait = 2)
		{
			if (scrollRect == null || target == null) yield break;

			for (int i = 0; i < framesToWait; i++)
				yield return null;

			Canvas.ForceUpdateCanvases();
			scrollRect.ScrollToVisible(target);
		}
	}
}
