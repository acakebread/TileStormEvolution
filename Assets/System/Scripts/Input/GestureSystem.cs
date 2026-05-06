using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MassiveHadronLtd
{
	public class GestureSystem : MonoBehaviour
	{
		public event Action<Vector3> OnBeginDrag;
		public event Action<Vector3> OnDrag;
		public event Action<Vector3> OnEndDrag;

		private void Update()
		{
			if (EventSystem.current && EventSystem.current.IsPointerOverGameObject())
				return;
			if (InputX.GetMouseButtonDown(0))
				OnBeginDrag?.Invoke(InputX.mousePosition);
			else if (InputX.GetMouseButton(0))
				OnDrag?.Invoke(InputX.mousePosition);
			else if (InputX.GetMouseButtonUp(0))
				OnEndDrag?.Invoke(InputX.mousePosition);
		}
	}
}