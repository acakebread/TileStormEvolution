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
	[SerializeField] bool continuous = true;

	IEnumerator Start()
	{
		while (true)
		{
			if (continuous || Input.GetKey(KeyCode.Space))
			{
				sparkSystem.SpawnSpark(Vector3.zero, Vector3.up + Random.onUnitSphere * Random.value);
			}
			yield return new WaitForSeconds(delay);
		}
	}
}
