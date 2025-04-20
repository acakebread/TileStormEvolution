using UnityEngine;
using System;
using System.Collections.Generic;

public class GestureSystem : MonoBehaviour
{
	public static GestureSystem instance;
	public enum GestureMode { Inactive, DraggingX, DraggingZ }
	public bool isDragging => currentMode == GestureMode.DraggingX || currentMode == GestureMode.DraggingZ;

	private GestureMode currentMode = GestureMode.Inactive;
	private Vector3 startMousePos;
	private bool isMouseDown;
	private const float lockThreshold = 0.1f;
	private const float gridSize = 1.0f;

	public event Action<Vector3> OnDragStart;
	public event Action<List<Vector3>> OnDragging;
	public event Action OnDragEnd;

	private void Awake() => instance = this;

	private void Update()
	{
		if (Input.GetMouseButtonDown(0) && currentMode == GestureMode.Inactive)
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

	public void ConsumeGesture(Vector3 gesture) => startMousePos += gesture;

	private void StartDrag()
	{
		Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
		Plane mapPlane = new Plane(Vector3.up, Vector3.zero);
		if (!mapPlane.Raycast(ray, out float distance))
		{
			return;
		}

		startMousePos = ray.GetPoint(distance);
		isMouseDown = true;

		currentMode = GestureMode.Inactive;
		OnDragStart?.Invoke(startMousePos);
	}

	private void UpdateDrag()
	{
		Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
		Plane mapPlane = new Plane(Vector3.up, Vector3.zero);
		if (!mapPlane.Raycast(ray, out float distance))
		{
			return;
		}

		Vector3 currentPos = ray.GetPoint(distance);
		Vector3 tempStartMousePos = startMousePos;

		var gestureList = new List<Vector3>();

		for (int i = 0; i < 10; i++)
		{
			Vector3 delta = currentPos - tempStartMousePos;
			float absX = Mathf.Abs(delta.x);
			float absZ = Mathf.Abs(delta.z);

			if (absX > absZ && absX >= gridSize)
			{
				int direction = delta.x > 0 ? 1 : -1;
				gestureList.Add(new Vector3(direction, 0, 0));
				tempStartMousePos.x += direction * gridSize;
			}
			else if (absZ >= gridSize)
			{
				int direction = delta.z > 0 ? 1 : -1;
				gestureList.Add(new Vector3(0, 0, direction));
				tempStartMousePos.z += direction * gridSize;
			}
			else
			{
				gestureList.Add(delta);
				break;
			}
		}

		if (gestureList.Count > 0)
		{
			OnDragging?.Invoke(gestureList);
		}
		currentMode = EvaluateMode(currentPos - startMousePos);
	}

	private GestureMode EvaluateMode(Vector3 delta)
	{
		float absX = Mathf.Abs(delta.x);
		float absZ = Mathf.Abs(delta.z);
		if (absX > absZ && absX > lockThreshold)
		{
			return GestureMode.DraggingX;
		}
		if (absZ > lockThreshold)
		{
			return GestureMode.DraggingZ;
		}
		return GestureMode.Inactive;
	}

	private void EndDrag()
	{
		isMouseDown = false;
		OnDragEnd?.Invoke();
		currentMode = GestureMode.Inactive;
	}
}