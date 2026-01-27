using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;
using TMPro;

namespace MassiveHadronLtd.UI
{
	public static class UIFocusUtility
	{
		public static bool InFocus(this GameObject go)
		{
			if (go == null || !go.activeInHierarchy) return false;

			var es = EventSystem.current;
			if (es == null) return false;

			var current = es.currentSelectedGameObject;

			// Direct selection or hierarchy match
			if (current != null && IsRelated(go, current))
				return true;

			//// Hover / pointer over
			//if (IsPointerOver(go))
			//	return true;

			// Dropdown open state (both legacy and TMP)
			var tmpDd = go.GetComponentInChildren<TMP_Dropdown>(true);
			if (tmpDd != null && tmpDd.IsExpanded)
				return true;

			var legacyDd = go.GetComponentInChildren<Dropdown>(true);
			if (legacyDd != null && HasDropdownListChild(legacyDd))
				return true;

			// Optional: input field focused (common blocker for arrows)
			if (go.GetComponentInChildren<TMP_InputField>(true)?.isFocused == true ||
				go.GetComponentInChildren<InputField>(true)?.isFocused == true)
				return true;

			return false;
		}

		private static bool IsRelated(GameObject subject, GameObject target)
		{
			if (subject == target) return true;
			if (target.transform.IsChildOf(subject.transform)) return true;

			Transform t = target.transform;
			while (t != null)
			{
				if (t.gameObject == subject) return true;
				t = t.parent;
			}
			return false;
		}

		private static bool IsPointerOver(GameObject go)
		{
			var ped = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
			var results = new List<RaycastResult>();
			EventSystem.current.RaycastAll(ped, results);
			return results.Any(r => IsRelated(go, r.gameObject));
		}

		// Fallback for legacy Dropdown: check if the runtime "Dropdown List" child exists and is active
		private static bool HasDropdownListChild(Dropdown dd)
		{
			if (dd == null) return false;
			// The runtime list is usually instantiated as a child named "Dropdown List"
			var list = dd.transform.Find("Dropdown List");
			return list != null && list.gameObject.activeInHierarchy;
		}
	}
}

//using UnityEngine;
//using UnityEngine.EventSystems;
//using TMPro;
//using System.Collections.Generic;
//using System.Linq;
//using UnityEngine.UI;

//namespace MassiveHadronLtd.UI
//{
//	public static class UIFocusUtility
//	{
//		/// <summary>
//		/// Extension method: Checks if the given GameObject (or its hierarchy) is currently "in focus" 
//		/// for keyboard/input purposes. Returns true if it or a child/related UI element should handle 
//		/// arrow keys, space, enter, etc.
//		/// </summary>
//		public static bool InFocus(this GameObject go)
//		{
//			if (go == null || !go.activeInHierarchy) return false;

//			var es = EventSystem.current;
//			if (es == null) return false;

//			// Core cases where Unity considers something focused
//			var current = es.currentSelectedGameObject;
//			if (current != null && IsInHierarchyOrRelated(go, current))
//				return true;

//			// Fallbacks for compound controls like Dropdown / ScrollView / InputField
//			// 1. Is it (or a descendant) hovered?
//			if (IsPointerOver(go))
//				return true;

//			// 2. Special case: TMP_Dropdown expanded (list open, Blocker active, etc.)
//			var dropdown = go.GetComponentInChildren<TMP_Dropdown>(true);
//			if (dropdown != null && dropdown.IsExpanded)
//				return true;

//			// 3. Input fields / other keyboard-capturing elements
//			var inputField = go.GetComponentInChildren<TMP_InputField>(true);
//			if (inputField != null && inputField.isFocused)
//				return true;

//			// Legacy variants if needed
//			var legacyInput = go.GetComponentInChildren<InputField>(true);
//			if (legacyInput != null && legacyInput.isFocused)
//				return true;

//			var legacyDropdown = go.GetComponentInChildren<Dropdown>(true);
//			if (legacyDropdown != null && legacyDropdown.IsExpanded)
//				return true;

//			// 4. Optional: Check if any DropdownKeyboardNavigator in hierarchy claims focus
//			//    (useful if you want to keep custom state but centralize query)
//			var nav = go.GetComponentInChildren<DropdownKeyboardNavigator>(true);
//			if (nav != null && nav.IsFocusedOrNavigating)
//				return true;

//			// 5. If the current selected is "Blocker" (dropdown overlay), check if our dropdown is open
//			if (current != null && current.name.Contains("Blocker"))
//			{
//				var blockerParent = current.transform.parent;
//				if (blockerParent != null && IsInHierarchyOrRelated(go, blockerParent.gameObject))
//					return true;
//			}

//			return false;
//		}

//		/// <summary>
//		/// Checks if target is the subject or in its hierarchy (child/parent).
//		/// </summary>
//		private static bool IsInHierarchyOrRelated(GameObject subject, GameObject target)
//		{
//			if (subject == target) return true;

//			// Child?
//			if (target.transform.IsChildOf(subject.transform)) return true;

//			// Parent chain?
//			Transform current = target.transform;
//			while (current != null)
//			{
//				if (current.gameObject == subject) return true;
//				current = current.parent;
//			}

//			return false;
//		}

//		/// <summary>
//		/// Rough check if pointer is over this GameObject (requires GraphicRaycaster + EventSystem).
//		/// Not 100% accurate in all layouts but good enough for most cases.
//		/// </summary>
//		private static bool IsPointerOver(GameObject go)
//		{
//			var pointerData = new PointerEventData(EventSystem.current)
//			{
//				position = Input.mousePosition
//			};

//			var results = new List<RaycastResult>();
//			EventSystem.current.RaycastAll(pointerData, results);

//			return results.Any(r => IsInHierarchyOrRelated(go, r.gameObject));
//		}
//	}
//}