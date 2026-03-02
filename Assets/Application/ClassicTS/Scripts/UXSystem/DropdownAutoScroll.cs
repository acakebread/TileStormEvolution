using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(ScrollRect))]
public class DropdownAutoScroll : MonoBehaviour
{
	private ScrollRect scrollRect;
	private TMP_Dropdown dropdown; // Reference to parent dropdown

	private void Awake()
	{
		scrollRect = GetComponent<ScrollRect>();

		// Find the TMP_Dropdown in parents (since template is instantiated)
		dropdown = GetComponentInParent<TMP_Dropdown>();
	}

	private void OnEnable()
	{
		if (dropdown == null) return;

		// Wait one frame after enable (template reset happens on enable)
		Invoke(nameof(ScrollToSelected), 0f);
	}

	private void ScrollToSelected()
	{
		if (dropdown == null || !dropdown.IsExpanded) return;

		int selectedIndex = dropdown.value;
		if (selectedIndex < 0 || selectedIndex >= dropdown.options.Count) return;

		var content = scrollRect.content;
		if (content == null || content.childCount <= selectedIndex) return;

		var targetItem = content.GetChild(selectedIndex) as RectTransform;
		if (targetItem == null) return;

		// Force layout
		LayoutRebuilder.ForceRebuildLayoutImmediate(content);
		Canvas.ForceUpdateCanvases();

		// Center the selected item
		float itemTop = -targetItem.anchoredPosition.y;
		float itemCenter = itemTop + (targetItem.rect.height / 2f);
		float viewportHeight = scrollRect.viewport.rect.height;
		float contentHeight = content.rect.height;

		if (contentHeight <= viewportHeight) return; // No scroll needed

		float targetPos = itemCenter - (viewportHeight / 2f);
		float normalized = Mathf.Clamp01(targetPos / (contentHeight - viewportHeight));

		// Dropdown scroll is inverted (1 = top, 0 = bottom)
		scrollRect.verticalNormalizedPosition = 1f - normalized;

		Debug.Log($"DropdownAutoScroll: Scrolled to index {selectedIndex} (pos = {scrollRect.verticalNormalizedPosition})");
	}
}