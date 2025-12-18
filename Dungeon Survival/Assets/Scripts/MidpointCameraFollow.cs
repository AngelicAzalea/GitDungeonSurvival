using UnityEngine;

public class MidpointCameraFollow : MonoBehaviour
{
	public Transform target1; // Should be Player
	public Transform target2; // Should be Cursor
	public Vector2 offset;
	public Vector3 TrueCameraPos;
	public float smoothSpeed = 0.125f;
	public float CameraZoffset = -10.0f;
	public float MaxCameraDistanceFromPlayer = 2.0f;
	 


	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
	{

	}

	// Update is called once per frame
	void Update()
	{

	}

	void LateUpdate()
	{
		if (target1 == null || target2 == null)
		{
			Debug.LogWarning("Please assign both target objects in the Inspector");
			return;
		}

		Vector2 midpoint = (target1.position + target2.position) / 2f;

		Vector2 playerPosition = target1.position;

		Vector2 desiredPosition = midpoint + offset;

		

		Vector2 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
		transform.position = smoothedPosition;
		TrueCameraPos = transform.position;
		TrueCameraPos.z = CameraZoffset;
		transform.position = TrueCameraPos;
	}
}
