using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

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

			GameObject candidate = es.currentSelectedGameObject;

			// If we have a direct selection → that's usually the winner
			if (candidate != null)
			{
				// But if it's a dropdown blocker or something unrelated, prefer expanded dropdown
				if (IsBlocker(candidate))
				{
					candidate = FindFirstExpandedDropdown() ?? candidate;
				}
			}
			else
			{
				// No selection → look for expanded dropdown / focused input / or fallback to a scroll view
				candidate = FindFirstExpandedDropdown() ??
							FindFirstFocusedInput() ??
							FindAnyActiveScrollView();  // ← NEW: fallback when nothing else is selected
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

		private static bool IsBlocker(GameObject go)
		{
			return go != null && go.name.Contains("Blocker");
		}

		private static GameObject FindFirstExpandedDropdown()
		{
			var tmps = FindObjectsByType<TMP_Dropdown>(FindObjectsSortMode.None);
			foreach (var dd in tmps) if (dd.IsExpanded) return dd.gameObject;

			var legacys = FindObjectsByType<Dropdown>(FindObjectsSortMode.None);
			foreach (var dd in legacys) if (HasDropdownListChild(dd)) return dd.gameObject;

			return null;
		}

		private static bool AnyDropdownIsExpanded()
		{
			return FindFirstExpandedDropdown() != null;
		}

		private static GameObject FindFirstFocusedInput()
		{
			var tmps = FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None);
			foreach (var input in tmps) if (input.isFocused) return input.gameObject;

			var legacys = FindObjectsByType<InputField>(FindObjectsSortMode.None);
			foreach (var input in legacys) if (input.isFocused) return input.gameObject;

			return null;
		}

		private static bool AnyInputIsFocused()
		{
			return FindFirstFocusedInput() != null;
		}

		private static bool HasDropdownListChild(Dropdown dd)
		{
			var list = dd?.transform.Find("Dropdown List");
			return list != null && list.gameObject.activeInHierarchy;
		}

		// NEW: Find a scroll view that could be considered active when nothing else is
		private static GameObject FindAnyActiveScrollView()
		{
			var scrolls = FindObjectsByType<ScrollRect>(FindObjectsSortMode.None);
			foreach (var scroll in scrolls)
			{
				if (scroll.gameObject.activeInHierarchy)
				{
					// Prefer the one with mouse over it, or the first visible one
					if (IsPointerOver(scroll.gameObject))
						return scroll.gameObject;

					// Or just return the first one if no hover check wanted
					return scroll.gameObject;
				}
			}
			return null;
		}

		// Optional pointer check (can remove if you hate hover entirely)
		private static bool IsPointerOver(GameObject go)
		{
			var ped = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
			var results = new List<RaycastResult>();
			EventSystem.current.RaycastAll(ped, results);
			return results.Any(r => r.gameObject == go || r.gameObject.transform.IsChildOf(go.transform));
		}
	}
}