using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MassiveHadronLtd
{
	public class ButtonStateFixer : MonoBehaviour
	{
		private PointerEventData lastPointerData;

		private void Update()
		{
			if (Input.GetMouseButtonUp(0) && lastPointerData != null)
			{
				// On any mouse up, force reset hovered/pressed states
				var current = EventSystem.current;
				if (current.currentSelectedGameObject != null)
				{
					current.SetSelectedGameObject(null);
				}

				// Optional: force exit on all current hovered
				var results = new List<RaycastResult>();
				PointerEventData ped = new PointerEventData(current);
				ped.position = Input.mousePosition;
				EventSystem.current.RaycastAll(ped, results);

				foreach (var result in results)
				{
					var selectable = result.gameObject.GetComponent<Selectable>();
					if (selectable != null)
					{
						selectable.OnPointerExit(ped);
					}
				}

				lastPointerData = null;
			}

			if (Input.GetMouseButtonDown(0))
			{
				lastPointerData = new PointerEventData(EventSystem.current)
				{
					position = Input.mousePosition
				};
			}
		}
	}
}