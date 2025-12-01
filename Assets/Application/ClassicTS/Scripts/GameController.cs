using UnityEngine;

namespace ClassicTilestorm
{
	public class GameController : MonoBehaviour
	{
		public void Awake()
		{
			gameObject.AddComponent<PlaceholderUI>();
		}

		public void Initialise()
		{
			//set default system
			if (!TryGetComponent<MainCameraController>(out var controller)) return;
			controller.SetCameraSystem(CameraModeRegistry.Follow, true);//for player mode
			controller.SetCameraSystem(CameraModeRegistry.Path, true);//for cinema mode
		}

		void OnEnable() 
		{
			if (!TryGetComponent<MainCameraController>(out var controller)) return;
			controller.UpdateGestureControllerState();
		}

		void OnDisable() { }
	}
}