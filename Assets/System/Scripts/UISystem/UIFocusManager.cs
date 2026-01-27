using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace MassiveHadronLtd.UI
{
	public class UIFocusManager : MonoBehaviour
	{
		private static UIFocusManager _instance;

		public static UIFocusManager Instance
		{
			get
			{
				if (_instance == null)
				{
					var found = FindObjectsByType<UIFocusManager>(FindObjectsSortMode.None);
					_instance = found.Length > 0 ? found[0] : null;

					if (_instance == null)
					{
						var go = new GameObject("[UIFocusManager]");
						go.hideFlags = HideFlags.HideAndDontSave;
						_instance = go.AddComponent<UIFocusManager>();
						DontDestroyOnLoad(go);
						Debug.Log("[UIFocusManager] Auto-created persistent instance.");
					}
				}
				return _instance;
			}
		}

		private static GameObject currentFocus;
		private static GameObject lastUserSelected;          // ← new: remembers last meaningful selection

		private void LateUpdate()
		{
			UpdateGlobalFocus();
		}

		private void OnDestroy()
		{
			if (_instance == this) _instance = null;
		}

		private static void UpdateGlobalFocus()
		{
			var es = EventSystem.current;
			if (es == null)
			{
				currentFocus = null;
				return;
			}

			var selected = es.currentSelectedGameObject;

			// If we have a real selection → remember it (unless it's a blocker/transient)
			if (selected != null && !IsTransient(selected))
			{
				lastUserSelected = selected;
			}

			GameObject candidate = null;

			// Priority order
			if (AnyDropdownIsExpanded())
			{
				candidate = FindFirstExpandedDropdown();
				lastUserSelected = candidate;
			}
			else if (AnyInputIsFocused())
			{
				candidate = FindFirstFocusedInput();
			}
			else if (selected != null)
			{
				candidate = selected;

				// If selection is inside a scroll view → promote the scroll view itself
				var scroll = FindScrollContaining(selected);
				if (scroll != null)
				{
					candidate = scroll;
					lastUserSelected = scroll;  // remember scroll view too
				}
			}
			// When nothing is selected → keep last meaningful thing
			else if (lastUserSelected != null)
			{
				candidate = lastUserSelected;

				// If last was a dropdown button → keep it after close
				var tmpDd = candidate.GetComponent<TMP_Dropdown>();
				var legacyDd = candidate.GetComponent<Dropdown>();

				var dd = tmpDd != null ? tmpDd : legacyDd as Component; // or just use tmpDd / legacyDd separately
				if (dd != null)
				{
					// Good – keep dropdown button focused after close
				}
				// If last was inside scroll → keep scroll
				else if (FindScrollContaining(candidate) != null)
				{
					candidate = FindScrollContaining(candidate);
				}
			}

			currentFocus = candidate;

			// Optional debug – comment out later
			// Debug.Log($"Focus → { (currentFocus ? currentFocus.name : "null") } | Selected: { (selected ? selected.name : "null") } | LastUser: { (lastUserSelected ? lastUserSelected.name : "null") }");
		}

		public static bool IsInFocus(GameObject go)
		{
			var _ = Instance;  // force creation

			if (currentFocus == null) return false;
			if (go == null) return false;

			return go == currentFocus || currentFocus.transform.IsChildOf(go.transform);
		}

		// ──────────────────────────────────────────────────────────────
		// Helpers
		// ──────────────────────────────────────────────────────────────

		private static bool IsTransient(GameObject go)
		{
			return go != null && go.name.Contains("Blocker");
		}

		private static bool AnyDropdownIsExpanded()
		{
			return FindFirstExpandedDropdown() != null;
		}

		private static GameObject FindFirstExpandedDropdown()
		{
			var tmps = FindObjectsByType<TMP_Dropdown>(FindObjectsSortMode.None);
			foreach (var dd in tmps) if (dd.IsExpanded) return dd.gameObject;

			var legacys = FindObjectsByType<Dropdown>(FindObjectsSortMode.None);
			foreach (var dd in legacys) if (HasDropdownListChild(dd)) return dd.gameObject;

			return null;
		}

		private static bool AnyInputIsFocused()
		{
			return FindFirstFocusedInput() != null;
		}

		private static GameObject FindFirstFocusedInput()
		{
			var tmps = FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None);
			foreach (var input in tmps) if (input.isFocused) return input.gameObject;

			var legacys = FindObjectsByType<InputField>(FindObjectsSortMode.None);
			foreach (var input in legacys) if (input.isFocused) return input.gameObject;

			return null;
		}

		private static bool HasDropdownListChild(Dropdown dd)
		{
			var list = dd?.transform.Find("Dropdown List");
			return list != null && list.gameObject.activeInHierarchy;
		}

		private static GameObject FindScrollContaining(GameObject obj)
		{
			if (obj == null) return null;

			Transform t = obj.transform;
			while (t != null)
			{
				var scroll = t.GetComponent<ScrollRect>();
				if (scroll != null && scroll.gameObject.activeInHierarchy)
					return scroll.gameObject;
				t = t.parent;
			}
			return null;
		}
	}
}