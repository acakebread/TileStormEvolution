using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MassiveHadronLtd
{
	public class SelectionDebugger : MonoBehaviour
	{
		void Update()
		{
			var es = EventSystem.current;
			if (es == null) return;

			var sel = es.currentSelectedGameObject;
			if (sel != null)
			{
				string name = sel.name;
				if (sel.GetComponent<TMP_InputField>() != null) name += " (TMP_InputField)";
				else if (sel.GetComponent<TMP_Dropdown>() != null) name += " (TMP_Dropdown)";
				else if (sel.GetComponent<Selectable>() != null) name += " (Selectable)";

				Debug.Log($"Current selection: {name} | Mouse over UI: {es.IsPointerOverGameObject()}");
			}
		}

		// Optional: log when selection changes
		private GameObject lastSel;
		void LateUpdate()
		{
			var es = EventSystem.current;
			if (es == null) return;

			if (es.currentSelectedGameObject != lastSel)
			{
				lastSel = es.currentSelectedGameObject;
				Debug.Log($"Selection CHANGED to: {(lastSel != null ? lastSel.name : "null")}");
			}
		}
	}
}