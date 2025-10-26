using MassiveHadronLtd;
using System.Collections;
using UnityEngine;

public class ParticleTest : MonoBehaviour
{
	//[SerializeField] private ParticleController particleController;

	//void Update()
	//{
	//	//if (Input.GetKey(KeyCode.Space))
	//	{
	//		//GetComponent<TracerSystem>().SpawnTestTracer();
	//		particleController.SpawnSpark(Vector3.zero, Random.onUnitSphere);
	//	}
	//}

	[SerializeField] private SparkSystem sparkSystem;
	[SerializeField][Range(0, 10)] private float delay = 0.1f;
	[SerializeField][Range(1, 32)] private int ct = 1;
	[SerializeField] bool continuous = true;

	IEnumerator Start()
	{
		while (true)
		{
			if (continuous || Input.GetKey(KeyCode.Space))
			{
				for (var n = 0; n < ct; ++n)
				{
					var vel = Vector3.up + Random.onUnitSphere * Random.value * 0.5f;
					vel.y *= 2f; // exaggerate vertical speed
					sparkSystem.SpawnSpark(Vector3.zero, vel);
				}
			}
			yield return new WaitForSeconds(delay);
		}
	}
}
