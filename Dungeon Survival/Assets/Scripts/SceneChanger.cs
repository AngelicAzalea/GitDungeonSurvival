using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SceneChanger : MonoBehaviour
{
	[Header("Scene Names (optional)")]
	[Tooltip("If set, these named scenes will be used. Otherwise numeric indices in existing methods may be used.")]
	public string menuSceneName = "MenuScene";
	public string gameplaySceneName = "DungeonScene";
	public string highscoresSceneName = "Highscores";

	// Backwards-compatible index-based loader (kept for existing UI hooks)
	public void LoadGameplayScene()
	{
		// Prefer GameManager if present to handle run setup and loading
		if (GameManager.Instance != null)
		{
			// Ensure any lingering GameOver UI is hidden before starting a new run
			GameManager.Instance.ResetGameOverUI();
			GameManager.Instance.StartRun(gameplaySceneName);
			return;
		}

		if (!string.IsNullOrEmpty(gameplaySceneName) && SceneExistsInBuild(gameplaySceneName))
		{
			// Reset any persistent GameOver UI if a GameManager exists in the project
			if (GameManager.Instance != null) GameManager.Instance.ResetGameOverUI();
			SceneManager.LoadScene(gameplaySceneName);
		}
		else
		{
			// fallback to index1 as before
			SceneManager.LoadScene(1);
		}
	}

	public void LoadMenuScene()
	{
		// If a specific menu scene name is configured and exists in build settings, load it directly.
		if (!string.IsNullOrEmpty(menuSceneName) && SceneExistsInBuild(menuSceneName))
		{
			// Reset GameOver UI if present so it won't persist into the menu
			if (GameManager.Instance != null) GameManager.Instance.ResetGameOverUI();
			SceneManager.LoadScene(menuSceneName);
			return;
		}

		// Prefer GameManager to drive state transitions if it exists and has a menu scene configured.
		if (GameManager.Instance != null)
		{
			// Ensure GameOver UI is cleared before transitioning
			GameManager.Instance.ResetGameOverUI();
			GameManager.Instance.GoToMenu();
			return;
		}

		// fallback to index0 as before
		SceneManager.LoadScene(0);
	}

	// Load the highscores scene (or open highscores UI) - simple scene loader
	public void LoadHighscoresScene()
	{
		if (!string.IsNullOrEmpty(highscoresSceneName) && SceneExistsInBuild(highscoresSceneName))
		{
			SceneManager.LoadScene(highscoresSceneName);
		}
		else
		{
			Debug.LogWarning("SceneChanger: highscoresSceneName not set or not in build settings.");
		}
	}

	// Clear saved highscores stored in PlayerPrefs under the key "Highscores" (format is app-specific)
	public void ClearHighscores()
	{
		if (PlayerPrefs.HasKey("Highscores"))
		{
			PlayerPrefs.DeleteKey("Highscores");
			PlayerPrefs.Save();
			Debug.Log("SceneChanger: Highscores cleared.");
		}
		else
		{
			Debug.Log("SceneChanger: No Highscores key found to clear.");
		}
	}

	// Quit the application (stops play mode in the Editor)
	public void QuitApplication()
	{
#if UNITY_EDITOR
		// Stop play mode in editor
		EditorApplication.isPlaying = false;
#else
		Application.Quit();
#endif
	}

	// Helper to check if a scene name exists in build settings
	private bool SceneExistsInBuild(string sceneName)
	{
		if (string.IsNullOrEmpty(sceneName)) return false;
		int count = SceneManager.sceneCountInBuildSettings;
		for (int i =0; i < count; i++)
		{
			string path = SceneUtility.GetScenePathByBuildIndex(i);
			string name = Path.GetFileNameWithoutExtension(path);
			if (string.Equals(name, sceneName, System.StringComparison.OrdinalIgnoreCase)) return true;
		}
		return false;
	}
}
