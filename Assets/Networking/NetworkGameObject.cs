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

    public abstract int Peek();

    public abstract float Priority(ulong sendTo);
    //don't perfabId, networkId, owner, controller are serialized and deserialized automatically before calling these
    //serialize is called on the entity instance
    public abstract void Serialize(ByteStream stream);

    //deserialize is called on the prefab instance. You can NOT use instance properties in this method
    //they will cause errors because they will be the prefab values, NOT the entity values
    public abstract void Deserialize(ByteStream stream, int prefabId, int networkId, int owner, int controller);

    public abstract void OnStateUpdate(params object[] args);
}
    
    

