using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

using BitTools;
using UdpKit;

public class Test : MonoBehaviour {

   
    void Start() {

        //Debug.Log((2 + 7) / 8);

        //Quaternion r = this.transform.rotation;
        //Debug.Log(r.ToString());

        //UdpStream stream = new UdpStream(new byte[12]);
        //Debug.Log("BitsRequired: " + SerializerUtils.RequiredBitsQuaternion(0.01f));

        ////SerializerUtils.WriteFloat(stream, 1f, 0f, 255f, 0.01f);
        //SerializerUtils.WriteQuaterinion(stream, r, 0.01f);

        //Debug.Log(BitDisplay.BytesToString(stream.Data));

        //UdpStream readStream = new UdpStream(stream.Data, stream.Ptr);

        ////float i = SerializerUtils.ReadFloat(readStream, 0f, 255f, 0.01f);
        ////Debug.Log("i: " + i);

        //Quaternion q = SerializerUtils.ReadQuaternion(readStream, 0.01f);
        //Debug.Log(q.ToString());

    }

}
