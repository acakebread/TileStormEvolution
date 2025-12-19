using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MassiveHadronLtd
{
	public class PostProcessingCameraController : MonoBehaviour
	{
		public Transform dofTarget { get; set; } // for DepthOfField

		[Header("Bokeh Focus Distance Compensation")]
		[Tooltip("Adjust this value in Play mode until the target at a known distance is perfectly sharp. " +
				 "Common values: 2.0-3.0 for longer focal lengths (e.g., 100-200mm), closer to 1.0 for wide (e.g., 35mm). " +
				 "Tune once per typical DoF preset.")]
		public float focusDistanceMultiplier = 3f; // Start with your observed value (~3)

		private Volume volume => GetComponentInChildren<Volume>(true);

		private DepthOfField depthOfField
		{
			get
			{
				if (null == volume || null == volume.profile) return null;
				volume.profile.TryGet<DepthOfField>(out var result);
				return result;
			}
		}

		private void OnEnable()
		{
			if (null == volume || null == volume.profile)
			{
				Debug.LogWarning("Volume or VolumeProfile not found.", this);
				return;
			}

			volume.enabled = true;

			var dof = depthOfField;
			if (null != dof)
			{
				dof.active = true;
				StartCoroutine(DepthOfFieldUpdate());
			}
		}

		private IEnumerator DepthOfFieldUpdate()
		{
			while (true)
			{
				if (null != dofTarget)
				{
					float worldDistance = (dofTarget.position - transform.position).magnitude;
					depthOfField.focusDistance.Override(worldDistance * focusDistanceMultiplier);
				}

				yield return null;
			}
		}

		private void OnDisable()
		{
			if (null == volume) return;

			volume.enabled = false;

			var dof = depthOfField;
			if (null == dof) return;

			dof.active = false;
		}
	}
}