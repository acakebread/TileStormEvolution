using System;
using UnityEngine;

namespace MassiveHadronLtd
{
	public class GestureSystem : MonoBehaviour
	{
		public event Action<Vector3> OnBeginDrag;
		public event Action<Vector3> OnDrag;
		public event Action<Vector3> OnEndDrag;

		private void Update()
		{
			// Check if any GUI control is being interacted with (hotControl != 0 means a control is active)
			bool isGuiControlActive = GUIUtility.hotControl != 0;

			if (Input.GetMouseButtonDown(0) && !isGuiControlActive)
			{
				OnBeginDrag?.Invoke(Input.mousePosition);
			}
			else if (Input.GetMouseButton(0) && !isGuiControlActive)
			{
				OnDrag?.Invoke(Input.mousePosition);
			}
			else if (Input.GetMouseButtonUp(0))
			{
				OnEndDrag?.Invoke(Input.mousePosition);
			}
		}
	}
}