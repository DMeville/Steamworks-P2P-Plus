using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

using InputStream = BitwiseMemoryInputStream;
using OutputStream = BitwiseMemoryOutputStream;

public class Test : MonoBehaviour {

	void Start () {

        return;
        OutputStream stream = new OutputStream();
        //int num = -10;
        //Debug.Log("n: " + num);
        //stream.WriteInt(num, int.MinValue, int.MaxValue);
        float num = -23.34234f;
        stream.WriteFloat(num, -100f, 100f, 0.001f);
      
        byte[] data = stream.GetBuffer();
        Debug.Log(data.ToStringBinary());

        InputStream iStream = new InputStream(data);
        //int val = iStream.ReadInt(int.MinValue, int.MaxValue);
        float val = iStream.ReadFloat(-100f, 100f, 0.001f);

        Debug.Log("Test: " + num + " : " + val.ToString("0.0000000"));


    }
}
