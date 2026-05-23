using UnityEngine;

namespace Miscellaneous
{
	public class RotateCamera : MonoBehaviour
	{
		private readonly float MouseRotateSpeed = 80f;

		// Update is called once per frame
		void Update()
		{
			float y = Input.GetAxis("Mouse X") * MouseRotateSpeed * Time.deltaTime;
			float x = Input.GetAxis("Mouse Y") * MouseRotateSpeed * Time.deltaTime;
			transform.Rotate(-x, y, 0);

			Vector3 Angles = Camera.main.transform.eulerAngles;
			transform.eulerAngles = new Vector3(Angles.x, Angles.y, 0);
		}
	}
}