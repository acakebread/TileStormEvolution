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
					// Fixed: Use the new non-obsolete overload (fastest, no sorting needed)
					var found = FindObjectsByType<UIFocusManager>();
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
		private static GameObject lastUserSelected;

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

			if (selected != null && !IsTransient(selected))
			{
				lastUserSelected = selected;
			}

			GameObject candidate = null;

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

				var scroll = FindScrollContaining(selected);
				if (scroll != null)
				{
					candidate = scroll;
					lastUserSelected = scroll;
				}
			}
			else if (lastUserSelected != null)
			{
				candidate = lastUserSelected;
			}

			currentFocus = candidate;
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
			// Fixed: Use new overloads
			var tmps = FindObjectsByType<TMP_Dropdown>();
			foreach (var dd in tmps)
				if (dd.IsExpanded) return dd.gameObject;

			var legacys = FindObjectsByType<Dropdown>();
			foreach (var dd in legacys)
				if (HasDropdownListChild(dd)) return dd.gameObject;

			return null;
		}

		private static bool AnyInputIsFocused()
		{
			return FindFirstFocusedInput() != null;
		}

		private static GameObject FindFirstFocusedInput()
		{
			// Fixed: Use new overloads
			var tmps = FindObjectsByType<TMP_InputField>();
			foreach (var input in tmps)
				if (input.isFocused) return input.gameObject;

			var legacys = FindObjectsByType<InputField>();
			foreach (var input in legacys)
				if (input.isFocused) return input.gameObject;

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

		public static bool AnyUIHasKeyboardFocus()
		{
			if (currentFocus == null) return false;

			return currentFocus.GetComponent<TMP_Dropdown>() != null ||
				   currentFocus.GetComponent<Dropdown>() != null ||
				   currentFocus.GetComponent<TMP_InputField>()?.isFocused == true ||
				   currentFocus.GetComponent<InputField>()?.isFocused == true ||
				   currentFocus.GetComponentInParent<ScrollRect>() != null;
		}
	}
}