using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ByteStream = UdpKit.UdpStream;

public abstract class NetworkGameObject:MonoBehaviour {

    public int prefabId;
    public int networkId;

    public int owner;
    public int controller;

    public int stateType = 0;


    int interpolationBufferTime = 100;//ms
    //hold the packet for this time then pop it to our targetPos;
    //needs interpolation buffer, whatever that means.

    /// <summary>
    /// Called when this gameobject is created in the world via a network spawn (on both the caller and reciever)
    /// use this to set initial state data, spawn position, etc.  Any data you set here, will be used in the first 
    /// state udpate (if you're the server)
    /// </summary>
    public abstract void OnSpawn();

    public abstract void OnNetworkSend();

    public abstract int Peek();

    public abstract float Priority(ulong sendTo);
    //don't perfabId, networkId, owner, controller are serialized and deserialized automatically before calling these
    //serialize is called on the entity instance
    public abstract void Serialize(ByteStream stream);

    //deserialize is called on the prefab instance. You can NOT use instance properties in this method
    //they will cause errors because they will be the prefab values, NOT the entity values
    public abstract void Deserialize(ByteStream stream, int prefabId, int networkId, int owner, int controller);

    public abstract void OnStateUpdate(params object[] args);

    public virtual void Update() {
    //    SimulateOwner();
    }

    //public abstract void SimulateOwner();


    /// <summary>
    /// Are you the owner of this entity?
    /// </summary>
    /// <returns>true if you are the owner</returns>
    public bool isOwner() {
        return Core.net.me.connectionIndex == owner;
    }


    /// <summary>
    /// Are you the controller of this entity?
    /// </summary>
    /// <returns>true if you are the controller</returns>
    public bool isController() {
        //I'm wondering if we really need a distinction between owner and controller though.
        //Can you ever be the owner but not the controller?
        //That would mean it would still be in your entity list, but you are not the highest auth 
        //someone else would be sending state updates, and that shouldn't happen.
        //If you spawn an object, then someone else shows up with higher auth, you should pass ownership to them
        //means they get the spawn, and you remove it from your entity list (and they remove it from their copy of your entity list)
        //and you both add it to theirs.  Now they are in control, and send updates.
        //You can still send updates about that object

        //eg,There is a physics box, you want to push it.  A has higher auth, you are B.  THey are the owner.
        //you start touching the box and INSTANTLY take control of it on your client so you can
        //push it responsively.  You start sending updates to the owner about how you are pushing this box
        //after some time of inactivity with the box they take control back.
        //if there is a disagreement, about who has control, the owner decides.

        //we would need to do logic in Update to decide if you want to try and take control
        //and send the "takeControl" message along with hooking into
        //NetworkSendEvent when we have control to send the state updates

        //So yes, there is need for a distinction.
        return Core.net.me.connectionIndex == controller;
    }
}
    
    

