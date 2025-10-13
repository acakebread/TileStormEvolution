using UnityEngine;

namespace MassiveHadronLtd
{
	public enum CameraMode { Absent, Editor, Static, Preset, Follow, Cinema }

	[RequireComponent(typeof(Camera))]
	public class CameraController : MonoBehaviour
	{
		[HideInInspector] public CameraBase cameraSystem = null;

		public void SetMode(CameraMode value)
		{
			cameraSystem = value switch
			{
				CameraMode.Editor => new CameraEditor(),
				CameraMode.Static => new CameraStatic(),
				CameraMode.Preset => new CameraPreset(),
				CameraMode.Follow => new CameraFollow(),
				//CameraState.Cinema => UnityEngine.Random.Range(0, 7) switch { 0 or 1 or 2 => new CameraOrbit(), _ => new CameraPath() },
				CameraMode.Cinema => new CameraPath(),
				_ => cameraSystem
			};
		}

		public void Initialise() => cameraSystem?.Start();

		private void Update() => cameraSystem?.Update();
	}
}
