using UnityEngine;
using UnityEngine.SceneManagement;

namespace MassiveHadronLtd
{
	[DefaultExecutionOrder(-100)]
	public class DynamicSceneInstance : MonoBehaviour
	{
		[Header("References")]
		[SerializeField] private Transform contentRoot;

		public Camera PreviewCamera { get; private set; }
		public Transform ContentRoot => contentRoot;

		private DynamicSceneCameraController cameraController;

		internal void Initialize(Scene previewScene)
		{
			// Create camera
			var camObj = new GameObject("PreviewCamera");
			camObj.transform.SetParent(transform);
			PreviewCamera = camObj.AddComponent<Camera>();

			// Configure camera
			SetupCamera(PreviewCamera);

			// Create content root
			var contentObj = new GameObject("Content");
			contentObj.transform.SetParent(transform);
			contentRoot = contentObj.transform;

			// Add camera controller
			cameraController = camObj.AddComponent<DynamicSceneCameraController>();
			cameraController.Initialize(this);

			// Optional: hide flags for cleaner hierarchy
			//gameObject.hideFlags = HideFlags.HideAndDontSave;
			//camObj.hideFlags = HideFlags.HideAndDontSave;
			//contentObj.hideFlags = HideFlags.HideAndDontSave;
		}

		private void SetupCamera(Camera cam)
		{
			cam.enabled = false; // we render manually
			cam.clearFlags = CameraClearFlags.SolidColor;
			cam.backgroundColor = new Color(0.08f, 0.10f, 0.15f);
			cam.fieldOfView = 60f;
			cam.nearClipPlane = 0.03f;
			cam.farClipPlane = 50f;
			cam.cullingMask = ~0; // everything in this scene is visible
		}

		public void RenderTo(RenderTexture rt)
		{
			if (PreviewCamera == null || rt == null) return;
			PreviewCamera.targetTexture = rt;
			PreviewCamera.Render();
		}

		public void ClearContent()
		{
			if (contentRoot == null) return;
			foreach (Transform child in contentRoot)
				Destroy(child.gameObject);
		}

		private void OnDestroy()
		{
			if (PreviewCamera && PreviewCamera.targetTexture)
				PreviewCamera.targetTexture = null;
		}
	}
}