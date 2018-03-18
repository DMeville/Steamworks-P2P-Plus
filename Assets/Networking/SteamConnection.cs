using Facepunch.Steamworks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Not actually a connection, just the node of the connection (endpoint, or us)
/// </summary>
[System.Serializable]
public class SteamConnection {
    public ulong steamID;
    public int connectionIndex = 0; //lower authority, the better.  0 is "master" authority.

    public Networking.SteamP2PSessionState connectionState;
    //we want the connection state here too... 
    //how do we get that... 
    public void Start() {
        //NetworkManager.instance.RegisterMessageType("ConnectAccept", S, D);
    }

    /// <summary>
    /// Serialize method.  This takes some data and seralizes it to a byte[] to send
    /// </summary>
    /// <param name="msgType"></param>
    /// <returns></returns>
    /// S needs to take in the data we want to seralize
    public byte[] S(int msgType, int intValue) {
        //seralize just the data, msgType is included in the header automatically
        
        return new byte[0];
    }

    ///this takes the byte and deseralizes it into a message
    ///ne need to know what to do with this data after though...
    public void D(int msgType, byte[] data) {
        //we know the message type so could we be like
        //since this is a ConnectRequest
        //we know this data needs to go to the 
        //NetworkManager.instance.RecConnectRequest();
        //return /* some data*/
    }

    //public void Process() {
    // maybe?
    //}
}

