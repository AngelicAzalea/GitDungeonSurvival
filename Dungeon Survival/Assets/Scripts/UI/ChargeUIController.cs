using UnityEngine;
using UnityEngine.UI;

// Simple runtime UI controller for the ranger charge bar
public class ChargeUIController : MonoBehaviour
{
	private Image bgImage;
	private Image fillImage;
	private RectTransform indicatorRect;
	private CanvasGroup canvasGroup;

	private Color baseFill;
	private Color warnFill;
	private Color perfectFill;

	// store the initial local position so shakes don't accumulate
	private Vector3 originalLocalPos;

	private Coroutine perfectRoutineHandle;
	private Coroutine pulseRoutineHandle;

	// Maximum shake intensity cap
	public float maxShakeIntensity = 0.6f;

	public void Initialize(Image bg, Image fill, RectTransform indicator, CanvasGroup cg, Color baseFillColor, Color warnColor, Color perfectColor)
	{
		bgImage = bg;
		fillImage = fill;
		indicatorRect = indicator;
		canvasGroup = cg;
		baseFill = baseFillColor;
		warnFill = warnColor;
		perfectFill = perfectColor;

		// capture starting local position
		originalLocalPos = transform.localPosition;
	}

	public void Show()
	{
		ResetPosition();
		if (canvasGroup != null) canvasGroup.alpha = 1f;
		gameObject.SetActive(true);
	}
	public void Hide()
	{
		StopOverchargePulse();
		ResetPosition();
		if (canvasGroup != null) canvasGroup.alpha = 0f;
		gameObject.SetActive(false);
	}
	public void HideInstant()
	{
		StopOverchargePulse();
		ResetPosition();
		if (canvasGroup != null) canvasGroup.alpha = 0f;
		gameObject.SetActive(false);
	}

	public void ResetPosition()
	{
		transform.localPosition = originalLocalPos;
	}

	// progress:0..1, overchargeRatio currently unused for color but available for effects
	public void UpdateProgress(float progress, float overchargeRatio)
	{
		if (fillImage != null)
		{
			fillImage.fillAmount = Mathf.Clamp01(progress);
			// Color logic: red until50%, then lerp to green by100%
			if (progress < 0.5f)
			{
				fillImage.color = warnFill; // red
			}
			else
			{
				float t = Mathf.InverseLerp(0.5f, 1f, progress); //0..1 from50%..100%
				fillImage.color = Color.Lerp(warnFill, baseFill, t);
			}
		}

		if (indicatorRect != null)
		{
			float x = Mathf.Lerp(-0.5f, 0.5f, progress) * ((RectTransform)transform).sizeDelta.x;
			indicatorRect.anchoredPosition = new Vector2(x, indicatorRect.anchoredPosition.y);
		}
	}

	public void ApplyShake(float intensity)
	{
		// cap intensity so it doesn't grow forever
		float capped = Mathf.Min(intensity, maxShakeIntensity);
		// Do not accumulate offsets; apply relative to the original local position.
		// Prefer vertical shake more than horizontal.
		float xJitter = Random.Range(-capped * 0.1f, capped * 0.1f);
		float yJitter = Random.Range(-capped, capped) * 0.6f; // reduce vertical amplitude slightly
		transform.localPosition = originalLocalPos + new Vector3(xJitter, yJitter, 0f);
	}

	// Start a gold-hue cycling animation for the given duration, then restore to baseFill.
	public void StartPerfectAnimation(float duration)
	{
		// Ensure UI is active before starting coroutine
		if (!gameObject.activeInHierarchy)
		{
			gameObject.SetActive(true);
			if (canvasGroup != null) canvasGroup.alpha = 1f;
		}
		if (perfectRoutineHandle != null) StopCoroutine(perfectRoutineHandle);
		perfectRoutineHandle = StartCoroutine(PerfectRoutine(duration));
	}

	private System.Collections.IEnumerator PerfectRoutine(float duration)
	{
		if (fillImage == null)
			yield break;

		float t = 0f;
		// define a few gold hues to cycle
		Color[] hues = new Color[] { new Color(1f, 0.84f, 0f), new Color(1f, 0.75f, 0.1f), new Color(1f, 0.9f, 0.2f) };
		int idx = 0;
		while (t < duration)
		{
			float seg = 0.12f; // each hue hold time
			Color from = hues[idx % hues.Length];
			Color to = hues[(idx + 1) % hues.Length];
			float segT = 0f;
			while (segT < seg && t < duration)
			{
				segT += Time.deltaTime;
				t += Time.deltaTime;
				float ft = Mathf.Clamp01(segT / seg);
				fillImage.color = Color.Lerp(from, to, ft);
				yield return null;
			}
			idx++;
		}

		// restore to baseFill (green) when done
		fillImage.color = baseFill;
		perfectRoutineHandle = null;
	}

	public void ShowPerfectFlash()
	{
		// convenience: short flash
		if (perfectRoutineHandle != null) return;
		if (fillImage != null)
		{
			StartCoroutine(FlashRoutine());
		}
	}

	private System.Collections.IEnumerator FlashRoutine()
	{
		if (fillImage == null) yield break;
		Color orig = fillImage.color;
		fillImage.color = perfectFill;
		yield return new WaitForSeconds(0.12f);
		fillImage.color = orig;
	}

	// Overcharge pulse: pulse the bar red while overcharge active
	public void StartOverchargePulse(float pulseSpeed = 2f, float pulseAmount = 0.5f)
	{
		if (pulseRoutineHandle != null) return;
		pulseRoutineHandle = StartCoroutine(OverchargePulseRoutine(pulseSpeed, pulseAmount));
	}

	public void StopOverchargePulse()
	{
		if (pulseRoutineHandle != null)
		{
			StopCoroutine(pulseRoutineHandle);
			pulseRoutineHandle = null;
			// restore color to baseFill
			if (fillImage != null) fillImage.color = baseFill;
		}
	}

	private System.Collections.IEnumerator OverchargePulseRoutine(float speed, float amount)
	{
		if (fillImage == null) yield break;
		float t = 0f;
		while (true)
		{
			t += Time.deltaTime * speed;
			float ping = (Mathf.Sin(t) * 0.5f + 0.5f) * amount; //0..amount
			// interpolate between baseFill and warnFill using ping (so pulses red)
			fillImage.color = Color.Lerp(baseFill, warnFill, ping);
			yield return null;
		}
	}
}
