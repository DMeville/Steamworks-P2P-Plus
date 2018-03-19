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
        NetworkManager.instance.RegisterMessageType("ConnectRequest", null, null, OnRecConnectRequest);
        NetworkManager.instance.RegisterMessageType("ConnectResponse", SConnectResponse, DConnectResponse, OnRecConnectRequestResponse);
        NetworkManager.instance.RegisterMessageType("KeepAlive", null, null, OnRecKeepAlive);
        NetworkManager.instance.RegisterMessageType("TestInt", STestInt, DTestInt, OnRecTestInt);
        NetworkManager.instance.RegisterMessageType("Ping", null, null, OnRecPing);
        NetworkManager.instance.RegisterMessageType("Pong", null, null, OnRecPong);
        NetworkManager.instance.RegisterMessageType("Pung", null, null, OnRecPung); //lulwut. Ping -> Pong -> Pung so we can get the ping on both sides we need two timestamps on each side. There must be a better way
    }

    //ConnectionResponse Serailize/Deserialize/Process methods
    //this message has two properties. 
    //arg[0] is a bool that says if our connect request was accepted (which is kind of redundant because we wouldn't get a response if it was rejected anyways)
    //arg[1] is an int telling us what connectionIndex the host is assigned assigned.
    //arg[2] is an int telling us what connectionIndex the host assigned us.
    private static byte[] SConnectResponse(int msgCode, params object[] args) {
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

        NetworkManager.instance.Process(sender, msgCode, arg0, arg1, arg2);
    }

   

    //test int S/D/P methods
    private static byte[] STestInt(int msgCode, params object[] args) {
        byte[] data = new byte[0];
        int arg0 = (int)args[0];
        OutputStream d = new OutputStream();
        d.WriteInt(arg0); //write it in the default range because this is just a test. sent as 32bit

        return d.GetBuffer() ;
    }

    private static void DTestInt(ulong sender, int msgCode, byte[] data) {
        InputStream d = new InputStream(data);

        int arg0 = d.ReadInt();
        NetworkManager.instance.Process(sender, msgCode, arg0);
    }

    private static void OnRecTestInt(ulong sender, params object[] args) {
        Debug.Log("OnRecTestInt: sender: " + sender + " int: " + args[0]);
    }
    // ----- 

    private static void OnRecKeepAlive(ulong sender, params object[] args) {
        if(NetworkManager.instance.connections.ContainsKey(sender)) {
            NetworkManager.instance.connections[sender].timeSinceLastMsg = 0f;
        }
    }

    //--
    private static void OnRecPing(ulong sender, params object[] args) {
        NetworkManager.instance.SendMessage(sender, "Pong");
        if(NetworkManager.instance.connections.ContainsKey(sender)) {
            NetworkManager.instance.connections[sender].openPings.Add(Time.realtimeSinceStartup);
        }
    }


    private static void OnRecPong(ulong sender, params object[] args) {
        //calculate ping
        if(NetworkManager.instance.connections.ContainsKey(sender)) {
            float pingSendTime = NetworkManager.instance.connections[sender].openPings[0];
            float pingRecTime = Time.realtimeSinceStartup;
            NetworkManager.instance.connections[sender].ping = (int)((pingRecTime - pingSendTime) * 1000f / 2f);
            //Debug.Log(string.Format("{0} - {1} - {2}", pingSendTime, pingRecTime, (int)((pingRecTime - pingSendTime)*1000f / 2f)));
            NetworkManager.instance.connections[sender].openPings.RemoveAt(0);
            NetworkManager.instance.SendMessage(sender, "Pung");
        }
    }

    private static void OnRecPung(ulong sender, params object[] args) {
        if(NetworkManager.instance.connections.ContainsKey(sender)) {
            float pingSendTime = NetworkManager.instance.connections[sender].openPings[0];
            float pingRecTime = Time.realtimeSinceStartup;
            NetworkManager.instance.connections[sender].ping = (int)((pingRecTime - pingSendTime) * 1000f / 2f);
            //Debug.Log(string.Format("{0} - {1} - {2}", pingSendTime, pingRecTime, (int)((pingRecTime - pingSendTime)*1000f / 2f)));
            NetworkManager.instance.connections[sender].openPings.RemoveAt(0);
        }
    }

    //---Internal message callbacks for things that need to be applied in the network manager
    private static void OnRecConnectRequest(ulong sender, params object[] args) {
        //no args
        Debug.Log("OnRecConnectionRequest sender: " + sender);
        //add the senders ID to our connections list
        NetworkManager.instance.connectionCounter++;
        NetworkManager.instance.RegisterConnection(sender, NetworkManager.instance.connectionCounter);

        NetworkManager.instance.QueueMessage(sender, "ConnectResponse", true, NetworkManager.instance.me.connectionIndex, NetworkManager.instance.connectionCounter);
        //send back a packet to the sender if we want to accept the connection, otherwise just ignore it.
    }

    private static void OnRecConnectRequestResponse(ulong sender, params object[] args) {
        Debug.Log("OnConnectionRequestResponse: sender: " + sender + ": accept:" + args[0] + " host cId: " + args[1] + " me cId: " + args[2]);
        NetworkManager.instance.RegisterConnection(sender, (int)args[1]);
        NetworkManager.instance.me.connectionIndex = (int)args[2];
        NetworkManager.instance.connectionCounter = NetworkManager.instance.me.connectionIndex;
    }
}
