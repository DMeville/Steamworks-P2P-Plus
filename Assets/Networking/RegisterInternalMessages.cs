using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OutputStream = BitwiseMemoryOutputStream;
using InputStream = BitwiseMemoryInputStream;

public class RegisterInternalMessages  {

    //this is just here to remove the message registering, and serialize/deserialize/process methods out of NetworkManager
    //to tidy it up a bit

    //all internal message stuff goes in this class.
    public static void Register() {
        Core.net.RegisterMessageType("ConnectRequest", null, null, OnRecConnectRequest);
        Core.net.RegisterMessageType("ConnectResponse", SConnectResponse, DConnectResponse, OnRecConnectRequestResponse);
        Core.net.RegisterMessageType("KeepAlive", null, null, OnRecKeepAlive);
        Core.net.RegisterMessageType("TestInt", STestInt, DTestInt, OnRecTestInt);
        Core.net.RegisterMessageType("Ping", null, null, OnRecPing);
        Core.net.RegisterMessageType("Pong", null, null, OnRecPong);
        Core.net.RegisterMessageType("Pung", null, null, OnRecPung); //lulwut. Ping -> Pong -> Pung so we can get the ping on both sides we need two timestamps on each side. There must be a better way
        Core.net.RegisterMessageType("SpawnPrefab", SSpawnPrefab, DSpawnPrefab, PSpawnPrefab);
        Core.net.RegisterMessageType("StateUpdate", SStateUpdate, DStateUpdate, PStateUpdate);


        Core.net.RegisterStateType("CubeState", CubeBehaviour.SerializeState, CubeBehaviour.DeserializeState);
    }

    //ConnectionResponse Serailize/Deserialize/Process methods
    //this message has two properties. 
    //arg[0] is a bool that says if our connect request was accepted (which is kind of redundant because we wouldn't get a response if it was rejected anyways)
    //arg[1] is an int telling us what connectionIndex the host is assigned assigned.
    //arg[2] is an int telling us what connectionIndex the host assigned us.
    private static byte[] SConnectResponse(ulong receiver, int msgCode, params object[] args) {
        //byte[] data = new byte[0];        

        //we need to know what data were are sending with each msg, but to keep it modular we need to cast to types here
        bool arg0 = (bool)args[0];
        int arg1 = (int)args[1];
        int arg2 = (int)args[2];

        //we need to write data in this method, and read it in the Deserialize method in the SAME ORDER.
        //if we do not, the data can't be read properly.
        //eg if we had multiple properties to seralize
        //WriteBool()    first
        //WriteString()  second
        //WriteInt()     third

        //then in deserialize
        //ReadBool()     first
        //ReadString()   second
        //ReadInt()      third

        //we also need to make sure we read/write with the same range (if any). 
        //If we write a [0,255] int we need to read it as a [0,255] too.  This saves space

        OutputStream d = new OutputStream();
        d.WriteBool(arg0);
        d.WriteInt(arg1, 0, 255); //values will be in the range of [0, 255] as they are player indicies, and shouldn't get that high up anyways so we can save some bits
        d.WriteInt(arg2, 0, 255);

        return d.GetBuffer();
    }

    private static void DConnectResponse(ulong sender, int msgCode, byte[] data) {
        InputStream d = new InputStream(data);

        bool arg0 = d.ReadBool();
        int arg1 = d.ReadInt(0, 255);
        int arg2 = d.ReadInt(0, 255);

        Core.net.Process(sender, msgCode, arg0, arg1, arg2);
    }

   

    //test int S/D/P methods
    private static byte[] STestInt(ulong receiver, int msgCode, params object[] args) {
        byte[] data = new byte[0];
        int arg0 = (int)args[0];
        OutputStream d = new OutputStream();
        d.WriteInt(arg0); //write it in the default range because this is just a test. sent as 32bit

        return d.GetBuffer() ;
    }

    private static void DTestInt(ulong sender, int msgCode, byte[] data) {
        InputStream d = new InputStream(data);

        int arg0 = d.ReadInt();
        Core.net.Process(sender, msgCode, arg0);
    }

    private static void OnRecTestInt(ulong sender, params object[] args) {
        Debug.Log("OnRecTestInt: sender: " + sender + " int: " + args[0]);
    }
    // ----- 

    private static void OnRecKeepAlive(ulong sender, params object[] args) {
        if(Core.net.connections.ContainsKey(sender)) {
            Core.net.connections[sender].timeSinceLastMsg = 0f;
        }
    }

    //--
    private static void OnRecPing(ulong sender, params object[] args) {
        Core.net.SendMessage(sender, "Pong");
        if(Core.net.connections.ContainsKey(sender)) {
            Core.net.connections[sender].openPings.Add(Time.realtimeSinceStartup);
        }
    }


    private static void OnRecPong(ulong sender, params object[] args) {
        //calculate ping
        if(Core.net.connections.ContainsKey(sender)) {
            float pingSendTime = Core.net.connections[sender].openPings[0];
            float pingRecTime = Time.realtimeSinceStartup;
            Core.net.connections[sender].ping = (int)((pingRecTime - pingSendTime) * 1000f / 2f);
            //Debug.Log(string.Format("{0} - {1} - {2}", pingSendTime, pingRecTime, (int)((pingRecTime - pingSendTime)*1000f / 2f)));
            Core.net.connections[sender].openPings.RemoveAt(0);
            Core.net.SendMessage(sender, "Pung");
        }
    }

    private static void OnRecPung(ulong sender, params object[] args) {
        if(Core.net.connections.ContainsKey(sender)) {
            float pingSendTime = Core.net.connections[sender].openPings[0];
            float pingRecTime = Time.realtimeSinceStartup;
            Core.net.connections[sender].ping = (int)((pingRecTime - pingSendTime) * 1000f / 2f);
            //Debug.Log(string.Format("{0} - {1} - {2}", pingSendTime, pingRecTime, (int)((pingRecTime - pingSendTime)*1000f / 2f)));
            Core.net.connections[sender].openPings.RemoveAt(0);
        }
    }

    //---Internal message callbacks for things that need to be applied in the network manager
    private static void OnRecConnectRequest(ulong sender, params object[] args) {
        //no args
        Debug.Log("OnRecConnectionRequest sender: " + sender);
        if(sender != Core.net.me.steamID) {
            //add the senders ID to our connections list
            Core.net.connectionCounter++;
            Core.net.RegisterConnection(sender, Core.net.connectionCounter);
        } else {
            //we're trying to connect to ourself.  We can do this in testing, but shouldn't do it live.
            //since we've already registred and incremented the connectionCounter for our local connection
            //don't do it again here.
        }

        Core.net.QueueMessage(sender, "ConnectResponse", true, Core.net.me.connectionIndex, Core.net.connectionCounter);
        //send back a packet to the sender if we want to accept the connection, otherwise just ignore it.

        
    }

    private static void OnRecConnectRequestResponse(ulong sender, params object[] args) {
        Debug.Log("OnConnectionRequestResponse: sender: " + sender + ": accept:" + args[0] + " host cId: " + args[1] + " me cId: " + args[2]);
        if(sender != Core.net.me.steamID) {
            Core.net.RegisterConnection(sender, (int)args[1]);
            Core.net.me.connectionIndex = (int)args[2];
            Core.net.connectionCounter = Core.net.me.connectionIndex;
        }

        Core.net.ConnectedToHost(sender);
    }


    private static byte[] SSpawnPrefab(ulong receiver, int msgCode, params object[] args) {
        Debug.Log("SSpawnPrefab");
        //these are the base methods we need for a prefab to spawn.
        //this doesn't send anything about the current data of the prefab (pos, rot, custom components, etc)
        //this should just send a request to spawn a prefab on the connected client
        int prefabId = (int)args[0];
        int networkId = (int)args[1];

        int owner = (int)args[2];
        int controller = (int)args[3];

        OutputStream o = new OutputStream();
        o.WriteInt(prefabId, 0, Core.net.maxPrefabs);
        o.WriteInt(networkId, 0, Core.net.maxNetworkIds);
        o.WriteInt(owner, 0, Core.net.maxPlayers);
        o.WriteInt(controller, 0, Core.net.maxPlayers);


        return o.GetBuffer();
        //need prefabId and a networkID, also needs to spawn the object on this client
        //and set him as the owner and controller
        //along with any initial values for this object? (pos, rot, custom binary data, idk)
    }

    private static void DSpawnPrefab(ulong sender, int msgCode, byte[] data) {
        if(sender == Core.net.me.steamID) return; //breakout early in the case that we sent this message to ourself.
        //we can do this while in testing, but if we don't breakout we get a server and client version of the "prefab"
        //when we are both.  Maybe it would be worth showing both copies for lag testing and junk, idk.
        Debug.Log("DSPawnPrefab");
        InputStream i = new InputStream(data);

        int prefabId = i.ReadInt(0, Core.net.maxPrefabs);
        int networkId = i.ReadInt(0, Core.net.maxNetworkIds);
        int owner = i.ReadInt(0, Core.net.maxPlayers);
        int controller = i.ReadInt(0, Core.net.maxPlayers);

        Core.net.Process(sender, msgCode, prefabId, networkId, owner, controller);
    }

    private static void PSpawnPrefab(ulong sender, params object[] args) {
        Debug.Log("PSpawnPrefab");
        int prefabId = (int)args[0];
        int networkId = (int)args[1];
        int owner = (int)args[2];
        int controller = (int)args[3];

        GameObject spawned = Core.net.SpawnPrefabInternal(prefabId, networkId, owner, controller);
    }

    private static byte[] SStateUpdate(ulong receiver, int msgCode, params object[] args) {
        //do we need to send owner? we're receiving data from someone who should always be the owner, I think?
        //so could just check connections[sender] to query the state update?
        //just do this for now, think about it later
        int owner = (int)args[0];
        int networkId = (int)args[1];
        int stateCode = (int)args[2];

        OutputStream o = new OutputStream();

        o.WriteInt(owner, 0, Core.net.maxPlayers);
        o.WriteInt(networkId, 0, Core.net.maxNetworkIds);
        o.WriteInt(stateCode, 0, Core.net.maxStates);



        //SerializeState(stateType, args[3], args[4], args[5]) //custom state serialize of three floats on the entity
        //byte[] stateData = Core.net.StateSerializeActions[stateType](args[3], args[4], args[5]);
        //
        return Core.net.SerializeState(receiver, msgCode, owner, networkId, stateCode, o, args);
    }

    private static void DStateUpdate(ulong sender, int msgCode, byte[] data) {
        InputStream i = new InputStream(data);
        int owner = i.ReadInt(0, Core.net.maxPlayers);
        int networkId = i.ReadInt(0, Core.net.maxNetworkIds);
        int stateCode = i.ReadInt(0, Core.net.maxStates);

        Core.net.DeserializeState(sender, msgCode, owner, networkId, stateCode, i);
        //byte[] stateData = i.ReadBytes()

        //Core.net.StateDeseralizeActions[stateType](sender, msgCode, owner, networkId, statetype, InputStream i) //so we can read from the current stream still
        //where we read in the ints, and call Core.net.Process on the data there..
        //we need this function as to act as an inbetween because we need to read in the networkId and stateType
        //in order to know what to do with this state data, and where to send it.

        //Core.net.Process(sender, msgCode, owner, networkId, stateType, x, y , z);
    }

    private static void PStateUpdate(ulong sender, params object[] args) {
        //find entity with network id and owner
        int owner = (int)args[0];
        int networkId = (int)args[1];
        int stateType = (int)args[2];
       
        //byte[] stateData = (byte[])args[3];

        if(Core.net.connections.ContainsKey(sender)) {
            Core.net.connections[sender].RecEntityUpdate(owner, networkId, stateType, args); 
        }
    }
}
