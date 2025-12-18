using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;

public enum GameState { Unknown, Menu, CharacterSelect, Loading, InRun, RunEnd }

[DefaultExecutionOrder(-50)]
public class GameManager : MonoBehaviour
{
	public static GameManager Instance { get; private set; }

	public event Action<GameState> OnGameStateChanged;
	public event Action OnRunStarted;
	public event Action OnRunEnded;

	[Header("References")]
	public RunManager runManager;
	public GameBootstrap bootstrap;
	public GameObject playerPrefab;

	[Header("UI Prefabs")]
	public GameObject runUIPrefab;
	private GameObject runUIInstance;
	private RunTimerUI runTimerUIRef;

	public GameState CurrentState { get; private set; } = GameState.Unknown;

	[Header("Scenes")]
	public string menuScene = "MenuScene";
	public string characterSelectScene = "CharacterSelect";
	public string runScene = "DungeonScene";

	[Header("Run Stats")]
	public int pointsPerKill =100;
	public int pointsPerItem =50;

	[Header("Difficulty Settings")]
	public float difficultyUpdateIntervalMinutes =5f;
	public float difficultyIncrement =0.2f;
	public float difficultyRate =1f;

	// Total run elapsed (kept for compatibility with other systems)
	public float RunElapsed => runElapsed;

	// timing owned by RunTimerUI now; GameManager only tracks total run state and difficulty
	private float runElapsed =0f;
	private bool runTimerActive = false;

	public int Score { get; private set; } =0;

	[Header("Runtime")]
	[Tooltip("Integer score value visible in inspector. Use AddScore to increase this value at runtime.")]
	public int ScoreInt =0;
	public float DifficultyMultiplier { get; private set; } =1f;

	public event Action<int> OnScoreChanged;
	public event Action<float> OnDifficultyChanged;

	private StatSystem statSystem;
	private float lastDifficultyUpdateTime =0f;

	// Game over UI handled here
	public bool IsGameOver { get; private set; } = false;
	[Header("GameOver UI")]
	public Canvas gameOverCanvas;
	public GameObject gameOverCanvasPrefab; // optional prefab to spawn at run start
	public CanvasGroup gameOverCanvasGroup;
	public float cameraZoomDuration =1.5f;
	public float cameraTargetSize =3.5f;
	public float playerFadeDuration =1.0f;
	public float uiFadeDuration =0.8f;
	private TextMeshProUGUI gameOverScoreText;
	private TextMeshProUGUI gameOverTimeText;
	private Camera mainCam;
	private float originalFixedDeltaTime_gameover;

	private GameObject persistentGameOverInstance = null; // tracks prefab instance we created so we can avoid duplicates

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}
		Instance = this;
		DontDestroyOnLoad(gameObject);

		bootstrap = bootstrap ?? FindObjectOfType<GameBootstrap>();
		runManager = runManager ?? (bootstrap != null ? bootstrap.RunManager : FindObjectOfType<RunManager>());

		// initialize game over UI bindings (if present)
		mainCam = Camera.main;
		originalFixedDeltaTime_gameover = Time.fixedDeltaTime;

		// attempt to auto-bind canvas if not assigned
		if (gameOverCanvas == null)
		{
			var canvases = FindObjectsOfType<Canvas>(true);
			foreach (var c in canvases)
			{
				var n = c.gameObject.name.ToLower();
				if (n.Contains("gameover") || n.Contains("game over") || n.Contains("gameovercanvas"))
				{
					gameOverCanvas = c; break;
				}
			}
		}

		if (gameOverCanvas != null)
		{
			gameOverCanvas.gameObject.SetActive(true);
			if (gameOverCanvasGroup == null)
				gameOverCanvasGroup = gameOverCanvas.GetComponent<CanvasGroup>() ?? gameOverCanvas.GetComponentInChildren<CanvasGroup>(true) ?? gameOverCanvas.gameObject.AddComponent<CanvasGroup>();
			gameOverCanvasGroup.alpha =0f;
			gameOverCanvasGroup.interactable = false;
			gameOverCanvasGroup.blocksRaycasts = false;
			gameOverCanvas.overrideSorting = false;

			var st = gameOverCanvas.transform.Find("ScoreActual"); if (st != null) gameOverScoreText = st.GetComponent<TextMeshProUGUI>();
			var tt = gameOverCanvas.transform.Find("TimeActual"); if (tt != null) gameOverTimeText = tt.GetComponent<TextMeshProUGUI>();
			if ((gameOverScoreText == null || gameOverTimeText == null) && gameOverCanvas != null)
			{
				var tmps = gameOverCanvas.GetComponentsInChildren<TextMeshProUGUI>(true);
				foreach (var t in tmps)
				{
					if (gameOverScoreText == null && t.gameObject.name == "ScoreActual") gameOverScoreText = t;
					if (gameOverTimeText == null && t.gameObject.name == "TimeActual") gameOverTimeText = t;
				}
			}
		}
	}

	private void Start()
	{
		ChangeState(GameState.Menu);
	}

	// Add to score (int). Keeps integer Score in sync for legacy systems.
	public void AddScore(int amount)
	{
		ScoreInt += amount;
		if (ScoreInt != Score)
		{
			Score = ScoreInt;
			OnScoreChanged?.Invoke(Score);
		}
	}

	// End the current run and optionally return to menu
	public void EndRun(bool returnToMenu = true)
	{
		runManager = runManager ?? FindObjectOfType<RunManager>();
		runManager?.EndRun();

		runTimerActive = false;

		OnRunEnded?.Invoke();
		ChangeState(GameState.RunEnd);

		// Reset GameOver UI when ending run so it doesn't persist
		ResetGameOverUI();

		if (returnToMenu)
		{
			if (runUIInstance != null) runUIInstance.SetActive(false);
			GoToMenu();
		}
	}

	// Triggered externally (e.g., player death)
	public void TriggerGameOver(GameObject player)
	{
		if (player == null) return;
		if (IsGameOver) return;
		IsGameOver = true;

		// make sure run ends (but stay in scene)
		EndRun(returnToMenu: false);

		int score = Score;
		float timeSeconds = RunElapsed;
		StartCoroutine(RunGameOverSequence(player, score, timeSeconds));
	}

	private IEnumerator RunGameOverSequence(GameObject player, int score, float timeSeconds)
	{
		if (mainCam == null) mainCam = Camera.main;
		Vector3 camStartPos = mainCam.transform.position;
		float camStartSize = mainCam.orthographicSize;
		Vector3 playerPos = player.transform.position;
		Vector3 camTargetPos = new Vector3(playerPos.x, playerPos.y, camStartPos.z);

		var midpointFollow = FindObjectOfType<MidpointCameraFollow>(); if (midpointFollow != null) midpointFollow.enabled = false;
		var mouseFollow = FindObjectOfType<MouseFollow2D>(); SpriteRenderer cross = null; if (mouseFollow != null) { cross = mouseFollow.GetComponent<SpriteRenderer>(); mouseFollow.enabled = false; }

		float t =0f;
		while (t < cameraZoomDuration)
		{
			t += Time.unscaledDeltaTime;
			float f = Mathf.Clamp01(t / cameraZoomDuration);
			mainCam.transform.position = Vector3.Lerp(camStartPos, camTargetPos, f);
			mainCam.orthographicSize = Mathf.Lerp(camStartSize, cameraTargetSize, f);
			yield return null;
		}

		// fade player
		var sprite = player.GetComponent<SpriteRenderer>();
		Color startColor = sprite != null ? sprite.color : Color.white;
		Color crossStart = cross != null ? cross.color : Color.white;
		t =0f;
		while (t < playerFadeDuration)
		{
			t += Time.unscaledDeltaTime;
			float f = Mathf.Clamp01(t / playerFadeDuration);
			if (sprite != null) { var c = startColor; c.a = Mathf.Lerp(startColor.a,0f, f); sprite.color = c; }
			if (cross != null) { var cc = crossStart; cc.a = Mathf.Lerp(crossStart.a,0f, f); cross.color = cc; }
			yield return null;
		}

		// show and fade in GameOver UI
		if (gameOverCanvas != null && gameOverCanvasGroup != null)
		{
			gameOverCanvas.overrideSorting = true; gameOverCanvas.sortingOrder =9999;
			gameOverCanvasGroup.alpha =0f; gameOverCanvasGroup.interactable = false; gameOverCanvasGroup.blocksRaycasts = false;
			var graphics = gameOverCanvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
			var originalColors = new Color[graphics.Length]; for (int i =0; i < graphics.Length; i++) originalColors[i] = graphics[i].color;

			t =0f;
			while (t < uiFadeDuration)
			{
				t += Time.unscaledDeltaTime;
				float f = Mathf.Clamp01(t / uiFadeDuration);
				gameOverCanvasGroup.alpha = f;
				for (int i =0; i < graphics.Length; i++) { var g = graphics[i]; var oc = originalColors[i]; g.color = new Color(oc.r, oc.g, oc.b, oc.a * f); }
				yield return null;
			}

			gameOverCanvasGroup.interactable = true; gameOverCanvasGroup.blocksRaycasts = true;

			// Ensure the Canvas can receive pointer input: add/enable a GraphicRaycaster and ensure an EventSystem exists.
			var gr = gameOverCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
			if (gr == null) gr = gameOverCanvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
			gr.enabled = true;
			if (EventSystem.current == null)
			{
				var es = new GameObject("EventSystem");
				es.AddComponent<EventSystem>();
				es.AddComponent<StandaloneInputModule>();
				DontDestroyOnLoad(es);
			}

			// Make sure the cursor is visible so the player can click the UI
			Cursor.visible = true;
			Cursor.lockState = CursorLockMode.None;

			// Debug info to help identify why UI isn't receiving clicks
			var btns = gameOverCanvas.GetComponentsInChildren<UnityEngine.UI.Button>(true);
			Debug.Log($"GameOver UI State: canvas.enabled={gameOverCanvas.enabled}, overrideSorting={gameOverCanvas.overrideSorting}, sortingOrder={gameOverCanvas.sortingOrder}, canvasGroup.alpha={gameOverCanvasGroup.alpha}, interactable={gameOverCanvasGroup.interactable}, blocksRaycasts={gameOverCanvasGroup.blocksRaycasts}, GraphicRaycaster={(gr!=null)}, EventSystem={(EventSystem.current!=null)}, Buttons={btns.Length}");
			for (int b =0; b < btns.Length; b++)
			{
				var btn = btns[b];
				Debug.Log($"Button[{b}] name={btn.gameObject.name} active={btn.gameObject.activeInHierarchy} interactable={btn.interactable}");
			}

			// Restore original graphic colors (ensure full opacity)
			if (graphics != null && originalColors != null)
			{
				for (int i =0; i < graphics.Length; i++)
				{
					var g = graphics[i];
					if (g == null) continue;
					var oc = originalColors[i];
					g.color = oc;
				}
			}
		}

		// populate
		if (gameOverScoreText == null && gameOverCanvas != null) gameOverScoreText = gameOverCanvas.transform.Find("ScoreActual")?.GetComponent<TextMeshProUGUI>();
		if (gameOverTimeText == null && gameOverCanvas != null) gameOverTimeText = gameOverCanvas.transform.Find("TimeActual")?.GetComponent<TextMeshProUGUI>();
		if (gameOverScoreText != null) gameOverScoreText.text = score.ToString();
		if (gameOverTimeText != null) { var rt = runTimerUIRef; if (rt != null && rt.tmpText != null) gameOverTimeText.text = rt.tmpText.text; else gameOverTimeText.text = System.TimeSpan.FromSeconds(timeSeconds).ToString(@"hh\:mm\:ss"); }

		// store highscore
		if (HighscoreManager.Instance != null) HighscoreManager.Instance.AddEntry(score, timeSeconds, null);
	}

	public void ChangeState(GameState newState)
	{
		if (newState == CurrentState) return;
		CurrentState = newState;
		OnGameStateChanged?.Invoke(CurrentState);
		Debug.Log($"GameManager: State changed to {CurrentState}");
	}

	public void GoToMenu() => StartCoroutine(LoadSceneAndSetState(menuScene, GameState.Menu));
	public void GoToCharacterSelect() => StartCoroutine(LoadSceneAndSetState(characterSelectScene, GameState.CharacterSelect));
	public void StartRun(string runSceneName = null)
	{
		string sceneToLoad = string.IsNullOrEmpty(runSceneName) ? runScene : runSceneName;
		StartCoroutine(StartRunCoroutine(sceneToLoad));
	}

	// Change access to public so external callers (SceneChanger) can reset the UI before scene loads
	public void ResetGameOverUI()
	{
		IsGameOver = false;
		if (gameOverCanvasGroup != null)
		{
			gameOverCanvasGroup.alpha =0f;
			gameOverCanvasGroup.interactable = false;
			gameOverCanvasGroup.blocksRaycasts = false;
		}
		if (gameOverCanvas != null)
		{
			// disable sorting override so it won't overlay other canvases
			gameOverCanvas.overrideSorting = false;
			// disable raycaster if present
			var gr = gameOverCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
			if (gr != null) gr.enabled = false;
		}
		// leave cursor visibility to the caller/state (menu will show it)
	}

	// use LoadSceneAndSetState to hide GameOver UI when going to menu
	private IEnumerator LoadSceneAndSetState(string sceneName, GameState targetState)
	{
		// If navigating to menu, reset the game over UI first so it won't persist into the menu or future runs
		if (targetState == GameState.Menu)
		{
			ResetGameOverUI();
			// Ensure run UI does not persist into menu: deactivate but keep instance for reuse
			if (runUIInstance != null)
			{
				try { runUIInstance.SetActive(false); } catch { }
				// keep runUIInstance for reuse on next run, do not destroy
				runTimerUIRef = runUIInstance.GetComponentInChildren<RunTimerUI>(true);
			}
		}

		if (SceneManager.GetActiveScene().name == sceneName)
		{
			ChangeState(targetState);
			yield break;
		}
		yield return StartCoroutine(LoadSceneAsync(sceneName));
		ChangeState(targetState);
	}

	private IEnumerator StartRunCoroutine(string sceneToLoad)
	{
		ChangeState(GameState.Loading);

		runManager = runManager ?? FindObjectOfType<RunManager>();
		statSystem = statSystem ?? FindObjectOfType<StatSystem>();

		var runtimeInv = ScriptableObject.CreateInstance<ItemInventory>();
		if (bootstrap != null) bootstrap.RunInventory = runtimeInv;
		runManager?.SetRunInventory(runtimeInv);
		statSystem?.Initialize(runtimeInv);
		statSystem?.NotifyInventoryChanged();

		// Reset run stats
		runElapsed =0f;
		Score =0;
		ScoreInt =0;
		DifficultyMultiplier =1f;
		lastDifficultyUpdateTime =0f;

		// Start run services early
		runManager?.StartRun();

		// load scene
		yield return StartCoroutine(LoadSceneAsync(sceneToLoad));
		yield return null;

		// After scene load, prefer a GameOver Canvas that exists in the loaded scene (editor-placed)
		Canvas sceneGameOver = null;
		var sceneCanvases = FindObjectsOfType<Canvas>(true);
		foreach (var c in sceneCanvases)
		{
			var n = c.gameObject.name.ToLower();
			if (n.Contains("gameover") || n.Contains("game over") || n.Contains("gameovercanvas"))
			{
				sceneGameOver = c;
				break;
			}
		}

		if (sceneGameOver != null)
		{
			// If we previously created a persistent instance, destroy it to avoid duplicates
			if (persistentGameOverInstance != null && persistentGameOverInstance != sceneGameOver.gameObject)
			{
				Destroy(persistentGameOverInstance);
				persistentGameOverInstance = null;
			}

			// Bind to the scene canvas (do NOT mark DontDestroyOnLoad)
			gameOverCanvas = sceneGameOver;
			// ensure CanvasGroup is present
			gameOverCanvasGroup = gameOverCanvas.GetComponent<CanvasGroup>() ?? gameOverCanvas.GetComponentInChildren<CanvasGroup>(true) ?? gameOverCanvas.gameObject.AddComponent<CanvasGroup>();
		}
		else
		{
			// No scene canvas found � fall back to instantiating the prefab (persistent)
			if (gameOverCanvasPrefab != null && gameOverCanvas == null)
			{
				var go = Instantiate(gameOverCanvasPrefab);
				persistentGameOverInstance = go;
				gameOverCanvas = go.GetComponent<Canvas>() ?? go.GetComponentInChildren<Canvas>();
				if (gameOverCanvas != null)
				{
					DontDestroyOnLoad(gameOverCanvas.gameObject);
					// find or add CanvasGroup
					gameOverCanvasGroup = gameOverCanvas.GetComponent<CanvasGroup>() ?? gameOverCanvas.GetComponentInChildren<CanvasGroup>(true);
					if (gameOverCanvasGroup == null) gameOverCanvasGroup = gameOverCanvas.gameObject.AddComponent<CanvasGroup>();
				}
			}
		}

		// If we bound to a scene canvas or created one above, ensure it's initialized (alpha=0)
		if (gameOverCanvas != null)
		{
			if (gameOverCanvasGroup == null)
				gameOverCanvasGroup = gameOverCanvas.GetComponent<CanvasGroup>() ?? gameOverCanvas.GetComponentInChildren<CanvasGroup>(true) ?? gameOverCanvas.gameObject.AddComponent<CanvasGroup>();
			gameOverCanvasGroup.alpha =0f;
			gameOverCanvasGroup.interactable = false;
			gameOverCanvasGroup.blocksRaycasts = false;
			// bind TMP fields if missing
			var st = gameOverCanvas.transform.Find("ScoreActual"); if (st != null) gameOverScoreText = st.GetComponent<TextMeshProUGUI>();
			var tt = gameOverCanvas.transform.Find("TimeActual"); if (tt != null) gameOverTimeText = tt.GetComponent<TextMeshProUGUI>();
		}

		// instantiate persistent run UI prefab
		if (runUIPrefab != null)
		{
			if (runUIInstance == null)
			{
				runUIInstance = Instantiate(runUIPrefab);
				runUIInstance.SetActive(true);
				DontDestroyOnLoad(runUIInstance);
				runTimerUIRef = runUIInstance.GetComponentInChildren<RunTimerUI>(true);
			}
			else
			{
				runUIInstance.SetActive(true);
				runTimerUIRef = runUIInstance.GetComponentInChildren<RunTimerUI>(true);
			}
			if (EventSystem.current == null)
			{
				var es = new GameObject("EventSystem");
				es.AddComponent<EventSystem>();
				es.AddComponent<StandaloneInputModule>();
				DontDestroyOnLoad(es);
			}
		}

		// spawn player
		SpawnPlayerInScene();

		// start run: notify UI and systems
		runTimerActive = true;
		OnRunStarted?.Invoke();
		if (runTimerUIRef != null) runTimerUIRef.ForceShow();

		// ensure game over UI hidden at run start
		IsGameOver = false;
		if (gameOverCanvasGroup != null) { gameOverCanvasGroup.alpha =0f; gameOverCanvasGroup.interactable = false; gameOverCanvasGroup.blocksRaycasts = false; }
		if (gameOverCanvas != null) gameOverCanvas.overrideSorting = false;

		ChangeState(GameState.InRun);
	}

	private IEnumerator LoadSceneAsync(string sceneName)
	{
		if (string.IsNullOrEmpty(sceneName)) { Debug.LogError("GameManager: sceneName is null or empty"); yield break; }
		var op = SceneManager.LoadSceneAsync(sceneName);
		if (op == null) { Debug.LogError($"GameManager: Failed to start loading scene '{sceneName}'"); yield break; }
		while (!op.isDone) yield return null;
	}

	private void SpawnPlayerInScene()
	{
		if (playerPrefab == null) { Debug.LogWarning("GameManager: playerPrefab not assigned - cannot spawn player automatically."); return; }
		Transform spawn = null;
		var spawnGO = GameObject.FindWithTag("PlayerSpawn");
		if (spawnGO != null) spawn = spawnGO.transform;
		else { var named = GameObject.Find("PlayerSpawn"); if (named != null) spawn = named.transform; }
		Vector3 pos = spawn != null ? spawn.position : Vector3.zero;
		Instantiate(playerPrefab, pos, Quaternion.identity);
	}
}
