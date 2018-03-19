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
        int num = -10;
        Debug.Log("n: " + num);
        stream.WriteInt(num, int.MinValue, int.MaxValue);

        byte[] data = stream.GetBuffer();
        Debug.Log(data.ToStringBinary());

        InputStream iStream = new InputStream(data);
        int val = iStream.ReadInt(int.MinValue, int.MaxValue);

        Debug.Log("v: " + val);
        
    }
}
