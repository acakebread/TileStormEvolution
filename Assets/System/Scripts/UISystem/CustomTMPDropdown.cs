using UnityEngine;
using TMPro;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class CustomTMPDropdown : TMP_Dropdown
{
	private bool needsScroll = false;

	protected override void Awake()
	{
		base.Awake();

		// Hook into value changed event (this is how TMP_Dropdown notifies changes)
		onValueChanged.AddListener(OnDropdownValueChanged);

		// Optional: deactivate template early to avoid initial flicker
		if (template != null)
			template.gameObject.SetActive(false);
	}

	public new void Show()
	{
		base.Show();

		// Flag that we want to scroll after base.Show() (which resets scroll to top)
		needsScroll = true;

		// Run scroll logic next frame (after instantiation + internal reset)
		CancelInvoke(nameof(DoScrollToSelected));
		Invoke(nameof(DoScrollToSelected), 0f);
	}

	private void DoScrollToSelected()
	{
		if (!needsScroll || !IsExpanded)
		{
			needsScroll = false;
			return;
		}

		needsScroll = false;

		if (template == null)
		{
			Debug.LogWarning("CustomTMPDropdown: No template found");
			return;
		}

		var scrollRect = template.GetComponentInChildren<ScrollRect>(true);
		if (scrollRect == null)
		{
			Debug.LogWarning("CustomTMPDropdown: No ScrollRect in template");
			return;
		}

		if (scrollRect.content == null)
		{
			Debug.LogWarning("CustomTMPDropdown: ScrollRect has no content");
			return;
		}

		int index = value;
		if (index < 0 || index >= options.Count || index >= scrollRect.content.childCount)
		{
			Debug.LogWarning($"CustomTMPDropdown: Invalid index {index} (options: {options.Count}, children: {scrollRect.content.childCount})");
			return;
		}

		var targetItem = scrollRect.content.GetChild(index) as RectTransform;
		if (targetItem == null)
		{
			Debug.LogWarning("CustomTMPDropdown: Target item is null");
			return;
		}

		// Force layout to be up-to-date
		LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
		LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.viewport);
		Canvas.ForceUpdateCanvases();

		float itemTop = -targetItem.anchoredPosition.y;
		float itemHeight = targetItem.rect.height;
		float itemCenter = itemTop + (itemHeight / 2f);

		float viewportHeight = scrollRect.viewport.rect.height;
		float contentHeight = scrollRect.content.rect.height;

		if (contentHeight <= viewportHeight)
		{
			Debug.Log("CustomTMPDropdown: No scrolling needed (content fits viewport)");
			return;
		}

		// Center the selected item
		float targetPos = itemCenter - (viewportHeight / 2f);
		float normalized = Mathf.Clamp01(targetPos / (contentHeight - viewportHeight));

		// TMP_Dropdown scroll is inverted: 1 = top, 0 = bottom
		scrollRect.verticalNormalizedPosition = 1f - normalized;

		Debug.Log($"CustomTMPDropdown: Scrolled to index {index} (normalized pos = {scrollRect.verticalNormalizedPosition:F3})");
	}

	// Called when value changes (via onValueChanged event)
	private void OnDropdownValueChanged(int newValue)
	{
		// If dropdown is already open, scroll to the new value
		if (IsExpanded)
		{
			CancelInvoke(nameof(DoScrollToSelected));
			Invoke(nameof(DoScrollToSelected), 0.02f); // tiny delay for safety
		}
	}
}