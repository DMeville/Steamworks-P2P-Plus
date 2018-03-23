using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class NetworkGameObject : MonoBehaviour {

    public int networkId;
    public int prefabId;

    public int owner;
    public int controller;

    public int stateType = 0;


    int interpolationBufferTime = 100;//ms
    //hold the packet for this time then pop it to our targetPos;
    //needs interpolation buffer, whatever that means.

    /// <summary>
    /// Called when this gameobject is created in the world via a network spawn (on both the caller and reciever)
    /// </summary>
    public virtual void OnSpawn() {}

    public virtual void OnStateUpdateReceived(int owner, int networkId, int stateType, params object[] args) {}
    
    //called whenever the network triggers a Core.net.NetworkSend();
    //or should we keep this internal to the object.  Would mean less looping as this is gunna get called 
    //manually anyways.
    //void NetworkSend() {}

    //return true if this object should send to this connection
    public virtual bool PriorityCheck(ulong sendTo) {
        return true;
    }

    ///don't use this.  This is just here so we can make copy it easily to a static method in the extended class
    //public virtual byte[] SerializeState(ulong receiver, int msgCode, int owner, int networkId, int stateCode, OutputStream stream, params object[] args) {
    //    return new byte[0];
    //}

    /////same as above
    //public virtual void DeserializeState(ulong sender, int msgCode, int owner, int networkId, int stateCode, InputStream stream) {
    //}

}
