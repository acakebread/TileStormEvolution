using System;
using UnityEngine;

public class GestureSystem : MonoBehaviour
{
	public static GestureSystem instance;

	public event Action<Vector3> OnDragStart;
	public event Action<Vector3> OnDragging;
	public event Action OnDragEnd;

	private void Awake() => instance = this;

	private void Update()
	{
		if (Input.GetMouseButtonDown(0))
			OnDragStart?.Invoke(Input.mousePosition);
		else if (Input.GetMouseButton(0))
			OnDragging?.Invoke(Input.mousePosition);
		else if (Input.GetMouseButtonUp(0))
			OnDragEnd?.Invoke();
	}
}