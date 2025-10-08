using UnityEngine;

namespace MassiveHadronLtd
{
	public class CameraEditor : CameraBase
	{
		protected override void Start()
		{
			_data.fieldOfView = 45f;
			_data.enablePostProcessing = false;
		}

		protected override void ApplyProjection(CameraAnimationData data)
		{
			if (data.camera == null) return;
			data.camera.fieldOfView = data.fieldOfView;
		}

		protected override void Update()
		{
			// No custom update logic for CameraEditor
		}
	}
}