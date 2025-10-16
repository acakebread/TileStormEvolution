using System;
using System.Collections.Generic;
using UnityEngine;

namespace MassiveHadronLtd
{
	[RequireComponent(typeof(Camera))]
	public class CameraController : MonoBehaviour
	{
		private CameraBase cameraSystem;
		private CameraMode currentMode = CameraMode.Absent;
		private Dictionary<CameraMode, CameraState> stateLookup = new();
		private bool hasCustomStates;

		public bool HasCompleted => cameraSystem != null && cameraSystem.HasCompleted;

		private void Awake()
		{
			if (GetComponent<Camera>() == null)
			{
				Debug.LogWarning("CameraController requires a Camera component");
				return;
			}

			var (srcPos, dstPos) = GetInitialCameraPositions();
			var defaultState = new CameraState
			{
				mode = CameraMode.Editor,
				data = new CameraData(GetComponent<Camera>()) { origin = srcPos, target = dstPos },
				origin = () => srcPos,
				target = () => dstPos,
				points = () => Array.Empty<Vector3>()
			};
			stateLookup[CameraMode.Editor] = defaultState;
			defaultState.ApplyToCamera(GetComponent<Camera>());
			SetCameraMode(CameraMode.Editor);
		}

		public virtual void Initialise(CameraMode initialMode = CameraMode.Editor)
		{
			if (GetComponent<Camera>() == null)
			{
				Debug.LogWarning("CameraController requires a Camera component");
				return;
			}

			SetupCameraStates();

			var state = GetStateForMode(initialMode);
			if (state != null)
			{
				state.ApplyToCamera(GetComponent<Camera>());
			}
			else
			{
				Debug.LogWarning($"No state for mode {initialMode}. Using default Editor position.");
				var (srcPos, dstPos) = GetInitialCameraPositions();
				GetComponent<Camera>().transform.position = srcPos;
				GetComponent<Camera>().transform.rotation = Quaternion.LookRotation(dstPos - srcPos, Vector3.up);
			}

			if (initialMode != CameraMode.Editor || hasCustomStates)
			{
				SetCameraMode(initialMode);
			}
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
			{
				stateLookup[mode] = state;
			}
			hasCustomStates = true;
		}

		public CameraState GetStateForMode(CameraMode mode) => stateLookup.TryGetValue(mode, out var state) ? state : null;

		public void SetCameraMode(CameraMode mode, bool background = false)
		{
			var state = GetStateForMode(mode);
			if (state == null)
			{
				Debug.LogWarning($"No state registered for CameraMode {mode}. Falling back to Editor mode.");
				state = GetStateForMode(CameraMode.Editor);
				if (state == null)
				{
					return;
				}
				mode = CameraMode.Editor;
			}
			state.mode = mode;

			if (background && mode != GetStateForMode(currentMode)?.mode) return;

			cameraSystem = mode switch
			{
				CameraMode.Editor => new CameraEditor(state),
				CameraMode.Static => new CameraStatic(state),
				CameraMode.Preset => new CameraPreset(state),
				CameraMode.Follow => new CameraFollow(state),
				CameraMode.Direct => new CameraDirect(state),
				CameraMode.Cinema => UnityEngine.Random.Range(0, 7) switch { 0 or 1 or 2 => new CameraOrbit(state), _ => new CameraPath(state) },
				_ => cameraSystem
			};

			// Manually copy state properties to the new camera system
			if (cameraSystem != null)
			{
				cameraSystem.mode = state.mode;
				cameraSystem.data = state.data;
				cameraSystem.originFn = state.origin;
				cameraSystem.targetFn = state.target;
				cameraSystem.pointsFn = state.points;
			}

			currentMode = mode;
			cameraSystem?.Start();
		}

		private void Update() => cameraSystem?.Update();

		private void OnApplicationFocus(bool hasFocus) => cameraSystem?.OnApplicationFocus(hasFocus);

		protected virtual void SetupCameraStates()
		{
			if (GetComponent<Camera>() == null)
			{
				Debug.LogWarning("Cannot setup camera states: Camera is null");
				return;
			}

			var (srcPos, dstPos) = GetInitialCameraPositions();
			var editorState = new CameraState
			{
				mode = CameraMode.Editor,
				data = new CameraData(GetComponent<Camera>()) { origin = srcPos, target = dstPos },
				origin = () => srcPos,
				target = GetTargetPosition(),
				points = GetFocusPoints()
			};
			RegisterState(editorState, new[] { CameraMode.Editor });
		}

		protected virtual (Vector3 srcPos, Vector3 dstPos) GetInitialCameraPositions()
		{
			return (new Vector3(0f, 14f, -14f), Vector3.zero);
		}

		protected virtual Func<Vector3> GetTargetPosition()
		{
			return () => Vector3.zero;
		}

		protected virtual Func<IReadOnlyList<Vector3>> GetFocusPoints()
		{
			return () => Array.Empty<Vector3>();
		}
	}
}