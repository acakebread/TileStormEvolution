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
		private bool hasCustomStates;

		public bool HasCompleted => cameraSystem != null && cameraSystem.HasCompleted;

		private void Awake()
		{
			if (GetComponent<Camera>() == null)
			{
				Debug.LogWarning("CameraController requires a Camera component");
				return;
			}

			// Register default Editor state
			var defaultState = new CameraState
			{
				mode = CameraMode.Editor,
				data = new CameraData(GetComponent<Camera>())
				{
					origin = new Vector3(0f, 14f, -14f),
					target = Vector3.zero
				},
				origin = () => new Vector3(0f, 14f, -14f),
				target = () => Vector3.zero,
				points = () => Array.Empty<Vector3>()
			};
			stateLookup[CameraMode.Editor] = defaultState;
			Debug.Log("Registered default CameraState for Editor mode in Awake");

			// Apply default state to camera - default camera position and orientation for first initialisation
			defaultState.ApplyToCamera(GetComponent<Camera>());
			SetCameraMode(CameraMode.Editor);
		}

		public void Initialise(CameraMode initialMode = CameraMode.Editor)
		{
			if (GetComponent<Camera>() == null)
			{
				Debug.LogWarning("CameraController requires a Camera component");
				return;
			}

			// Apply state for initial mode - default camera position and orientation for first initialisation
			var state = GetStateForMode(initialMode);
			if (state != null)
			{
				state.ApplyToCamera(GetComponent<Camera>());
			}
			else
			{
				Debug.LogWarning($"No state for mode {initialMode}. Using default Editor position.");
				var defaultPos = new Vector3(0f, 14f, -14f);
				GetComponent<Camera>().transform.position = defaultPos;
				GetComponent<Camera>().transform.rotation = Quaternion.LookRotation(Vector3.zero - defaultPos, Vector3.up);
			}

			// Skip SetCameraMode if using default Editor mode and no custom states
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
				CameraMode.Cinema => UnityEngine.Random.Range(0, 7) switch { 0 or 1 or 2 => new CameraOrbit(state), _ => new CameraPath(state) },
				_ => cameraSystem
			};

			currentMode = mode;
			cameraSystem?.Start();
		}

		private void Update() => cameraSystem?.Update();

		private void OnApplicationFocus(bool hasFocus) => cameraSystem?.OnApplicationFocus(hasFocus);
	}
}