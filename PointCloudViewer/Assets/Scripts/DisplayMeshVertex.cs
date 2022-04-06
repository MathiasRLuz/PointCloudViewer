using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisplayMeshVertex : MonoBehaviour
{
    BoxCollider boxCollider;
    BoxCollider _boxCollider;
    // Start is called before the first frame update
    void Start()
    {        
        foreach (Vector3 ponto in this.gameObject.GetComponent<MeshFilter>().mesh.vertices)
        {
            this.gameObject.AddComponent<BoxCollider>();
            _boxCollider = this.gameObject.GetComponent<BoxCollider>();
            _boxCollider.size = new Vector3(1, 1, 1);
            _boxCollider.center = new Vector3(ponto.x, ponto.y, ponto.z);
        }
        //this.gameObject.AddComponent<BoxCollider>();
        //boxCollider = this.gameObject.GetComponent<BoxCollider>();
        //Vector3 size = boxCollider.size;
        //Vector3 center = boxCollider.center; // ver ClosestPoint do boxcollider;
        //float meioZ = size.z / 2;

        //_boxCollider = this.gameObject.AddComponent<BoxCollider>();
        //_boxCollider.size = new Vector3(size.x,size.y,meioZ);
        //_boxCollider.center = new Vector3(center.x, center.y, center.z-meioZ/2);
        //_boxCollider = this.gameObject.AddComponent<BoxCollider>();
        //_boxCollider.size = new Vector3(size.x, size.y, meioZ);
        //_boxCollider.center = new Vector3(center.x, center.y, center.z + meioZ/2);
        //Destroy(boxCollider);

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
