using Facepunch.Steamworks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ByteStream = UdpKit.UdpStream;
using Sirenix.OdinInspector;

/// <summary>
/// Not actually a connection, just the node of the connection (endpoint, or us)
/// </summary>
[System.Serializable]
public class SteamConnection {
    public ulong steamID;
    public int connectionIndex = 0; //lower authority, the better.  0 is "master" authority.
    public int zone = 0; //current level loaded into.

    public Networking.SteamP2PSessionState connectionState;
    public int ping = 0;//in ms
    public List<float> openPings = new List<float>();
    public float connectionEstablishedTime = 0f;
    public float timeSinceLastMsg = 0f; //send or rec

    //[HideInInspector]
    //public NetworkEntity[] entities; //entities this connection owns.  Everyone replicates this list, or tries to.
                                         //we need a readstream for all incoming data, and a write stream per packet. along with a message queue to each connection
    //[HideInPlayMode]
    public List<NetworkMessage> messageQueue = new List<NetworkMessage>();



    public SteamConnection() {
        if(Core.net == null) return; //this should only return true in the editor.  Odin does something weird that throws errors.
        //int max = Core.net.maxNetworkIds;
        //entities = new NetworkEntity[max];
    }
    
    //Returns true if you have higher auth than c, this means you're responisible for sending data to them
    //like state data or keep alives or whatever
    public bool HasAuthOver(SteamConnection c) {
        return connectionIndex < c.connectionIndex;
    }

    //starts the ping->pong->pung sequence so we can calculate ping
    //on both sides between this connection and me
    public void Ping() {
        //Core.net.SendMessage(steamID, "Ping");
        openPings.Add(Time.realtimeSinceStartup);
    }

    public bool inSameZone(SteamConnection c) {
        return zone == c.zone;
    }




}

