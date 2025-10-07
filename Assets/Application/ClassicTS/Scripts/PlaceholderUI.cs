using UnityEngine;
using MassiveHadronLtd;
using System.Linq;

namespace ClassicTilestorm
{
	[RequireComponent(typeof(GameController))]
	public class PlaceholderUI : MonoBehaviour
	{
		private GameController gameController => GetComponent<GameController>();
		private MapManager mapManager => gameController.mapManager;
		private bool isGuiVisible = false;
		private float hideTimer = 0f;
		private float hideDelay = 3f; // 3 seconds before hiding
		private Rect guiRect = new Rect(10, -100, 980, 40); // Adjusted width for new button
		private float targetY = -100; // Target Y position for animation
		private float animationSpeed = 300f; // Pixels per second for animation
		private bool isMouseOverGui = false; 
		private CameraController cameraController => Camera.main?.GetComponent<CameraController>();

		private void Update()
		{
			// Handle left/right arrow key inputs for Previous/Next Level with repeat
			if (InputUtility.GetKeyRepeat(KeyCode.LeftArrow)) ChangeMap(-1); // Previous map
			if (InputUtility.GetKeyRepeat(KeyCode.RightArrow)) ChangeMap(1); // Next map

			// Check if mouse is near the top of the screen (within 50 pixels)
			Vector2 mousePos = Input.mousePosition;
			bool isMouseNearTop = mousePos.y >= Screen.height - 50;

			// Check if mouse is over the GUI area
			isMouseOverGui = guiRect.Contains(new Vector2(mousePos.x, Screen.height - mousePos.y));

			if (isMouseNearTop || isMouseOverGui)
			{
				// Show GUI by setting target position to 10
				targetY = 10;
				isGuiVisible = true;
				hideTimer = 0f; // Reset timer when mouse is near or over GUI
			}
			else if (isGuiVisible)
			{
				// Increment timer when mouse is not over GUI or near top
				hideTimer += Time.deltaTime;
				if (hideTimer >= hideDelay)
				{
					// Hide GUI by setting target position off-screen
					targetY = -100;
					isGuiVisible = false;
				}
			}

			// Animate the GUI position
			float currentY = guiRect.y;
			float newY = Mathf.MoveTowards(currentY, targetY, animationSpeed * Time.deltaTime);
			guiRect.y = newY;
		}

		public void ChangeMap(int delta)
		{
			if (delta == 0)
			{
				gameController.LoadMap(); // Reload current map
				return;
			}

			var currentIndex = DatabaseLoader.Maps.ToList().FindIndex(m => m.name == PreviewSettings.LoadMapName);
			currentIndex = (DatabaseLoader.Maps.Count + currentIndex + delta) % DatabaseLoader.Maps.Count;
			PreviewSettings.LoadMapName = DatabaseLoader.Maps[currentIndex].name;
			gameController.LoadMap();
		}

		private void OnGUI()
		{
			// Only draw GUI if it's visible or animating
			if (guiRect.y < -90 && !isGuiVisible) return; // Skip drawing if fully hidden

			GUI.skin.label.fontSize = 24;
			GUI.color = Color.green;

			// Draw buttons within the animated rect
			if (GUI.Button(new Rect(guiRect.x + 0, guiRect.y, 100, 30), "Reload")) ChangeMap(0);

			if (GUI.Button(new Rect(guiRect.x + 110, guiRect.y, 100, 30), "Scramble")) mapManager.Scramble();

			if (GUI.Button(new Rect(guiRect.x + 220, guiRect.y, 100, 30), "Solve")) mapManager.Solve();

			if (GUI.Button(new Rect(guiRect.x + 330, guiRect.y, 150, 30), "Previous Level")) ChangeMap(-1);

			if (GUI.Button(new Rect(guiRect.x + 490, guiRect.y, 150, 30), "Next Level")) ChangeMap(1);

			if (cameraController != null && GUI.Button(new Rect(guiRect.x + 650, guiRect.y, 150, 30), gameController.CinemaEnabled ? "Disable Cinematic" : "Enable Cinematic"))
			{
				gameController.SetAutoCinema(!gameController.CinemaEnabled);
				cameraController.Refresh(Time.time - (gameController.CinemaEnabled ? 999f : 0f));
			}

			// New button to toggle DebugMode
			if (GUI.Button(new Rect(guiRect.x + 810, guiRect.y, 150, 30), PreviewSettings.DebugMode ? "Disable Debug" : "Enable Debug"))
			{
				PreviewSettings.DebugMode = !PreviewSettings.DebugMode;
				// Update camera FOV immediately to reflect DebugMode change
				if (PreviewSettings.DebugMode)
				{
					Camera.main.fieldOfView = 45;
				}
				else
				{
					// Reset to default FOV (you may need to adjust this value based on your game's default)
					Camera.main.fieldOfView = 60; // Assuming 60 is the default FOV; adjust if needed
				}
			}
		}
	}
}
