using System;
using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	[RequireComponent(typeof(Camera))]
	public class CameraController : MonoBehaviour
	{
		private CameraBase cameraSystem;
		private CameraMode currentMode = CameraMode.Absent;
		private Dictionary<CameraMode, CameraState> stateLookup = new();
		private Func<IReadOnlyList<Vector3>> getFocusPoints;
		private Func<Vector3> getTargetPosition;

		public bool HasCompleted => cameraSystem != null && cameraSystem.HasCompleted;

		public void Initialise(
			Vector3 initialSrcPos,
			Vector3 initialDstPos,
			Func<Vector3> targetPositionGetter,
			Func<IReadOnlyList<Vector3>> focusPointsGetter,
			CameraMode initialMode = CameraMode.Follow)
		{
			getTargetPosition = targetPositionGetter ?? throw new ArgumentNullException(nameof(targetPositionGetter));
			getFocusPoints = focusPointsGetter ?? throw new ArgumentNullException(nameof(focusPointsGetter));
			SetupCameraStates(initialSrcPos, initialDstPos);
			SetCameraMode(initialMode);
		}

		public void UpdateCameraForPreset(CameraMode mode, Vector3 origin, Vector3 target)
		{
			var state = GetStateForMode(mode);
			if (state == null) return;

			state.origin = () => origin;
			state.target = () => target;
			state.mode = mode;
			SetCameraMode(mode, true);
		}

		public void RegisterState(CameraState state, CameraMode[] modes)
		{
			if (state == null || state.data == null)
			{
				Debug.LogError("Cannot register null CameraState or CameraData");
				return;
			}
			foreach (var mode in modes)
				stateLookup[mode] = state;
		}

		public CameraState GetStateForMode(CameraMode mode) => stateLookup.TryGetValue(mode, out var state) ? state : null;

		public void SetCameraMode(CameraMode mode, bool background = false)
		{
			var state = GetStateForMode(mode);
			if (state == null) return;
			state.mode = mode;

			if (background && mode != GetStateForMode(currentMode)?.mode) return;

			cameraSystem = mode switch
			{
				CameraMode.Editor => new CameraEditor(state),
				CameraMode.Static => new CameraStatic(state),
				CameraMode.Preset => new CameraPreset(state),
				CameraMode.Follow => new CameraFollow(state),
				CameraMode.Cinema => UnityEngine.Random.Range(0, 7) switch { 0 or 1 or 2 => new CameraOrbit(state), _ => new CameraPath(state) },
				_ => cameraSystem
			};

			currentMode = mode;
			cameraSystem?.Start();
		}

		private void Update() => cameraSystem?.Update();

		private void OnApplicationFocus(bool hasFocus) => cameraSystem?.OnApplicationFocus(hasFocus);

		private void SetupCameraStates(Vector3 srcPos, Vector3 dstPos)
		{
			var editorState = new CameraState
			{
				mode = CameraMode.Editor,
				data = new CameraData(GetComponent<Camera>()) { origin = srcPos, target = dstPos }
			};

			var playerState = new CameraState
			{
				mode = CameraMode.Follow,
				data = new CameraData(GetComponent<Camera>()) { origin = srcPos, target = dstPos },
				target = getTargetPosition,
				origin = () => srcPos
			};

			var cinemaState = new CameraState
			{
				mode = CameraMode.Cinema,
				data = new CameraData(GetComponent<Camera>()) { origin = srcPos, target = dstPos },
				target = getTargetPosition,
				points = getFocusPoints
			};

			RegisterState(editorState, new[] { CameraMode.Editor });
			RegisterState(playerState, new[] { CameraMode.Follow, CameraMode.Preset });
			RegisterState(cinemaState, new[] { CameraMode.Cinema });
		}
	}
}