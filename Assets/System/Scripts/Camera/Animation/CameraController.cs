using UnityEngine;

namespace MassiveHadronLtd
{
	public enum CameraMode { Absent, Editor, Static, Preset, Follow, Cinema }

	[RequireComponent(typeof(Camera))]
	public class CameraController : MonoBehaviour
	{
		[HideInInspector] public CameraBase cameraSystem = null;
		private CameraMode currentMode = CameraMode.Absent;

		public CameraMode CurrentMode => currentMode;

		public void SetCameraMode(CameraMode mode, CameraState state = null)
		{
			currentMode = mode;
			SetMode(mode);

			if (state != null)
			{
				cameraSystem.data = state.data;
				cameraSystem.originFunc = state.origin;
				cameraSystem.targetFunc = state.target;
				cameraSystem.focusPointsFunc = state.focusPoints;
			}

			Initialise();
		}

		public void SetMode(CameraMode value)
		{
			cameraSystem = value switch
			{
				CameraMode.Editor => new CameraEditor(),
				CameraMode.Static => new CameraStatic(),
				CameraMode.Preset => new CameraPreset(),
				CameraMode.Follow => new CameraFollow(),
				CameraMode.Cinema => new CameraPath(),
				_ => cameraSystem
			};
		}

		public void Initialise() => cameraSystem?.Start();

		private void Update() => cameraSystem?.Update();
	}
}