using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(TMP_Dropdown))]
public class DropdownAutoScroller : MonoBehaviour
{
	private TMP_Dropdown dropdown;
	private GameObject lastInstance;

	private void Awake()
	{
		dropdown = GetComponent<TMP_Dropdown>();
	}

	private void Update()
	{
		var instance = GetRuntimeDropdownInstance();

		// Dropdown just opened
		if (instance != null && lastInstance == null)
		{
			lastInstance = instance;
			StartCoroutine(ScrollNextFrame(instance));
		}

		// Dropdown just closed
		else if (instance == null && lastInstance != null)
		{
			lastInstance = null;
		}
	}

	private GameObject GetRuntimeDropdownInstance()
	{
		// TMP always parents the runtime clone next to the template
		var parent = dropdown.template.parent;
		if (!parent)
			return null;

		for (int i = 0; i < parent.childCount; i++)
		{
			var child = parent.GetChild(i).gameObject;

			// Ignore the template itself
			if (child == dropdown.template.gameObject)
				continue;

			// The runtime list is active while open
			if (child.activeInHierarchy)
				return child;
		}

		return null;
	}

	private IEnumerator ScrollNextFrame(GameObject instance)
	{
		// Wait for layout
		yield return null;
		yield return new WaitForEndOfFrame();

		if (!instance || !instance.activeInHierarchy)
			yield break;

		var scrollRect = instance.GetComponentInChildren<ScrollRect>(true);
		if (!scrollRect || !scrollRect.content || !scrollRect.viewport)
			yield break;

		int index = dropdown.value;
		if (scrollRect.content.childCount == 0)
			yield break;

		var item = scrollRect.content.GetChild(0) as RectTransform;
		if (!item)
			yield break;

		float itemHeight = item.rect.height;
		float viewportHeight = scrollRect.viewport.rect.height;
		float totalHeight = dropdown.options.Count * itemHeight;

		if (totalHeight <= viewportHeight)
			yield break;

		float itemTop = index * itemHeight;
		float target = itemTop - (viewportHeight * 0.5f) + (itemHeight * 0.5f);
		float normalized = Mathf.Clamp01(target / (totalHeight - viewportHeight));

		// TMP uses inverted vertical scroll
		scrollRect.verticalNormalizedPosition = 1f - normalized;
	}
}
