using UnityEngine;

public class MouseFollow2D : MonoBehaviour
{
	private Vector3 mousePosition;
	public float moveSpeed = 0.3f;
	private float baseRotationSpeed = 1.0f;
	public float currentRotationSpeed = 1.0f;
	private Vector3 baseRotation;
	public Transform playerTarget;


	// Use this for initialization
	void Start()
	{
		
	}

	void OnMouseUp()
	{
	   
	}
	// Update is called once per frame
	void Update()
	{
		mousePosition = Input.mousePosition;

	   


		mousePosition = Camera.main.ScreenToWorldPoint(mousePosition);
	   
		transform.position = Vector2.Lerp(transform.position, mousePosition, moveSpeed);


		if (Input.GetMouseButton(0))
		{
			transform.Rotate(0, 0, currentRotationSpeed);
			currentRotationSpeed = currentRotationSpeed + 0.2f * Time.deltaTime;
		}
		else
		{
			currentRotationSpeed = baseRotationSpeed;
			transform.rotation = Quaternion.identity;
		}
	}

   //void FixedUpdate()
   //{
   //    if(isSpinning)
   //    {
   //        transform.Rotate(0, 0, currentRotationSpeed);
   //        currentRotationSpeed *= 1.1f * Time.deltaTime;
   //    }
   //    else if (!isSpinning)
   //    {
   //        currentRotationSpeed = baseRotationSpeed;
   //    }
   //}
   //

}
