using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class PlayerHealthContainerUI : MonoBehaviour
{
	[SerializeField] GameObject heartContainerPrefab;

	public void DrawHeartContainer(int CurrentHealth, int MaximumHealth)
	{
		//Calculate number of Heart Containers to draw
		int numContainers = MaximumHealth / 20; 


		// Clear existing heart containers
		foreach (Transform child in transform)
		{
			Destroy(child.gameObject);
		}

		// Draw new heart containers
		for (int i = 0; i < numContainers; i++)
		{
		   if (i + 1 <= numContainers)
			{
				GameObject HeartContainer = Instantiate(heartContainerPrefab, transform);
				HeartContainer.transform.SetParent(transform, false);
			}
		}
	}
}
