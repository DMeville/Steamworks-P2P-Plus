using Facepunch.Steamworks;
using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.Linq;
using System;

using BitTools;
using ByteStream = UdpKit.UdpStream;

public class NetworkManager:SerializedMonoBehaviour {
    public static NetworkManager instance;

    public int networkSimulationRate = 20; //# of packets to send per second (at 60fps)
    private float networkSimulationTimer; //should this be frame based? maybe? idk for now just do it based on a timer
    private float _networkSimulationTimer; //should happen on fixedUpdate at least
    public float keepAliveTimer = 10f; //seconds

    public SteamConnection me;
    public Dictionary<ulong, SteamConnection> connections = new Dictionary<ulong, SteamConnection>();
    public int connectionCounter = 0;

    public int maxPlayers = 255; //connected at once. should be a power of 2 (-1). 2, 4, 8, 16, 32, 64, 128, 256.  etc.  Used for packing data, so leaving this at 255 when you only connect 4 players would be a huge waste.  don't  do that.
    public int maxPrefabs = 4095; //[0-maxPrefab] range for bitpacking.  The fewer prefabs we have, the better we can pack.  255 fits in 8 bits.  These are prefabs you can spawn over the network.
    public int maxNetworkIds = 4095; // sent over the net as (connectionNum) (networkId). since every client can spawn prefabs, this will ensure no overlap while not having to sync lists, or go through one player to ensure no overlap. 
    public int maxStates = 255; //separate state defintions.  Need to do lookups so we know how to read/write custom state data.
    private int networkIds = 0;
    public int packetSize = 1024 * 2; //(udpkit default, dunno)

    //per player, we clear between each queue.
    public ByteStream readStream = new ByteStream(new byte[1024 * 2]); //max packet size 1024 bytes? (not sure why x2, but that's how udpkit did it)
    public ByteStream writeStream = new ByteStream(new byte[1024 * 2]);

    //msgCodes
    public List<string> MessageCodes = new List<string>();
    public List<MessagePriorityCalculator> MessagePriorityCalculators = new List<MessagePriorityCalculator>();
    public List<MessageSerializer> MessageSerializers = new List<MessageSerializer>();
    public List<MessageDeserializer> MessageDeserializers = new List<MessageDeserializer>();
    public List<MessageProcessor> MessageProcessors = new List<MessageProcessor>();
    public List<MessagePeeker> MessagePeekers = new List<MessagePeeker>();

    public delegate float MessagePriorityCalculator(ulong receiver, params object[] args); //calculates the priority for this message
    public delegate void MessageSerializer(ulong receiver, ByteStream stream, params object[] args); //writes args to stream
    public delegate void MessageDeserializer(ulong sender, int msgCode, ByteStream stream); //reads args from stream (and then forwards that data to processor)
    public delegate void MessageProcessor(ulong sender, params object[] args); //does whatever with the data. Update state, notify a manager, etc
    public delegate int MessagePeeker(params object[] args); //peeks into the message to find out how many bits we need to write it. Used for packing

    //we need a priority system.  Every time a message in the queue is skipped we 
    //should we define a "Priority" Message delegate, that we can call to calculate the priority 
    //would need one for normal messages, and one for state replicate/entity events?
    //state messages should be cleared from the queue if they sit there too long
    //or maybe they can be replaced/updated or something with the "latest" data

    void Awake() {
        DontDestroyOnLoad(this.gameObject);
        instance = this;
        Core.net = this;

        networkSimulationTimer = (1f / 60f) * (60f / networkSimulationRate);
        //register internal message types
        //RegisterInternalMessages.Register(); //to tidy it up, moved all this stuff to a nother class

        readStream = new ByteStream(new byte[packetSize]); //max packet size 1024 bytes? (not sure why x2, but that's how udpkit did it)
        writeStream = new ByteStream(new byte[packetSize]);

        RegisterMessageType("Empty", null, null, null, null, null); //added to the end of the packet so we don't read to the end if we don't have to.
        RegisterMessageType("ConnectRequest", 
            null, 
            null, 
            null, 
            null, 
            MessageCode.ConnectRequest.Process);

        RegisterMessageType("ConnectRequestResponse", 
            MessageCode.ConnectRequestResponse.Peek, 
            null,  //doesn't need a priority because this can not be queued, because it's sent before the connection is established
            MessageCode.ConnectRequestResponse.Serialize, 
            MessageCode.ConnectRequestResponse.Deserialize, 
            MessageCode.ConnectRequestResponse.Process);

        RegisterMessageType("TestState", 
            MessageCode.TestState.Peek, 
            MessageCode.TestState.Priority, 
            MessageCode.TestState.Serialize, 
            MessageCode.TestState.Deserialize, 
            MessageCode.TestState.Process);

    }


    public int RegisterMessageType(string messageName, MessagePeeker peeker, MessagePriorityCalculator priority, MessageSerializer serializer, MessageDeserializer deserializer, MessageProcessor processor) {
        if(MessageCodes.Contains(messageName)) {
            Debug.LogException(new Exception("Can not register message type [" + messageName + "] because a message already exists with that name"));
        }
        MessageCodes.Add(messageName);
        MessageSerializers.Add(serializer);
        MessageDeserializers.Add(deserializer);
        MessageProcessors.Add(processor);
        MessagePeekers.Add(peeker);
        MessagePriorityCalculators.Add(priority);

        int msgId = MessageCodes.Count - 1;
        return msgId;
    }

    public int GetMessageCode(string messageName) {
        if(MessageCodes.Contains(messageName)) {
            return MessageCodes.IndexOf(messageName);
        }
        throw new Exception("Message with name [" + messageName + "] does not exist");
    }

    //some messages we want to send immediately. Like a connection request.  Can't queue it on a connection that doesn't exsist
    //use this in these cases.  Shouldn't be used for everything, as it's better to combine messages in the queue and sort them by
    //priority and then send them off.
    //this will send one packet per message you want to send.
    public void SendMessageImmediate(ulong sendTo, int msgCode, params object[] args) {
        writeStream.Reset(packetSize);

        SerializerUtils.WriteInt(writeStream, msgCode, 0, 255);
        if(MessageSerializers[msgCode] != null) {
            MessageSerializers[msgCode](sendTo, writeStream, args);
        } else {
            //no data to go with this message, just the msgCoe
        }

        SendP2PData(sendTo, writeStream.Data, writeStream.Position, Networking.SendType.Reliable, 0);
    }

    //queue message to go out in the next packet (will be priority filtering eventually)
    public void QueueMessage(ulong sendTo, string msgCode, params object[] args) {
        int iMsgCode = GetMessageCode(msgCode);
        QueueMessage(sendTo, iMsgCode, args);
    }

    public void QueueMessage(ulong sendTo, int msgCode, params object[] args) {
        //Debug.Log("[SEND] " + MessageCodes[msgCode]);
        //int dataSize = 0;
        //byte[] data = PackMessage(out dataSize, sendTo, msgCode, args);
        //SendP2PData(sendTo, data, dataSize, Networking.SendType.ReliableWithBuffering);        
        //SendP2PData(sendTo, data, data.Length);

        SteamConnection c = GetConnection(sendTo);
        if(c != null) {
            c.messageQueue.Add(new NetworkMessage(msgCode, args));
        }
    }
    
    public SteamConnection GetConnection(ulong steamId) {
        if(connections[steamId] != null) {
            return connections[steamId];
        } else {
            return null;
        }
    }

    /// <summary>
    /// walks through the message queue and packs the messages together and sends them off.
    /// later we can add priority filtering to the messages we add to the message queue, as it's just sorting the queued messages in some way.
    /// any messages that don't fit in this packet we leave until next time around (and increase their priority)
    /// maybe we should keep two message queues, one for entity updates or something
    /// </summary>
    public void NetworkSend() {
        //Debug.Log("NetworkSend");
        UnityEngine.Profiling.Profiler.BeginSample("Network Send");
        foreach(SteamConnection sc in connections.Values) {
            if(sc.messageQueue.Count > 0) {
                //pack
                writeStream.Reset(packetSize);

                //grab the first message
                //check if it can fit in the stream
                //if it can, remove it from the queue and write it
                //continue until writeStream can't fit any other messages
                //or until the message queue is empty

                //we should loop through all the messages and calculate their priority.
                //store this message list in a new list, and sort it by priority (or maybe just sort the queue, since we don't really need the original order?
                for(int i = 0; i < sc.messageQueue.Count; i++) {
                    NetworkMessage m = sc.messageQueue[i];
                    if(MessagePriorityCalculators[m.msgCode] != null) {
                        m.priority = MessagePriorityCalculators[m.msgCode](sc.steamID, m.args);
                    } else {
                        m.priority = 1f; //default priority for normal messages.  These will send *eventually* whenever there is space
                    }
                }

                //sort it by priority, with higher priority values first, lower last.
                sc.messageQueue = sc.messageQueue.OrderByDescending(p => p.priority + p.skipped).ToList();
            
                for(int i = 0; i < sc.messageQueue.Count; i++) {
                    //Debug.Log("queue count: " + sc.messageQueue.Count);
                    //Debug.Log("message: " + i);
                    
                    NetworkMessage m = sc.messageQueue[i];
                    if(!writeStream.CanWrite()) {
                        m.skipped++;
                        //Debug.Log("!writeStream.CanWrite()");
                        continue; //continue because we need to mark the rest of the messages as skipped.
                        //break; //ZERO room left m
                    } else {

                        if(m.priority == 0f) { //if the message has a priority of 0 we dont want to send the message 
                            sc.messageQueue.RemoveAt(i); //this will happen when we queue something to everyone, but 
                            i--;                        //some players are too far away to care about the message (in a different map, etc)
                            continue;                   //so it just gets discarded. 
                        }

                        //get the total message size to check if it will fit
                        int msgSize = 8; //8 bits for the msgCode included before the data
                        if(MessagePeekers[m.msgCode] != null) { //if it's null we don't have any data to send, just the msgCode (like for a keep alive)
                            msgSize += Core.net.MessagePeekers[m.msgCode](m.args);
                        }
                        //Debug.Log("CanWrite: " + msgSize + " : " + writeStream.CanWrite(msgSize));
                        if(writeStream.CanWrite(msgSize)) { //will it fit?
                            SerializerUtils.WriteInt(writeStream, m.msgCode, 0, 255); //write the msgCode
                            if(MessageSerializers[m.msgCode] != null) {
                                Core.net.MessageSerializers[m.msgCode](sc.steamID, writeStream, m.args); //write the rest of the data
                            }
                            //Debug.Log("Wrote: " + msgSize + " : new bit position: " + writeStream.Position);
                            //remove this message from the list.
                            sc.messageQueue.RemoveAt(i);
                            i--;
                        } else {
                            m.skipped++;
                            //Debug.Log("trying next message, can't fit..");
                            continue; //try the next message I guess? until the packet is full or we run out
                        }
                    }
                }

                //try and add a "StreamEmpty" message at the end if it fits
                //"there are no more messages" message. Stop trying to read any data after.
                //this isn't actually necessary, because since our Empty code is 00000000, and junk data after is 00000000
                //it picks up the empty automagically.  Since when we grab the message code, if it's junk data it's the empty msgCode!
                //if(writeStream.CanWrite() && writeStream.CanWrite(8)) {
                //    SerializerUtils.WriteInt(writeStream, GetMessageCode("Empty"), 0, 255); 
                //}

                SendP2PData(sc.steamID, writeStream.Data, writeStream.Position, Networking.SendType.Reliable, 0);
                //send it!
            }
        }
        UnityEngine.Profiling.Profiler.EndSample();
    }


    //wrapper
    public bool SendP2PData(ulong steamID, byte[] data, int length, Networking.SendType sendType = Networking.SendType.Reliable, int channel = 0) {
        if(connections.ContainsKey(steamID)) {
            connections[steamID].timeSinceLastMsg = 0f;
        } //otherwise we just haven't established the connection yet (as this must be a connect request message)
        Debug.Log("SendP2PData: " + length);
        return Client.Instance.Networking.SendP2PPacket(steamID, data, data.Length, sendType, channel);
    }

    //callback from SteamClient. Read the data and decide how to process it.
    public void ReceiveP2PData(ulong steamID, byte[] bytes, int length, int channel) {
        readStream = new UdpKit.UdpStream(bytes, length);
        Debug.Log("rec message");
        //string s = stream.ReadString();
        while(readStream.CanRead() && readStream.CanRead(8)) {
            int msgCode = (int)SerializerUtils.ReadInt(readStream, 0, 255);
            //Debug.Log("[REC] MessageCode: " + msgCode);
            if(msgCode == GetMessageCode("Empty")) {
                break; //no more data, the rest of this packet is junk
            } 

            if(MessageDeserializers[msgCode] != null) {
                MessageDeserializers[msgCode](steamID, msgCode, readStream);
            } else {
                MessageProcessors[msgCode](steamID); //process is usually called within the deserializer, but since we have no deserializer (because we have no data, just a msgCode), call it here.
            }
        }
    }

    //connection stuff below
    public void RegisterMyConnection(ulong steamID) {
        Debug.Log("Register my connection");
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

    public void OnConnectRequest(ulong sender) {
        if(sender != me.steamID) {
            connectionCounter++;
            RegisterConnection(sender, connectionCounter);
        } else {
            //we're trying to connect to ourself.  We can do this in testing, but shouldn't do it live.
            //since we've already registred and incremented the connectionCounter for our local connection
            //don't do it again here.
        }

        SendMessageImmediate(sender, GetMessageCode("ConnectRequestResponse"), me.connectionIndex, connectionCounter);
    }

    public void OnConnectRequestResponse(ulong sender, int senderIndex, int yourIndex) {
        if(sender != Core.net.me.steamID) {
            RegisterConnection(sender, senderIndex);
            me.connectionIndex = yourIndex;
            connectionCounter = yourIndex;
        }

        ConnectedToHost(sender);
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

    //called on a client when they successful connect to a host
    //here we can spawn our client player, or set up more data or something
    public void ConnectedToHost(ulong host) {
        //Debug.Log("ConnectedToHost:: SpawnPrefab");
        //for(int i = 0; i < 4; i++) {
            //Core.net.SpawnPrefab(0);
        //}
    }

    //Update just handles checking the session state of all current connections, and if anyone has timed out/disconnected
    //remove them from the connection list and do a bit of cleaup
    public void FixedUpdate() {
     
        _networkSimulationTimer -= Time.fixedDeltaTime;
        foreach(var kvp in connections) {
            kvp.Value.timeSinceLastMsg += Time.fixedDeltaTime;
        }
        //network loop
        if(_networkSimulationTimer <= 0f) {
            _networkSimulationTimer = networkSimulationTimer;
            List<ulong> disconnects = new List<ulong>();

            foreach(var kvp in connections) {
                SteamConnection c = kvp.Value;

                if(me.HasAuthOver(c)) {//only send keepalives if you're the responsible one in this relationship
                    if(c.timeSinceLastMsg >= keepAliveTimer) { //15 seconds?
                        c.Ping();
                    }
                }

                

                Facepunch.Steamworks.Client.Instance.Networking.GetSessionState(c.steamID, out c.connectionState);

                if(c.connectionState.ConnectionActive == 0 && c.connectionState.Connecting == 0) {
                    disconnects.Add(c.steamID);
                }
            }

            for(int i = 0; i < disconnects.Count; i++) {
                Disconnect(disconnects[i]);
            }

            NetworkSend();
        }
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
