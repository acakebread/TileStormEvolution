using System;
using UnityEngine;

namespace MassiveHadronLtd
{
	public enum CameraState { Absent, Editor, Static, Preset, Follow, Cinema }
	[RequireComponent(typeof(Camera))]
	public class CameraController : MonoBehaviour
	{
		public Func<CameraDelegates> delegates;

		public CameraBase CameraSystem { get; set; }
		public CameraState RestoreState { get; set; }
		public CameraState CurrentState { get; set; }

		private CameraData restoreData;

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

			CameraSystem = value switch
			{
				CameraState.Editor => new CameraEditor(),
				CameraState.Static => new CameraStatic(),
				CameraState.Preset => new CameraPreset(),
				CameraState.Follow => new CameraFollow(),
				//CameraState.Cinema => UnityEngine.Random.Range(0, 7) switch { 0 or 1 or 2 => new CameraOrbit(), _ => new CameraPath() },
				CameraState.Cinema => new CameraPath(),
				_ => CameraSystem
			};

			CameraSystem.delegates = delegates;
			CameraSystem.data = restoreData;
			CameraSystem.Awake();

			if (value != CurrentState) RestoreState = CurrentState;
			CurrentState = value;
		}

		private void Update() => CameraSystem?.Update();
	}
}



//using System;
//using UnityEngine;
//using System.Collections.Generic;

//namespace MassiveHadronLtd
//{
//	public enum CameraState { Absent, Editor, Static, Preset, Follow, Cinema }

//	[RequireComponent(typeof(Camera))]
//	public class CameraController : MonoBehaviour
//	{
//		public Func<Transform> playerTransform;
//		public Func<IReadOnlyList<Vector3>> focusPoints;//I want to replace these with the callbacks
//		public Func<CameraCallbacks> callbacks;//I want to replace these with the callbacks

//		public CameraBase CameraSystem { get; set; }
//		public CameraState RestoreState { get; set; }
//		public CameraState CurrentState { get; set; }

//		private CameraData restoreData;

//		private void Awake()
//		{
//			restoreData = new CameraData(GetComponent<Camera>());
//			Reset();
//		}

//		public void Reset()
//		{
//			CameraSystem = null;
//			RestoreState = CameraState.Absent;
//			CurrentState = CameraState.Absent;
//			SetMode(CameraState.Static);
//		}

//		public void SetOrigin(Vector3 value, bool immediate = false) => CameraSystem?.SetOrigin(value, immediate);
//		public void SetTarget(Vector3 value, bool immediate = false) => CameraSystem?.SetTarget(value, immediate);

//		public void SetMode(CameraState value)
//		{
//			if (CameraState.Editor != CurrentState && CameraState.Cinema != CurrentState && null != CameraSystem)
//				restoreData = CameraSystem.data;

//			CameraSystem = value switch
//			{
//				CameraState.Editor => new CameraEditor(),
//				CameraState.Static => new CameraStatic(),
//				CameraState.Preset => new CameraPreset(),
//				CameraState.Follow => new CameraFollow(),
//				CameraState.Cinema => UnityEngine.Random.Range(0, 7) switch { 0 or 1 or 2 => new CameraOrbit(), _ => new CameraPath() },
//				_ => CameraSystem
//			};

//			CameraSystem.callbacks += callbacks;
//			CameraSystem.playerTransform += playerTransform;//I want to replace these with the callbacks
//			CameraSystem.focusPoints += focusPoints;//I want to replace these with the callbacks
//			CameraSystem.data = restoreData;
//			CameraSystem.Awake();

//			if (value != CurrentState) RestoreState = CurrentState;
//			CurrentState = value;
//		}

//		private void Update() => CameraSystem?.Update();
//	}
//}