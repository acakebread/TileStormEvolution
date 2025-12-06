// File: Editor/SquatrixTest.cs
using UnityEngine;
using UnityEditor;
using MassiveHadronLtd;

public class SquatrixTest : EditorWindow
{
	[MenuItem("Tools/Test Squatrix Round-Trip")]
	static void Test()
	{
		//bool allPass = true;

		//allPass &= Squatrix.TestRoundTrip(new Vector3(100, 20, -300), Quaternion.Euler(45, 90, 0), 15f);
		//allPass &= Squatrix.TestRoundTrip(new Vector3(-500, 5, 0), Quaternion.identity, 8f);
		//allPass &= Squatrix.TestRoundTrip(new Vector3(0, 100, 0), Quaternion.Euler(0, 180, 0), 30f);
		//allPass &= Squatrix.TestRoundTrip(new Vector3(999, 50, 999), Quaternion.Euler(30, 60, 90), 22f);

		//Debug.Log(allPass ? "SQUATRIX IS PERFECT" : "SQUATRIX HAS FAILED");

		Squatrix.TestRoundTrip();
	}
}