using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

using BitTools;
using UdpKit;

public class Test : MonoBehaviour {

    public float messageTimer = 0f;
    private float _messageTimer = 0f;
    public int numMessages = 10;
    void Start() {

        //having issues reading P2P data.  It's slow.
        //can we change to something other than the BitwiseMemoryOutputSTream?
        //https://github.com/DMeville/udpkit/blob/master/src/managed/udpkit/udpStream.cs 
        //could grab that out of udpkit from fholm
        //or https://github.com/rafvall/UnityAssets/blob/master/BitTools/Src/BitTools/BitWriter.cs

        //in order to use this we need to know the size of the packet we want to pack at creation
        //we could add this.. but would that be optimal?
        //

        byte[] b = new byte[1024 * 2]; //packetsize *2, why *2? That's what udpSocket.cs does. idk

        int packetSize = 32;

        UdpStream writeStream = new UdpStream(new byte[packetSize * 2]);
        UdpStream readStream = new UdpStream(new byte[packetSize * 2]);

        writeStream.WriteBool(true);
        writeStream.WriteBool(false);
        writeStream.WriteBool(true);
        writeStream.WriteBool(true);

        Debug.Log(BitTools.BitDisplay.BytesToString(writeStream.Data)); //this sends the entire packetSize of bytes, doesn't trim any of them off...
        Debug.Log(writeStream.Length + " : " + writeStream.Ptr + " : " + writeStream.Position);
        //we could arrayCopy it to the appropriate size and send that? idk
        //steam does this internally I 
    }
}
