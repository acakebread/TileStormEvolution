using UnityEngine;

namespace MassiveHadronLtd
{
	[RequireComponent(typeof(Camera))]
	public class CameraPropertySync : MonoBehaviour
	{
		[SerializeField] private Camera referenceCamera; // The camera to copy properties from (e.g., Main Camera)

		private Camera targetCamera; // The camera on this GameObject (e.g., SkyboxCamera)

		void Awake()
		{
			// Get the Camera component on this GameObject
			targetCamera = GetComponent<Camera>();
		}

		void LateUpdate()
		{
			if (referenceCamera == null || targetCamera == null)
			{
				Debug.LogWarning("Reference camera or target camera is not assigned.", this);
				return;
			}

			// Sync transform (position and rotation)
			transform.position = referenceCamera.transform.position;
			transform.rotation = referenceCamera.transform.rotation;

			// Sync camera properties
			targetCamera.fieldOfView = referenceCamera.fieldOfView;
			targetCamera.nearClipPlane = referenceCamera.nearClipPlane;
			targetCamera.farClipPlane = referenceCamera.farClipPlane;
			targetCamera.projectionMatrix = referenceCamera.projectionMatrix; // Syncs projection type (Perspective/Orthographic) and FOV
			targetCamera.orthographic = referenceCamera.orthographic;
			targetCamera.orthographicSize = referenceCamera.orthographicSize; // For orthographic cameras
			targetCamera.aspect = referenceCamera.aspect;

			// Optional: Sync additional properties if needed
			// targetCamera.rect = referenceCamera.rect; // Viewport rect
			// targetCamera.sensorSize = referenceCamera.sensorSize; // For physical camera properties
			// targetCamera.lensShift = referenceCamera.lensShift; // For physical camera properties
		}
	}
}