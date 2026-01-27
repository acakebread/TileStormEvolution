using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(TMP_Dropdown))]
public class TMPDropdownSelectedGraphic : MonoBehaviour
{
	private TMP_Dropdown dropdown;
	private Graphic targetGraphic;

	// Original colours
	private ColorBlock originalColors;

	private bool isCurrentlyActive;

	// Global tracker: the one dropdown that is allowed to show selected
	private static TMPDropdownSelectedGraphic globalActiveDropdown;

	private void Awake()
	{
		dropdown = GetComponent<TMP_Dropdown>();
		targetGraphic = dropdown.targetGraphic;

		if (targetGraphic == null)
		{
			Debug.LogWarning("No targetGraphic on TMP_Dropdown", this);
			enabled = false;
			return;
		}

		originalColors = dropdown.colors;
	}

	private void Update()
	{
		var es = EventSystem.current;
		if (es == null) return;

		bool nowExpanded = dropdown.IsExpanded;
		bool nowSelected = es.currentSelectedGameObject == gameObject;

		// If this dropdown is expanded or selected → claim global active status
		if (nowExpanded || nowSelected)
		{
			globalActiveDropdown = this;
			isCurrentlyActive = true;
		}
		// Otherwise → check if we are still the global active one
		else
		{
			isCurrentlyActive = (globalActiveDropdown == this);

			// If we are the global one but no longer active → check if someone else took over
			if (isCurrentlyActive)
			{
				// If something else is now selected or expanded → give up
				if (es.currentSelectedGameObject != null && es.currentSelectedGameObject != gameObject)
				{
					var otherGraphic = es.currentSelectedGameObject.GetComponent<TMPDropdownSelectedGraphic>();
					if (otherGraphic != null && otherGraphic != this)
					{
						globalActiveDropdown = otherGraphic;
						isCurrentlyActive = false;
					}
				}
				else if (FindAnyOtherExpandedDropdown())
				{
					globalActiveDropdown = null;
					isCurrentlyActive = false;
				}
			}
		}

		var colors = dropdown.colors;

		if (isCurrentlyActive)
		{
			colors.normalColor = originalColors.selectedColor;
		}
		else
		{
			colors.normalColor = originalColors.normalColor;
			colors.highlightedColor = originalColors.highlightedColor;
			colors.pressedColor = originalColors.pressedColor;
		}

		dropdown.colors = colors;
	}

	// Check if any other dropdown is expanded
	private bool FindAnyOtherExpandedDropdown()
	{
		var all = FindObjectsByType<TMP_Dropdown>(FindObjectsSortMode.None);
		foreach (var dd in all)
		{
			if (dd != dropdown && dd.IsExpanded)
				return true;
		}
		return false;
	}
}