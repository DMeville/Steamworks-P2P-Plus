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
        string s = "Hello World! ";
        stream.WriteString(s);
      
        byte[] data = stream.GetBuffer();
        Debug.Log(data.ToStringBinary());

        InputStream iStream = new InputStream(data);
        //int val = iStream.ReadInt(int.MinValue, int.MaxValue);
        string val = iStream.ReadString();

        Debug.Log("Test: " + s + " : " + val);


    }
}
