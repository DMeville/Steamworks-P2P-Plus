using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

using BitTools;
using UdpKit;

public class Test : MonoBehaviour {

   
    void Start() {
        //Debug.Log(SerializerUtils.RequiredBitsFloat(-100f, 100f, 1f));
        //Debug.Log(SerializerUtils.RequiredBitsFloat(-100f, 100f, 0.1f));
        //Debug.Log(SerializerUtils.RequiredBitsFloat(-10000f, 10000f, 1f));

        //Debug.Log(SerializerUtils.RequiredBitsInt());

        //Debug.Log(SerializerUtils.RequiredBitsFloat(-10f, 10f, 0.0001f));
        //Debug.Log(SerializerUtils.RequiredBitsFloat(-10f, 10f, 0.0001f));
        //Debug.Log(SerializerUtils.RequiredBitsFloat(-10f, 10f, 0.0001f));
        //Debug.Log(SerializerUtils.RequiredBitsInt(-100, 100));
        //Debug.Log(SerializerUtils.RequiredBitsFloat(-100f, 100f, 0.001f));
        //Debug.Log(SerializerUtils.RequiredBitsQuaternion(0.01f));

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
