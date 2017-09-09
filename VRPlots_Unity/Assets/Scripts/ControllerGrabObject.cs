using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//based on https://www.raywenderlich.com/149239/htc-vive-tutorial-unity

public class ControllerGrabObject : MonoBehaviour {

    private SteamVR_TrackedObject trackedObj;
    private GameObject collidingObject;
    private GameObject objectInHand;

    private SteamVR_Controller.Device Controller
    {
        get { return SteamVR_Controller.Input((int)trackedObj.index); }
    }

    void Awake()
    {
        trackedObj = GetComponent<SteamVR_TrackedObject>();
    }

    private void SetCollidingObject(Collider col)
    {
        Debug.LogFormat("SetCollidingObject: {0}", col.gameObject.name);
        if (collidingObject || !col.GetComponent<Rigidbody>())
            return;
        collidingObject = col.gameObject;
    }

    // Update is called once per frame
    void Update () {
        if (Controller.GetHairTriggerDown())
        {
            Debug.Log("Hair trigger down");
            if (collidingObject)
            {
                GrabObject();
            }
        }
        if (Controller.GetHairTriggerUp())
        {
            if (objectInHand)
            {
                ReleaseObject();
            }
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        Debug.Log("Trigger enter");
        SetCollidingObject(other);
    }

    public void OnTriggerStay(Collider other)
    {
        Debug.Log("Trigger stay");
        SetCollidingObject(other);
    }

    public void OnTriggerExit(Collider other)
    {
        Debug.Log("Trigger exit");
        if (!collidingObject)
            return;

        collidingObject = null;
    }

    private void GrabObject()
    {
        Debug.Log("Grabbing object");
        objectInHand = collidingObject;
        collidingObject = null;
        var joint = AddFixedJoint();
        joint.connectedBody = objectInHand.GetComponent<Rigidbody>();
    }

    private FixedJoint AddFixedJoint()
    {
        FixedJoint fx = gameObject.AddComponent<FixedJoint>();
        fx.breakForce = 20000;
        fx.breakTorque = 20000;
        return fx;
    }

    private void ReleaseObject()
    {
        if (GetComponent<FixedJoint>())
        {
            GetComponent<FixedJoint>().connectedBody = null;
            Destroy(GetComponent<FixedJoint>());
        }

        var velocityThreshold = 1f;
        var angularVelocityThreshold = 1.5f;
        Debug.LogFormat("Released with velocity: {0} and angVelocity {1}", Controller.velocity.magnitude, Controller.angularVelocity.magnitude);
        if (Controller.velocity.magnitude > velocityThreshold)
        {
            objectInHand.GetComponent<Rigidbody>().velocity = Controller.velocity;
            objectInHand.GetComponent<Rigidbody>().angularVelocity = Controller.angularVelocity;
        } else if (Controller.angularVelocity.magnitude > angularVelocityThreshold) {
            objectInHand.GetComponent<Rigidbody>().angularVelocity = Controller.angularVelocity;
        }

        objectInHand = null;
    }

    private void ReleaseObjectWithVelocity()
    {
        if (GetComponent<FixedJoint>())
        {
            GetComponent<FixedJoint>().connectedBody = null;
            Destroy(GetComponent<FixedJoint>());

            
        }
        objectInHand = null;
    }
}
