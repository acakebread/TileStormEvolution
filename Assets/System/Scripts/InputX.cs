using UnityEngine;

namespace MassiveHadronLtd
{
	public static class InputX
	{
		public static bool mouseInsideWindow => new Rect(0, 0, Screen.width, Screen.height) .Contains(Input.mousePosition);
	}
}