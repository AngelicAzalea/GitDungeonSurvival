using UnityEngine;

[DefaultExecutionOrder(-100)]
public class GameBootstrap : MonoBehaviour
{
 public static GameBootstrap Instance { get; private set; }

 [Header("Run Services")]
 public ItemInventory RunInventory;
 public StatSystem StatSystem;
 public UnlocksManager UnlocksManager;
 public RunManager RunManager;

 [Header("UI")]
 public Canvas UICanvas;
 public RunHUDController RunHUDController;

 private void Awake()
 {
 if (Instance != null && Instance != this)
 {
 Destroy(gameObject);
 return;
 }
 Instance = this;
 DontDestroyOnLoad(gameObject);

 // If StatSystem component not assigned, try to find on same GameObject
 if (StatSystem == null) StatSystem = GetComponent<StatSystem>();
 if (RunManager == null) RunManager = GetComponent<RunManager>();
 if (UnlocksManager == null) UnlocksManager = GetComponent<UnlocksManager>();
 if (RunInventory == null) RunInventory = Resources.Load<ItemInventory>("RunInventory");
 }
}
