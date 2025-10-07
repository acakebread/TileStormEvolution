using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PostProcessingCameraController : MonoBehaviour
{
	public Transform target { get; set; } // for DOF

	private void OnEnable()
	{
		var volume = GetComponentInChildren<Volume>(true);
		if (null == volume || null == volume.profile)
		{
			Debug.LogWarning("Volume or VolumeProfile not found.", this);
			return;
		}
		volume.enabled = true;

		StartCoroutine(Run());

		//local function
		IEnumerator Run()
		{
			while (true)
			{
				if (volume.profile.TryGet<DepthOfField>(out var dof))
				{
					if (target != null)
					{
						// Enable DoF and set focus distance
						dof.active = true;
						dof.focusDistance.Override((target.position - transform.position).magnitude);
						// Debug.Log($"DoF enabled, focusDistance set to {(target.position - transform.position).magnitude}");
					}
					else
					{
						// Disable DoF
						dof.active = false;
					}
				}
				//else
				//{
				//	Debug.LogWarning("DepthOfField component not found in VolumeProfile.", this);
				//}
				yield return null;
			}
		}
	}

	private void OnDisable()
	{
		var volume = GetComponentInChildren<Volume>(true);
		if (null != volume) volume.enabled = false;
	}
}