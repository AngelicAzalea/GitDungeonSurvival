using UnityEngine;

// Base class for enemy behaviours. Attach derived behaviour components to enemy prefabs or let GameManager/EnemyBaseClass add them at runtime.
public abstract class EnemyBehaviour : MonoBehaviour
{
 public EnemyBaseClass Owner { get; private set; }

 public virtual void Initialize(EnemyBaseClass owner)
 {
 Owner = owner;
 }

 // Called from EnemyBaseClass.AttackPlayer()
 public virtual void AttackPlayer() { }

 // Optional per-frame update hook if behaviour needs to update independently
 public virtual void OnBehaviourUpdate() { }

 public virtual void OnSpawn() { }

 public virtual void OnReset() { }
}
