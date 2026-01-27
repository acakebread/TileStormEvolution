using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(TMP_Dropdown))]
public class TMPDropdownSelectedGraphic : MonoBehaviour
{
	private TMP_Dropdown dropdown;
	private Graphic targetGraphic;

	// Original colours (captured once)
	private ColorBlock originalColors;

	private bool isCurrentlyActive;

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

		// Capture original
		originalColors = dropdown.colors;
	}

	private void Update()
	{
		var es = EventSystem.current;
		if (es != null && null != es.currentSelectedGameObject && es.currentSelectedGameObject != gameObject)
			isCurrentlyActive = false;

		if (dropdown.IsExpanded)
			isCurrentlyActive = true;

		var colors = dropdown.colors;
		if (isCurrentlyActive)
		{
			// Override normal to use selected colour while "sticky"
			colors.normalColor = originalColors.selectedColor;
			// Optional: adjust highlighted/pressed to match selected family
			// colors.highlightedColor = originalColors.selectedColor * 1.1f;
			// colors.pressedColor    = originalColors.selectedColor * 0.85f;
		}
		else
		{
			// Restore originals
			colors.normalColor = originalColors.normalColor;
			colors.highlightedColor = originalColors.highlightedColor;
			colors.pressedColor = originalColors.pressedColor;
		}

		dropdown.colors = colors; // Apply back → triggers Unity transition
	}
}