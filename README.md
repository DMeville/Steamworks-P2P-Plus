Steamworks P2P Plus

Still super WIP.

I wanted something slightly higher level than using the SendP2PPacket method in steamworks so I made this. Has a modular system to add new message types (along with how to serialize/deserialize each messsage and what to do with it when received).


Project uses:
Facepunch.Steamworks for as a steamworks wrapper, included and required. (https://github.com/Facepunch/Facepunch.Steamworks)
Odin Inspector, to show dictionaries in the ditor, not included but not required.(https://assetstore.unity.com/packages/tools/utilities/odin-inspector-and-serializer-89041) to show dictionaries in the editor, but it's not required.