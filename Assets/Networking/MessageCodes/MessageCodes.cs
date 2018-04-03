using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ByteStream = UdpKit.UdpStream;


namespace MessageCode {
    //helper class just to move the Message messages out of NetworkManager
    //and keep it a bit cleane and more organized

    //internal priority calculator used for internal messages (connect requests mainly)
    //this ensures they go out asap
    public class Internal {
        public static float Priority(ulong receiver, params object[] args) {
            return 999f;
        }

        //returns the header size for (prefabId, networkId, controller, owner)
        public static int PeekEntityHeader() {
            int s = 0;
            s += SerializerUtils.RequiredBitsInt(0, Core.net.maxPrefabs);
            s += SerializerUtils.RequiredBitsInt(0, Core.net.maxNetworkIds);
            s += SerializerUtils.RequiredBitsInt(0, Core.net.maxPlayers);
            s += SerializerUtils.RequiredBitsInt(0, Core.net.maxPlayers);
            return s;
        }
    }

    public class TestMessage {
        public static void Process(ulong sender, params object[] args) {
            Debug.Log("Received message with code: " + args[0]);
        }

        public static void Serialize(ulong receiver, ByteStream stream, params object[] args) { }

        public static void Deserialize(ulong sender, int msgCode, ByteStream stream) {Core.net.MessageProcessors[msgCode](sender, msgCode);}

        public static int Peek(params object[] args) { return 0; }

        public static float Priority(ulong receiver, params object[] args) { return 1f; }
    }

    //copy and paste this template when creating a new message.
    public class Template {
        public static void Process(ulong sender, params object[] args) {

        }

        public static void Serialize(ulong receiver, ByteStream stream, params object[] args) {

        }

        public static void Deserialize(ulong sender, int msgCode, ByteStream stream) {
            //Remember to call Core.Net.MessageProcessors[msgCode](sender, args...) here!
            //Core.net.MessageProcessors[msgCode](sender, arg0, arg1, arg2, arg3, arg4, arg5);
        }

        public static int Peek(params object[] args) {
            int s = 0;

            return s;
        }

        public static float Priority(ulong receiver, params object[] args) {
            float p = 1f;

            return p;
        }
    }

    //has no data, so it doesn't need serialize/deserialize/peek methods
    //priority is Internal.Priority
    public class ConnectRequest {
        public static void Process(ulong sender, params object[] args) {
            Debug.Log("On Rec ConnectRequest.Process");
            Core.net.OnConnectRequest(sender);
        }
    }

    //We send this event back when someone tries to connect to us. 
    //we send them our connectionId and their assigned connectionId
    public class ConnectRequestResponse {
        public static void Process(ulong sender, params object[] args) {
            Debug.Log("On Rec Connect Req Response");
            Core.net.OnConnectRequestResponse(sender, (int)args[0], (int)args[1]);
        }

        //
        public static void Serialize(ulong receiver, ByteStream stream, params object[] args) {

            int arg0 = (int)args[0]; //the connectionIndex of the player who sent you this message (the host)
            int arg1 = (int)args[1]; //the connectionIndex of you assigned by the host

            SerializerUtils.WriteInt(stream, arg0, 0, 255);
            SerializerUtils.WriteInt(stream, arg1, 0, 255);
        }

        public static void Deserialize(ulong sender, int msgCode, ByteStream stream) {

            int arg0 = SerializerUtils.ReadInt(stream, 0, 255);
            int arg1 = SerializerUtils.ReadInt(stream, 0, 255);

            //no need for a null check, can't have a deserializer without a processor.
            //I mean, you can, but it wouldn't do anything with the data you just received
            Core.net.MessageProcessors[msgCode](sender, arg0, arg1);
        }

        public static int Peek(params object[] args) {
            int s = 0;
            s += SerializerUtils.RequiredBitsInt(0, 255);
            s += SerializerUtils.RequiredBitsInt(0, 255);
            return s;
        }

        //pass in params here just in case we ever want to base priority off of *something* we're sending.
        //eg, if speed > 1000 we want to send this NOW or something.
        public static float Priority(ulong receiver, params object[] args) {
            return 999f;
        }
    }

    //test state, passing 3 ints, and 3 floats.  Along with using the first it as the priority number
    public class TestState {
        public static void Process(ulong sender, params object[] args) {
            Debug.Log("TestState : " + args[0] + " : " + args[1] + " : " + args[2] + " : " + args[3]);
        }

        //
        public static void Serialize(ulong receiver, ByteStream stream, params object[] args) {

            int arg0 = (int)args[0]; //the connectionIndex of the player who sent you this message (the host)
            int arg1 = (int)args[1]; //the connectionIndex of you assigned by the host
            int arg2 = (int)args[2];
            int arg3 = (int)args[3];

            SerializerUtils.WriteInt(stream, arg0, 0, 16);
            SerializerUtils.WriteInt(stream, arg1, 0, 32);
            SerializerUtils.WriteInt(stream, arg2, 0, 128);
            SerializerUtils.WriteInt(stream, arg3, 0, 255);

        }

        public static void Deserialize(ulong sender, int msgCode, ByteStream stream) {

            int arg0 = SerializerUtils.ReadInt(stream, 0, 16);
            int arg1 = SerializerUtils.ReadInt(stream, 0, 32);
            int arg2 = SerializerUtils.ReadInt(stream, 0, 128);
            int arg3 = SerializerUtils.ReadInt(stream, 0, 255);

            //no need for a null check, can't have a deserializer without a processor.
            //I mean, you can, but it wouldn't do anything with the data you just received
            Core.net.MessageProcessors[msgCode](sender, arg0, arg1, arg2, arg3);
        }

        public static int Peek(params object[] args) {
            int s = 0;
            s += SerializerUtils.RequiredBitsInt(0, 16);
            s += SerializerUtils.RequiredBitsInt(0, 32);
            s += SerializerUtils.RequiredBitsInt(0, 128);
            s += SerializerUtils.RequiredBitsInt(0, 255);

            return s;
        }
        
        public static float Priority(ulong receiver, params object[] args) {
            return 1f; //we send our first int as a random number between 0, 255
                                   //so we're sorting priority based on the value we're sending.  
            //Messages with higher arg[0] value will come through first.
        }
    }

    //this is used for spawn too, because we spawn an object when we get the first state update from them
    public class EntityUpdate {

        public static void Process(ulong sender, params object[] args) {
            //Debug.Log("Process: " + args[0] + " : " + args[1] + " : " + args[2] + " : " + args[3]);
            int prefabId = (int)args[0];
            int networkId = (int)args[1];
            int owner = (int)args[2];
            int controller = (int)args[3];
            //Debug.Log("SpawnPrefab::Process: " + prefabId);
            //Core.net.SpawnPrefabInternal(prefabId, networkId, owner, controller);
        }

        //
        public static void Serialize(ulong receiver, ByteStream stream, params object[] args) {
            //Debug.Log("Serialize: " + args[0] + " : " + args[1] + " : " + args[2] + " : " + args[3]);
            //Debug.Log("Serialize: " + args[0]);
            int prefabId = (int)args[0];
            int networkId = (int)args[1];
            int owner = (int)args[2];
            int controller = (int)args[3];

            //Debug.Log("NetworkID :: " + networkId);

            SerializerUtils.WriteInt(stream, prefabId, 0, Core.net.maxPrefabs);
            SerializerUtils.WriteInt(stream, networkId, 0, Core.net.maxNetworkIds);
            SerializerUtils.WriteInt(stream, owner, 0, Core.net.maxPlayers);
            SerializerUtils.WriteInt(stream, controller, 0, Core.net.maxPlayers);

            //Debug.Log(BitTools.BitDisplay.BytesToString(stream.Data));
        }

        public static void Deserialize(ulong sender, int msgCode, ByteStream stream) {
            //Debug.Log(BitTools.BitDisplay.BytesToString(stream.Data));

            //Debug.Log("MessageCode.EntityUpdate.Deserialize");
            //read the entity header
            int prefabId = SerializerUtils.ReadInt(stream, 0, Core.net.maxPrefabs);
            int networkId = SerializerUtils.ReadInt(stream, 0, Core.net.maxNetworkIds);
            int owner = SerializerUtils.ReadInt(stream, 0, Core.net.maxPlayers);
            int controller = SerializerUtils.ReadInt(stream, 0, Core.net.maxPlayers);

            //pass it down to the Behaviour to process further
            Core.net.GetPrefabNetworkGameObject(prefabId).Deserialize(stream, prefabId, networkId, owner, controller);
            //what happens if we get here and we CAN"T find the entity? It's already been destroyed locally?
            //we won't know the state, and we won't know how much data to read.
            //and we can't just throw away the packet.. because it might have important data AFTER this message
            //so we need a way to know the message size even if the entity doesn't exisit.

            //we could send the message size but that's another 8 bits at least.. and that would kind of be a waste.
            //hmmmmm
            //cause use the prefabs template peek because it could have a different size (delta compressed)
            //HMMMMM

            //can we use a static deserialize (on the prefab template)
            //that will read anything conditional? I guess

            //Debug.Log("Deserialize: " + prefabId + " : " + networkId + " : " + owner + " : " + controller);

            //process is called the next level down (prefab's behaviour's deserialize)
            //Core.net.MessageProcessors[msgCode](sender, prefabId, networkId, owner, controller);

        }

        public static int Peek(params object[] args) {
            int s = 0;

            s += MessageCode.Internal.PeekEntityHeader();
            //s += Core.net.StatePeekers[prefabId] for initial data

            return s;
        }

        public static float Priority(ulong receiver, params object[] args) {
            float p = 1f;

            return p;
        }
    }

    public class EntityDestroy {

        public static void Process(ulong sender, params object[] args) {
            //Debug.Log("Process: " + args[0] + " : " + args[1] + " : " + args[2] + " : " + args[3]);
            int prefabId = (int)args[0];
            int networkId = (int)args[1];
            int owner = (int)args[2];
            int controller = (int)args[3];
            //Debug.Log("SpawnPrefab::Process: " + prefabId);
            //Core.net.SpawnPrefabInternal(prefabId, networkId, owner, controller);
            //Core.net.QueueEntityMessage()
            Core.net.GetEntity(owner, networkId).DestroyInternal();
        }

        public static void ProcessDestroyRequest(ulong sender, params object[] args) {
            //Debug.Log("Process: " + args[0] + " : " + args[1] + " : " + args[2] + " : " + args[3]);
            int prefabId = (int)args[0];
            int networkId = (int)args[1];
            int owner = (int)args[2];
            int controller = (int)args[3];
            //Debug.Log("SpawnPrefab::Process: " + prefabId);
            //Core.net.SpawnPrefabInternal(prefabId, networkId, owner, controller);
            //Core.net.QueueEntityMessage()
            Core.net.GetEntity(owner, networkId).Destroy();
        }

        //
        public static void Serialize(ulong receiver, ByteStream stream, params object[] args) {
            //Debug.Log("Serialize: " + args[0] + " : " + args[1] + " : " + args[2] + " : " + args[3]);
            //Debug.Log("Serialize: " + args[0]);
            int prefabId = (int)args[0];
            int networkId = (int)args[1];
            int owner = (int)args[2];
            int controller = (int)args[3];

            //Debug.Log("NetworkID :: " + networkId);

            SerializerUtils.WriteInt(stream, prefabId, 0, Core.net.maxPrefabs);
            SerializerUtils.WriteInt(stream, networkId, 0, Core.net.maxNetworkIds);
            SerializerUtils.WriteInt(stream, owner, 0, Core.net.maxPlayers);
            SerializerUtils.WriteInt(stream, controller, 0, Core.net.maxPlayers);

            //Debug.Log(BitTools.BitDisplay.BytesToString(stream.Data));
        }

        public static void Deserialize(ulong sender, int msgCode, ByteStream stream) {
            //Debug.Log(BitTools.BitDisplay.BytesToString(stream.Data));
            //Debug.Log("MessageCode.EntityUpdate.Deserialize");
            //read the entity header
            int prefabId = SerializerUtils.ReadInt(stream, 0, Core.net.maxPrefabs);
            int networkId = SerializerUtils.ReadInt(stream, 0, Core.net.maxNetworkIds);
            int owner = SerializerUtils.ReadInt(stream, 0, Core.net.maxPlayers);
            int controller = SerializerUtils.ReadInt(stream, 0, Core.net.maxPlayers);

            //pass it down to the Behaviour to process further
            //Core.net.GetPrefabNetworkGameObject(prefabId).Deserialize(stream, prefabId, networkId, owner, controller);

            //Core.net.ProcessEntityMessage(prefabId, networkId, owner, controller);
            Core.net.MessageProcessors[msgCode](sender, prefabId, networkId, owner, controller);

        }

        public static int Peek(params object[] args) {
            int s = 0;

            s += MessageCode.Internal.PeekEntityHeader();
            //s += Core.net.StatePeekers[prefabId] for initial data

            return s;
        }

        public static float Priority(ulong receiver, params object[] args) {
            float p = 1f;

            return p;
        }
    }
    //could we pack entity messages together or something?
    //the prefabId, networkId, owner, and controler are all pretty heavy to send with every event
    //but....eh
    public class EntityScopeRequest {
        public static void Process(ulong sender, params object[] args) {
            //Debug.Log("On Rec Connect Req Response");
            //Core.net.OnConnectRequestResponse(sender, (int)args[0], (int)args[1]);
            int prefabId = (int)args[0];
            int networkId = (int)args[1];
            int owner = (int)args[2];
            int controller = (int)args[3];

            NetworkEntity e = Core.net.GetEntity(owner, networkId);

            //we send this event to an enity from client->entity controller if the client hasn't seen an update in a *while*
            //essentially the client is asking "what's going on" because they need to either take control, destroy the entity, or do something else.
            int scopeStatus = 0;
            if(e == null) {
                
                //this client is no longer has this entity.  It was probably destroyed because out of scope.
                scopeStatus = 2; //But since B sent this message, B still cares about the entity, so B should take control of this immediately
            } else {
                
                    float p = e.Priority(sender);
                    if(p <= 0f) {
                        //the sender is no longer getting updates because they are outside of priority (fell out of scope)
                        //they should destroy locally on B, because B shouldn't care about it anymore
                        scopeStatus = 1;
                    } else {
                        //the sender is still in scope, but isn't getting updates. Must mean there is just no data to be sent (static, non-moving entity?)
                        //B should do nothing.  Keep their entity around because it's still *important* and in scope.
                        scopeStatus = 0;
                    }

            }

            //the only other thing that can happen is if the receiver of this event is already disconnected.  in which case this 
            //event will never get processed.  So the sender of this event needs to keep a timer (or listen for disconnects)
            //If the event never comes through B should take control of this entity because they obviously still care about it.

            Core.net.QueueMessage(sender, Core.net.GetMessageCode("EntityScopeResponse"), prefabId, networkId, owner, controller, scopeStatus);
        }

        //
        public static void Serialize(ulong receiver, ByteStream stream, params object[] args) {

            int prefabId = (int)args[0];
            int networkId = (int)args[1];
            int owner = (int)args[2];
            int controller = (int)args[3];

            //Debug.Log("NetworkID :: " + networkId);
            SerializerUtils.WriteInt(stream, prefabId, 0, Core.net.maxPrefabs);
            SerializerUtils.WriteInt(stream, networkId, 0, Core.net.maxNetworkIds);
            SerializerUtils.WriteInt(stream, owner, 0, Core.net.maxPlayers);
            SerializerUtils.WriteInt(stream, controller, 0, Core.net.maxPlayers);
        }

        public static void Deserialize(ulong sender, int msgCode, ByteStream stream) {

            int prefabId = SerializerUtils.ReadInt(stream, 0, Core.net.maxPrefabs);
            int networkId = SerializerUtils.ReadInt(stream, 0, Core.net.maxNetworkIds);
            int owner = SerializerUtils.ReadInt(stream, 0, Core.net.maxPlayers);
            int controller = SerializerUtils.ReadInt(stream, 0, Core.net.maxPlayers);

            //no need for a null check, can't have a deserializer without a processor.
            //I mean, you can, but it wouldn't do anything with the data you just received
            Core.net.MessageProcessors[msgCode](sender, prefabId, networkId, owner, controller);
        }

        public static int Peek(params object[] args) {
            int s = 0;
            s += MessageCode.Internal.PeekEntityHeader(); //
            return s;
        }

        //I guess we use normal priority here because we want to make sure this goes
        //to clients who are not scoped in anymore...
        public static float Priority(ulong receiver, params object[] args) {
            return 1f; 
        }
    }

    public class EntityScopeResponse {
        public static void Process(ulong sender, params object[] args) {
            //Debug.Log("On Rec Connect Req Response");
            //Core.net.OnConnectRequestResponse(sender, (int)args[0], (int)args[1]);
            int prefabId = (int)args[0];
            int networkId = (int)args[1];
            int owner = (int)args[2];
            int controller = (int)args[3];
            int scopeStatus = (int)args[4];
            NetworkEntity e = Core.net.GetEntity(owner, networkId);
            if(e == null) return;
            Debug.Log("EntityScopeResponse: " + scopeStatus);
            switch(scopeStatus) {
                case 0:
                    //sender just has no data for this entity, but it is still important. Don't do anything
                    break;
                case 1:
                    //sender isn't replicating this entity to you anymore because you have fallen out of scope.
                    //destroy this locally
                    e.DestroyInternal();
                    break;
                case 2:
                    //sender doesn't care about this entity anymore (they fell out of scope).  You still care about this
                    //entity beacuse you sent the EntityScopeRequest.
                    //take control of this entity.
                    //sending a take control request will not come back because the entity no longer exists on the other end. 
                    //so assume the request came back as true
                    if(e.canMigrate) { 
                        e.TakeControlInternal();
                    } else {
                        e.DestroyInternal();
                    }
                    break;
            }
            
            //if(e != null && canDestroy) {
            //    e.DestroyInternal();
            //}
        }

        //
        public static void Serialize(ulong receiver, ByteStream stream, params object[] args) {

            int prefabId = (int)args[0];
            int networkId = (int)args[1];
            int owner = (int)args[2];
            int controller = (int)args[3];
            int scopeStatus = (int)args[4];

            //Debug.Log("NetworkID :: " + networkId);
            SerializerUtils.WriteInt(stream, prefabId, 0, Core.net.maxPrefabs);
            SerializerUtils.WriteInt(stream, networkId, 0, Core.net.maxNetworkIds);
            SerializerUtils.WriteInt(stream, owner, 0, Core.net.maxPlayers);
            SerializerUtils.WriteInt(stream, controller, 0, Core.net.maxPlayers);
            SerializerUtils.WriteInt(stream, scopeStatus, 0, 3);
        }

        public static void Deserialize(ulong sender, int msgCode, ByteStream stream) {

            int prefabId = SerializerUtils.ReadInt(stream, 0, Core.net.maxPrefabs);
            int networkId = SerializerUtils.ReadInt(stream, 0, Core.net.maxNetworkIds);
            int owner = SerializerUtils.ReadInt(stream, 0, Core.net.maxPlayers);
            int controller = SerializerUtils.ReadInt(stream, 0, Core.net.maxPlayers);
            int scopeStatus = SerializerUtils.ReadInt(stream, 0, 3);
            //no need for a null check, can't have a deserializer without a processor.
            //I mean, you can, but it wouldn't do anything with the data you just received
            Core.net.MessageProcessors[msgCode](sender, prefabId, networkId, owner, controller, scopeStatus);
        }

        public static int Peek(params object[] args) {
            int s = 0;
            s += MessageCode.Internal.PeekEntityHeader();
            s += SerializerUtils.RequiredBitsInt(0,3);
            return s;
        }

        //I guess we use normal priority here because we want to make sure this goes
        //to clients who are not scoped in anymore...
        public static float Priority(ulong receiver, params object[] args) {
            return 1f;
        }
    }

    public class EntityChangeOwner {
        public static void Process(ulong sender, params object[] args) {
            //Debug.Log("On Rec Connect Req Response");
            //Core.net.OnConnectRequestResponse(sender, (int)args[0], (int)args[1]);
            int prefabId = (int)args[0];
            int networkId = (int)args[1];
            int owner = (int)args[2];
            int controller = (int)args[3];
            int newOwner = (int)args[4];

            NetworkEntity e = Core.net.GetEntity(owner, networkId);

            if(e != null) {
                e.OnChangeOwner(newOwner);
            }
        }

        //
        public static void Serialize(ulong receiver, ByteStream stream, params object[] args) {

            int prefabId = (int)args[0];
            int networkId = (int)args[1];
            int owner = (int)args[2];
            int controller = (int)args[3];
            int newOwner = (int)args[4];

            //Debug.Log("NetworkID :: " + networkId);
            SerializerUtils.WriteInt(stream, prefabId, 0, Core.net.maxPrefabs);
            SerializerUtils.WriteInt(stream, networkId, 0, Core.net.maxNetworkIds);
            SerializerUtils.WriteInt(stream, owner, 0, Core.net.maxPlayers);
            SerializerUtils.WriteInt(stream, controller, 0, Core.net.maxPlayers);
            SerializerUtils.WriteInt(stream, newOwner, 0, Core.net.maxPlayers);
        }

        public static void Deserialize(ulong sender, int msgCode, ByteStream stream) {

            int prefabId = SerializerUtils.ReadInt(stream, 0, Core.net.maxPrefabs);
            int networkId = SerializerUtils.ReadInt(stream, 0, Core.net.maxNetworkIds);
            int owner = SerializerUtils.ReadInt(stream, 0, Core.net.maxPlayers);
            int controller = SerializerUtils.ReadInt(stream, 0, Core.net.maxPlayers);
            int newOwner = SerializerUtils.ReadInt(stream, 0, Core.net.maxPlayers);
            //no need for a null check, can't have a deserializer without a processor.
            //I mean, you can, but it wouldn't do anything with the data you just received
            Core.net.MessageProcessors[msgCode](sender, prefabId, networkId, owner, controller, newOwner);
        }

        public static int Peek(params object[] args) {
            int s = 0;
            s += MessageCode.Internal.PeekEntityHeader();
            s += SerializerUtils.RequiredBitsInt(0, Core.net.maxPlayers);
            return s;
        }

        //I guess we use normal priority here because we want to make sure this goes
        //to clients who are not scoped in anymore...
        public static float Priority(ulong receiver, params object[] args) {
            return 1f;
        }
    }

    public class EntityControlRequest {
        public static void Process(ulong sender, params object[] args) {
            Debug.Log("EntityControlRequest.Process");
            //Core.net.OnConnectRequestResponse(sender, (int)args[0], (int)args[1]);
            int prefabId = (int)args[0];
            int networkId = (int)args[1];
            int owner = (int)args[2];
            int controller = (int)args[3];

            NetworkEntity e = Core.net.GetEntity(owner, networkId);
            SteamConnection c = Core.net.GetConnection(sender);

            if(e != null && c != null && e.controller == Core.net.me.connectionIndex) { //we can only give control if we are the controller
                e.controller = c.connectionIndex;
                
                //could do some custom logic here to decide whether to return true or false to allow
                //the take control.  In most cases I just want to accept anyways.
                Core.net.QueueMessage(sender, Core.net.GetMessageCode("EntityControlResponse"), prefabId, networkId, owner, e.controller, true); 
            } else {
                //no, they can't take control
                Core.net.QueueMessage(sender, Core.net.GetMessageCode("EntityControlResponse"), prefabId, networkId, owner, e.controller, false);
            }
        }

        //
        public static void Serialize(ulong receiver, ByteStream stream, params object[] args) {

            int prefabId = (int)args[0];
            int networkId = (int)args[1];
            int owner = (int)args[2];
            int controller = (int)args[3];

            //Debug.Log("NetworkID :: " + networkId);
            SerializerUtils.WriteInt(stream, prefabId, 0, Core.net.maxPrefabs);
            SerializerUtils.WriteInt(stream, networkId, 0, Core.net.maxNetworkIds);
            SerializerUtils.WriteInt(stream, owner, 0, Core.net.maxPlayers);
            SerializerUtils.WriteInt(stream, controller, 0, Core.net.maxPlayers);
        }

        public static void Deserialize(ulong sender, int msgCode, ByteStream stream) {
            int prefabId = SerializerUtils.ReadInt(stream, 0, Core.net.maxPrefabs);
            int networkId = SerializerUtils.ReadInt(stream, 0, Core.net.maxNetworkIds);
            int owner = SerializerUtils.ReadInt(stream, 0, Core.net.maxPlayers);
            int controller = SerializerUtils.ReadInt(stream, 0, Core.net.maxPlayers);
            //no need for a null check, can't have a deserializer without a processor.
            //I mean, you can, but it wouldn't do anything with the data you just received
            Core.net.MessageProcessors[msgCode](sender, prefabId, networkId, owner, controller);
        }

        public static int Peek(params object[] args) {
            int s = 0;
            s += MessageCode.Internal.PeekEntityHeader();
            return s;
        }

        //I guess we use normal priority here because we want to make sure this goes
        //to clients who are not scoped in anymore...
        public static float Priority(ulong receiver, params object[] args) {
            return 1f;
        }

    }

    public class EntityControlResponse {
        public static void Process(ulong sender, params object[] args) {
            //Debug.Log("On Rec Connect Req Response");
            //Core.net.OnConnectRequestResponse(sender, (int)args[0], (int)args[1]);
            int prefabId = (int)args[0];
            int networkId = (int)args[1];
            int owner = (int)args[2];
            int controller = (int)args[3];
            bool approved = (bool)args[4];

            NetworkEntity e = Core.net.GetEntity(owner, networkId);
            SteamConnection c = Core.net.GetConnection(sender);

            //you have control now according to the old controller
            if(e != null && c != null && approved) {
                e.isPredictingControl = false;
                e.controller = controller;
            } else {
                e.isPredictingControl = false;
            }
            //if(e != null) {
            //    e.ControlChanged();
            //}

            Debug.Log("EntityControlResponse::Success: " + approved);
        }

        //
        public static void Serialize(ulong receiver, ByteStream stream, params object[] args) {

            int prefabId = (int)args[0];
            int networkId = (int)args[1];
            int owner = (int)args[2];
            int controller = (int)args[3];
            bool approved = (bool)args[4];

            //Debug.Log("NetworkID :: " + networkId);
            SerializerUtils.WriteInt(stream, prefabId, 0, Core.net.maxPrefabs);
            SerializerUtils.WriteInt(stream, networkId, 0, Core.net.maxNetworkIds);
            SerializerUtils.WriteInt(stream, owner, 0, Core.net.maxPlayers);
            SerializerUtils.WriteInt(stream, controller, 0, Core.net.maxPlayers);
            SerializerUtils.WriteBool(stream, approved);
        }

        public static void Deserialize(ulong sender, int msgCode, ByteStream stream) {

            int prefabId = SerializerUtils.ReadInt(stream, 0, Core.net.maxPrefabs);
            int networkId = SerializerUtils.ReadInt(stream, 0, Core.net.maxNetworkIds);
            int owner = SerializerUtils.ReadInt(stream, 0, Core.net.maxPlayers);
            int controller = SerializerUtils.ReadInt(stream, 0, Core.net.maxPlayers);
            bool approved = SerializerUtils.ReadBool(stream);
            //no need for a null check, can't have a deserializer without a processor.
            //I mean, you can, but it wouldn't do anything with the data you just received
            Core.net.MessageProcessors[msgCode](sender, prefabId, networkId, owner, controller, approved);
        }

        public static int Peek(params object[] args) {
            int s = 0;
            s += MessageCode.Internal.PeekEntityHeader();
            s += SerializerUtils.RequiredBitsBool();
            return s;
        }

        //I guess we use normal priority here because we want to make sure this goes
        //to clients who are not scoped in anymore...
        public static float Priority(ulong receiver, params object[] args) {
            return 1f;
        }
    }

    public class SetConnectionData_Zone {
        public static void Process(ulong sender, params object[] args) {
            Core.net.GetConnection(sender).zone = (int)args[0];
        }

        public static void Serialize(ulong receiver, ByteStream stream, params object[] args) {
            int zone = (int)args[0];
            SerializerUtils.WriteInt(stream, zone, 0, 31);
        }

        public static void Deserialize(ulong sender, int msgCode, ByteStream stream) {
            //Remember to call Core.Net.MessageProcessors[msgCode](sender, args...) here!
            //Core.net.MessageProcessors[msgCode](sender, arg0, arg1, arg2, arg3, arg4, arg5);
            int zone = SerializerUtils.ReadInt(stream, 0, 31);
            Core.net.MessageProcessors[msgCode](sender, zone);
        }

        public static int Peek(params object[] args) {
            int s = 0;
            s += SerializerUtils.RequiredBitsInt(0, 31); 
            return s;
        }

        public static float Priority(ulong receiver, params object[] args) {
            float p = 999f;
            return p;
        }
    }

}
