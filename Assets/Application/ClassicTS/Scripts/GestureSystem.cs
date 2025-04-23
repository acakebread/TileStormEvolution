using System;
using UnityEngine;

public class GestureSystem : MonoBehaviour
{
	public static GestureSystem instance;

	public event Action<Vector3> OnBeginDrag;
	public event Action<Vector3> OnDrag;
	public event Action OnEndDrag;

	private void Awake() => instance = this;

	private void Update()
	{
		if (Input.GetMouseButtonDown(0))
			OnBeginDrag?.Invoke(Input.mousePosition);
		else if (Input.GetMouseButton(0))
			OnDrag?.Invoke(Input.mousePosition);
		else if (Input.GetMouseButtonUp(0))
			OnEndDrag?.Invoke();
	}
}