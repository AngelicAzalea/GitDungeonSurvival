using UnityEngine;
using UnityEngine.UI;

public class RangerBehaviour : PlayerClassBehaviour
{
	[Header("Ranger - Charge / Projectile")]
	public GameObject projectilePrefab;
	public Transform projectileSpawnPoint;
	[Tooltip("If true, use the world position of a cursorObject. Otherwise use the current mouse position via ScreenToWorldPoint.")]
	public bool useCursorObject = false;
	public GameObject cursorObject; // optional world-space cursor object
	public float maxChargeTime =1.5f;
	public float maxDamageMultiplier =2f;
	public float maxSpeedMultiplier =1.5f;
	public float projectileBaseDamage =0f; //0 => use classData.attack or owner.Attack
	public float projectileBaseSpeed =0f; //0 => use classData.moveSpeed or owner.moveSpeed
	public bool destroyOtherProjectiles = false; // whether this behaviour's projectiles destroy other projectiles

	[Header("Charge Rules")]
	public float overdrawTime =2.0f; // seconds before auto-release (overdraw)
	[Range(0f,1f)] public float minChargeToShoot =0.5f; //50%
	public float overdrawDamageMultiplier =0.75f;
	public float partialSpeedMultiplier =0.5f; // speed when barely charged
	public float partialLifetime =1.2f; // lifetime when barely charged
	public float perfectWindowDuration =0.12f; // seconds window after reaching maxChargeTime
	public float perfectDamageMultiplier =1.5f;
	public int perfectPierceCount =1;

	// UI settings
	[Header("Charge UI")]
	public Vector3 uiLocalOffset = new Vector3(0f, -1.2f,0f);
	public Vector2 uiSize = new Vector2(120f,18f);
	public Color uiFillColor = Color.green;
	public Color uiWarnColor = Color.red;
	public Color uiOutlineColor = Color.white;
	public Color uiPerfectColor = new Color(1f,0.84f,0f); // gold

	private bool isCharging = false;
	private float chargeStartTime =0f;
	private float timeReachedMax = -1f;
	private float overchargeDelayEnd = -1f;

	// Cache the most recent cursor world position while charging to avoid relying on external object update order
	private Vector3 lastCursorWorld = Vector3.zero;

	// runtime-created UI controller
	private ChargeUIController uiController;

	// New fields for overcharge handling
	public float overchargeAutoEjectAfter =1.0f; // seconds after overcharge becomes active to auto-eject
	private float overchargeStartTime = -1f;
	private bool hasAutoEjected = false;

	public override void Initialize(CharacterInput owner)
	{
		base.Initialize(owner);
		// ensure classData is set
		if (classData == null && owner != null) classData = owner.classData;
	}

	public override void HandleInput()
	{
		if (owner == null) return;

		// Start charge on left mouse button down
		if (Input.GetMouseButtonDown(0) && !isCharging)
		{
			StartCharge();
		}

		// Update charge (could drive UI/animator)
		if (isCharging && Input.GetMouseButton(0))
		{
			float elapsed = Time.time - chargeStartTime;
			float chargeRatio = Mathf.Clamp01(elapsed / maxChargeTime);
			UpdateCursorWorld();
			UpdateChargeUI(chargeRatio);

			// register time reached max for perfect window
			if (chargeRatio >=1f && timeReachedMax <0f)
			{
				timeReachedMax = Time.time;
				// start the pronounced perfect animation for the configured window duration
				uiController?.StartPerfectAnimation(perfectWindowDuration);
				// set overcharge delay to begin after animation +0.5s
				overchargeDelayEnd = timeReachedMax + perfectWindowDuration +0.5f;
			}

			// Overdraw handling
			float effectiveOverdraw = (overchargeDelayEnd >0f) ? overchargeDelayEnd : overdrawTime;
			if (elapsed >= effectiveOverdraw)
			{
				// auto-release as overdraw
				UpdateCursorWorld();
				FireChargedShot(Mathf.Clamp01(overdrawTime / maxChargeTime), lastCursorWorld, isOverdraw: true);
				StopCharge();
			}
		}

		// Release and fire on left mouse button up
		if (isCharging && Input.GetMouseButtonUp(0))
		{
			UpdateCursorWorld();
			float elapsed = Time.time - chargeStartTime;
			float chargeRatio = Mathf.Clamp01(elapsed / maxChargeTime);

			bool isPerfect = (timeReachedMax >0f) && (Time.time - timeReachedMax <= perfectWindowDuration);

			// Determine hit cases
			if (chargeRatio < minChargeToShoot)
			{
				// Not enough charge - do not shoot; maybe play a click sound
				StopCharge();
			}
			else if (isPerfect)
			{
				FireChargedShot(1f, lastCursorWorld, isPerfect: true);
				StopCharge();
			}
			else if (chargeRatio >=1f)
			{
				// Fully charged but missed perfect window - normal max
				FireChargedShot(1f, lastCursorWorld);
				StopCharge();
			}
			else
			{
				// Partial shot (>=50%)
				// If charge >=75%, treat as near-max for speed and lifetime
				if (chargeRatio >=0.75f)
				{
					FireChargedShot(chargeRatio, lastCursorWorld, isPartial: true, treatNearMax: true);
				}
				else
				{
					FireChargedShot(chargeRatio, lastCursorWorld, isPartial: true);
				}
				StopCharge();
			}
		}
	}

	private void StartCharge()
	{
		if (owner == null) return; // safety
		isCharging = true;
		chargeStartTime = Time.time;
		timeReachedMax = -1f;
		UpdateCursorWorld();
		CreateChargeUI();
		uiController?.Show();

		// Reset auto-eject timing
		overchargeStartTime = -1f;
		hasAutoEjected = false;
		overchargeDelayEnd = -1f;
	}

	private void StopCharge()
	{
		isCharging = false;
		chargeStartTime =0f;
		timeReachedMax = -1f;
		uiController?.Hide();

		// Stop pulse and reset auto-eject flags
		uiController?.StopOverchargePulse();
		overchargeStartTime = -1f;
		hasAutoEjected = false;
	}

	private void UpdateCursorWorld()
	{
		if (useCursorObject && cursorObject != null)
		{
			lastCursorWorld = cursorObject.transform.position;
		}
		else
		{
			if (Camera.main != null)
			{
				Vector3 mw = Camera.main.ScreenToWorldPoint(Input.mousePosition);
				mw.z =0f;
				lastCursorWorld = mw;
			}
			else
			{
				// fallback: use cursorObject if available
				lastCursorWorld = cursorObject != null ? cursorObject.transform.position : owner.transform.position;
			}
		}
	}

	// Replace UpdateChargeUI method with enhanced overcharge handling
	private void UpdateChargeUI(float chargeRatio)
	{
		if (uiController == null) return;
		float elapsed = Time.time - chargeStartTime;
		// compute whether overcharge is active. If a perfect window+delay is set (overchargeDelayEnd), overcharge only begins after that time.
		bool overchargeActive = false;
		if (elapsed > maxChargeTime)
		{
			if (overchargeDelayEnd >0f)
			{
				if (Time.time >= overchargeDelayEnd) overchargeActive = true;
			}
			else
			{
				overchargeActive = true;
			}
		}

		// start/stop pulse based on overchargeActive
		if (overchargeActive)
		{
			if (overchargeStartTime <0f)
			{
				overchargeStartTime = Time.time;
				uiController?.StartOverchargePulse();
			}
		}
		else
		{
			if (overchargeStartTime >=0f)
			{
				overchargeStartTime = -1f;
				uiController?.StopOverchargePulse();
			}
		}

		// compute overchargeRatio for UI feedback (use effective overdraw region)
		float overchargeRatio =0f;
		float effectiveOverdraw = (overchargeDelayEnd >0f) ? overchargeDelayEnd : overdrawTime;
		if (elapsed > maxChargeTime)
		{
			overchargeRatio = Mathf.InverseLerp(maxChargeTime, effectiveOverdraw, elapsed);
		}

		uiController.UpdateProgress(chargeRatio, overchargeRatio);

		// Only shake once we're in active overcharge region
		if (overchargeActive && overchargeStartTime >0f)
		{
			float overElapsed = Time.time - overchargeStartTime;
			float shakeIntensity = Mathf.Clamp01(overElapsed / overchargeAutoEjectAfter);
			uiController.ApplyShake(shakeIntensity *0.6f);
			// auto-eject handling
			if (!hasAutoEjected && overElapsed >= overchargeAutoEjectAfter)
			{
				// perform auto-eject at overdraw multiplier
				UpdateCursorWorld();
				// perform auto-eject and ensure we clear UI and stop further charging logic
				hasAutoEjected = true;
				uiController?.StopOverchargePulse();
				uiController?.Hide();
				// Fire with specific0.75x damage regardless of overdrawDamageMultiplier
				FireChargedShot(1f, lastCursorWorld, isOverdraw: true);
				// ensure stop charge state and require next input to begin charging again
				isCharging = false;
				chargeStartTime =0f;
				timeReachedMax = -1f;
				overchargeStartTime = -1f;
				overchargeDelayEnd = -1f;
				StopCharge();
			}
		}
	}

	private void CreateChargeUI()
	{
		if (uiController != null) return;
		if (owner == null) return; // safety: avoid null reference if owner not yet initialized
		// create a simple world-space UI under the player
		var go = new GameObject("ChargeUI");
		go.transform.SetParent(owner.transform, false);
		go.transform.localPosition = uiLocalOffset;
		go.transform.localRotation = Quaternion.identity;
		go.transform.localScale = Vector3.one *0.01f; // small world-space scale

		var canvas = go.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.WorldSpace;
		// only assign worldCamera if Camera.main is available
		if (Camera.main != null) canvas.worldCamera = Camera.main;

		// RectTransform may already exist (Canvas/Unity can add it). Use existing or add if missing.
		var rect = go.GetComponent<RectTransform>();
		if (rect == null) rect = go.AddComponent<RectTransform>();
		rect.sizeDelta = uiSize;

		var cg = go.AddComponent<CanvasGroup>();

		// Outline/background image
		var bg = new GameObject("BG");
		bg.transform.SetParent(go.transform, false);
		var bgRect = bg.AddComponent<RectTransform>();
		bgRect.anchorMin = new Vector2(0f,0f);
		bgRect.anchorMax = new Vector2(1f,1f);
		bgRect.offsetMin = Vector2.zero; bgRect.offsetMax = Vector2.zero;
		var bgImg = bg.AddComponent<Image>();
		bgImg.color = new Color(0f,0f,0f,0.5f);
		var outline = bg.AddComponent<Outline>();
		outline.effectColor = uiOutlineColor;

		// Fill image
		var fill = new GameObject("Fill");
		fill.transform.SetParent(go.transform, false);
		var fillRect = fill.AddComponent<RectTransform>();
		fillRect.sizeDelta = new Vector2(uiSize.x *0.98f, uiSize.y *0.9f);
		fillRect.anchoredPosition = Vector2.zero;
		var fillImg = fill.AddComponent<Image>();
		// Use Image.Type filled so fillAmount works
		fillImg.type = Image.Type.Filled;
		fillImg.fillMethod = Image.FillMethod.Horizontal;
		fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
		fillImg.fillAmount =0f;
		fillImg.color = uiFillColor;

		// Indicator
		var ind = new GameObject("Indicator");
		ind.transform.SetParent(go.transform, false);
		var indRect = ind.AddComponent<RectTransform>();
		indRect.sizeDelta = new Vector2(6f, uiSize.y *0.95f);
		indRect.anchoredPosition = new Vector2(-uiSize.x *0.5f,0f);
		var indImg = ind.AddComponent<Image>();
		indImg.color = Color.white;

		uiController = go.AddComponent<ChargeUIController>();
		uiController.Initialize(bgImg, fillImg, indRect, cg, uiFillColor, uiWarnColor, uiPerfectColor);
		uiController.HideInstant();

		// set GameObject and children to UI layer if it exists and ensure canvas draws on UI sorting layer above tiles
		int uiLayer = LayerMask.NameToLayer("UI");
		if (uiLayer >=0)
		{
			go.layer = uiLayer;
		}
		// also set canvas to override sorting so it draws on top
		canvas.overrideSorting = true;
		// prefer 'UI' sorting layer if it exists
		try
		{
			canvas.sortingLayerName = "UI";
		}
		catch { }
		canvas.sortingOrder =10000;

		// set all children to same layer for correct raycasting/rendering
		foreach (Transform t in go.transform)
		{
			if (uiLayer >=0) t.gameObject.layer = uiLayer;
		}
	}

	private void FireChargedShot(float chargeRatio, Vector3 cursorWorld, bool isOverdraw = false, bool isPartial = false, bool isPerfect = false, bool treatNearMax = false)
	{
		if (projectilePrefab == null)
		{
			Debug.LogWarning("RangerBehaviour: projectilePrefab not assigned.");
			return;
		}

		Vector3 spawnPos = (projectileSpawnPoint != null) ? projectileSpawnPoint.position : owner.transform.position;

		// cursorWorld.z already set to0 by UpdateCursorWorld
		Vector2 baseFireDir = (cursorWorld - spawnPos);
		if (baseFireDir.sqrMagnitude <=0.0001f)
		{
			baseFireDir = owner.transform.right;
		}
		baseFireDir.Normalize();

		float damageMul = Mathf.Lerp(1f, maxDamageMultiplier, chargeRatio);
		float speedMul = Mathf.Lerp(1f, maxSpeedMultiplier, chargeRatio);

		float projectileDamage = ProjectileBaseDamageFromClassOrFallback();
		float projectileSpeed = ProjectileBaseSpeedFromInspectorOrClass();

		// include class/owner damage multiplier from StatSystem so items that modify DamageMultiplier affect outgoing damage
		float damageMultiplier =1f;
		if (owner != null && owner.StatSystem != null)
		{
			float baseDamageMultiplier = (classData != null) ? classData.damageMultiplier :1f;
			damageMultiplier = owner.StatSystem.GetDamageMultiplier(baseDamageMultiplier);
		}
		else if (owner != null)
		{
			damageMultiplier = owner.DamageMultiplier;
		}

		float finalDamage = projectileDamage * damageMul * damageMultiplier;
		float finalSpeed = projectileSpeed * speedMul;
		float finalLifetime =5f; // default lifetime
		int pierce = -1; // default no override

		if (isOverdraw)
		{
			finalDamage *= overdrawDamageMultiplier;
		}
		else if (isPartial)
		{
			// partial shot: scale damage by chargeRatio, reduce speed and lifetime
			finalDamage = projectileDamage * chargeRatio * damageMultiplier;
			finalSpeed = projectileSpeed * partialSpeedMultiplier;
			finalLifetime = partialLifetime;
		}
		else if (isPerfect)
		{
			finalDamage *= perfectDamageMultiplier;
			pierce = Mathf.Max(1, perfectPierceCount);
		}
		else if (treatNearMax)
		{
			// near-max: use max speed multiplier and full lifetime, keep damage scaled by chargeRatio
			finalSpeed = projectileSpeed * maxSpeedMultiplier;
			finalLifetime =5f; // match default/max lifetime
			// scale damage as partial (based on chargeRatio)
			finalDamage = projectileDamage * chargeRatio * damageMultiplier;
		}

		// Determine how many projectiles to fire (StatSystem applies modifiers)
		int projectileCount =1;
		float spreadDegrees =0f;
		if (owner != null && owner.StatSystem != null)
		{
			projectileCount = Mathf.Max(1, owner.StatSystem.GetProjectileCount(1));
			// Experimental: get spread (degrees) from stat system; default15 degrees
			spreadDegrees = owner.StatSystem.GetModifiedStatFloat(15f, StatType.ProjectileSpread);
		}

		float critMult = classData != null ? classData.critMultiplier :1.5f;
		float critChance = owner != null ? owner.CritChance : (classData != null ? classData.Luck :0f);

		// Fire using helper: arrows ignore gravity and use kinematic movement
		// determine tint for perfect shot
		Color? tint = null;
		if (isPerfect)
		{
			tint = uiPerfectColor;
		}

		// Fire using helper: arrows ignore gravity and use kinematic movement
		FireProjectiles(
			projectilePrefab,
			spawnPos,
			baseFireDir,
			projectileCount,
			spreadDegrees,
			finalSpeed,
			finalDamage,
			critChance,
			critMult,
			classData != null ? classData.classType : owner.selectedClass,
			classData,
			destroyOtherProjectiles,
			usePhysicsOverride: false,
			gravityScaleOverride:0f,
			sequentialDelay:0.08f,
			lifetimeOverride: finalLifetime,
			pierceOverride: pierce,
			tintColor: tint
		);
	}

	// Helper to instantiate and initialize a single projectile given direction (kept for compatibility)
	private void FireSingleProjectile(Vector2 fireDir, Vector3 spawnPos, float speed, float damage)
	{
		bool isCrit = Random.Range(0f,100f) <= (owner != null ? owner.CritChance : (classData != null ? classData.Luck :0f));
		float critMult = classData != null ? classData.critMultiplier :1.5f;

		Debug.Log($"Firing projectile: dir={fireDir} isCrit={isCrit} finalDamage={damage}");

		var go = GameObject.Instantiate(projectilePrefab, spawnPos, Quaternion.identity);

		float angleDeg = Mathf.Atan2(fireDir.y, fireDir.x) * Mathf.Rad2Deg;
		var projScript = go.GetComponent<ProjectileBaseClassScript>();
		if (projScript != null && !projScript.spriteFacesRight)
		{
			angleDeg +=180f;
		}
		go.transform.rotation = Quaternion.Euler(0f,0f, angleDeg);

		var projRb = go.GetComponent<Rigidbody2D>();
		if (projRb != null)
		{
			projRb.gravityScale =0f;
			projRb.constraints = RigidbodyConstraints2D.FreezeRotation;
		}

		if (projScript != null)
		{
			projScript.Initialize(
				owner: owner.gameObject,
				worldDirection: fireDir,
				speedOverride: speed,
				damageOverride: damage,
				wasCritical: isCrit,
				critMultiplierOverride: critMult,
				ownerCls: classData != null ? classData.classType : owner.selectedClass,
				pierceOverride: -1,
				spriteOverride: null,
				classData: classData,
				destroyOtherProjectilesOverride: destroyOtherProjectiles,
				usePhysicsOverride: false,
				gravityScaleOverride:0f
			);

			projScript.SetDirection(fireDir);
		}
		else
		{
			Debug.LogWarning("RangerBehaviour: projectilePrefab does not contain ProjectileBaseClassScript.");
		}
	}

	// Rotate a2D vector by degrees
	private Vector2 RotateVector(Vector2 v, float degrees)
	{
		float rad = degrees * Mathf.Deg2Rad;
		float cos = Mathf.Cos(rad);
		float sin = Mathf.Sin(rad);
		return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
	}

	private float ProjectileBaseDamageFromClassOrFallback()
	{
		if (projectileBaseDamage >0f) return projectileBaseDamage;
		// Prefer primary stat-derived attack if available
		if (classData != null)
		{
			switch (classData.primaryStat)
			{
				case StatType.Strength: return classData.Strength;
				case StatType.Dexterity: return classData.Dexterity;
				case StatType.Intelligence: return classData.Intelligence;
				default: return 1f;
			}
		}
		if (owner != null) return owner.Attack;
		return 1f;
	}

	private float ProjectileBaseSpeedFromInspectorOrClass()
	{
		if (projectileBaseSpeed >0f) return projectileBaseSpeed;
		if (classData != null) return classData.speed;
		if (owner != null) return owner.moveSpeed;
		return 5f;
	}
}
