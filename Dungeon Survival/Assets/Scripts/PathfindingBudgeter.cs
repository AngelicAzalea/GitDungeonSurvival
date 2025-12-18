using System.Collections.Generic;
using UnityEngine;

public class PathfindingBudgeter : MonoBehaviour
{
	public static PathfindingBudgeter Instance { get; private set; }

	[Tooltip("Max A* local searches allowed per frame")]
	public int maxLocalAstarPerFrame =6;

	private int usedThisFrame =0;
	private Queue<System.Action> queuedRequests = new Queue<System.Action>();

	void Awake()
	{
		if (Instance != null && Instance != this) Destroy(this);
		Instance = this;
	}

	void LateUpdate()
	{
		// reset budget each frame and process queued requests up to budget
		usedThisFrame =0;
		int allowed = maxLocalAstarPerFrame;
		while (usedThisFrame < allowed && queuedRequests.Count >0)
		{
			var req = queuedRequests.Dequeue();
			req.Invoke();
			usedThisFrame++;
		}
	}

	public bool TryExecuteOrQueue(System.Action astarAction)
	{
		if (usedThisFrame < maxLocalAstarPerFrame)
		{
			astarAction.Invoke();
			usedThisFrame++;
			return true;
		}
		else
		{
			queuedRequests.Enqueue(astarAction);
			return false;
		}
	}
}
