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

    public class EntityScopeRequest {
        public static void Process(ulong sender, params object[] args) {
            //Debug.Log("On Rec Connect Req Response");
            //Core.net.OnConnectRequestResponse(sender, (int)args[0], (int)args[1]);
            int prefabId = (int)args[0];
            int networkId = (int)args[1];
            int owner = (int)args[2];
            int controller = (int)args[3];

            NetworkEntity e = Core.net.GetEntity(owner, networkId);
            bool canDestroy = false;
            if(e != null) {
                //can player "sender", destroy entity because it's outside of their scope?
                //or should they keep it alive because this entity just doesn't have data to send?
                float p = e.Priority(sender);
                if(p <= 0f) {
                    canDestroy = true;
                }
            } else {
                //entity is null, so I guess?
                canDestroy = true;
            }

            Core.net.QueueMessage(sender, Core.net.GetMessageCode("EntityScopeResponse"), prefabId, networkId, owner, controller, canDestroy);
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
            bool canDestroy = (bool)args[4];

            NetworkEntity e = Core.net.GetEntity(owner, networkId);
            
            if(e != null && canDestroy) {
                e.DestroyInternal();
            }
        }

        //
        public static void Serialize(ulong receiver, ByteStream stream, params object[] args) {

            int prefabId = (int)args[0];
            int networkId = (int)args[1];
            int owner = (int)args[2];
            int controller = (int)args[3];
            bool canDestroy = (bool)args[4];

            //Debug.Log("NetworkID :: " + networkId);
            SerializerUtils.WriteInt(stream, prefabId, 0, Core.net.maxPrefabs);
            SerializerUtils.WriteInt(stream, networkId, 0, Core.net.maxNetworkIds);
            SerializerUtils.WriteInt(stream, owner, 0, Core.net.maxPlayers);
            SerializerUtils.WriteInt(stream, controller, 0, Core.net.maxPlayers);
            SerializerUtils.WriteBool(stream, canDestroy);
        }

        public static void Deserialize(ulong sender, int msgCode, ByteStream stream) {

            int prefabId = SerializerUtils.ReadInt(stream, 0, Core.net.maxPrefabs);
            int networkId = SerializerUtils.ReadInt(stream, 0, Core.net.maxNetworkIds);
            int owner = SerializerUtils.ReadInt(stream, 0, Core.net.maxPlayers);
            int controller = SerializerUtils.ReadInt(stream, 0, Core.net.maxPlayers);
            bool canDestroy = SerializerUtils.ReadBool(stream);
            //no need for a null check, can't have a deserializer without a processor.
            //I mean, you can, but it wouldn't do anything with the data you just received
            Core.net.MessageProcessors[msgCode](sender, prefabId, networkId, owner, controller, canDestroy);
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

            if(e != null && c != null) {
                e.controller = c.connectionIndex;
                //this entity will stop sending updates...
                //sinc it is no longer the controller...
                //BUT what if it already has an event queued that's just lower priority?
                //it will send it out, and maybe that's ok, since the new controller will
                //send out them every update, so eventually anything buffered would be overwritten
                //we should still just remove any events though.
                //what if it's a destroy that's queued?
                //oh well? id
                Debug.Log("------ QueueMessage::EntityControlResponse");
                Core.net.QueueMessage(sender, Core.net.GetMessageCode("EntityControlResponse"), prefabId, networkId, owner, e.controller);
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
            Debug.Log("EntityControlRequest.Deserialize");
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

            NetworkEntity e = Core.net.GetEntity(owner, networkId);
            SteamConnection c = Core.net.GetConnection(sender);

            //you have control now according to the old controller
            if(e != null && c != null) {
                e.isPredictingControl = false;
                e.controller = controller;
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

   

}
