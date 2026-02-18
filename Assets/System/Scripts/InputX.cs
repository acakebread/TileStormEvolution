using UnityEngine;

namespace MassiveHadronLtd
{
	public static class InputX
	{
		public static Vector3 mousePosition => Input.mousePosition;
		public static bool GetMouseButtonDown(int button) => Input.GetMouseButtonDown(button);
		public static bool GetMouseButton(int button) => Input.GetMouseButton(button);
		public static bool GetMouseButtonUp(int button) => Input.GetMouseButtonUp(button);

		public static bool mouseInsideWindow => new Rect(0, 0, Screen.width, Screen.height).Contains(mousePosition);

		public static int touchCount => Input.touchCount;
		public static Touch[] touches => Input.touches;
		public static float GetAxis(string axisName) => Input.GetAxis(axisName);

		public static bool GetKeyDown(KeyCode key) => Input.GetKeyDown(key);
		public static bool GetKey(KeyCode key) => Input.GetKey(key);
		public static bool GetKeyUp(KeyCode key) => Input.GetKeyUp(key);
	}
}