using UnityEngine;
using MassiveHadronLtd;

public class ParticleTest : MonoBehaviour
{
	private ParticleController[] controllers;
	private bool[] buttonStates; // Tracks which buttons are held

	void Awake()
	{
		// Find all ParticleController instances in the scene, unsorted for performance
		controllers = FindObjectsByType<ParticleController>(FindObjectsInactive.Exclude);
		buttonStates = new bool[controllers.Length];
	}

	void OnGUI()
	{
		// Simple vertical layout for buttons
		float buttonHeight = 30f;
		float buttonWidth = 200f;
		float yOffset = 10f;

		for (int i = 0; i < controllers.Length; i++)
		{
			Rect buttonRect = new (10, yOffset + i * (buttonHeight + 5), buttonWidth, buttonHeight);
			bool wasPressed = buttonStates[i];
			buttonStates[i] = GUI.RepeatButton(buttonRect, controllers[i].gameObject.name);

			if (buttonStates[i] && !wasPressed)
			{
				// Button was just pressed
				controllers[i].EmitParticles();
			}
			else if (!buttonStates[i] && wasPressed)
			{
				// Button was just released
				controllers[i].StopEmitting();
			}
		}
	}
}