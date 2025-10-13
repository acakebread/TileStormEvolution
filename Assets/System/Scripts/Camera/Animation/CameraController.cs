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

		public CameraMode CurrentMode => currentMode;

		// Public property to expose HasCompleted from cameraSystem
		public bool HasCompleted => cameraSystem != null && cameraSystem.HasCompleted;

		public void RegisterState(CameraState state, CameraMode[] modes)
		{
			foreach (var mode in modes)
				stateLookup[mode] = state;
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

		public CameraState GetStateForMode(CameraMode mode) => stateLookup.TryGetValue(mode, out var state) ? state : null;

		public void Initialise() => cameraSystem?.Start();

		private void Update() => cameraSystem?.Update();
	}
}