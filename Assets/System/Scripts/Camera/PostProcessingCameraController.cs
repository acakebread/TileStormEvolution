using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PostProcessingCameraController : MonoBehaviour
{
	public Transform dofTarget { get; set; } // for DepthOfField

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

		//local function
		IEnumerator DepthOfFieldUpdate()
		{
			while (true)
			{
				if (null != dofTarget) dof.focusDistance.Override((dofTarget.position - transform.position).magnitude);
				yield return null;
			}
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