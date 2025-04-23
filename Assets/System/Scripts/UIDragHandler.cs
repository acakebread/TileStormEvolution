using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
	public static UIDragHandler instance;

	public event Action<Vector3> OnDragStart;
	public event Action<Vector3> OnDragging;
	public event Action OnDragEnd;

	public void OnBeginDrag(PointerEventData eventData) => OnDragStart?.Invoke(eventData.position);
	public void OnDrag(PointerEventData eventData) => OnDragging?.Invoke(eventData.position);
	public void OnEndDrag(PointerEventData eventData) => OnDragEnd?.Invoke();
}