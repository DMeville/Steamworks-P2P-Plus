﻿using Facepunch.Steamworks;
using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.Linq;
using System;

using BitTools;
using ByteStream = UdpKit.UdpStream;
using UnityEngine.SceneManagement;

public class NetworkManager:SerializedMonoBehaviour {
    public static NetworkManager instance;

    public int networkSimulationRate = 20; //# of packets to send per second (at 60fps)
    private float networkSimulationTimer; //should this be frame based? maybe? idk for now just do it based on a timer
    private float _networkSimulationTimer; //should happen on fixedUpdate at least
    public float keepAliveTimer = 10f; //seconds

    public int maxPlayers = 255; //connected at once. should be a power of 2 (-1). 2, 4, 8, 16, 32, 64, 128, 256.  etc.  Used for packing data, so leaving this at 255 when you only connect 4 players would be a huge waste.  don't  do that.
    public int maxMessageTypes = 255; //does not include states.  States use maxPrefabs
    public int maxMessagesQueued = 255; //how many messages can be queued until we stop (so things don't spiral out too badly)
    public int maxPrefabs = 4095; //[0-maxPrefab] range for bitpacking.  The fewer prefabs we have, the better we can pack.  255 fits in 8 bits.  These are prefabs you can spawn over the network.
    public int maxNetworkIds = 4095; // sent over the net as (connectionNum) (networkId). since every client can spawn prefabs, this will ensure no overlap while not having to sync lists, or go through one player to ensure no overlap. 
    ///zones = scenes.  Zones are used to make sure players don't get events for an object in a difference scene.
    //eg, player A is in zone 1, player b is in zone 2. Player A is trying to send an entity (for some reason) to b, so b
    //rejects it because they're in different zones
    //woldPos stuff is using in priorities, and is the most recent position a player was at.
    //set min/max/precision to compress values better.
    //this is like 3 bytes per player every send (~1 send per second) so pretty small.
    public float SendConnectionMetadataTimer = 1f;
    private float _sendConnectionMetadataTimer = 1f;
    public int maxZones = 31;
    public Vector3 minWorldPos = new Vector3(-1000, -1000, -1000);
    public Vector3 maxWorldPos = new Vector3(1000, 1000, 1000);
    public float worldPosPrecision = 1f;

    //public int maxStates = 255; //separate state defintions.  Need to do lookups so we know how to read/write custom state data.
    private int networkIds = 0;
    public int packetSize = 1024 * 2; //(udpkit default, dunno)


    public bool measureBandwidth = false; //measuring bandwidth isn't _slow_ or anything, just might not be something you need/want
    public float bandwidthBuffer = 1f; //how long (in seconds) to keep data in the bandwidthBuffer for measuring in/out. A larger time means it's averaged over that time.  1s will be erratic but more accurate, 10s will be smoother

    //[HideInInspector]
    public List<BandwidthData> bitsInBuffer = new List<BandwidthData>();
    //[HideInInspector]
    public List<BandwidthData> bitsOutBuffer = new List<BandwidthData>();

    //public int bitsIn = 0;
    //public int bitsOut = 0;

    public int bytesInPerSecond = 0;
    public int bytesOutPerSecond = 0;

    public List<NetworkEntity> registerPrefabsOnStart = new List<NetworkEntity>();

    public SteamConnection me;
    public Dictionary<ulong, SteamConnection> connections = new Dictionary<ulong, SteamConnection>();
    public Dictionary<int, NetworkEntity[]> entities = new Dictionary<int, NetworkEntity[]>();
    public int connectionCounter = 0;

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

    //prefab stuff
    public List<string> NetworkPrefabNames = new List<string>();
    public List<NetworkEntity> NetworkPrefabs = new List<NetworkEntity>();

    public Action NetworkSendEvent;    

    //STATES
    //needs a way to update queued messages with the most recent data right before they go out. (not important for now, but would be nice, no reason to queue two messages, only need the one with the latest data (unless we need it for interpolation?) idk
    
  

    void Awake() {
        if(instance != null) {
            DestroyImmediate(this.gameObject);
            return;
        }
        DontDestroyOnLoad(this.gameObject);
        instance = this;
        Core.net = this;

        networkSimulationTimer = GetNetworkSimulationTimer();
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


        RegisterMessageType("EntityUpdate",
            MessageCode.EntityUpdate.Peek,
            MessageCode.EntityUpdate.Priority,
            MessageCode.EntityUpdate.Serialize,
            MessageCode.EntityUpdate.Deserialize,
            MessageCode.EntityUpdate.Process);

        RegisterMessageType("EntityDestroyRequest", //request event from client -> owner asking to destroy this object
            MessageCode.EntityDestroy.Peek,
            MessageCode.EntityDestroy.Priority,
            MessageCode.EntityDestroy.Serialize,
            MessageCode.EntityDestroy.Deserialize,
            MessageCode.EntityDestroy.ProcessDestroyRequest);

        RegisterMessageType("EntityDestroy", //request event from client -> owner asking to destroy this object
            MessageCode.EntityDestroy.Peek,
            MessageCode.EntityDestroy.Priority,
            MessageCode.EntityDestroy.Serialize,
            MessageCode.EntityDestroy.Deserialize,
            MessageCode.EntityDestroy.Process);

        RegisterMessageType("EntityScopeRequest", //asking the controller "Do I care about this still? 
            MessageCode.EntityScopeRequest.Peek, //I haven't got an update in a while.  is there just nothing to send me?
            MessageCode.EntityScopeRequest.Priority, //or have you deleted this object, and can I now delete it too?
            MessageCode.EntityScopeRequest.Serialize,
            MessageCode.EntityScopeRequest.Deserialize,
            MessageCode.EntityScopeRequest.Process);

        RegisterMessageType("EntityScopeResponse", //Yes you can delete it, or no, don't delete, there is just no data to send but this entity should stay alive
            MessageCode.EntityScopeResponse.Peek,
            MessageCode.EntityScopeResponse.Priority,
            MessageCode.EntityScopeResponse.Serialize,
            MessageCode.EntityScopeResponse.Deserialize,
            MessageCode.EntityScopeResponse.Process);

        RegisterMessageType("EntityChangeOwner", //TODO
            MessageCode.EntityChangeOwner.Peek,
            MessageCode.EntityChangeOwner.Priority,
            MessageCode.EntityChangeOwner.Serialize,
            MessageCode.EntityChangeOwner.Deserialize,
            MessageCode.EntityChangeOwner.Process);

        RegisterMessageType("EntityControlRequest", //9) I'm trying to take control of this entity. 
            MessageCode.EntityControlRequest.Peek,
            MessageCode.EntityControlRequest.Priority,
            MessageCode.EntityControlRequest.Serialize,
            MessageCode.EntityControlRequest.Deserialize,
            MessageCode.EntityControlRequest.Process);

        RegisterMessageType("EntityControlResponse", //10) I'm trying to take control of this entity. 
            MessageCode.EntityControlResponse.Peek,
            MessageCode.EntityControlResponse.Priority,
            MessageCode.EntityControlResponse.Serialize,
            MessageCode.EntityControlResponse.Deserialize,
            MessageCode.EntityControlResponse.Process);

        RegisterMessageType("SetConnectionData", //used to set connection metadata for all players. (zone and player pos)
            MessageCode.SetConnectionData.Peek,
            MessageCode.SetConnectionData.Priority,
            MessageCode.SetConnectionData.Serialize,
            MessageCode.SetConnectionData.Deserialize,
            MessageCode.SetConnectionData.Process);
        

        for(int i = 0; i < registerPrefabsOnStart.Count; i++) {
            RegisterPrefab(registerPrefabsOnStart[i].gameObject.name, registerPrefabsOnStart[i]);
        }

        LoadScene(2);
    }

    //returns how many seconds per network tick (networkSimulation rate of 20 => 50ms, or one tick ever ~ 3 fixedUpdates
    public float GetNetworkSimulationTimer() {
        return (1f / 60f) * (60f / networkSimulationRate);
    }

    //private int RegisterStateType(string stateName, StatePeeker peeker, StatePriorityCalculator priority, StateSerializer serializer, StateDeserializer deserializer) {
    //    if(StateCodes.Contains(stateName)) {
    //        Debug.LogException(new Exception("Can not register state type [" + stateName + "] because a state already exists with that name"));
    //    }
    //    StateCodes.Add(stateName);
    //    StateSerializers.Add(serializer);
    //    StateDeserializers.Add(deserializer);
    //    StatePeekers.Add(peeker);
    //    StatePriorityCalculators.Add(priority);

    //    int stateId = StateCodes.Count - 1;
    //    return stateId;
    //}

    //public int GetStateCode(string stateName) {
    //    if(StateCodes.Contains(stateName)) {
    //        return StateCodes.IndexOf(stateName);
    //    }
    //    throw new Exception("State with name [" + stateName + "] does not exist");
    //}

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
        if(msgId > maxMessageTypes) {
            Debug.LogException(new Exception("Message type [" + messageName + "] was registered outside the max range [" + maxMessageTypes + "]. Using this message will cause problems"));
        }
        return msgId;
    }

    public int GetMessageCode(string messageName) {
        if(MessageCodes.Contains(messageName)) {
            //Debug.Log("MessageCode [" + messageName + "] : [" + MessageCodes.IndexOf(messageName) + "]");
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

        SerializerUtils.WriteInt(writeStream, msgCode, 0, maxMessageTypes);
        if(MessageSerializers[msgCode] != null) {
            MessageSerializers[msgCode](sendTo, writeStream, args);
        } else {
            //no data to go with this message, just the msgCoe
        }

        SendP2PData(sendTo, writeStream.Data, writeStream.Position, Networking.SendType.Reliable, 0);
    }


    //queues the message to every connected client (not including yourself)
    public void QueueMessage(string msgCode, params object[] args) {
        QueueMessage(GetMessageCode(msgCode), args);

    }

    //queues the message to every connected client (not including yourself)
    public void QueueMessage(int msgCode, params object[] args) {
        foreach(SteamConnection s in connections.Values) {
            QueueMessage(s.steamID, msgCode, args);
        }
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
            NetworkMessage msg = new NetworkMessage(msgCode, args);
            c.messageQueue.Add(msg);
        }
    }

    //sends an entity event to all connections
    //returns true if the message was queued, false if the queue was full
    public bool QueueEntityMessage(string msgCode, NetworkEntity entity, params object[] args) {
        return QueueEntityMessage(GetMessageCode(msgCode), entity, args);
    }

    public bool QueueEntityMessage(int msgCode, NetworkEntity entity, params object[] args) {
        //only queue the message if all connections have room in their queue for it
        bool allHaveRoom = true;
        foreach(SteamConnection c in connections.Values) {
            if(c.messageQueue.Count >= maxMessagesQueued) {
                allHaveRoom = false;
                break;
            }
        }

        if(!allHaveRoom) return false;
        foreach(SteamConnection s in connections.Values) {
            QueueEntityMessage(s.steamID, msgCode, entity, args);
        }
        return true;
    }
    public bool QueueEntityMessage(ulong sendTo, string msgCode, NetworkEntity entity, params object[] args) {
        return QueueEntityMessage(sendTo, GetMessageCode(msgCode), entity, args);
    }

    public bool QueueEntityMessage(ulong sendTo, int msgCode, NetworkEntity entity, params object[] args) {
        SteamConnection c = GetConnection(sendTo);
        if(c != null) {
            if(c.messageQueue.Count < maxMessagesQueued) {
                NetworkMessage msg = new NetworkMessage(msgCode, args);
                msg.entity = entity;
                msg.isEntityMsg = true;
                c.messageQueue.Add(msg);
                entity.queuedMessage[sendTo] = msg; //straight overwrites, so make sure you handle this interally if it's not null
                return true;
            } else {
                return false;
            }
        } else {
            return false;
        }
    }

    

    //prefab stuff
    public int RegisterPrefab(string prefabName, NetworkEntity prefab) {
        if(NetworkPrefabs.Count + 1 > maxPrefabs) {
            Debug.LogException(new Exception("Can not register prefab [" + prefabName + "] - Prefab limit has been reached"));
        }
        if(NetworkPrefabNames.Contains(prefabName)) {
            Debug.LogException(new Exception("Can not register prefab [" + prefabName + "] because a prefab already exists with that name"));
        }
        NetworkPrefabs.Add(prefab);
        NetworkPrefabNames.Add(prefabName);
        int prefabId = NetworkPrefabs.Count - 1;
        Debug.Log("Registered Prefab: " + prefabName + " - id: " + prefabId);

        return prefabId;
    }

    //Gets the prefabId by name.  Use this like Core.net.SpawnPrefab(Core.net.GetPrefabId("MyPrefab"))
    public int GetPrefabId(string prefabName) {
        if(NetworkPrefabNames.Contains(prefabName)) {
            return NetworkPrefabNames.IndexOf(prefabName);
        }
        throw new Exception("Prefab with name [" + prefabName + "] does not exist");
    }

    public GameObject GetPrefab(int prefabId) {
        NetworkEntity n = GetPrefabNetworkGameObject(prefabId);
        if(n != null) {
            return n.gameObject;
        } else {
            return null;
        }
    }

    public NetworkEntity GetPrefabNetworkGameObject(int prefabId) {
        if(NetworkPrefabs.WithinRange(prefabId)) {
            return NetworkPrefabs[prefabId];
        } else {
            Debug.LogError("Prefab id [" + prefabId + "] is outside the range of [0, " + maxPrefabs + "]");
            return null;
        }
    }

    public GameObject GetPrefab(string prefabName) {
        if(NetworkPrefabNames.Contains(prefabName)) {
            return GetPrefab(NetworkPrefabNames.IndexOf(prefabName));
        } else {
            Debug.LogError("Prefab with name [" + prefabName + "] does not exist in the prefab database");
            return null;
        }
    }

    public int GetNextNetworkId() {
        return networkIds++;
    }

    public GameObject SpawnPrefab(string prefabName) {
        return SpawnPrefab(GetPrefabId(prefabName));
    }

    public GameObject SpawnPrefab(int prefabId) {

        //we create the object on our client 
        //we set the intial data
        //and we send the spawn message

        int networkId = GetNextNetworkId();
        int owner = me.connectionIndex;
        int controller = me.connectionIndex;

        GameObject g = SpawnPrefabInternal(prefabId, networkId, owner, controller);

        QueueEntityMessage(GetMessageCode("EntityUpdate"), g.GetComponent<NetworkEntity>(), prefabId, networkId, owner, controller);

        //spawn prefab internal

        return g;
    }

    public GameObject SpawnPrefabInternal(int prefabId, int networkId, int owner, int controller, params object[] args) {
        Debug.Log("SpawnPrefabInternal : " + prefabId + " :" + networkId + " : " + owner + " : " + controller);

        if(GetConnection(owner) == null) {
            Debug.Log("SpawnPrefabInternal:: No connection with index: " + owner + ". Ignoring this SpawnPrefabInternal call");
        }

        GameObject prefab = GetPrefab(prefabId);
        NetworkEntity ngo = null;

        if(prefab != null) {
            GameObject g = GameObject.Instantiate(prefab);
            ngo = g.GetComponent<NetworkEntity>();
            ngo.prefabId = prefabId;
            ngo.networkId = networkId;
            ngo.owner = owner;
            ngo.controller = controller;
            ngo.OnSpawn(args);
        }

        //add it to the owners entity list
        if(ngo != null) {
            //SteamConnection c = GetConnection(owner);
            StoreEntity(owner, networkId, ngo);
            //c.entities[networkId] = ngo;
        }

        return ngo.gameObject;
    }

    public void StoreEntity(int connectionIndex, int networkId, NetworkEntity entity) {
        if(!entities.ContainsKey(connectionIndex)) {
            entities.Add(connectionIndex, new NetworkEntity[maxNetworkIds]);
        } else {
        }

        entities[connectionIndex][networkId] = entity;
    }

    //includes me
    public SteamConnection GetConnection(int connectionIndex) {
        if(me.connectionIndex == connectionIndex) return me;
        SteamConnection c = connections.Values.Where(s => s.connectionIndex == connectionIndex).FirstOrDefault();
        return c;
    }

    public SteamConnection GetConnection(ulong steamId) {
        if(steamId == me.steamID) return me;
        if(connections[steamId] != null) {
            return connections[steamId];
        } else {
            return null;
        }
    }

    public bool IsEntityUpdateMsg(int msgCode) {
        return /*msgCode == GetMessageCode("SpawnPrefab") ||*/ msgCode == GetMessageCode("EntityUpdate");
    }

    public NetworkEntity GetEntity(int owner, int networkId) {
        //SteamConnection c = GetConnection(owner);
        if(entities.ContainsKey(owner)) {
            return entities[owner][networkId];
        } else {
            return null;
        }
    }
    /// <summary>
    ///called after entity deserialize.  If you do NOT call this, your state will not be applied
    ///This also spawns an object if the object we received state data for doesn't exist.
    /// </summary>
    public void ProcessEntityMessage(int prefabId, int networkId, int owner, int controller, params object[] args) {
        NetworkEntity entity = null;
        entity = GetEntity(owner, networkId);
        if(entity != null) {
            entity.OnEntityUpdate(args);
            entity.controller = controller;
        } else {
            //this entity doesn't exist for some reason. Maybe it has already been destroyed locally
            //anyways, since we already read from the stream, we can just discard whatever we read
            //without issues to the next messages we read.
            //OR we should spawn it, then pass it the data
            entity = Core.net.SpawnPrefabInternal(prefabId, networkId, owner, controller, args).GetComponent<NetworkEntity>();
            entity.OnEntityUpdate(args);
            entity.controller = controller;
        }
        //don't need to process, just call onEntityUpdate with our new values
        //Core.net.MessageProcessors[msgCode](sender, prefabId, networkId, owner, controller);
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



        if(NetworkSendEvent != null) NetworkSendEvent();

        foreach(SteamConnection sc in connections.Values) {

            //walk the entities and say "hey, we're sending this frame" so we can update the state?
            //or, let entities subscribe to an event?  Wonder how much faster it would be vs walking the entire entity list.
            //if we had 4096 entites (max) things would break down anyways.  Probably don't want to just
            //add state update messages willy-nilly.  Should check priority or somethings?

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

                        if(m.isEntityMsg) {
                            if(m.entity == null) {
                                m.priority = 0; //can't send this because our entity is GONE
                            } else {
                                //this checks the priority to whoever we are sending to
                                //via distance check around their player
                                //what if the entity we want to send is too far away from US for us to care about the entity
                                //anymore and we want to destroy it locally?
                                //do we do a separate check somewhere for that too?
                                m.priority = m.entity.PriorityCaller(sc.steamID, true);
                            }
                        } else {
                            //normal message
                            m.priority = MessagePriorityCalculators[m.msgCode](sc.steamID, m.args);
                        }

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

                        if(m.priority == 0f) {
                            if(m.isEntityMsg) {
                                m.entity.queuedMessage[sc.steamID] = null; 
                            }
                            
                            //if the message has a priority of 0 we dont want to send the message 
                            sc.messageQueue.RemoveAt(i); //this will happen when we queue something to everyone, but 
                            i--;                        //some players are too far away to care about the message (in a different map, etc)
                            continue;                   //so it just gets discarded. 
                            //what happens if we discard a spawn request, and then move into the area
                            //the object should be in, and start receiving state updates from that spawn???
                            //maybe NEVER remove a spawn request (even with priority 0?)
                            //add it to a list and just buffer the message until we get into the area?
                            //that could work...

                        }

                        //get the total message size to check if it will fit
                        int msgSize = SerializerUtils.RequiredBitsInt(0, maxMessageTypes); //8 bits for the msgCode included before the data
                        if(MessagePeekers[m.msgCode] != null) { //if it's null we don't have any data to send, just the msgCode (like for a keep alive)
                            msgSize += Core.net.MessagePeekers[m.msgCode](m.args);

                            if(m.msgCode == GetMessageCode("EntityUpdate")) {
                                int entityDataSize = m.entity.Peek();
                                msgSize += entityDataSize; //entity state msgSize
                                //Debug.Log("msgSize: ["+m.msgCode+"]: " + msgSize);
                                //if size is zero, we should just NOT send the message
                                if(entityDataSize == 0) {
                                    //just don't send it, no point wasting 8 bits with an empty message code.
                                    //Debug.Log("removed");
                                    m.entity.queuedMessage[sc.steamID] = null;
                                    sc.messageQueue.RemoveAt(i);
                                    i--;
                                    continue;
                                }
                            }

                        }
                        //Debug.Log("CanWrite: " + msgSize + " : " + writeStream.CanWrite(msgSize));
                        if(writeStream.CanWrite(msgSize)) { //will it fit?
                            //Debug.Log("write message");
                            SerializerUtils.WriteInt(writeStream, m.msgCode, 0, maxMessageTypes); //write the msgCode
                            if(MessageSerializers[m.msgCode] != null) {
                                Core.net.MessageSerializers[m.msgCode](sc.steamID, writeStream, m.args); //write the rest of the data

                                if(m.msgCode == GetMessageCode("EntityUpdate")) {
                                    m.entity.queuedMessage[sc.steamID] = null; //clear this so they can queue the next message
                                    m.entity.Serialize(writeStream);
                                } else if(m.msgCode == GetMessageCode("EntityDestroy")) {
                                    m.entity.queuedMessage[sc.steamID] = null;
                                    m.entity.TryDestroyInternal(); //we can only destroy the entity after the message has been sent. 
                                                                   //If we try to destroy it earlier, the entity is already null when trying to send and things fail
                                }
                            }
                            //Debug.Log("Wrote: " + msgSize + " : new bit position: " + writeStream.Position);
                            //remove this message from the list.
                            sc.messageQueue.RemoveAt(i);
                            i--;
                            continue;
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
        //Debug.Log("rec message");
        //string s = stream.ReadString();
        while(readStream.CanRead() && readStream.CanRead(SerializerUtils.RequiredBitsInt(0, maxMessageTypes))) {
            int msgCode = (int)SerializerUtils.ReadInt(readStream, 0, maxMessageTypes);
            //Debug.Log("[REC] MessageCode: " + msgCode);
            if(msgCode == GetMessageCode("Empty")) {
                AddToBandwidthInBuffer(-8);
                break; //no more data, the rest of this packet is junk
            } 
            //can we ignore all state data here if we're not in the same zone?
            //we don't know what kind of entity it's from..so...
            //maybe all zoneless data should just go through events?
            //or we can check the prefab to find out if it's zoneless?

            if(MessageDeserializers[msgCode] != null) {
                MessageDeserializers[msgCode](steamID, msgCode, readStream); 
                //entity messages deserialize is called internally (MessageCodes.SpawnPrefab.Deserialize  calles entity.Deserialize)
            } else {
                MessageProcessors[msgCode](steamID); //process is usually called within the deserializer, but since we have no deserializer (because we have no data, just a msgCode), call it here.
            }
        }
    }

    //connection stuff below
    public void RegisterMyConnection(ulong steamID) {
        Debug.Log("Register my connection: 0");
        SteamConnection c = new SteamConnection();
        c.steamID = steamID;
        c.connectionIndex = 0;
        me = c;
    }

    public void RegisterConnection(ulong steamID, int playerNum ) {
        SteamConnection c = new SteamConnection();
        c.steamID = steamID;
        c.connectionIndex = playerNum;
        c.connectionEstablishedTime = Time.realtimeSinceStartup;
        connections.Add(c.steamID, c);
        Debug.Log("Registred incoming connection as: " + playerNum);
    }

    public void RemoveConnection(ulong steamID) {
        if(connections.ContainsKey(steamID)) {
            connections.Remove(steamID);
        }
    }

    public void OnConnectRequest(ulong sender) {
        if(sender != me.steamID && !connections.ContainsKey(sender)) {
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

        _sendConnectionMetadataTimer -= Time.deltaTime;
        if(_sendConnectionMetadataTimer <= 0f) {
            _sendConnectionMetadataTimer = SendConnectionMetadataTimer;
            Core.net.me.BroadcastMetadata();
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

        //measuring bits in and bits out
        int bIn = 0;
        for(int i = 0; i < bitsInBuffer.Count; i++) {
            bitsInBuffer[i].timeInBuffer += Time.deltaTime;
            if(bitsInBuffer[i].timeInBuffer >= bandwidthBuffer) {
                bitsInBuffer.RemoveAt(i);
                i--;
            } else {
                bIn += bitsInBuffer[i].bits;
            }
        }

        int bOut = 0;
        for(int i = 0; i < bitsOutBuffer.Count; i++) {
            bitsOutBuffer[i].timeInBuffer += Time.deltaTime;
            if(bitsOutBuffer[i].timeInBuffer >= bandwidthBuffer) {
                bitsOutBuffer.RemoveAt(i);
                i--;
            } else {
                bOut += bitsOutBuffer[i].bits;
            }
        }

        bytesInPerSecond = (int)((float)bIn / (8f * bandwidthBuffer));
        bytesOutPerSecond = (int)((float)bOut / (8f * bandwidthBuffer));

        if(Input.GetKeyDown(KeyCode.L)) {
            Core.net.LoadScene(2);
        }
        if(Input.GetKeyDown(KeyCode.K)) {
            Core.net.LoadScene(3);
        }
    }


    public void AddToBandwidthOutBuffer(int numBits) {
        if(!measureBandwidth) return;
        bitsOutBuffer.Add(new BandwidthData(numBits));
    }

    public void AddToBandwidthInBuffer(int numBits) {
        if(!measureBandwidth) return;
        bitsInBuffer.Add(new BandwidthData(numBits));
    }

    public bool isLoadingScene = false;
    public void LoadScene(int sceneIndex) {
        if(!isLoadingScene) {
            isLoadingScene = true;
            //when this is called, we should STOP receving all state update
            //as we're about to move to a new scene
            me.zone = sceneIndex;
            //Core.net.QueueMessage(GetMessageCode("SetConnectionData"), me.zone);
            StartCoroutine("LoadSceneInternal", sceneIndex);
        }
    }

    private IEnumerator LoadSceneInternal(int sceneIndex) {
        AsyncOperation load = SceneManager.LoadSceneAsync(sceneIndex);
        while(!load.isDone) {
            //load.progress update progress bar
            yield return null;
        }
        isLoadingScene = false;
        //scene loaded sucessfully
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



