using UnityEngine;
using UnityEngine.EventSystems;
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

			var selected = es.currentSelectedGameObject;

			// Dropdown or input style
			if (HasDropdownOrInput(go))
			{
				// Selected directly or descendant
				if (selected != null && IsRelated(go, selected))
					return true;

				// Dropdown expanded
				var tmp = go.GetComponentInChildren<TMP_Dropdown>(true);
				if (tmp != null && tmp.IsExpanded) return true;

				var legacy = go.GetComponentInChildren<Dropdown>(true);
				if (legacy != null && HasDropdownListChild(legacy)) return true;

				// Input typing active
				if (go.GetComponentInChildren<TMP_InputField>(true)?.isFocused == true ||
					go.GetComponentInChildren<InputField>(true)?.isFocused == true)
					return true;

				return false;
			}

			// Scroll view style
			if (go.GetComponent<ScrollRect>() != null)
			{
				// Selection inside this scroll view
				if (selected != null && IsRelated(go, selected))
					return true;

				// No selection at all → scroll view can take it (unless blocked below)
				if (selected == null)
					return true;

				// Block if anything else is active
				if (AnyOtherDropdownOrInputActive(selected))
					return false;

				return true;
			}

			// Default fallback
			return selected == go;
		}

		private static bool HasDropdownOrInput(GameObject go)
		{
			return go.GetComponentInChildren<TMP_Dropdown>(true) != null ||
				   go.GetComponentInChildren<Dropdown>(true) != null ||
				   go.GetComponentInChildren<TMP_InputField>(true) != null ||
				   go.GetComponentInChildren<InputField>(true) != null;
		}

		private static bool AnyOtherDropdownOrInputActive(GameObject selectedGo)
		{
			if (selectedGo == null) return false;

			if (selectedGo.GetComponent<TMP_Dropdown>() != null ||
				selectedGo.GetComponent<Dropdown>() != null ||
				selectedGo.GetComponent<TMP_InputField>()?.isFocused == true ||
				selectedGo.GetComponent<InputField>()?.isFocused == true)
				return true;

			return HasDropdownOrInput(selectedGo);
		}

		private static bool IsRelated(GameObject subject, GameObject target)
		{
			if (subject == target) return true;
			if (target == null) return false;

			Transform t = target.transform;
			while (t != null)
			{
				if (t.gameObject == subject) return true;
				t = t.parent;
			}
			return false;
		}

		private static bool HasDropdownListChild(Dropdown dd)
		{
			if (dd == null) return false;
			var list = dd.transform.Find("Dropdown List");
			return list != null && list.gameObject.activeInHierarchy;
		}
	}
}

//using UnityEngine;
//using UnityEngine.EventSystems;
//using UnityEngine.UI;
//using TMPro;

//namespace MassiveHadronLtd.UI
//{
//	public static class UIFocusUtility
//	{
//		public static bool InFocus(this GameObject go)
//		{
//			if (go == null || !go.activeInHierarchy) return false;

//			var es = EventSystem.current;
//			if (es == null) return false;

//			var currentSelected = es.currentSelectedGameObject;

//			// ── Dropdown / Input style objects ──
//			if (HasDropdownOrInput(go))
//			{
//				// 1. This object is directly selected (tabbed to or clicked)
//				if (currentSelected == go)
//					return true;

//				// 2. A child/descendant is selected (common when dropdown list is open)
//				if (currentSelected != null && IsRelated(go, currentSelected))
//					return true;

//				// 3. Dropdown list is expanded (reliable signal when open)
//				var tmpDd = go.GetComponentInChildren<TMP_Dropdown>(true);
//				if (tmpDd != null && tmpDd.IsExpanded)
//					return true;

//				var legacyDd = go.GetComponentInChildren<Dropdown>(true);
//				if (legacyDd != null && HasDropdownListChild(legacyDd))
//					return true;

//				// 4. Input field is typing-focused
//				if (go.GetComponentInChildren<TMP_InputField>(true)?.isFocused == true ||
//					go.GetComponentInChildren<InputField>(true)?.isFocused == true)
//					return true;

//				return false;
//			}

//			// ── Scroll view / container ──
//			if (go.GetComponent<ScrollRect>() != null)
//			{
//				// If nothing is selected globally, let scroll view have priority
//				if (currentSelected == null)
//					return true;

//				// Current selection is inside this scroll view
//				if (currentSelected != null && IsRelated(go, currentSelected))
//					return true;

//				// Block if any dropdown or input is active elsewhere
//				if (AnyDropdownOrInputIsActive())
//					return false;

//				// Default: scroll view can take arrows if no obvious conflict
//				return true;
//			}

//			// Fallback for other objects
//			return currentSelected == go;
//		}

//		private static bool HasDropdownOrInput(GameObject go)
//		{
//			return go.GetComponentInChildren<TMP_Dropdown>(true) != null ||
//				   go.GetComponentInChildren<Dropdown>(true) != null ||
//				   go.GetComponentInChildren<TMP_InputField>(true) != null ||
//				   go.GetComponentInChildren<InputField>(true) != null;
//		}

//		private static bool AnyDropdownOrInputIsActive()
//		{
//			var es = EventSystem.current;
//			if (es == null) return false;

//			var selected = es.currentSelectedGameObject;
//			if (selected == null) return false;

//			// Quick checks on current selection
//			if (selected.GetComponent<TMP_Dropdown>() != null ||
//				selected.GetComponent<Dropdown>() != null ||
//				selected.GetComponent<TMP_InputField>()?.isFocused == true ||
//				selected.GetComponent<InputField>()?.isFocused == true)
//				return true;

//			// Broader hierarchy check
//			return HasDropdownOrInput(selected);
//		}

//		private static bool IsRelated(GameObject subject, GameObject target)
//		{
//			if (subject == target) return true;
//			if (target == null) return false;

//			Transform t = target.transform;
//			while (t != null)
//			{
//				if (t.gameObject == subject) return true;
//				t = t.parent;
//			}
//			return false;
//		}

//		private static bool HasDropdownListChild(Dropdown dd)
//		{
//			if (dd == null) return false;
//			var list = dd.transform.Find("Dropdown List");
//			return list != null && list.gameObject.activeInHierarchy;
//		}
//	}
//}

////using UnityEngine;
////using UnityEngine.EventSystems;
////using UnityEngine.UI;
////using TMPro;
////using System.Collections.Generic;
////using System.Linq;

////namespace MassiveHadronLtd.UI
////{
////	public static class UIFocusUtility
////	{
////		public static bool InFocus(this GameObject go)
////		{
////			if (go == null || !go.activeInHierarchy) return false;

////			var es = EventSystem.current;
////			if (es == null) return false;

////			var currentSelected = es.currentSelectedGameObject;

////			// ──────────────────────────────────────────────────────────────
////			// Case 1: Dropdown or input field style objects
////			// ──────────────────────────────────────────────────────────────
////			if (HasDropdownOrInput(go))
////			{
////				// Direct selection
////				if (currentSelected == go) return true;

////				// Hierarchy match (selected child of this dropdown)
////				if (currentSelected != null && IsRelated(go, currentSelected)) return true;

////				// Dropdown expanded
////				var tmpDd = go.GetComponentInChildren<TMP_Dropdown>(true);
////				if (tmpDd != null && tmpDd.IsExpanded) return true;

////				var legacyDd = go.GetComponentInChildren<Dropdown>(true);
////				if (legacyDd != null && HasDropdownListChild(legacyDd)) return true;

////				// Input field focused
////				if (go.GetComponentInChildren<TMP_InputField>(true)?.isFocused == true ||
////					go.GetComponentInChildren<InputField>(true)?.isFocused == true)
////					return true;

////				return false;
////			}

////			// ──────────────────────────────────────────────────────────────
////			// Case 2: Scroll view / container style objects
////			// ──────────────────────────────────────────────────────────────
////			if (go.GetComponent<ScrollRect>() != null)
////			{
////				// No global selection at all → assume scroll view can take it if mouse was over it before or something
////				if (currentSelected == null)
////				{
////					// Optional: only allow if last known selection was inside
////					// for now we allow to make it work
////					return true;
////				}

////				// Current selection is inside this scroll view's content hierarchy?
////				if (currentSelected != null && IsRelated(go, currentSelected))
////					return true;

////				// No dropdown or input field anywhere is active
////				if (AnyDropdownOrInputIsActive())
////					return false;

////				// Fallback: if no other obvious focus, let scroll view have it
////				return true;
////			}

////			// Other UI objects — default to direct selection
////			return currentSelected == go;
////		}

////		// ──────────────────────────────────────────────────────────────
////		// Helpers
////		// ──────────────────────────────────────────────────────────────

////		private static bool HasDropdownOrInput(GameObject go)
////		{
////			return go.GetComponentInChildren<TMP_Dropdown>(true) != null ||
////				   go.GetComponentInChildren<Dropdown>(true) != null ||
////				   go.GetComponentInChildren<TMP_InputField>(true) != null ||
////				   go.GetComponentInChildren<InputField>(true) != null;
////		}

////		private static bool AnyDropdownOrInputIsActive()
////		{
////			var es = EventSystem.current;
////			if (es == null) return false;

////			var selected = es.currentSelectedGameObject;
////			if (selected == null) return false;

////			// Selected something that is dropdown or input
////			if (selected.GetComponent<TMP_Dropdown>() != null ||
////				selected.GetComponent<Dropdown>() != null ||
////				selected.GetComponent<TMP_InputField>()?.isFocused == true ||
////				selected.GetComponent<InputField>()?.isFocused == true)
////				return true;

////			// Or hierarchy check
////			if (HasDropdownOrInput(selected))
////				return true;

////			return false;
////		}

////		private static bool IsRelated(GameObject subject, GameObject target)
////		{
////			if (subject == target) return true;
////			if (target == null) return false;

////			Transform t = target.transform;
////			while (t != null)
////			{
////				if (t.gameObject == subject) return true;
////				t = t.parent;
////			}
////			return false;
////		}

////		private static bool HasDropdownListChild(Dropdown dd)
////		{
////			if (dd == null) return false;
////			var list = dd.transform.Find("Dropdown List");
////			return list != null && list.gameObject.activeInHierarchy;
////		}
////	}
////}

////using UnityEngine;
////using UnityEngine.EventSystems;
////using System.Collections.Generic;
////using System.Linq;
////using UnityEngine.UI;
////using TMPro;

////namespace MassiveHadronLtd.UI
////{
////	public static class UIFocusUtility
////	{
////		public static bool InFocus(this GameObject go)
////		{
////			if (go == null || !go.activeInHierarchy) return false;

////			var es = EventSystem.current;
////			if (es == null) return false;

////			var current = es.currentSelectedGameObject;

////			// Direct selection or hierarchy match
////			if (current != null && IsRelated(go, current))
////				return true;

////			//// Hover / pointer over
////			//if (IsPointerOver(go))
////			//	return true;

////			// Dropdown open state (both legacy and TMP)
////			var tmpDd = go.GetComponentInChildren<TMP_Dropdown>(true);
////			if (tmpDd != null && tmpDd.IsExpanded)
////				return true;

////			var legacyDd = go.GetComponentInChildren<Dropdown>(true);
////			if (legacyDd != null && HasDropdownListChild(legacyDd))
////				return true;

////			// Optional: input field focused (common blocker for arrows)
////			if (go.GetComponentInChildren<TMP_InputField>(true)?.isFocused == true ||
////				go.GetComponentInChildren<InputField>(true)?.isFocused == true)
////				return true;

////			return false;
////		}

////		private static bool IsRelated(GameObject subject, GameObject target)
////		{
////			if (subject == target) return true;
////			if (target.transform.IsChildOf(subject.transform)) return true;

////			Transform t = target.transform;
////			while (t != null)
////			{
////				if (t.gameObject == subject) return true;
////				t = t.parent;
////			}
////			return false;
////		}

////		private static bool IsPointerOver(GameObject go)
////		{
////			var ped = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
////			var results = new List<RaycastResult>();
////			EventSystem.current.RaycastAll(ped, results);
////			return results.Any(r => IsRelated(go, r.gameObject));
////		}

////		// Fallback for legacy Dropdown: check if the runtime "Dropdown List" child exists and is active
////		private static bool HasDropdownListChild(Dropdown dd)
////		{
////			if (dd == null) return false;
////			// The runtime list is usually instantiated as a child named "Dropdown List"
////			var list = dd.transform.Find("Dropdown List");
////			return list != null && list.gameObject.activeInHierarchy;
////		}
////	}
////}
