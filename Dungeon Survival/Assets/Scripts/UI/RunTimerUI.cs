using System;
using UnityEngine;
using TMPro;

// Attach to the "Canvas - Time - Difficulty" GameObject. RunTimerUI manages its own timer when a run starts.
public class RunTimerUI : MonoBehaviour
{
	[Header("UI Refs")]
	[Tooltip("TextMeshProUGUI component used to display the timer (required)")]
	public TextMeshProUGUI tmpText;

	[Header("Display Options")]
	[Tooltip("Show difficulty multiplier next to the timer")]
	public bool showDifficulty = true;

	// local timer components
	private float elapsedMs =0f;
	private float elapsedS =0f;
	private float elapsedM =0f;
	private float elapsedH =0f;
	private bool running = true;

	private void OnEnable()
	{
		if (tmpText == null)
		{
			Debug.LogError("RunTimerUI: tmpText is not assigned in the inspector. Assign a TextMeshProUGUI component.");
		}

		if (GameManager.Instance != null)
		{
			GameManager.Instance.OnRunStarted += HandleRunStarted;
			GameManager.Instance.OnRunEnded += HandleRunEnded;
		}

		// ensure timer text is always visible
		if (tmpText != null)
		{
			tmpText.gameObject.SetActive(true);
			tmpText.enabled = true;
		}

		// show initial zero time
		UpdateDisplayImmediate();
	}

	private void OnDisable()
	{
		if (GameManager.Instance != null)
		{
			GameManager.Instance.OnRunStarted -= HandleRunStarted;
			GameManager.Instance.OnRunEnded -= HandleRunEnded;
		}
	}

	private void HandleRunStarted()
	{
		// reset local timer and start
		elapsedMs = elapsedS = elapsedM = elapsedH =0f;
		running = true;
		UpdateDisplayImmediate();
	}

	private void HandleRunEnded()
	{
		running = false;
		// leave final time visible
		UpdateDisplayImmediate();
	}

	private void Update()
	{
		if (!running)
			return;

		// Stop incrementing the run timer when the game over sequence starts
		if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
		{
			running = false;
			// ensure final time displayed
			UpdateDisplayImmediate();
			return;
		}

		float dt = Time.deltaTime;
		// update components
		elapsedMs += dt *1000f;
		if (elapsedMs >=1000f)
		{
			float whole = Mathf.Floor(elapsedMs /1000f);
			elapsedS += whole;
			elapsedMs = elapsedMs %1000f;
		}

		if (elapsedS >=60f)
		{
			float whole = Mathf.Floor(elapsedS /60f);
			elapsedM += whole;
			elapsedS = elapsedS %60f;
		}

		if (elapsedM >=60f)
		{
			float whole = Mathf.Floor(elapsedM /60f);
			elapsedH += whole;
			elapsedM = elapsedM %60f;
		}

		UpdateDisplayImmediate();
	}

	private void UpdateDisplayImmediate()
	{
		if (tmpText == null) return;

		int ms = Mathf.FloorToInt(elapsedMs);
		int s = Mathf.FloorToInt(elapsedS);
		int m = Mathf.FloorToInt(elapsedM);
		int h = Mathf.FloorToInt(elapsedH);

		string timeStr;
		if (h >0)
			timeStr = string.Format("{0:D2}:{1:D2}:{2:D2}.{3:D3}", h, m, s, ms);
		else
			timeStr = string.Format("{0:D2}:{1:D2}.{2:D3}", m, s, ms);

		if (showDifficulty && GameManager.Instance != null)
		{
			float diff = GameManager.Instance.DifficultyMultiplier;
			tmpText.text = string.Format("{0} | x{1:0.00}", timeStr, diff);
		}
		else
		{
			tmpText.text = timeStr;
		}

		// ensure visible
		tmpText.enabled = true;
		var c = tmpText.color;
		tmpText.color = new Color(c.r, c.g, c.b,1f);
	}

	// Optional: allow external forced update (keeps compatibility)
	public void UpdateDisplay(float milliseconds, float seconds, float minutes, float hours)
	{
		elapsedMs = milliseconds;
		elapsedS = seconds;
		elapsedM = minutes;
		elapsedH = hours;
		UpdateDisplayImmediate();
	}

	// Compatibility: allow GameManager to force display/showing
	public void ForceShow()
	{
		if (tmpText == null) return;
		running = true;
		tmpText.gameObject.SetActive(true);
		UpdateDisplayImmediate();
	}

	// Reset the timer to zero and stop running. Call at the start of a new run to clear previous state.
	public void Reset()
	{
		elapsedMs = elapsedS = elapsedM = elapsedH =0f;
		running = false;
		UpdateDisplayImmediate();
	}
}
