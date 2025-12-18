using UnityEngine;

// Skeleton enemy behaviour: ranged shooter when player in range
[RequireComponent(typeof(EnemyBaseClass))]
public class SkeletonBehaviour : EnemyBehaviour
{
	[Header("Ranged Settings")]
	[Tooltip("Seconds between shots")]
	public float fireCooldown = 1.2f;

	[Tooltip("Projectile prefab to spawn")]
	public GameObject projectilePrefab;

	[Tooltip("Projectile travel speed (units/sec). If <=0, prefab's speed is used")]
	public float projectileSpeed = 8f;

	[Tooltip("Projectile lifetime in seconds. If <=0, prefab's lifetime is used")]
	public float projectileLifetime = 5f;

	[Header("Damage")]
	[Tooltip("Multiplier applied to Owner.damage to compute projectile damage. Use1.0 to match owner damage.")]
	public float projectileDamageMultiplier = 1f;

	[Tooltip("Optional absolute projectile damage override. If >=0 this value is used instead of Owner.damage * projectileDamageMultiplier.")]
	public float projectileDamageOverride = -1f;

	[Header("Visuals")]
	[Tooltip("Spin speed applied to spawned projectiles (degrees per second). Set0 to disable spin.")]
	public float projectileSpinSpeed = 90f;

	private float lastFire = 0f;

	public override void Initialize(EnemyBaseClass owner)
	{
		base.Initialize(owner);
		// Keep inspector-specified behaviour settings; do not copy ranged settings from owner here.
		// Damage will be computed at fire time using Owner.damage and the multiplier/override.
	}

	public override void OnBehaviourUpdate()
	{
		if (Owner == null || Owner.playerTarget == null) return;
		float dist = Vector2.Distance(Owner.transform.position, Owner.playerTarget.position);
		if (dist <= Owner.attackRange)
		{
			float now = Time.time;
			if (now - lastFire >= fireCooldown)
			{
				lastFire = now;
				if (projectilePrefab != null)
				{
					Vector2 dir = (Owner.playerTarget.position - Owner.transform.position).normalized;
					var projGo = Object.Instantiate(projectilePrefab, Owner.transform.position, Quaternion.identity);
					var proj = projGo.GetComponent<ProjectileBaseClassScript>();
					if (proj != null)
					{
						// compute projectile damage: override if provided, otherwise owner damage * multiplier
						float computedDamage = projectileDamageOverride >= 0f ? projectileDamageOverride : (Owner != null ? Owner.damage * projectileDamageMultiplier : proj.damage);

						float speedToUse = projectileSpeed > 0f ? projectileSpeed : proj.speed;
						proj.Initialize(owner: Owner.gameObject, worldDirection: dir, speedOverride: speedToUse, damageOverride: computedDamage, ownerCls: null);

						if (projectileLifetime > 0f) proj.lifetime = projectileLifetime;

						// apply spin to the projectile sprite (degrees per second). Positive = clockwise
						proj.angularSpeed = projectileSpinSpeed;
					}
				}
			}
		}
	}
}
