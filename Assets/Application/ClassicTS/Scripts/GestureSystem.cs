using UnityEngine;
using System;

public class GestureSystem : MonoBehaviour
{
	public static GestureSystem instance;
	private Vector3 startMousePos;
	private bool isMouseDown;

	public event Action<Vector3> OnDragStart;
	public event Action<Vector3> OnDragging;
	public event Action OnDragEnd;

	private void Awake() => instance = this;

	private void Update()
	{
		if (Input.GetMouseButtonDown(0) && !isMouseDown)
		{
			StartDrag();
		}
		else if (Input.GetMouseButton(0) && isMouseDown)
		{
			UpdateDrag();
		}
		else if (Input.GetMouseButtonUp(0) && isMouseDown)
		{
			EndDrag();
		}
	}

	private void StartDrag()
	{
		startMousePos = Input.mousePosition;
		isMouseDown = true;
		OnDragStart?.Invoke(startMousePos);
	}

	private void UpdateDrag()
	{
		OnDragging?.Invoke(Input.mousePosition);
	}

	private void EndDrag()
	{
		isMouseDown = false;
		OnDragEnd?.Invoke();
	}
}