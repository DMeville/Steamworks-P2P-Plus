using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ByteStream = UdpKit.UdpStream;


namespace MessageCode {
    
    public class Internal {
        public static float Priority(ulong receiver, params object[] args) {
            return 999f;
        }
    }

    public class ConnectRequest {
        public static void Process(ulong sender, params object[] args) {
            Debug.Log("On Rec ConnectRequest.Process");
            Core.net.OnConnectRequest(sender);
        }
    }

    public class ConnectRequestResponse {
        public static void Process(ulong sender, params object[] args) {
            Debug.Log("On Rec Connect Req Response");
            Core.net.OnConnectRequestResponse(sender, (int)args[0], (int)args[1]);
        }

        //
        public static void Serialize(ulong receiver, ByteStream stream, params object[] args) {

            int arg0 = (int)args[0]; //the connectionIndex of the player who sent you this message (the host)
            int arg1 = (int)args[1]; //the connectionIndex of you assigned by the host

            SerializerUtils.WriteInt(stream, arg0, 0, 255);
            SerializerUtils.WriteInt(stream, arg1, 0, 255);
        }

        public static void Deserialize(ulong sender, int msgCode, ByteStream stream) {

            int arg0 = SerializerUtils.ReadInt(stream, 0, 255);
            int arg1 = SerializerUtils.ReadInt(stream, 0, 255);

            //no need for a null check, can't have a deserializer without a processor.
            //I mean, you can, but it wouldn't do anything with the data you just received
            Core.net.MessageProcessors[msgCode](sender, arg0, arg1);
        }

        public static int Peek(params object[] args) {
            int s = 0;
            s += SerializerUtils.RequiredBitsInt(0, 255);
            s += SerializerUtils.RequiredBitsInt(0, 255);
            return s;
        }

        //pass in params here just in case we ever want to base priority off of *something* we're sending.
        //eg, if speed > 1000 we want to send this NOW or something.
        public static float Priority(ulong receiver, params object[] args) {
            return 999f;
        }
    }

    public class TestState {
        public static void Process(ulong sender, params object[] args) {
            Debug.Log("TestState : " + args[0]);
            
        }

        //
        public static void Serialize(ulong receiver, ByteStream stream, params object[] args) {

            int arg0 = (int)args[0]; //the connectionIndex of the player who sent you this message (the host)
            int arg1 = (int)args[1]; //the connectionIndex of you assigned by the host
            int arg2 = (int)args[2];

            float arg3 = (float)args[3];
            float arg4 = (float)args[4];
            float arg5 = (float)args[5];

            SerializerUtils.WriteInt(stream, arg0, 0, 255);
            SerializerUtils.WriteInt(stream, arg1, 0, 255);
            SerializerUtils.WriteInt(stream, arg2, 0, 255);

            SerializerUtils.WriteFloat(stream, arg3, 0, 1024, 0.0001f);
            SerializerUtils.WriteFloat(stream, arg4, 0, 1024, 0.0001f);
            SerializerUtils.WriteFloat(stream, arg5, 0, 1024, 0.0001f);

        }

        public static void Deserialize(ulong sender, int msgCode, ByteStream stream) {

            int arg0 = SerializerUtils.ReadInt(stream, 0, 255);
            int arg1 = SerializerUtils.ReadInt(stream, 0, 255);
            int arg2 = SerializerUtils.ReadInt(stream, 0, 255);

            float arg3 = SerializerUtils.ReadFloat(stream, 0, 1024, 0.0001f);
            float arg4 = SerializerUtils.ReadFloat(stream, 0, 1024, 0.0001f);
            float arg5 = SerializerUtils.ReadFloat(stream, 0, 1024, 0.0001f);

            //no need for a null check, can't have a deserializer without a processor.
            //I mean, you can, but it wouldn't do anything with the data you just received
            Core.net.MessageProcessors[msgCode](sender, arg0, arg1, arg2, arg3, arg4, arg5);
        }

        public static int Peek(params object[] args) {
            int s = 0;
            s += SerializerUtils.RequiredBitsInt(0, 255);
            s += SerializerUtils.RequiredBitsInt(0, 255);
            s += SerializerUtils.RequiredBitsInt(0, 255);

            s += SerializerUtils.RequiredBitsFloat(0, 1024, 0.0001f);
            s += SerializerUtils.RequiredBitsFloat(0, 1024, 0.0001f);
            s += SerializerUtils.RequiredBitsFloat(0, 1024, 0.0001f);

            return s;
        }
        
        public static float Priority(ulong receiver, params object[] args) {
            return (float)((int)args[0]); //we send our first int as a random number between 0, 255
                                   //so we're sorting priority based on the value we're sending.  
            //Messages with higher arg[0] value will come through first.
        }
    }

}
