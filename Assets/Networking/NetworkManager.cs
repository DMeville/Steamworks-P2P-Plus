using Facepunch.Steamworks;
using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.Linq;
using System;

public class NetworkManager:SerializedMonoBehaviour {
    public static NetworkManager instance;

    public float updateTimer = 5f; //update session state every 5 seconds
    private float _updateTimer = 0f;

    public int networkSimulationRate = 20; //# of packets to send per second (at 60fps)
    private float networkSimulationTimer; //should this be frame based? maybe? idk for now just do it based on a timer
    private float _networkSimulationTimer; //should happen on fixedUpdate at least
    public float keepAliveTimer = 15f; //seconds

    public SteamConnection me;
    public Dictionary<ulong, SteamConnection> connections = new Dictionary<ulong, SteamConnection>();
    private int connectionCounter = 0;

    public List<string> MessageCodes = new List<string>();
    public List<SerializerAction> SerializeActions = new List<SerializerAction>();
    public List<DeserializerAction> DeserializeActions = new List<DeserializerAction>();
    public List<ProcessDeserializedDataAction> ProcessDeserializedDataActions = new List<ProcessDeserializedDataAction>();

    public delegate byte[] SerializerAction(int msgType, params object[] args);
    public delegate void DeserializerAction(ulong sender, int msgType, byte[] data);
    public delegate void ProcessDeserializedDataAction(ulong sender, params object[] args);

    void Awake() {
        DontDestroyOnLoad(this.gameObject);
        instance = this;

        networkSimulationTimer = (1f / 60f) * (60f / networkSimulationRate); 
        //register internal message types
        RegisterMessageType("ConnectRequest", null, null, OnRecConnectRequest);
        RegisterMessageType("ConnectResponse", SConnectResponse, DConnectResponse, OnRecConnectRequestResponse);
        RegisterMessageType("KeepAlive", null, null, OnRecKeepAlive);
        RegisterMessageType("TestInt", STestInt, DTestInt, OnRecTestInt);
        RegisterMessageType("Ping", null, null, OnRecPing); 
        RegisterMessageType("Pong", null, null, OnRecPong);
        //RegisterMessageType("ConnectResponse", SConnectResponse, DConnectionResponse, OnRecConnectionResponse);
        //RegisterMessageType("KeepAlive", SKeepAlive, DKeepAlive, OnRecKeepAlive);
    }

    #region Message Definition Helpers
    //Pass in null for serialize and deserialize if you have no data and just want to send a message id (for connect, keep alive, etc)
    public int RegisterMessageType(string messageName, SerializerAction serialize, DeserializerAction deserialize, ProcessDeserializedDataAction process) {
        MessageCodes.Add(messageName);
        SerializeActions.Add(serialize);
        DeserializeActions.Add(deserialize);
        ProcessDeserializedDataActions.Add(process);
        int messageId = MessageCodes.Count - 1;
        Debug.Log("Registered Message Type: " + messageName + " - id: " + messageId);
        return messageId;
    }

    public void Process(ulong sender, int msgType, params object[] args) {
        NetworkManager.instance.ProcessDeserializedDataActions[msgType](sender, args);
    }

    public byte[] Serialize(int msgId, params object[] args) {
        return SerializeActions[msgId](msgId, args);
    }

    public void Deserialize(ulong sender, int msgId, byte[] data) {
        DeserializeActions[msgId](sender, msgId, data);
    }
    #endregion


    public void OnRecConnectRequest(ulong sender, params object[] args) {
        //no args
        Debug.Log("OnRecConnectionRequest sender: " + sender);
        //add the senders ID to our connections list
        connectionCounter++;
        RegisterConnection(sender, connectionCounter);

        QueueMessage(sender, "ConnectResponse", true, me.connectionIndex, connectionCounter);
        //send back a packet to the sender if we want to accept the connection, otherwise just ignore it.
    }


    //ConnectionResponse Serailize/Deserialize/Process methods
    //this message has two properties. 
    //arg[0] is a bool that says if our connect request was accepted (which is kind of redundant because we wouldn't get a response if it was rejected anyways)
    //arg[1] is an int telling us what connectionIndex the host is assigned assigned.
    //arg[2] is an int telling us what connectionIndex the host assigned us.
    public byte[] SConnectResponse(int msgCode, params object[] args) {
        byte[] data = new byte[0];

        //we need to know what data were are sending with each msg, but to keep it modular we need to cast to types here
        bool arg0 = (bool)args[0]; 
        int arg1 = (int)args[1];
        int arg2 = (int)args[2];

        //we need to write data in this method, and read it in the Deserialize method in the SAME ORDER.
        //if we do not, the data can't be read properly.
        //eg if we had multiple properties to seralize
        //WriteBool()
        //WriteString()
        //WriteInt()

        //then in deserialize
        //ReadBool()
        //ReadString()
        //ReadInt()
        data = data.Append(SerializerUtils.WriteBool(arg0));
        data = data.Append(SerializerUtils.WriteInt(arg1));
        data = data.Append(SerializerUtils.WriteInt(arg2));
        return data;
    }

    public void DConnectResponse(ulong sender, int msgCode, byte[] data) {
        bool arg0 = SerializerUtils.ReadBool(ref data);
        int arg1 = SerializerUtils.ReadInt(ref data);
        int arg2 = SerializerUtils.ReadInt(ref data);
        NetworkManager.instance.Process(sender, msgCode, arg0, arg1, arg2);
    }

    public void OnRecConnectRequestResponse(ulong sender, params object[] args) {
        Debug.Log("OnConnectionRequestResponse: sender: " + sender + ": accept:" + args[0] + " host cId: " + args[1] + " me cId: " + args[2]);
        RegisterConnection(sender, (int)args[1]);
        me.connectionIndex = (int)args[2];
        connectionCounter = me.connectionIndex;
    }

    //test int S/D/P methods
    public byte[] STestInt(int msgCode, params object[] args) {
        byte[] data = new byte[0];
        int arg0 = (int)args[0];
        data = data.Append(SerializerUtils.WriteInt(arg0));
        return data;
    }

    public void DTestInt(ulong sender, int msgCode, byte[] data) {
        int arg0 = SerializerUtils.ReadInt(ref data);
        Process(sender, msgCode, arg0);
    }

    public void OnRecTestInt(ulong sender, params object[] args) {
        Debug.Log("OnRecTestInt: sender: " + sender + " int: " + args[0]);
    }
    // ----- 

    public void OnRecKeepAlive(ulong sender, params object[] args) {
        connections[sender].timeSinceLastMsg = 0f;
    }

    //--
    public void OnRecPing(ulong sender, params object[] args) {
        QueueMessage(sender, "Pong");
    }


    public void OnRecPong(ulong sender, params object[] args) {
        //calculate ping
        float pingSendTime = connections[sender].openPings[0];
        float pingRecTime = Time.realtimeSinceStartup;
        connections[sender].ping = (int)((pingRecTime - pingSendTime)*1000f / 2f);
        //Debug.Log(string.Format("{0} - {1} - {2}", pingSendTime, pingRecTime, (int)((pingRecTime - pingSendTime)*1000f / 2f)));
        connections[sender].openPings.RemoveAt(0);
    }
    //queue message to go out in the next packet (will be priority filtering eventually.
    public void QueueMessage(ulong sendTo, string msgCode, params object[] args) {
        int iMsgCode = GetMessageCode(msgCode);
        QueueMessage(sendTo, iMsgCode, args);
    }

    public void QueueMessage(ulong sendTo, int msgCode, params object[] args) {
        //just send it right NOW
        Debug.Log("[SEND] " + MessageCodes[msgCode]);
        byte[] data = PackMessage(msgCode, args);
        SendP2PData(sendTo, data, data.Length);
    }

    /// <summary>
    /// Combines msgCode and serialized message data (from args) into a byte[]
    /// </summary>
    public byte[] PackMessage(int msgCode, params object[] args) {
        if(msgCode > 255 || msgCode < 0) throw new Exception(string.Format("msgCode [{0}] is outside the accepted range of [0-255]", msgCode));
        byte[] data = new byte[1] { ((byte)msgCode) };
        if(SerializeActions[msgCode] != null) { //if we just want to send an "empty" message there is no serializer/deserializer
            byte[] msgData = Serialize(msgCode, args);
            data = data.Append(msgData);
        }
        return data;
    }

    /// <summary>
    /// shouldn't use this except for messages we want to send immediately (like connection requests/accepts or keep alives)
    /// Use QueueMessage instead.  It sends (by default) 20 packets per second.
    /// </summary>
    public void SendMessage(ulong sendTo, int msgCode, params object[] args) {
        //TODO.  Right now QueueMessage just sends the message immediately anyways.
    }

    public void SendMessage(ulong sendTo, string msgCode, params object[] args) {
        int iMsgCode = GetMessageCode(msgCode);
        SendMessage(sendTo, iMsgCode, args);
    }

    //wrapper
    public bool SendP2PData(ulong steamID, byte[] data, int length, Networking.SendType sendType = Networking.SendType.Reliable, int channel = 0) {
        if(connections.ContainsKey(steamID)) {
            connections[steamID].timeSinceLastMsg = 0f;
        } //otherwise we just haven't established the connection yet (as this must be a connect request message)
        return Client.Instance.Networking.SendP2PPacket(steamID, data, data.Length, sendType, channel);
    }

    //callback from SteamClient. Read the data and decide how to process it.
    public void ReceiveP2PData(ulong steamID, byte[] bytes, int length, int channel) {
        //first byte is the message code, the rest is the data for that message
        //messages are not combined, one message per packet (for now)
        byte[] msgCodeBytes = bytes.Take(1).ToArray();

        int msgCode = msgCodeBytes[0];
        Debug.Log("[REC]" + MessageCodes[msgCode]);

        byte[] msgData = null;
        if(DeserializeActions[msgCode] != null) {
           msgData = bytes.Skip(1).ToArray();
            NetworkManager.instance.Deserialize(steamID, msgCode, msgData);
        } else {
            NetworkManager.instance.Process(steamID, msgCode); //usually called in Deserialize, but since we have no data just forward the messageCode
        }
    }

    public int GetMessageCode(string messageName) {
        if(MessageCodes.Contains(messageName)) {
            return MessageCodes.IndexOf(messageName);
        }
        throw new Exception("Message with name [" + messageName + "] does not exist");
        return -1;
    }

    public void RegisterMyConnection(ulong steamID) {
        SteamConnection c = new SteamConnection();
        c.steamID = steamID;
        c.connectionIndex = 0;
        me = c;
    }

    public void RegisterConnection(ulong steamID, int playerNum = -1) {
        if(connections.ContainsKey(steamID)) return; //already in the list
        SteamConnection c = new SteamConnection();
        c.steamID = steamID;
        c.connectionIndex = playerNum;
        c.connectionEstablishedTime = Time.realtimeSinceStartup;
        connections.Add(c.steamID, c);
    }

    public void RemoveConnection(ulong steamID) {
        if(connections.ContainsKey(steamID)) {
            connections.Remove(steamID);
        }
    }

    //Highest Auth is the player with the lowest connectionIndex.  The host will have connectionIndex of 0
    //using an index instead of a bool here so I can have multiple authorities for seperated areas in the same game.
    //So that you find connected players in your area (map/region) and whoever has the highest auth sends state updates
    //to everyone else in the area. Similar to Destiny 2's physics hosts.
    public bool IsHighestAuth(SteamConnection sc) {
        int a = sc.connectionIndex;
        return connections.Any(s => s.Value.connectionIndex < a);
    }

    //This forces a disconnect with another player.
    //if they time out or leave or steam no longer detects an active connection state with them.
    public void Disconnect(ulong steamID) {
        RemoveConnection(steamID);
        Client.Instance.Networking.CloseSession(steamID);
        //cleanup stuff this player might have left behind, all player client owned networked objects instantiated on other clients?
    }

    //Connections fail for a few reasons, connection issues, steamID doesn't have the game running, etc...
    public void ConnectionFailed(ulong steamID, Networking.SessionError error) {
        Debug.Log("Connection Error: " + steamID + " - " + error);
    }

    //this is triggered when the first packet is sent to a connection.  The receiver is asked to accept or reject
    //the connection before receiving any messaages from them.  If we return false, we reject.
    //could add a check here to only accept a connection if we're in the same lobby (once we get lobby stuff working)
    public bool ConnectRequestResponse(ulong steamID) {
        Debug.Log("Incoming P2P Connection: " + steamID);
        return true;
    }

    //Update just handles checking the session state of all current connections, and if anyone has timed out/disconnected
    //remove them from the connection list and do a bit of cleaup
    public void FixedUpdate() {
        _updateTimer -= Time.deltaTime;
        if(_updateTimer <= 0f) {
            _updateTimer = updateTimer;
            List<ulong> disconnects = new List<ulong>();
            foreach(var c in connections) {
                Facepunch.Steamworks.Client.Instance.Networking.GetSessionState(c.Value.steamID, out c.Value.connectionState);

                if(c.Value.connectionState.ConnectionActive == 0 && c.Value.connectionState.Connecting == 0) {
                    disconnects.Add(c.Value.steamID);
                }
            }

            for(int i = 0; i < disconnects.Count; i++) {
                Disconnect(disconnects[i]);
            }
        }

        _networkSimulationTimer -= Time.fixedDeltaTime;
        foreach(var kvp in connections) {
            kvp.Value.timeSinceLastMsg += Time.fixedDeltaTime;
        }

        if(_networkSimulationTimer <= 0f) {
            _networkSimulationTimer = networkSimulationTimer;
            //SendMessages();

            foreach(var kvp in connections) {
                SteamConnection c = kvp.Value;
                if(me.HasAuthOver(c)) {//only send keepalives if you're the responsible one in this relationship
                    if(c.timeSinceLastMsg >= keepAliveTimer) { //15 seconds?
                        //NetworkManager.instance.QueueMessage(c.steamID, "Ping");
                        //c.openPings.Add(Time.realtimeSinceStartup);
                        NetworkManager.instance.QueueMessage(c.steamID, "KeepAlive");
                    }
                }
            }
            //we need a message queue per connection
            //and we need to know when was the last time we received anything from the connection
            //if we're the high
            //figure out if we need to send a keep alive packet
            //when was the last time we
        }
    }

    public void Simulate() {
        //loop through our message queue and try to pack everything into a packet and send it off
        //we should also simulate entities/state data here to auto-replicate to everyone? idk
    }

    //cleanup method.  Closes all sessions when we close the game, this makes it so 
    //other players don't have to wait for a timeout to be detected before removing you when you leave (in most cases)
    public void CloseConnectionsOnDestroy() {
        if(Client.Instance == null) return;
        foreach(var sc in connections) {
            Client.Instance.Networking.CloseSession(sc.Value.steamID);
        }
    }

    private void OnDestroy() {
        CloseConnectionsOnDestroy();
        NetworkManager.instance = null;
    }
}
