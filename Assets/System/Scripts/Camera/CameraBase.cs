using UnityEngine;
using System.Collections.Generic;

namespace MassiveHadronLtd
{
	public abstract class CameraBase
	{
		//public float smoothing;
		//public Vector3 originSrc;
		//public Vector3 originDst;
		//public Vector3 targetSrc;
		//public Vector3 targetDst;
		//public float fieldOfView;
		//public float shake;//deviation amplitude
		public CameraData cameraData;

		public virtual void Start() { }
		public virtual void Update() { if (HasStarted) return; HasStarted = true; Start(); }
		public virtual void Project(Camera camera = null)
		{
			camera ??= Camera.main;
			camera.transform.position = cameraData.originSrc;
			var direction = cameraData.targetSrc - cameraData.originSrc;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			camera.fieldOfView = cameraData.fieldOfView;
			CameraUtils.ApplyCameraShake(camera, cameraData.shake);
		}

		public virtual Transform playerTransform { get; set; }
		public virtual List<Vector3> focusPoints { get; set; }
		public virtual void SetOrigin(Vector3 value, bool both = false) { cameraData.originDst = value; if (both) cameraData.originSrc = value; }
		public virtual void SetTarget(Vector3 value, bool both = false) { cameraData.targetDst = value; if (both) cameraData.targetSrc = value; }
		public bool HasStarted { private get; set; }
		public virtual bool HasCompleted => false;
		//public virtual void SetPlayer(Vector3 value) { }
	}
}