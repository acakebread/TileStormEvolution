using UnityEngine;
using MassiveHadronLtd;
using UnityEngine.Rendering;

namespace ClassicTilestorm
{
	public class EditorController : MonoBehaviour
	{
		public IMapEdit iMap;

		private EditorControllerModify modifier;

		private bool gridEnabled = true;
		private bool postProcessingEnabled = false;

		public bool GridEnabled { get => gridEnabled; set => OnGridLinesToggled(value); }
		public bool PostProcessingEnabled { get => postProcessingEnabled; set => OnPostProcessingToggled(value); }

		private Volume getVolume(GameObject root) => root.GetComponentInChildren<Volume>(true);

		private MainCameraController mainCameraController { get { TryGetComponent<MainCameraController>(out var controller); return controller; } }
		private GameCameraEditor gameCameraEditor => null != mainCameraController && mainCameraController.activeSystem is GameCameraEditor editorCam ? editorCam : null;

		private System.Action _unsubscribeMapAction;

		private void Awake() => modifier = new EditorControllerModify(this);

		public void Initialise(IMapEdit iMap)
		{
			this.iMap = iMap;
			iMap.OnMapEdited += OnMapEdited;// Subscribe to map changes
			_unsubscribeMapAction = () => iMap.OnMapEdited -= OnMapEdited;
			if (!isActiveAndEnabled) return;
			UpdateGridLines(gridEnabled);
			modifier?.OnMapLoaded();
			EnableEggbot(false);
		}

		public void Reset()
		{
			_unsubscribeMapAction?.Invoke();
			_unsubscribeMapAction = null;
		}

		private void OnEnable()
		{
			if (TryGetComponent<MainCameraController>(out var controller))
			{
				controller.SetCameraSystem(CameraModeRegistry.Editor, false);
				controller.UpdateGestureControllerState();
			}
			UpdateGridLines(gridEnabled);
			UpdatePostProcessing(postProcessingEnabled);
			modifier?.OnEnable();
			EnableEggbot(false);
		}

		private void OnDisable()
		{
			modifier?.OnDisable();
			GridLinesUtil.Hide();
			EnableEggbot(true);
		}

		private void Update() 
		{
			modifier?.Update();

			var cameraEditor = gameCameraEditor;
			if (null != cameraEditor)
			{
				var volume = getVolume(cameraEditor.controller.gameObject);
				var distance  = (cameraEditor.controller.transform.position - Map.CameraToWorld(cameraEditor.camera)).magnitude;
				VolumeUtils.SetDepthOfFieldDistance(volume, Mathf.Max(Mathf.Min(distance, cameraEditor.controller.transform.position.y * 3f), 1f));
			}
		}

		private void OnGUI() => modifier?.OnGUI();

		public void OnApplicationFocus(bool hasFocus) => modifier?.OnApplicationFocus(hasFocus);

		private void OnDestroy()
		{
			GridLinesUtil.Hide();
			if (null != iMap) iMap.OnMapEdited -= OnMapEdited;
			modifier?.OnDestroy();
		}

		private void EnableEggbot(bool value)
		{
			var eggbotController = GetComponentInChildren<EggbotController>(true);
			if (null != eggbotController) eggbotController.gameObject.SetActive(value);
		}

		private void UpdateGridLines(bool enabled = true) => GridLinesUtil.Show(transform, null != iMap ? iMap.Width : 32, null != iMap ? iMap.Height : 32, gridEnabled = enabled, offset: null!=iMap ? iMap.TileRenderPosition(0) + new Vector3(-0.5f, 0f, -0.5f) : Vector3.zero);
		private void UpdatePostProcessing(bool enabled = true)
		{
			if (null != gameCameraEditor)
			{
				var volume = getVolume(gameCameraEditor.controller.gameObject);
				volume.enabled = enabled;
				VolumeUtils.EnableDepthOfField(volume, enabled);
				VolumeUtils.SetDepthOfFieldDistance(volume, 8f);
			}
		}

		private void OnGridLinesToggled(bool value) => UpdateGridLines(gridEnabled = value);
		private void OnPostProcessingToggled(bool value) => UpdatePostProcessing(postProcessingEnabled = value);

		// ===================================================================
		// Map events
		// ===================================================================

		private void OnMapEdited(Map map, bool resized, Vector3 originDelta)
		{
			if (map == null) return;
			ResourceManager.ApplyMapChanges(map);
			if (!resized) return;
			if (gridEnabled) GridLinesUtil.UpdateSize(map.width, map.height);
			if (Vector3.zero == originDelta) return;
		}
	}
}
