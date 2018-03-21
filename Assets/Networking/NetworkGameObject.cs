using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkGameObject : MonoBehaviour {

    public int networkId;
    public int prefabId;

    public int owner;
    public int controller;

    /// <summary>
    /// Called when this gameobject is created in the world via a network spawn (on both the caller and reciever)
    /// </summary>
    public void OnSpawn() {

    }

    //called whenever the network triggers a Core.net.NetworkSend()
    void NetworkSend() {
        //should pack the state
    }

    void Pack() {

    }

    void Unpack() {

    }
}
