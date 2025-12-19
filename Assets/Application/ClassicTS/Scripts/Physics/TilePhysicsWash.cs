using UnityEngine;

namespace MassiveHadronLtd
{
	public class TilePhysicsWash : MonoBehaviour
	{
		public struct TilePhysicsWashParameters
		{
			public Vector3 normal;
			public float magnitude;
			public float speed;
		}

		private Vector3 displacement = Vector3.zero;
		private Vector3 normal;
		private float magnitude;
		private float speed;

		private void Update()
		{
			var phase = transform.position.x + transform.position.z;
			var scale = Mathf.Sin(Time.time * speed + phase + Mathf.Sin(Time.time * speed * 0.77f + phase));

			transform.position -= displacement;
			displacement = magnitude * scale * normal - magnitude * normal;
			transform.position += displacement;
		}

		public static TilePhysicsWash AddWash(GameObject gameObject, TilePhysicsWashParameters? parameters = null)
		{
			var tilePhysicsWash = gameObject.AddComponent<TilePhysicsWash>();
			tilePhysicsWash.normal = parameters.HasValue ? parameters.Value.normal : Vector3.up;
			tilePhysicsWash.magnitude = parameters.HasValue ? parameters.Value.magnitude : 0.05f;
			tilePhysicsWash.speed = parameters.HasValue ? parameters.Value.speed : 2f;
			return tilePhysicsWash;
		}
	}
}