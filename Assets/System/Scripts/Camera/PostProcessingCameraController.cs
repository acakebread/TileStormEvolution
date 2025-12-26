using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace MassiveHadronLtd
{
	public class PostProcessingCameraController : MonoBehaviour
	{
		public Transform dofTarget { get; set; } // for DepthOfField

		[Header("Bokeh Focus Distance Compensation")]
		public float focusDistanceMultiplier = 1f;

		private Volume volume => GetComponentInChildren<Volume>(true);
		private Coroutine dofCoroutine;

		private void OnEnable()
		{
			if (null == volume || null == volume.profile)
			{
				Debug.LogWarning("Volume or VolumeProfile not found.", this);
				return;
			}

			volume.enabled = true;
			VolumeUtils.EnableDepthOfField(volume, true);

			// Start and store the reference
			dofCoroutine = StartCoroutine(DepthOfFieldUpdate());
		}

		private IEnumerator DepthOfFieldUpdate()
		{
			while (true)
			{
				if (null != dofTarget)
				{
					var worldDistance = (dofTarget.position - transform.position).magnitude;
					VolumeUtils.SetDepthOfFieldDistance(volume, worldDistance * focusDistanceMultiplier);
				}
				yield return null;
			}
		}

		private void OnDisable()
		{
			if (dofCoroutine != null)
			{
				StopCoroutine(dofCoroutine);  // This stops the infinite loop
				dofCoroutine = null;
			}

			if (null == volume) return;

			volume.enabled = false;
			VolumeUtils.EnableDepthOfField(volume, false);
		}
	}
}