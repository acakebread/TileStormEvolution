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

		public CameraState GetStateForMode(CameraMode mode) => stateLookup.TryGetValue(mode, out var state) ? state : null;

		public void SetCameraMode(CameraMode mode, bool background = false)
		{
			var state = GetStateForMode(mode);
			if (null == state) return;
			state.cameraMode = mode;

			if (background && mode != GetStateForMode(currentMode).cameraMode)
				return;

			cameraSystem = mode switch
			{
				CameraMode.Editor => new CameraEditor(state),
				CameraMode.Static => new CameraStatic(state),
				CameraMode.Preset => new CameraPreset(state),
				CameraMode.Follow => new CameraFollow(state),
				CameraMode.Cinema => Random.Range(0, 7) switch { 0 or 1 or 2 => new CameraOrbit(state), _ => new CameraPath(state) },
				_ => cameraSystem
			};

			currentMode = mode;
			cameraSystem?.Start();
		}

		private void Update() => cameraSystem?.Update();
	}
}