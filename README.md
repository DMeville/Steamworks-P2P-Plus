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
    //we need to know what data we want to send with this message type
    string playerName = arg[0];
    int playerId =      arg[1];
    int hp =            arg[2];
    int str =           arg[3];
    bool isCool =       arg[4];
    
    OutputStream stream = new OutputStream(); //Using OutputStream = BitwiseMemoryOutputStream;
    
    //Note: We must write these in the serialize method and read these in the deserialize method in the SAME order
    //along with the same ranges
    
    stream.WriteString(playerName);
    stream.WriteInt(playerId, 0, 255); //sends an int in the range of [0,255]. Packs it as 8 bits instead of 32.
    stream.WriteInt(hp);               //sends as a full 32 bit int
    stream.WriteInt(str);
    stream.WriteBool(isCool);          //sends one bit, instead of 8
    
    return stream.GetBuffer();
}

private void DeserializePlayerData(ulong sender, int msgCode, byte[] data){
    InputStream stream = new InputStream(data);  //Using InputStream = BitwiseMemoryInputStream;
    string playerName  = stream.ReadString(data);
    int playerId       = stream.ReadInt(0,255);
    int hp             = stream.ReadInt();
    int str            = stream.ReadInt();
    bool isCool        = stream.ReadBool();
    
    NetworkManager.instance.Process(sender, msgCode, playerName, playerId, hp, str, isCool)
}

private void OnRecPlayerData(ulong sender, int msgCode, params object[] args){
    string playerName = arg[0];
    int playerId      = arg[1];
    int hp            = arg[2];
    int str           = arg[3];
    bool isCool       = arg[4];
    
    //do whatever you want with this data now
    //Players.GetPlayer(playerId).SetStats(hp, str, isCool);
    //Players.GetPlayer(playerId).SetName(playerName);
}
```

Then send the message:
```
string playerName = "Tayne"
int playerId = 3;
int hp = 120;
int str = 9001;
bool isCool = false;

NetworkManager.instance.QueueMessage(targetSteamId, "PlayerData", playerName, playerId, hp, str, isCool);
NetworkManager.instance.QueueMessage(targetSteamId, "PlayerData", "Benjals", 4, 11, 0, true);
```

Project uses:

Facepunch.Steamworks for as a steamworks wrapper, included and required. 
(https://github.com/Facepunch/Facepunch.Steamworks)

UBitstream-Utilities BitStream classes to pack data, included and required. 
(https://github.com/M-Aghasi/Unity-UdpSocket-BitStream-Utilities)

Odin Inspector, to show dictionaries in the ditor, not included but not required.(https://assetstore.unity.com/packages/tools/utilities/odin-inspector-and-serializer-89041) 
