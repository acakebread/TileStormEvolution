using UnityEngine;
using System;
using System.Collections.Generic;

public class GestureSystem : MonoBehaviour
{
	public enum GestureMode { Inactive, DraggingX, DraggingZ }

	private GestureMode currentMode = GestureMode.Inactive;
	private Vector3 startMousePos;
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
		int maxIterations = 10;
		Vector3 tempStartMousePos = startMousePos; // Track pending gestures without modifying startMousePos
		gestureList.Clear(); // Clear previous gestures

		int iteration = 0;
		while (iteration < maxIterations)
		{
			Vector3 delta = currentPos - tempStartMousePos;
			float absX = Mathf.Abs(delta.x);
			float absZ = Mathf.Abs(delta.z);

			if (absX > absZ && absX >= gridSize)
			{
				int direction = delta.x > 0 ? 1 : -1;
				gestureList.Add((GestureMode.DraggingX, direction));
				tempStartMousePos.x += direction * gridSize; // Reduce delta for next iteration
															 //Debug.Log($"Detected axis mode: DraggingX (direction: {direction})");
			}
			else if (absZ >= gridSize)
			{
				int direction = delta.z > 0 ? 1 : -1;
				gestureList.Add((GestureMode.DraggingZ, direction));
				tempStartMousePos.z += direction * gridSize; // Reduce delta for next iteration
															 //Debug.Log($"Detected axis mode: DraggingZ (direction: {direction})");
			}
			else
			{
				break;
			}

			iteration++;
		}

		if (gestureList.Count > 0)
		{
			OnGesturesUpdated?.Invoke(gestureList);
		}

		currentMode = EvaluateMode(currentPos - startMousePos);
	}

	private GestureMode EvaluateMode(Vector3 delta)
	{
		var mode = GestureMode.Inactive;
		float absX = Mathf.Abs(delta.x);
		float absZ = Mathf.Abs(delta.z);
		if (absX > absZ && absX > lockThreshold)
		{
			mode = GestureMode.DraggingX;
		}
		else if (absZ > lockThreshold)
		{
			mode = GestureMode.DraggingZ;
		}
		return mode;
	}

	private void EndDrag()
	{
		Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
		Plane mapPlane = new Plane(Vector3.up, Vector3.zero);
		float distance;
		Vector3 finalPos = mapPlane.Raycast(ray, out distance) ? ray.GetPoint(distance) : Vector3.zero;

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

	public void ClearGestures()
	{
		gestureList.Clear();
	}

	public GestureMode GetCurrentMode()
	{
		return currentMode;
	}
}





//using UnityEngine;
//using System;

//public class GestureSystem : MonoBehaviour
//{
//	public enum GestureMode { Inactive, DraggingX, DraggingZ }

//	private GestureMode currentMode = GestureMode.Inactive;
//	private Vector3 initialMousePos;
//	private Vector3 thresholdMousePos;
//	private bool isMouseDown;
//	private bool isThresholdDetectionActive;
//	private bool hasLoggedAtThreshold;
//	private const float lockThreshold = 0.1f;

//	public event Action<Vector3> OnDragStarted;
//	public event Action<Vector3> OnDragEnded;
//	public event Action<GestureMode> OnModeChanged;

//	private void Update()
//	{
//		if (Input.GetMouseButtonDown(0) && currentMode == GestureMode.Inactive)
//		{
//			StartDrag();
//		}
//		else if (Input.GetMouseButton(0) && isMouseDown)
//		{
//			UpdateDrag();
//		}
//		else if (Input.GetMouseButtonUp(0) && isMouseDown)
//		{
//			EndDrag();
//		}
//	}

//	private void StartDrag()
//	{
//		Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
//		Plane mapPlane = new Plane(Vector3.up, Vector3.zero);
//		if (!mapPlane.Raycast(ray, out float distance))
//		{
//			return;
//		}

//		initialMousePos = ray.GetPoint(distance);
//		isMouseDown = true;
//		currentMode = GestureMode.Inactive;
//		isThresholdDetectionActive = false;
//		hasLoggedAtThreshold = false;
//		OnDragStarted?.Invoke(initialMousePos);
//	}

//	private void UpdateDrag()
//	{
//		Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
//		Plane mapPlane = new Plane(Vector3.up, Vector3.zero);
//		if (!mapPlane.Raycast(ray, out float distance))
//		{
//			return;
//		}

//		Vector3 currentPos = ray.GetPoint(distance);

//		// Lock initial axis
//		if (currentMode == GestureMode.Inactive)
//		{
//			Vector3 delta = currentPos - initialMousePos;
//			float absX = Mathf.Abs(delta.x);
//			float absZ = Mathf.Abs(delta.z);
//			if (absX > absZ && absX > lockThreshold)
//			{
//				currentMode = GestureMode.DraggingX;
//			}
//			else if (absZ > lockThreshold)
//			{
//				currentMode = GestureMode.DraggingZ;
//			}
//		}

//		// Check for new axis detection at threshold
//		if (isThresholdDetectionActive && !hasLoggedAtThreshold)
//		{
//			Vector3 delta = currentPos - thresholdMousePos;
//			float absX = Mathf.Abs(delta.x);
//			float absZ = Mathf.Abs(delta.z);
//			if (absX > absZ && absX > lockThreshold)
//			{
//				Debug.Log("Detected axis mode: DraggingX");
//				hasLoggedAtThreshold = true;
//				if (currentMode != GestureMode.DraggingX)
//				{
//					currentMode = GestureMode.DraggingX;
//					OnModeChanged?.Invoke(currentMode);
//				}
//			}
//			else if (absZ > lockThreshold)
//			{
//				Debug.Log("Detected axis mode: DraggingZ");
//				hasLoggedAtThreshold = true;
//				if (currentMode != GestureMode.DraggingZ)
//				{
//					currentMode = GestureMode.DraggingZ;
//					OnModeChanged?.Invoke(currentMode);
//				}
//			}
//		}
//	}

//	private void EndDrag()
//	{
//		Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
//		Plane mapPlane = new Plane(Vector3.up, Vector3.zero);
//		float distance;
//		Vector3 finalPos = mapPlane.Raycast(ray, out distance) ? ray.GetPoint(distance) : Vector3.zero;

//		isMouseDown = false;
//		currentMode = GestureMode.Inactive;
//		isThresholdDetectionActive = false;
//		hasLoggedAtThreshold = false;
//		OnDragEnded?.Invoke(finalPos);
//	}

//	public void SignalDeadZone(Vector3 currentPos)
//	{
//		thresholdMousePos = currentPos;
//		isThresholdDetectionActive = true;
//		hasLoggedAtThreshold = false;
//	}

//	public GestureMode GetCurrentMode()
//	{
//		return currentMode;
//	}
//}