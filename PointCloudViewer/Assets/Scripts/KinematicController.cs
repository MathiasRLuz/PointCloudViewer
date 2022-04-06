using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KinematicController : MonoBehaviour
{    
    public int speed;
    public int rotateSpeed;
    private bool olhou = false;
    Rigidbody rigidbody;
    [SerializeField] PointCloudManager pointCloudManager;

    // Start is called before the first frame update
    void Start()
    {
        speed = 150;
        rotateSpeed = 20;
        rigidbody = GetComponent<Rigidbody>();        
    }

    // Update is called once per frame
    void Update()
    {
        if (pointCloudManager.loaded && !olhou)
        {
            olhou = true;
            //transform.LookAt(pointCloudManager.pointCloud.transform);
        }
        if (Input.GetKey(KeyCode.W))
        {
            if (Input.GetKey(KeyCode.LeftShift))
            {
                rigidbody.AddForce(new Vector3(0, speed * Time.deltaTime, 0));
            }
            else
            {
                rigidbody.AddForce(new Vector3(0, 0, speed * Time.deltaTime));
            }
        }
        if (Input.GetKey(KeyCode.S))
        {
            if (Input.GetKey(KeyCode.LeftShift))
            {
                rigidbody.AddForce(new Vector3(0, -speed * Time.deltaTime, 0));
            }
            else
            {
                rigidbody.AddForce(new Vector3(0, 0, -speed * Time.deltaTime));
            }
        }
        if (Input.GetKey(KeyCode.D))
        {
            rigidbody.AddRelativeForce(new Vector3(speed * Time.deltaTime, 0, 0));
        }
        if (Input.GetKey(KeyCode.A))
        {
            rigidbody.AddRelativeForce(new Vector3(-speed * Time.deltaTime, 0, 0));
        }
        if (Input.GetKey(KeyCode.Q))
        {
            rigidbody.AddTorque(new Vector3(0, -rotateSpeed * Time.deltaTime, 0));
        }
        if (Input.GetKey(KeyCode.E))
        {
            rigidbody.AddTorque(new Vector3(0, rotateSpeed * Time.deltaTime, 0));
        }
        if (Input.GetKey(KeyCode.Z))
        {
            rigidbody.AddRelativeTorque(new Vector3(-rotateSpeed * Time.deltaTime, 0, 0));
        }
        if (Input.GetKey(KeyCode.X))
        {
            rigidbody.AddRelativeTorque(new Vector3(rotateSpeed * Time.deltaTime, 0, 0));
        }
        if (Input.GetKeyDown(KeyCode.Equals))
        {
            speed += 10;
        }
        if (Input.GetKeyDown(KeyCode.Minus))
        {
            speed -= 10;
        }
    }
}
