# Steamworks P2P Plus

Still super WIP.

I wanted something slightly higher level than using the SendP2PPacket method in steamworks so I made this. Has a modular system to add new message types (along with how to serialize/deserialize each messsage and what to do with it when received).  Is connection based.

All the good stuff is in NetworkManager.cs

Define your message type and signature somewhere. Ideally all in the same class. These message types are turned into ints and sent over the wire, so it's crucial every connected client has the same ordering.
```
NetworkManager.instance.RegisterMessageType("PlayerData", SerializePlayerData, DeserializePlayerdata, OnRecPlayerData);
````

Define the Serialize, Deserialize, and Process methods
```
private void byte[] SerializePlayerData(int msgCode, params object[] args){
    byte[] data = new byte[0];
    
    //we need to know what data we want to send with this message type
    string playerName = arg[0];
    int playerId =      arg[1];
    int hp =            arg[2];
    int str =           arg[3];
    float magicNumber = arg[4];
    bool isCool =       arg[5];
    
    //Note: We must write these in the serialize method and read these in the deserialize method in the SAME order
    data = data.Append(SerializerUtils.WriteString(playerName));
    data = data.Append(SerializerUtils.WriteInt(playerId));
    data = data.Append(SerializerUtils.WriteInt(hp));
    data = data.Append(SerializerUtils.WrintInt(str));
    data = data.Append(SerializerUtils.WriteFloat(magicNumber));
    data = data.Append(SerializerUtils.WriteBool(isCool));
    
    return data;
}

private void DeserializePlayerData(ulong sender, int msgCode, byte[] data){
    string playerName = SerializerUtils.ReadString(ref data);
    int playerId =      SerializerUtils.ReadInt(ref data);
    int hp =            SerializerUtils.ReadInt(ref data);
    int str =           SerializerUtils.ReadInt(ref data);
    float magicNumber = SerializerUtils.ReadFloat(ref data);
    bool isCool =       SerializerUtils.ReadBool(ref data);
    
    NetworkManager.instance.Process(sender, msgCode, playerName, playerId, hp, str, magicNumber, isCool)
}

private void OnRecPlayerData(ulong sender, int msgCode, params object[] args){
    string playerName = arg[0];
    int playerId =      arg[1];
    int hp =            arg[2];
    int str =           arg[3];
    float magicNumber = arg[4];
    bool isCool =       arg[5];
    
    //do whatever you want with this data now
    //Players.GetPlayer(playerId).SetStats(hp, str, magicNumber, isCool);
    //Players.GetPlayer(playerId).SetName(playerName);
}
```

Then send the message:
```
string playerName = "Tayne"
int playerId = 3;
int hp = 120;
int str = 9001;
float magicNumber = 3.143f;
bool isCool = false;

NetworkManager.instance.QueueMessage(targetSteamId, "PlayerData", playerName, playerId, hp, str, magicNumber, isCool);
NetworkManager.instance.QueueMessage(targetSteamId, "PlayerData", "Benjals", 4, 11, 0, 0.325f, true);
```

Project uses:

Facepunch.Steamworks for as a steamworks wrapper, included and required. 
(https://github.com/Facepunch/Facepunch.Steamworks)

Odin Inspector, to show dictionaries in the ditor, not included but not required.(https://assetstore.unity.com/packages/tools/utilities/odin-inspector-and-serializer-89041) 
