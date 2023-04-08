using UnityEngine;


public class RaycastPoint : MonoBehaviour
{
	public Transform target1;
    void Start()
    {
		target1 = this.gameObject.transform;
	}

    void Update()
	{
		if (Input.GetMouseButton(0))
		{			
			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			RaycastHit hit;

			if (Physics.Raycast(ray, out hit))
			{
				if (hit.transform == target1)
				{
					Debug.Log("Hit target 1");
				}				
			}
			else
			{
				//Debug.Log("Hit nothing");
			}
		}
	}
}
