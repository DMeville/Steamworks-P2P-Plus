using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class NetworkGameObject:MonoBehaviour {

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
    /// </summary>
    public virtual void OnSpawn() {

    }
}
    
    

