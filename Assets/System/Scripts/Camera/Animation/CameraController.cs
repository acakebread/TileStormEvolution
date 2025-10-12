using UnityEngine;

namespace MassiveHadronLtd
{
	public enum CameraState { Absent, Editor, Static, Preset, Follow, Cinema }
	[RequireComponent(typeof(Camera))]
	public class CameraController : MonoBehaviour
	{
		[HideInInspector] public CameraBase cameraSystem = null;

		public void SetMode(CameraState value)
		{
			cameraSystem = value switch
			{
				CameraState.Editor => new CameraEditor(),
				CameraState.Static => new CameraStatic(),
				CameraState.Preset => new CameraPreset(),
				CameraState.Follow => new CameraFollow(),
				//CameraState.Cinema => UnityEngine.Random.Range(0, 7) switch { 0 or 1 or 2 => new CameraOrbit(), _ => new CameraPath() },
				CameraState.Cinema => new CameraPath(),
				_ => cameraSystem
			};
		}

		public void Initialise() => cameraSystem?.Start();

		private void Update() => cameraSystem?.Update();
	}
}
