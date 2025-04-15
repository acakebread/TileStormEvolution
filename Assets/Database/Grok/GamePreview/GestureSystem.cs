using UnityEngine;
using System;

public class GestureSystem : MonoBehaviour
{
	public enum GestureMode { Inactive, DraggingX, DraggingZ }

	private GestureMode currentMode = GestureMode.Inactive;
	private Vector3 initialMousePos;
	private Vector3 thresholdMousePos;
	private bool isMouseDown;
	private bool isThresholdDetectionActive;
	private bool hasLoggedAtThreshold;
	private const float lockThreshold = 0.1f;

	public event Action<Vector3> OnDragStarted;
	public event Action<Vector3> OnDragEnded;

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

		initialMousePos = ray.GetPoint(distance);
		isMouseDown = true;
		currentMode = GestureMode.Inactive;
		isThresholdDetectionActive = false;
		hasLoggedAtThreshold = false;
		OnDragStarted?.Invoke(initialMousePos);
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

		// Lock initial axis
		if (currentMode == GestureMode.Inactive)
		{
			Vector3 delta = currentPos - initialMousePos;
			float absX = Mathf.Abs(delta.x);
			float absZ = Mathf.Abs(delta.z);
			if (absX > absZ && absX > lockThreshold)
			{
				currentMode = GestureMode.DraggingX;
			}
			else if (absZ > lockThreshold)
			{
				currentMode = GestureMode.DraggingZ;
			}
		}

		// Check for new axis detection at threshold
		if (isThresholdDetectionActive && !hasLoggedAtThreshold)
		{
			Vector3 delta = currentPos - thresholdMousePos;
			float absX = Mathf.Abs(delta.x);
			float absZ = Mathf.Abs(delta.z);
			if (absX > absZ && absX > lockThreshold)
			{
				Debug.Log("Detected axis mode: DraggingX");
				hasLoggedAtThreshold = true;
			}
			else if (absZ > lockThreshold)
			{
				Debug.Log("Detected axis mode: DraggingZ");
				hasLoggedAtThreshold = true;
			}
		}
	}

	private void EndDrag()
	{
		Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
		Plane mapPlane = new Plane(Vector3.up, Vector3.zero);
		float distance;
		Vector3 finalPos = mapPlane.Raycast(ray, out distance) ? ray.GetPoint(distance) : Vector3.zero;

		isMouseDown = false;
		currentMode = GestureMode.Inactive;
		isThresholdDetectionActive = false;
		hasLoggedAtThreshold = false;
		OnDragEnded?.Invoke(finalPos);
	}

	public void SignalDeadZone(Vector3 currentPos)
	{
		thresholdMousePos = currentPos;
		isThresholdDetectionActive = true;
		hasLoggedAtThreshold = false;
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
//	private bool isMouseDown;
//	private const float lockThreshold = 0.1f;

//	public event Action<Vector3> OnDragStarted;
//	public event Action<Vector3> OnDragEnded;

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
//			Debug.LogWarning("GestureSystem: Raycast failed in StartDrag");
//			return;
//		}

//		initialMousePos = ray.GetPoint(distance);
//		isMouseDown = true;
//		currentMode = GestureMode.Inactive;
//		Debug.Log($"GestureSystem: StartDrag at {initialMousePos}");
//		OnDragStarted?.Invoke(initialMousePos);
//	}

//	private void UpdateDrag()
//	{
//		Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
//		Plane mapPlane = new Plane(Vector3.up, Vector3.zero);
//		if (!mapPlane.Raycast(ray, out float distance))
//		{
//			Debug.LogWarning("GestureSystem: Raycast failed in UpdateDrag");
//			return;
//		}

//		Vector3 currentPos = ray.GetPoint(distance);
//		Vector3 delta = currentPos - initialMousePos;

//		if (currentMode == GestureMode.Inactive)
//		{
//			float absX = Mathf.Abs(delta.x);
//			float absZ = Mathf.Abs(delta.z);
//			if (absX > absZ && absX > lockThreshold)
//			{
//				currentMode = GestureMode.DraggingX;
//				Debug.Log($"GestureSystem: Locked DraggingX, delta=({delta.x},{delta.z})");
//			}
//			else if (absZ > lockThreshold)
//			{
//				currentMode = GestureMode.DraggingZ;
//				Debug.Log($"GestureSystem: Locked DraggingZ, delta=({delta.x},{delta.z})");
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
//		Debug.Log($"GestureSystem: EndDrag at {finalPos}");
//		OnDragEnded?.Invoke(finalPos);
//	}

//	public void SignalDeadZone()
//	{
//		if (currentMode == GestureMode.DraggingX || currentMode == GestureMode.DraggingZ)
//		{
//			Debug.Log("GestureSystem: Dead zone signaled");
//			// No mode change, just acknowledge for grid snap
//		}
//	}

//	public GestureMode GetCurrentMode()
//	{
//		return currentMode;
//	}
//}