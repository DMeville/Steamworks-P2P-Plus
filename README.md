# Steamworks P2P Plus

Still super WIP.

I wanted something slightly higher level than using the SendP2PPacket method in steamworks so I made this for a prototype I'm working on which is a distributed authority co-op adventure game. Has to be able to work with mods, has to be able to migrate entities smoothly, and has to be able to run without a central authority/server.  I'm trying to keep the networking system seperate from my prototype-specific stuff, but some features go pretty deep. 

All the good stuff is in NetworkManager.cs, methods called via Core.net or NetworkManager.instance

## Features
- Connection-based!
- Extendable Messaging System. Easily define custom message types and send them to connected clients. (mod friendly!)
- Entities (Networked Game Objects) with state replication, and entity property interpolation (vec3, quaternion, float, int)
- Entity Migration
- Bitpacked messages and Type Compression. Send a bool as 1 bit instead of 8, an int in the range of [0,10] as 4 bits instead of 16!
- Priority Sorting of messages. Players closer to you will receive messages more often than players on the other side of the map.
- Use a server-client model, a fully connected p2p graph, or something else entirely! Easy to extend! (Server-client only works if one player acts as the server. Doesn't work with dedicated servers because steamworks has no way to send data to a server that's not logged into any steam account)


## Usage
### Messaging System
Define your message type and signature somewhere. Ideally all in the same class. These message types are turned into ints and sent over the wire, so it's crucial every connected client has the same message type ordering.
```
RegisterMessageType("TestMessage",
            TestMessagePeek, //peeks into the message to find out how many bits we want to send (to see if it will fit in the next packet)
            TestMessagePriority, //calculates the priority of the message, 0 = doesn't get sent, higher values get sent sooner.
            TestMessageSerialize, //serializes the message data to the bitstream
            TestMessageDeserialize, //deserializes the message data from the bitstream
            TestMessageProcess); //processes the message, do something with the deserialized data
````

Define the message methods for our new message type "TestMessage"
For this example, we will send one bool, one int, and two floats (in different ranges) with our message
```
//Peek looks into our message to find out how many bits this message needs.
//Since we know what data we want to send, we know how to figure this out.
public static int TestMessagePeek(params object[] args) {
    int s = 0;
    //our first piece of data is a bool
    s += SerializerUtils.RequiredBitsBool();
    //compress the int into [0,32] range to save bits, because our int we know will never be larger than 32!
    s += SerializerUtils.RequiredBitsInt(0, 32); 
    //compress our float in the [0, 2000] range, with a precision of 0.1 (0.0, 0.1, 0.2 .. 1999.8, 1999.9, 2000.0)
    s += SerializerUtils.RequiredBitsFloat(0f, 2000f, 0.1f);
    s += SerializerUtils.RequiredBitsFloat(-255f, 255f, 0.0001f);
    return s;
}

public static float TestMessagePriority(ulong receiver, params object[] args) {
    return 1f; //default
    //we could do something like return DistanceBetween(GetPlayer(me), GetPlayer(receiver));
    //so that if receiver is far away we don't rush in sending him this message, he will get it eventually
    //but if someone is close to you, we send it to them sooner
}

public static void TestMessageSerialize(ulong receiver, ByteStream stream, params object[] args) {

    bool myBool = (bool)args[0];
    int myInt = (int)args[1];
    float myFloat1 = (float)args[2];
    float myFloat2 = (float)args[3];

    //NOTE: We MUST Read these values in Deserialize in the SAME order we write them here.  We must use the same ranges too!
    SerializerUtils.WriteBool(stream, myBool)
    SerializerUtils.WriteInt(stream, myInt, 0, 32);
    SerializerUtils.WriteFloat(stream, myFloat1, 0f, 2000f, 0.1f);
    SerializerUtils.WriteFloat(stream, myFloat2, -255f, 255f, 0.001f);
}

public static void TestMessageDeserialize(ulong sender, int msgCode, ByteStream stream) {
    bool myBool = SerializerUtils.ReadBool(stream);
    int myInt = SerializerUtils.ReadInt(stream, 0, 32);
    float myFloat1 = SerializerUtils.ReadFloat(stream, 0f, 2000f, 0.1f);
    float myFloat2 = SerializerUtils.ReadFloat(stream, -255f, 255f, 0.001f);
    
    //Now that we've got our data, we pass it along to the processor!
    Core.net.MessageProcessors[msgCode](sender, myBool, myInt, myFloat1, myFloat2);
}
public static void TestMessageProcess(ulong sender, params object[] args) {
    bool myBool = (bool)arg[0];
    int myInt = (int)arg[1];
    float myFloat1 = (float)arg[2];
    float myFloat2 = (float)arg[3];
    
    //do whatever you want with this data now.
    //Player.GetPlayer(me).SetDead(myBool);
    //Players.GetPlayer(me).SetStats(myInt, myFloat1, myFloat2);
}

```

Then send the message:
```
bool myBool = true;
int myInt = 14;
float myFloat1 = 249.142f; //Since we send this in the range [0f, 2000f] with precision of 0.1f, this sends as 249.1f
float myFloat2 = -34.324f;

Core.net.QueueMessage(targetSteamId, "TestMessage", myBool, myInt, myFloat1, myFloat2);
Core.net.QueueMessage(targetSteamId, "TestMessage", false, 5, 0.34f, -3.241f);
```

# Network Entity
See [Assets/Networking/CubeBehaviour.cs](https://github.com/DMeville/Steamworks-P2P-Plus/blob/master/Assets/Networking/CubeBehaviour.cs) for a full example.

Define your prefab type and signature somewhere. Ideally all in the same class. These types are turned into ints and sent over the wire, so it's crucial every connected client has the same message type ordering.

```
Core.net.RegisterPrefab("MyPrefab", myPrefab);
```
Extend NetworkEntity and override Peek, Priority, Serialize, and Deserialize using the same method we did for a message above. Attach this "Behaviour" to your prefab. 

Then to spawn the prefab:

```
Core.net.SpawnPrefab(Core.net.GetPrefabId("MyPrefab"));

```

Internally this just sends a state update, but since the receiver doesn't have the prefab spawned yet it does so and applies the state data.


### Dependencies
- [Facepunch.Steamworks](https://github.com/Facepunch/Facepunch.Steamworks) for as a steamworks wrapper, included and required. 
- [UdpKit](https://github.com/DMeville/udpkit) Modified version, using Udpstream to bitpack data, included and required. 
- [Odin Inspector](https://assetstore.unity.com/packages/tools/utilities/odin-inspector-and-serializer-89041) to show dictionaries in the editor, not included but not required.
