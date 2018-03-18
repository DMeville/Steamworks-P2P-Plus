using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Test : MonoBehaviour {

	void Start () {

        var b = Serialize(0, true, false, true);
        Deserialize(0, b);
	}

    public delegate void ProcessDeserializedData(params object[] args);

    public byte[] Serialize(int msgType, params object[] args) {
        Debug.Log("S has args: " + args.Length);

        byte[] data = new byte[0];

        bool arg0 = (bool)args[0];
        bool arg1 = (bool)args[1];
        bool arg2 = (bool)args[2];

        data = data.Append(SerializerUtils.WriteBool(arg0));
        data = data.Append(SerializerUtils.WriteBool(arg1));
        data = data.Append(SerializerUtils.WriteBool(arg2));

        Debug.Log(BitConverter.ToString(data) + " : " + data.Length);

        return data;
    }

    public void Deserialize(int msgType, byte[] data) {
        //int mType = SerializerUtils.ReadInt(data);
        Debug.Log("D: " + BitConverter.ToString(data) + " : " + data.Length);
        bool arg0 = SerializerUtils.ReadBool(ref data);
        bool arg1 = SerializerUtils.ReadBool(ref data);
        bool arg2 = SerializerUtils.ReadBool(ref data);

        //what do we do with this data now?
        //send if off *somewhere* to be applied?
        //should that happen here?

        //need to wrap this data *somehow* to make it nice to send around.  Otherwise we hardcode 
        //process functions into Deserialize functions and I don't know if we want to do that?

        //Process(arg0, arg1, arg0);
        //NetworkManager.instance.ProcessDeserializedData[msgType](arg0, arg1, arg0);
        //NetworkManager.instance.ProcessDeserializedData[msgType] = ProcessConnectAccept;
        //public void ProcessConnectAccept(bool, bool, bool)?

        //Register("ConnectAccept", S, D, P);
        //when we receive a "ConnectAccept, we D(), which will interally call P() to our callback with the proper type args
        //when we send we go QueueSend(MessageType, bool, bool, bool) and interally it will S() our args?
        //our Q needs a per-message priority filter (not type) so that we can eventually send everything out on our simulation frame.
        //can we send more than one packet per simulation frame? What if we get into a downward spiral?
        //eventually things should slow down, right?
        Debug.Log(string.Format("{0}, {1}, {2}", arg0, arg1, arg2));
    }

    public void Process(bool arg0, bool arg1, bool arg2) {
        //do whatever with the data, hook into game objects, managers, etc.
    }
}
