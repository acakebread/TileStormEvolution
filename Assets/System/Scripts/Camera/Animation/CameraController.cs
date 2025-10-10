using System;
using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public enum CameraState { Absent, Editor, Static, Preset, Follow, Cinema }

	[RequireComponent(typeof(Camera))]
	public class CameraController : MonoBehaviour
	{
		public Func<Transform> playerTransform;
		public Func<IReadOnlyList<Vector3>> focusPoints;

		public CameraBase CameraSystem { get; set; }
		public CameraState RestoreState { get; set; }
		public CameraState CurrentState { get; set; }

		private CameraData restoreData;

		private static CameraBase CreateCinemaCamera()
		{
			return UnityEngine.Random.Range(0, 7) switch
			{
				0 or 1 or 2 => new CameraPath(),
				3 or 4 or 5 => new CameraOrbit(),
				//6 => new CameraDollyZoom(),
				_ => new CameraOrbit()
			};
		}

		private void Awake()
		{
			restoreData = new CameraData(GetComponent<Camera>());
			Reset();
		}

		public void Reset()
		{
			CameraSystem = null;
			RestoreState = CameraState.Absent;
			CurrentState = CameraState.Absent;

			SetMode(CameraState.Static);
		}

		public void SetOrigin(Vector3 value, bool immediate = false) => CameraSystem?.SetOrigin(value, immediate);
		public void SetTarget(Vector3 value, bool immediate = false) => CameraSystem?.SetTarget(value, immediate);

		public void SetMode(CameraState value)
		{
			if (CameraState.Editor != CurrentState && CameraState.Cinema != CurrentState && null != CameraSystem)
				restoreData = CameraSystem.data;
			var currentData = restoreData;

			CameraSystem = value switch
			{
				CameraState.Editor => new CameraEditor(),
				CameraState.Static => new CameraStatic(),
				CameraState.Preset => new CameraPreset(),
				CameraState.Follow => new CameraFollow(),
				CameraState.Cinema => CreateCinemaCamera(),
				_ => CameraSystem
			};

			CameraSystem.playerTransform += playerTransform;
			CameraSystem.focusPoints += focusPoints;
			CameraSystem.data = currentData;
			CameraSystem.Awake();

			RestoreState = CurrentState;
			CurrentState = value;

			CameraSystem.Start();
		}

		private void Update() => CameraSystem?.Update();
	}
}