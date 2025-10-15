using System;
using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public abstract class CameraStateController : MonoBehaviour
	{
		protected CameraController cameraController;

		public virtual void Initialise(CameraController camera, CameraMode initialMode = CameraMode.Editor)
		{
			cameraController = camera ?? throw new ArgumentNullException(nameof(camera));
			SetupCameraStates();
			cameraController.Initialise(initialMode);
		}

		public virtual (Vector3 srcPos, Vector3 dstPos) GetInitialCameraPositions()
		{
			return (new Vector3(0f, 14f, -14f), Vector3.zero);
		}

		public virtual Func<Vector3> GetTargetPosition()
		{
			return () => Vector3.zero;
		}

		public virtual Func<IReadOnlyList<Vector3>> GetFocusPoints()
		{
			return () => Array.Empty<Vector3>();
		}

		protected virtual void SetupCameraStates()
		{
			if (cameraController == null || cameraController.GetComponent<Camera>() == null)
			{
				Debug.LogWarning("Cannot setup camera states: CameraController or Camera is null");
				return;
			}

			var (srcPos, dstPos) = GetInitialCameraPositions();
			var editorState = new CameraState
			{
				mode = CameraMode.Editor,
				data = new CameraData(cameraController.GetComponent<Camera>()) { origin = srcPos, target = dstPos },
				origin = () => srcPos,
				target = GetTargetPosition(),
				points = GetFocusPoints()
			};
			cameraController.RegisterState(editorState, new[] { CameraMode.Editor });
		}

		protected virtual void OnDestroy() { }
	}
}