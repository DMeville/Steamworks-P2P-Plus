using Facepunch.Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

//
// This class takes care of a lot of stuff for you.
//
//  1. It initializes steam on startup.
//  2. It calls Update so you don't have to.
//  3. It disposes and shuts down Steam on close.
//
// You don't need to reference this class anywhere or access the client via it.
// To access the client use Facepunch.Steamworks.Client.Instance, see SteamAvatar
// for an example of doing this in a nice way.
//

//do we need a way to abstract this or will we be too locked into the steam ecosystem to be able to change it later?
//
public class SteamClient:MonoBehaviour {
    public uint AppId;

    private Facepunch.Steamworks.Client client;
    private Networking net;

    public void Start() {
        // keep us around until the game closes
        GameObject.DontDestroyOnLoad(gameObject);
        if(AppId == 0)
            throw new System.Exception("You need to set the AppId to your game");

        //
        // Configure us for this unity platform
        //
        Facepunch.Steamworks.Config.ForUnity(Application.platform.ToString());
        // Create the client
        client = new Facepunch.Steamworks.Client(506500);

        if(!client.IsValid) {
            client = null;
            Debug.Log("Couldn't initialize Steam");
            return;
        }

        Debug.Log("Steam Initialized: " + client.Username + " / " + client.SteamId);

        Core.net.RegisterMyConnection(client.SteamId);

        //Debug.Log("Registering Callbacks");
        client.Networking.SetListenChannel(0, true);
        client.Networking.OnP2PData = OnP2PData;
        client.Networking.OnIncomingConnection = OnIncommingConnection;
        client.Networking.OnConnectionFailed = OnConnectionFailed;
    }

    void Update() {
        if(client == null)
            return;

        try {
            UnityEngine.Profiling.Profiler.BeginSample("Steam Update");
            client.Update();
        } finally {
            UnityEngine.Profiling.Profiler.EndSample();
        }
    }

    public void OnP2PData(ulong steamID, byte[] bytes, int length, int channel) {
        Core.net.ReceiveP2PData(steamID, bytes, length, channel);
    }

    public bool OnIncommingConnection(ulong steamID) {
        //are they in the same lobby as us
        return true;// Core.net.ConnectRequestResponse(steamID);
    }

    public void OnConnectionFailed(ulong steamID, Networking.SessionError error) {
        Core.net.ConnectionFailed(steamID, error);
    }

    public void OnGUI() {
        if(client == null) return;


        if(GUILayout.Button("Send Connection Request to DMeville")) {
            //int a = 255;
            //byte b = (byte)a;
            //Debug.Log(b.ToStringBinary());
            Core.net.SendMessageImmediate(76561198030288857, 1); //same as passing in "ConnectRequest"
        }

        List<ulong> connectionsToClose = new List<ulong>();
        foreach(KeyValuePair<ulong, SteamConnection> k in Core.net.connections) {

            ulong steamid = k.Value.steamID;
            GUILayout.Label("Connection: " + steamid + " / " + client.Friends.GetName(steamid));
            string connectionState = "Disconnected";
            if(k.Value.connectionState.ConnectionActive == 1) {
                connectionState = "Connected";
            } else if(k.Value.connectionState.Connecting == 1) {
                connectionState = "Connecting";
            } else {
                //if we're disconnected from a timeout we should clear this connection from the connections list, shouldn't we?
                //do we need to handle cleanup of any of their objects too?  Only player objects, I think... everything else
                //should be persistant in the world...
            }

            GUILayout.Label(string.Format("State: {0} - Relay: {1} - Packets: {2} - Bytes:{3} - Ping:{4}", connectionState, k.Value.connectionState.UsingRelay, k.Value.connectionState.PacketsQueuedForSend, k.Value.connectionState.BytesQueuedForSend, k.Value.ping));
            GUILayout.Label(string.Format("In: {0} - Out: {1}", Core.net.bytesInPerSecond, Core.net.bytesOutPerSecond));
            GUILayout.BeginHorizontal();

            if(GUILayout.Button("Spawn Player")) {
                if(Core.net.me.playerEntity == null) {
                    GameObject go = Core.net.SpawnPrefab("Player");
                    Core.net.me.playerEntity = go.GetComponent<NetworkEntity>();
                }
            }

            if(GUILayout.Button("SendP2P")) {
                Debug.Log("Sending P2P");
                try {
                    UnityEngine.Profiling.Profiler.BeginSample("Send P2P");

                    //for(int i = 0; i < 50; i++) {
                    //    Core.net.QueueMessage(steamid, Core.net.GetMessageCode("TestState"), UnityEngine.Random.Range(0, 255), 2, 3, 4.0f, 3.324f, 354.00034f);
                    //}

                    Core.net.SpawnPrefab(UnityEngine.Random.Range(0, 3));
                    //Core.net.QueueMessage(Core.net.GetMessageCode("SpawnPrefab"), UnityEngine.Random.Range(0, 3), 100, 1, 1);
                    //Core.net.QueueMessage(Core.net.GetMessageCode("SpawnPrefab"), UnityEngine.Random.Range(0, 3), 1, 1);
                    
                    //Core.net.QueueMessage(steamid, 2, UnityEngine.Random.Range(0, 40));
                    //for(int i = 0; i < 10; i++) {
                    //    Core.net.SpawnPrefab(0);
                    //}
                    //might need a way to ensure messageType and the parameters match.
                    //pass in paramters with the register so we can check against the args list?
                    //idk
                    //for(int i = 0; i < 6; i++) {
                    //    Core.net.QueueMessage(Core.net.GetMessageCode("MessageTest" + i));
                    //}
                } finally {
                    UnityEngine.Profiling.Profiler.EndSample();
                }

            }

            if(GUILayout.Button("Log connection state")) {
                // could query this every few seconds...
                Networking.SteamP2PSessionState state = new Networking.SteamP2PSessionState();
                Debug.Log("State: " + client.Networking.GetSessionState(steamid, out state));
                Debug.Log("State.UsingRelay " + state.UsingRelay);
                Debug.Log("State.Connecting " + state.Connecting);
                Debug.Log("State.ConnectionActive " + state.ConnectionActive);
            }

            //we should try and close any session when we shutdown otherwise the next connection request
            //gets ignored and the packet just getes recieved directly
            if(GUILayout.Button("Close Connection")) {
                client.Networking.CloseSession(steamid);
                connectionsToClose.Add(steamid);
            }
            GUILayout.EndHorizontal();
        }

        for(int i = 0; i < connectionsToClose.Count; i++) {
            Core.net.RemoveConnection(connectionsToClose[i]);
        }
    }



    private void OnDestroy() {
        if(client != null) {
            //should we try to close any open connections?
            //client.Networking.CloseSession()
            //any clients should time out when closed...once we have keep-alive packets up and running
            if(Core.net != null) {
                Core.net.CloseConnectionsOnDestroy();
            }

            client.Dispose();
            client = null;
        }
    }
}
