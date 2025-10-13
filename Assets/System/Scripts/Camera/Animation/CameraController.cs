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

		private CameraState editorState;
		private CameraState playerState;
		private CameraState cinemaState;

		public void RegisterState(CameraState state)
		{
			switch (state.cameraMode)
			{
				case CameraMode.Editor:
					editorState = state;
					break;

				case CameraMode.Follow:
				case CameraMode.Preset:
					playerState = state;
					break;

				case CameraMode.Cinema:
					cinemaState = state;
					break;
			}
		}

		public void SetCameraMode(CameraMode mode, bool updateMode = true)
		{
			CameraState state = GetStateForMode(mode);
			if (state == null) return;

			var system = mode switch
			{
				CameraMode.Editor => new CameraEditor(),
				CameraMode.Static => new CameraStatic(),
				CameraMode.Preset => new CameraPreset(),
				CameraMode.Follow => new CameraFollow(),
				CameraMode.Cinema => new CameraPath(),
				_ => cameraSystem
			};

			if (system != null)
			{
				system.data = state.data;
				system.originFunc = state.origin;
				system.targetFunc = state.target;
				system.focusPointsFunc = state.focusPoints;
			}

			if (!updateMode) return;

			cameraSystem = system;
			currentMode = mode;

			Initialise();
		}

		public CameraState GetStateForMode(CameraMode mode)
		{
			return mode switch
			{
				CameraMode.Editor => editorState,
				CameraMode.Cinema => cinemaState,
				CameraMode.Preset or CameraMode.Follow => playerState,
				_ => null
			};
		}

		public void Initialise() => cameraSystem?.Start();

		private void Update() => cameraSystem?.Update();
	}
}