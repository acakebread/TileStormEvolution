using UnityEngine;
using System;
using System.Collections.Generic;

public class GestureSystem : MonoBehaviour
{
	public enum GestureMode { Inactive, DraggingX, DraggingZ }

	private GestureMode currentMode = GestureMode.Inactive;
	private Vector3 startMousePos;
	private Vector3 remainderPos;
	private bool isMouseDown;
	private List<(GestureMode mode, int direction)> gestureList = new List<(GestureMode, int)>();
	private const float lockThreshold = 0.1f;
	private const float gridSize = 1.0f;

	public event Action<Vector3> OnDragStarted;
	public event Action<Vector3> OnDragEnded;
	public event Action<List<(GestureMode mode, int direction)>> OnGesturesUpdated;

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
		gestureList.Clear();
		OnDragStarted?.Invoke(startMousePos);
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
		gestureList.Clear();

		for (int i = 0; i < 10; i++)
		{
			Vector3 delta = currentPos - tempStartMousePos;
			float absX = Mathf.Abs(delta.x);
			float absZ = Mathf.Abs(delta.z);

			if (absX > absZ && absX >= gridSize)
			{
				int direction = delta.x > 0 ? 1 : -1;
				gestureList.Add((GestureMode.DraggingX, direction));
				tempStartMousePos.x += direction * gridSize;
			}
			else if (absZ >= gridSize)
			{
				int direction = delta.z > 0 ? 1 : -1;
				gestureList.Add((GestureMode.DraggingZ, direction));
				tempStartMousePos.z += direction * gridSize;
			}
			else
			{
				break;
			}
		}

		if (gestureList.Count > 0)
		{
			OnGesturesUpdated?.Invoke(gestureList);
		}
		remainderPos = currentPos;
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
		Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
		Plane mapPlane = new Plane(Vector3.up, Vector3.zero);
		Vector3 finalPos = mapPlane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : Vector3.zero;

		isMouseDown = false;
		currentMode = GestureMode.Inactive;
		gestureList.Clear();
		OnDragEnded?.Invoke(finalPos);
	}

	public void ConsumeGesture(GestureMode mode, int direction)
	{
		if (mode == GestureMode.DraggingX)
		{
			startMousePos.x += direction * gridSize;
		}
		else if (mode == GestureMode.DraggingZ)
		{
			startMousePos.z += direction * gridSize;
		}
	}

	public void ClearGestures() => gestureList.Clear();
	public GestureMode GetCurrentMode() => currentMode;
	public Vector3 GetCurrentPos() => remainderPos;
}
