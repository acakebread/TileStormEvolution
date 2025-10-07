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
		
		public virtual void Start(ref CameraData data) { HasStarted = true; }
		public virtual void Update(ref CameraData data) { if (HasStarted) return; Start(ref data); }
		public virtual void Project(ref CameraData data, Camera camera = null)
		{
			camera ??= Camera.main;
			camera.transform.position = data.originSrc;
			var direction = data.targetSrc - data.originSrc;
			if (direction.sqrMagnitude > Mathf.Epsilon)
				camera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
			camera.fieldOfView = data.fieldOfView;
			CameraUtils.ApplyCameraShake(camera, data.shake);
		}

		public virtual Transform playerTransform { get; set; }
		public virtual List<Vector3> focusPoints { get; set; }
		public virtual void SetOrigin(ref CameraData data, Vector3 value, bool both = false) { data.originDst = value; if (both) data.originSrc = value; }
		public virtual void SetTarget(ref CameraData data, Vector3 value, bool both = false) { data.targetDst = value; if (both) data.targetSrc = value; }
		public bool HasStarted { private get; set; }
		public virtual bool HasCompleted => false;
		//public virtual void SetPlayer(Vector3 value) { }
	}
}
