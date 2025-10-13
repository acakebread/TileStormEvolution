using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public enum CameraMode { Absent, Editor, Static, Preset, Follow, Cinema }

	[RequireComponent(typeof(Camera))]
	public class CameraController : MonoBehaviour
	{
		private CameraBase cameraSystem = null;
		private CameraMode currentMode = CameraMode.Absent;
		private Dictionary<CameraMode, CameraState> stateLookup = new();

		public bool HasCompleted => cameraSystem != null && cameraSystem.HasCompleted;

		public void RegisterState(CameraState state, CameraMode[] modes)
		{
			foreach (var mode in modes)
				stateLookup[mode] = state;
		}

		public void SetCameraMode(CameraMode mode, bool background = false)
		{
			var state = GetStateForMode(mode);
			if (null == state) return;
			state.cameraMode = mode;
			if (background)
			{
				if (mode != GetStateForMode(currentMode).cameraMode)
					return;// if current state is not the same as 'mode state' then exit
			}

			cameraSystem = mode switch
			{
				CameraMode.Editor => new CameraEditor(),
				CameraMode.Static => new CameraStatic(),
				CameraMode.Preset => new CameraPreset(),
				CameraMode.Follow => new CameraFollow(),
				CameraMode.Cinema => new CameraPath(),
				_ => cameraSystem
			};

			if (null != cameraSystem)
			{
				cameraSystem.data = state.data;
				cameraSystem.originFunc = state.origin;
				cameraSystem.targetFunc = state.target;
				cameraSystem.focusPointsFunc = state.focusPoints;
			}

			currentMode = mode;
			Initialise();
		}

		public CameraState GetStateForMode(CameraMode mode) => stateLookup.TryGetValue(mode, out var state) ? state : null;

		private void Initialise() => cameraSystem?.Start();

		private void Update() => cameraSystem?.Update();
	}
}