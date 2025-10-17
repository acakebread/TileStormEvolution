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
		private Dictionary<CameraState, CameraMode> stateMode = new();

		public bool HasCompleted => cameraSystem != null && cameraSystem.HasCompleted;

		private void Awake()
		{
			if (GetComponent<Camera>() == null)
			{
				Debug.LogWarning("CameraController requires a Camera component");
				return;
			}

			// Initialize with default Editor state to ensure camera is positioned correctly on Awake
			var (srcPos, dstPos) = GetInitialCameraPositions();
			var defaultState = new CameraState
			{
				//mode = CameraMode.Editor,
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

			// Setup camera states before applying the initial mode
			SetupCameraStates();

			// Apply state for initial mode
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

			// Skip SetCameraMode if using default Editor mode and no custom states
			if (initialMode != CameraMode.Editor || hasCustomStates)
			{
				SetCameraMode(initialMode);
			}
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
			if (modes.Length > 0) stateMode[state] = modes[0];

			hasCustomStates = true;
		}

		public CameraState GetStateForMode(CameraMode mode) => stateLookup.TryGetValue(mode, out var state) ? state : null;

		public CameraMode GetModeForState(CameraState state) => stateMode.TryGetValue(state, out var mode) ? mode : CameraMode.Absent;

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
			//state.mode = mode;
			stateMode[state] = mode;

			//var match = mode == GetStateForMode(currentMode)?.mode;
			//var test = false;
			var new_match = false;

			if (null!=GetStateForMode(currentMode))
			{
				//test = true;
				var cstate = GetStateForMode(currentMode);
				stateMode.TryGetValue(GetStateForMode(currentMode), out var tmode);
				new_match = tmode == mode;

				Debug.Log(cstate + " " + tmode + " " + mode);
			}

			//if (background && false == match && false == test)
			//{
			//	Debug.LogError("failed a test");
			//}

			//if (background && false == match && false != new_match)
			//{
			//	Debug.LogError("failed match");
			//}

			if (background && false == new_match) return;

			//if (background && mode != GetStateForMode(currentMode)?.mode) return;

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
				//mode = CameraMode.Editor,
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