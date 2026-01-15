using UnityEngine;

namespace MassiveHadronLtd
{
	public class DynamicSceneCameraController : MonoBehaviour
	{
		[SerializeField] private float distance = 5f;
		[SerializeField] private float height = 2.5f;
		[SerializeField] private float tiltAngle = 20f;
		[SerializeField] private float orbitSpeed = 0f;

		private DynamicSceneInstance owner;
		private float currentAngle = 45f;

		public void Initialize(DynamicSceneInstance instance)
		{
			owner = instance;
			UpdateCameraPosition();
		}

		private void Update()
		{
			if (orbitSpeed > 0.01f)
			{
				currentAngle += orbitSpeed * Time.deltaTime;
				UpdateCameraPosition();
			}
		}

		public void Drag(Vector2 delta)
		{
			currentAngle += delta.x * 0.35f;
			height -= delta.y * 0.025f;
			height = Mathf.Clamp(height, 0.5f, 12f);
			UpdateCameraPosition();
		}

		public void Zoom(float scrollDelta)
		{
			distance -= scrollDelta * 0.4f;
			distance = Mathf.Clamp(distance, 1.2f, 25f);
			UpdateCameraPosition();
		}

		private void UpdateCameraPosition()
		{
			if (owner == null) return;

			float x = Mathf.Sin(currentAngle * Mathf.Deg2Rad) * distance;
			float z = Mathf.Cos(currentAngle * Mathf.Deg2Rad) * distance;

			transform.position = new Vector3(x, height, z);
			transform.LookAt(Vector3.up * 1.2f);
			transform.Rotate(Vector3.right, tiltAngle, Space.Self);
		}
	}
}