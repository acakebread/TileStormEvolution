using System;
using UnityEngine;

public class GestureSystem : MonoBehaviour
{
	public static GestureSystem instance;

	public event Action<Vector3> OnDragStart;
	public event Action<Vector3> OnDragging;
	public event Action OnDragEnd;

	private bool isMouseDown = false;

	private void Awake() => instance = this;

	private void Update()
	{
		if (Input.GetMouseButtonDown(0) && !isMouseDown)
		{
			isMouseDown = true;
			OnDragStart?.Invoke(Input.mousePosition);
		}
		else if (Input.GetMouseButton(0) && isMouseDown)
		{
			OnDragging?.Invoke(Input.mousePosition);
		}
		else if (Input.GetMouseButtonUp(0) && isMouseDown)
		{
			isMouseDown = false;
			OnDragEnd?.Invoke();
		}
	}
}