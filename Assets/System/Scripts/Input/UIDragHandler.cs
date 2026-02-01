using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MassiveHadronLtd
{
	public class UIDragHandler : MonoBehaviour,
		IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
	{
		public event Action<UIDragHandler, PointerEventData> OnPointerDownEvent;
		public event Action<UIDragHandler, PointerEventData> OnBeginDragEvent;
		public event Action<UIDragHandler, PointerEventData> OnDragEvent;
		public event Action<UIDragHandler, PointerEventData> OnEndDragEvent;
		public event Action<UIDragHandler, PointerEventData> OnPointerUpEvent;

		public void OnPointerDown(PointerEventData eventData)
			=> OnPointerDownEvent?.Invoke(this, eventData);

		public void OnBeginDrag(PointerEventData eventData)
			=> OnBeginDragEvent?.Invoke(this, eventData);

		public void OnDrag(PointerEventData eventData)
			=> OnDragEvent?.Invoke(this, eventData);

		public void OnEndDrag(PointerEventData eventData)
			=> OnEndDragEvent?.Invoke(this, eventData);

		public void OnPointerUp(PointerEventData eventData)
			=> OnPointerUpEvent?.Invoke(this, eventData);
	}
}


//using System;
//using UnityEngine;
//using UnityEngine.EventSystems;

//namespace MassiveHadronLtd
//{
//	public class UIDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
//	{
//		public static UIDragHandler instance;

//		public event Action<Vector3> OnDragStart;
//		public event Action<Vector3> OnDragging;
//		public event Action<Vector3> OnDragEnd;

//		public void OnBeginDrag(PointerEventData eventData) => OnDragStart?.Invoke(eventData.position);
//		public void OnDrag(PointerEventData eventData) => OnDragging?.Invoke(eventData.position);
//		public void OnEndDrag(PointerEventData eventData) => OnDragEnd?.Invoke(eventData.position);
//	}
//}