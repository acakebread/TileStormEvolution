using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PostProcessingCameraController : MonoBehaviour
{
	private Volume volume;
	public Transform target { get; set; } // for DOF

	IEnumerator Start()
	{
		volume = FindAnyObjectByType<Volume>(FindObjectsInactive.Include);

		if (volume != null && volume.profile != null)
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
				else
				{
					Debug.LogWarning("DepthOfField component not found in VolumeProfile.", this);
				}

				yield return null;
			}
		}
		else
		{
			Debug.LogWarning("Volume or VolumeProfile not found.", this);
		}
	}
}