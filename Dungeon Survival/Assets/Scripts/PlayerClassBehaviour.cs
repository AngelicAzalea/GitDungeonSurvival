using System.Collections;
using UnityEngine;

// Base class for player class behaviours (Warrior, Ranger, Mage).
// Subclasses should implement class-specific input handling and stat application.
public abstract class PlayerClassBehaviour : MonoBehaviour
{
 protected CharacterInput owner;
 public CharacterClassData classData;

 // Called by CharacterInput during Start()
 public virtual void Initialize(CharacterInput owner)
 {
 this.owner = owner;
 if (classData == null && owner != null)
 {
 // prefer owner-assigned classData if present
 classData = owner.classData;
 }
 }

 // Called when owner stats are applied; allow behaviour to modify derived stats
 public virtual void ApplyStats() { }

 // Called each frame to handle class-specific input/abilities
 public virtual void HandleInput() { }

 // --- Shared projectile firing helper --------------------------------------------------
 // Fires `count` projectiles with optional fan `spreadDegrees`. If spreadDegrees>0 and count>1,
 // all projectiles are spawned simultaneously (shotgun). Otherwise, projectiles are spawned
 // sequentially with `sequentialDelay` between them.
 protected void FireProjectiles(
 GameObject prefab,
 Vector3 spawnPos,
 Vector2 baseDir,
 int count,
 float spreadDegrees,
 float speed,
 float damage,
 float critChance,
 float critMultiplier,
 CharacterClass ownerClass,
 CharacterClassData classData = null,
 bool destroyOtherProjectiles = false,
 bool? usePhysicsOverride = null,
 float? gravityScaleOverride = null,
 float sequentialDelay =0.08f,
 float? lifetimeOverride = null,
 int? pierceOverride = null,
 Color? tintColor = null)
 {
 if (prefab == null)
 {
 Debug.LogWarning("PlayerClassBehaviour.FireProjectiles: prefab is null");
 return;
 }

 if (baseDir.sqrMagnitude <=0.000001f)
 baseDir = Vector2.right;
 baseDir = baseDir.normalized;

 if (count <=1)
 {
 SpawnOne(prefab, spawnPos, baseDir, speed, damage, critChance, critMultiplier, ownerClass, classData, destroyOtherProjectiles, usePhysicsOverride, gravityScaleOverride, lifetimeOverride, pierceOverride, tintColor);
 return;
 }

 // If spread specified, spawn all projectiles instantly in a fan
 if (spreadDegrees >0f)
 {
 float step = (count >1) ? (spreadDegrees / (count -1)) :0f;
 float startAngle = -spreadDegrees *0.5f;
 for (int i =0; i < count; i++)
 {
 float angleOffset = (count ==1) ?0f : (startAngle + step * i);
 Vector2 dir = RotateVector(baseDir, angleOffset);
 SpawnOne(prefab, spawnPos, dir, speed, damage, critChance, critMultiplier, ownerClass, classData, destroyOtherProjectiles, usePhysicsOverride, gravityScaleOverride, lifetimeOverride, pierceOverride, tintColor);
 }
 return;
 }

 // Otherwise spawn sequentially with a short delay between each shot
 if (owner != null)
 {
 owner.StartCoroutine(FireSequentialCoroutine(prefab, spawnPos, baseDir, count, speed, damage, critChance, critMultiplier, ownerClass, classData, destroyOtherProjectiles, usePhysicsOverride, gravityScaleOverride, sequentialDelay, lifetimeOverride, pierceOverride, tintColor));
 }
 else
 {
 // fallback: spawn all immediately
 for (int i =0; i < count; i++)
 {
 SpawnOne(prefab, spawnPos, baseDir, speed, damage, critChance, critMultiplier, ownerClass, classData, destroyOtherProjectiles, usePhysicsOverride, gravityScaleOverride, lifetimeOverride, pierceOverride, tintColor);
 }
 }
 }

 private IEnumerator FireSequentialCoroutine(
 GameObject prefab,
 Vector3 spawnPos,
 Vector2 dir,
 int count,
 float speed,
 float damage,
 float critChance,
 float critMultiplier,
 CharacterClass ownerClass,
 CharacterClassData classData,
 bool destroyOtherProjectiles,
 bool? usePhysicsOverride,
 float? gravityScaleOverride,
 float delay,
 float? lifetimeOverride = null,
 int? pierceOverride = null,
 Color? tintColor = null)
 {
 for (int i =0; i < count; i++)
 {
 SpawnOne(prefab, spawnPos, dir, speed, damage, critChance, critMultiplier, ownerClass, classData, destroyOtherProjectiles, usePhysicsOverride, gravityScaleOverride, lifetimeOverride, pierceOverride, tintColor);
 yield return new WaitForSeconds(delay);
 }
 }

 // Instantiate and initialize a single projectile instance.
 protected void SpawnOne(
 GameObject prefab,
 Vector3 spawnPos,
 Vector2 dir,
 float speed,
 float damage,
 float critChance,
 float critMultiplier,
 CharacterClass ownerClass,
 CharacterClassData classData = null,
 bool destroyOtherProjectiles = false,
 bool? usePhysicsOverride = null,
 float? gravityScaleOverride = null,
 float? lifetimeOverride = null,
 int? pierceOverride = null,
 Color? tintColor = null)
 {
 bool isCrit = Random.Range(0f,100f) <= critChance;
 var go = GameObject.Instantiate(prefab, spawnPos, Quaternion.identity);
 var projScript = go.GetComponent<ProjectileBaseClassScript>();
 if (projScript != null)
 {
 // Ensure Initialize runs before OnEnable by activating after initialization.
 go.SetActive(false);

 // Initialize with provided parameters and pass destroyOtherProjectiles override so pooled projectiles don't leak state
 projScript.Initialize(
 owner: owner != null ? owner.gameObject : null,
 worldDirection: dir,
 speedOverride: speed,
 damageOverride: damage,
 wasCritical: isCrit,
 critMultiplierOverride: critMultiplier,
 ownerCls: ownerClass,
 pierceOverride: pierceOverride.HasValue ? pierceOverride.Value : -1,
 spriteOverride: null,
 classData: classData,
 usePhysicsOverride: usePhysicsOverride,
 gravityScaleOverride: gravityScaleOverride,
 destroyOtherProjectilesOverride: destroyOtherProjectiles,
 lifetimeOverride: lifetimeOverride
 );

 projScript.SetDirection(dir);

 // apply tint if requested
 if (tintColor.HasValue)
 {
 projScript.SetTint(tintColor.Value);
 }

 go.SetActive(true);
 }
 else
 {
 Debug.LogWarning("SpawnOne: prefab does not contain ProjectileBaseClassScript");
 }
 }

 protected ProjectileBaseClassScript SpawnOneAndGet(
 GameObject prefab,
 Vector3 spawnPos,
 Vector2 dir,
 float speed,
 float damage,
 float critChance,
 float critMultiplier,
 CharacterClass ownerClass,
 CharacterClassData classData = null,
 bool destroyOtherProjectiles = false,
 bool? usePhysicsOverride = null,
 float? gravityScaleOverride = null,
 float? lifetimeOverride = null,
 int? pierceOverride = null)
 {
 bool isCrit = Random.Range(0f,100f) <= critChance;
 var go = GameObject.Instantiate(prefab, spawnPos, Quaternion.identity);
 var projScript = go.GetComponent<ProjectileBaseClassScript>();
 if (projScript != null)
 {
 // Ensure Initialize runs before OnEnable by activating after initialization.
 go.SetActive(false);

 projScript.Initialize(
 owner: owner != null ? owner.gameObject : null,
 worldDirection: dir,
 speedOverride: speed,
 damageOverride: damage,
 wasCritical: isCrit,
 critMultiplierOverride: critMultiplier,
 ownerCls: ownerClass,
 pierceOverride: pierceOverride.HasValue ? pierceOverride.Value : -1,
 spriteOverride: null,
 classData: classData,
 usePhysicsOverride: usePhysicsOverride,
 gravityScaleOverride: gravityScaleOverride,
 destroyOtherProjectilesOverride: destroyOtherProjectiles,
 lifetimeOverride: lifetimeOverride
 );

 projScript.SetDirection(dir);

 go.SetActive(true);
 }
 else
 {
 Debug.LogWarning("SpawnOneAndGet: prefab does not contain ProjectileBaseClassScript");
 }
 return projScript;
 }

 // Utility: rotate a2D vector by degrees
 protected Vector2 RotateVector(Vector2 v, float degrees)
 {
 float rad = degrees * Mathf.Deg2Rad;
 float cos = Mathf.Cos(rad);
 float sin = Mathf.Sin(rad);
 return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
 }
}
