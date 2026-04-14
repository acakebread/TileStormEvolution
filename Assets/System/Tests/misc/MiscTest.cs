using UnityEngine;

namespace Miscellaneous
{
	public class MiscTest : MonoBehaviour
	{
		private void OnGUI()
		{
			GUI.depth = -1000;//doesn't work
			GUI.Button(new Rect(500, 0, 200, 100), "hello");//obstructed by unity stats panel when active!!! why!!
		}
	}
}