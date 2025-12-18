using UnityEngine;

// Simple runner that clears run inventory on new run and persists it during a run
public class RunManager : MonoBehaviour
{
 [SerializeField] private ItemInventory runInventory;
 [SerializeField] private StatSystem statSystem;
 [SerializeField] private UnlocksManager unlocksManager;

 private void Awake()
 {
 // prefer injected bootstrap
 if (GameBootstrap.Instance != null)
 {
 runInventory = runInventory ?? GameBootstrap.Instance.RunInventory;
 statSystem = statSystem ?? GameBootstrap.Instance.StatSystem;
 unlocksManager = unlocksManager ?? GameBootstrap.Instance.UnlocksManager;
 }

 if (runInventory == null)
 runInventory = Resources.Load<ItemInventory>("RunInventory");

 if (statSystem == null)
 statSystem = FindObjectOfType<StatSystem>();

 // Initialize stat system with current run inventory so it doesn't rely on Resources.Load
 if (statSystem != null && runInventory != null)
 statSystem.Initialize(runInventory);

 if (unlocksManager == null)
 unlocksManager = FindObjectOfType<UnlocksManager>();
 }

 public void StartRun()
 {
 if (runInventory != null) runInventory.Clear();
 // notify stat system
 if (statSystem != null) statSystem.NotifyInventoryChanged();
 }

 public void EndRun()
 {
 // keep items or reset depending on design; for roguelike we clear on StartRun
 }

 // Allow GameManager or other systems to supply a runtime inventory instance (so we don't mutate asset files)
 public void SetRunInventory(ItemInventory inventory)
 {
 runInventory = inventory;
 if (statSystem != null && runInventory != null)
 statSystem.Initialize(runInventory);
 }

 public ItemInventory GetRunInventory() => runInventory;
}
