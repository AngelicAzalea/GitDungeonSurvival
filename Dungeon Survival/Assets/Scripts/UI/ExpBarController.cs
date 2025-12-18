using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas))]
public class ExpBarController : MonoBehaviour
{
	[Header("References")]
	public CharacterInput player;
	public Image fillImage; // assign a UI Image with Type = Filled or use rect width to set fill

	[Header("Display")]
	public bool useImageFill = true; // when false, will scale RectTransform.width instead
	[Tooltip("When using Image.FillMethod.Horizontal, set fillOrigin appropriately (left=0)")]
	public Image.FillMethod fillMethod = Image.FillMethod.Horizontal;

	[Header("Debug")]
	[Tooltip("Enable to print debug logs for binding and updates")]
	public bool debugLogs = false;

	private RectTransform fillRect;

	private void Awake()
	{
		if (fillImage != null)
		{
			fillRect = fillImage.GetComponent<RectTransform>();
			fillImage.type = Image.Type.Filled;
			fillImage.fillMethod = fillMethod;
			fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
			fillImage.fillAmount =0f;
		}
	}

	private void OnEnable()
	{
		BindToPlayer();
	}

	private void OnDisable()
	{
		if (player != null)
		{
			player.OnExpChanged -= OnExpChanged;
		}
	}

	public void BindToPlayer()
	{
		player = FindBestPlayer();

		if (player != null)
		{
			if (debugLogs) Debug.Log($"ExpBarController: Binding to player '{player.gameObject.name}' (id={player.GetInstanceID()})");
			player.OnExpChanged -= OnExpChanged;
			player.OnExpChanged += OnExpChanged;
			// initialize current value
			try
			{
				OnExpChanged(player.CurrentExp, player.ExpForLevel(player.Level));
			}
			catch (System.Exception ex)
			{
				if (debugLogs) Debug.LogWarning($"ExpBarController: failed to initialize exp display: {ex.Message}");
			}
		}
		else
		{
			if (debugLogs) Debug.LogWarning("ExpBarController: No player found to bind to.");
		}
	}

	// Choose the best CharacterInput instance to bind to: prefer tagged Player, else highest CurrentExp, else first.
	private CharacterInput FindBestPlayer()
	{
		// try tagged player first
		var go = GameObject.FindGameObjectWithTag("Player");
		if (go != null)
		{
			var ci = go.GetComponent<CharacterInput>();
			if (ci != null && ci.enabled && ci.gameObject.activeInHierarchy) return ci;
		}

		// search all active CharacterInput instances
		CharacterInput[] all = FindObjectsOfType<CharacterInput>();
		if (all == null || all.Length ==0) return null;

		// prefer active one with highest CurrentExp
		CharacterInput best = null;
		float bestExp = -1f;
		foreach (var c in all)
		{
			if (c == null) continue;
			if (!c.enabled || !c.gameObject.activeInHierarchy) continue;
			if (c.CurrentExp > bestExp)
			{
				best = c;
				bestExp = c.CurrentExp;
			}
		}
		if (best != null) return best;
		// fallback to first
		return all.Length >0 ? all[0] : null;
	}

	private void OnExpChanged(float current, float total)
	{
		if (debugLogs) Debug.Log($"ExpBarController.OnExpChanged: player={ (player!=null?player.gameObject.name:"null") } id={(player!=null?player.GetInstanceID():0)} current={current} total={total}");
		if (fillImage == null) return;
		float t = (total <=0f) ?0f : Mathf.Clamp01(current / total);
		if (useImageFill)
		{
			float before = fillImage.fillAmount;
			fillImage.fillAmount = t;
			if (debugLogs) Debug.Log($"ExpBarController: fill changed {before} -> {fillImage.fillAmount}");
		}
		else if (fillRect != null)
		{
			var p = fillRect.pivot;
			var parent = fillRect.parent as RectTransform;
			if (parent != null)
			{
				float w = parent.rect.width * t;
				fillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
			}
		}
	}

	// Poll as a fallback in case event subscription was missed. Updates at a low frequency.
	private float pollTimer =0f;
	private void Update()
	{
		if (player == null) return;
		pollTimer -= Time.unscaledDeltaTime;
		if (pollTimer <=0f)
		{
			pollTimer =0.12f; // ~8-9 times per second
			try
			{
				OnExpChanged(player.CurrentExp, player.ExpForLevel(player.Level));
			}
			catch { }
		}
	}
}
