using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GetPointPosition : MonoBehaviour
{
    Ray _ray;
    RaycastHit _raycastHit;
    public Camera camera;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        _ray = camera.ScreenPointToRay(Input.mousePosition);
        if (Input.GetMouseButtonDown(0)) {
            //Debug.Log("CLICOU");
            if (Physics.Raycast(_ray, out _raycastHit)) {
                Debug.Log(_raycastHit.collider.ClosestPoint(_raycastHit.point));
            }
        }        
    }
}
