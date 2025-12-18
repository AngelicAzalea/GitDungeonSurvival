using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Pool;

[RequireComponent(typeof(Collider2D))]
public class ItemPickup : MonoBehaviour
{
	public ItemData item;
	[Tooltip("Optional UI prefab to show pickup notification (simple) ")]
	public GameObject pickupUIPrefab;

	private StatSystem statSystem;
	private UnlocksManager unlocksManager;

	// simple pool for UI instances
	private static ObjectPool<GameObject> uiPool;

	private void Awake()
	{
		statSystem = FindObjectOfType<StatSystem>();
		unlocksManager = FindObjectOfType<UnlocksManager>();
		EnsurePool();
	}

	private void EnsurePool()
	{
		if (uiPool != null) return;
		if (pickupUIPrefab == null) return;
		uiPool = new ObjectPool<GameObject>(
			() => Instantiate(pickupUIPrefab, GameObject.FindObjectOfType<Canvas>()?.transform ?? null),
			go => go.SetActive(true),
			go => go.SetActive(false),
			go => Destroy(go),
			true,
			5,
			20
		);
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		if (other.CompareTag("Player"))
		{
			if (statSystem != null && item != null)
			{
				statSystem.AddItem(item);
				ShowPickupUI();
				// Optionally unlock persistently (game design choice - comment out if not desired)
				if (unlocksManager != null && !string.IsNullOrEmpty(item.id)) unlocksManager.Unlock(item.id);
				Destroy(gameObject);
			}
		}
	}

	private void ShowPickupUI()
	{
		if (pickupUIPrefab == null || uiPool == null) return;
		var ui = uiPool.Get();
		// Expect ui has an Image and Text child tagged/known; simple hookup:
		var img = ui.GetComponentInChildren<Image>();
		var txt = ui.GetComponentInChildren<UnityEngine.UI.Text>();
		if (img != null) img.sprite = item.icon;
		if (txt != null) txt.text = item.itemName;
		// release after short time
		StartCoroutine(ReleaseAfterDelay(ui, 1.5f));
	}

	private System.Collections.IEnumerator ReleaseAfterDelay(GameObject go, float delay)
	{
		yield return new WaitForSeconds(delay);
		uiPool.Release(go);
	}
}
