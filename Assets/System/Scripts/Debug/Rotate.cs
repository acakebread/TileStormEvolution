using UnityEngine;

namespace MassiveHadronLtd
{
	public class Rotate : MonoBehaviour
	{
		public float rotX = 0;
		public float rotY = 0;
		public float rotZ = 0;

		void Update() => transform.Rotate(Time.deltaTime * rotX, Time.deltaTime * rotY, rotZ);
	}
}