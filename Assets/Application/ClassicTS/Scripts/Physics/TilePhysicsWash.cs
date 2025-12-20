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
			var phase = transform.position.x * 1.618f + transform.position.z * 0.618f;  // Keeps no diagonal stripes

			var inner = Mathf.Sin(Time.time * speed * 0.77f + phase);
			var angle = Time.time * speed + phase + 0.5f * inner;  // Your approved wobble strength
			var scale = Mathf.Sin(angle);

			// Remap scale from [-1, 1] → [-magnitude, 0]
			var offsetAmount = -magnitude * (scale * 0.5f + 0.5f);  // Clean, predictable, hits exactly 0 and -magnitude

			transform.position -= displacement;
			displacement = offsetAmount * normal;
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