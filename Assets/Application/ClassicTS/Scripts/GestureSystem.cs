using System;
using UnityEngine;

public class GestureSystem : MonoBehaviour
{
	public static GestureSystem instance;

	public event Action<Vector3> OnDragStart;
	public event Action<Vector3> OnDragging;
	public event Action OnDragEnd;

	private bool isMouseDown;

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
		isMouseDown = true;
		OnDragStart?.Invoke(Input.mousePosition);
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