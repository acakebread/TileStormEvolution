using MassiveHadronLtd;
using System.Collections;
using UnityEngine;

public class ParticleTest : MonoBehaviour
{
	[SerializeField] private SparkController sparkController;
	[SerializeField][Range(0, 10)] private float delay = 0.1f;
	[SerializeField][Range(1, 128)] private int ct = 1;
	[SerializeField] bool continuous = true;

	[SerializeField] private Vector3 spawnOffset = Vector3.zero;

	IEnumerator Start()
	{
		while (true)
		{
			if (continuous || Input.GetKey(KeyCode.Space))
			{
				for (var n = 0; n < ct; ++n)
				{
					var vel = Random.onUnitSphere * Random.value * 0.5f;
					vel.y *= 2f; // exaggerate vertical speed
					vel += Vector3.up;
					Vector3 worldPos = sparkController.transform.position + spawnOffset;
					//sparkSystem.SpawnSpark(worldPos, vel, settings);
					sparkController.SpawnSpark(worldPos, vel);//
				}
			}
			yield return new WaitForSeconds(delay);
		}
	}
}
