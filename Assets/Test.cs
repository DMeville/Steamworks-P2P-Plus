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



        byte[] b = new byte[1024 * 2]; //packetsize *2, why *2? That's what udpSocket.cs does. idk

        int packetSize = 32;

        UdpStream writeStream = new UdpStream(new byte[packetSize * 2]);
        UdpStream readStream = new UdpStream(new byte[packetSize * 2]);

        int maxValue = 4095;
        int value = 256;
        //writeStream.WriteInt(value); //writes as 32 bit
        //writeStream.WriteInt(value);



        SerializerUtils.WriteInt(writeStream, 255, 0, 255);
        SerializerUtils.WriteInt(writeStream, 256, 0, 256);

        //SerializerUtils.WriteInt(writeStream, value,);

        readStream = new UdpStream(writeStream.Data, writeStream.Position);
        Debug.Log("0: " + SerializerUtils.ReadInt(readStream, 0, 255));
        Debug.Log("1: " + SerializerUtils.ReadInt(readStream, 0, 256));
        //Debug.Log("read: " + SerializerUtils.ReadInt(readStream));

        //Debug.Log("255: " + BitTools.BitDisplay.BytesToString(writeStream.Data)); //this sends the entire packetSize of bytes, doesn't trim any of them off...
        //Debug.Log("256: " + BitTools.BitDisplay.BytesToString(readStream.Data)); //this sends the entire packetSize of bytes, doesn't trim any of them off...

       // Debug.Log(writeStream.Data.Length + " : " + writeStream.Ptr + " : " + writeStream.Position);

        //Debug.Log("read: " + BitTools.BitDisplay.BytesToString(readStream.Data));
        //Debug.Log(readStream.Data.Length + " : " + readStream.Ptr + " : " + readStream.Position);

        //we could arrayCopy it to the appropriate size and send that? idk
        //steam does this internally I 
    }
}
