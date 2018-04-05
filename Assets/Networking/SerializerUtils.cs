using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.Linq;


public static class SerializerUtils {

    public static string ToStringBinary(this byte[] a, bool pad = true) {
        string s = "";
        if(pad) {
            a.ToList().ForEach(p => { s += Convert.ToString(p, 2).PadLeft(8, '0'); s += " "; });
        } else {
            a.ToList().ForEach(p => { s += Convert.ToString(p, 2); s += " "; });
        }

        return s;
    }

    public static string ToStringBinary(this byte a) {
        return Convert.ToString(a, 2).PadLeft(8, '0');
    }

    //combine two byte arrays
    //byte[] a = byte[1];
    //a = a.Append(new byte[1]);
    public static byte[] Append(this byte[] a, byte[] b) {
        return a.Concat(b).ToArray();
    }

    //Calculates the number of bits required to pack a value inside a specific range
    //eg, [0,4] range would only require 3 bits, instead of 32bit int
    //RequiredBits(0, 4) => 000->100 = 3 bits required
    public static uint RequiredBits(int minValue, int maxValue) {
        //shift min and max into range starting at 0
        //so (1,5) -> (0,4). (-2, 2) -> (0,4)

        //uint c = (uint)(maxValue - maxValue);
        //uint d = (uint)(maxValue - minValue);

        uint delta = (uint)(maxValue - minValue);

        //I have no idea how this works.  I found it on stackoverflow. ᕕ( ᐛ )ᕗ
        uint x = delta;
        x |= x >> 1;
        x |= x >> 2;
        x |= x >> 4;
        x |= x >> 8;
        x |= x >> 16;
        //count the ones
        x -= x >> 1 & 0x55555555;
        x = (x >> 2 & 0x33333333) + (x & 0x33333333);
        x = (x >> 4) + x & 0x0f0f0f0f;
        x += x >> 8;
        x += x >> 16;

        return x & 0x0000003f;
    }

    //values outside the specified range will be clamped 
    //shifts values into positive range so we don't have to send a sign bit
    //eg, [-4, 4] range and value of 0 gets shifted to
    // [0, 8] range and a value of 4
    //then we reverse it on the other side
    public static void WriteInt(UdpKit.UdpStream stream, int value, int minValue = int.MinValue, int maxValue = int.MaxValue) {
        int reqBit = (int)RequiredBits(minValue, maxValue);
        //Debug.Log("WriteInt: " + value + " : requiredBits : " + reqBit);
        if(value < minValue || value > maxValue) {
            Debug.Log($"Value [{value}] is outside of range [{minValue}, {maxValue}] and will be clamped");
        }

        value = Mathf.Clamp(value, minValue, maxValue);
        value = value - minValue;

        stream.WriteInt(value, reqBit);
    }


    public static int ReadInt(UdpKit.UdpStream stream, int minValue = int.MinValue, int maxValue = int.MaxValue) {
        int reqBit = (int)RequiredBits(minValue, maxValue);
        //Debug.Log("ReadInt: required bits:" + reqBit);
        return stream.ReadInt(reqBit) + minValue;
    }

    public static int RequiredBitsInt(int minValue = int.MinValue, int maxValue = int.MaxValue) {
        return (int)RequiredBits(minValue, maxValue);
    }

    public static int RequiredBitsBool() {
        return 1;
    }

    public static void WriteBool(UdpKit.UdpStream stream, bool value) {
        stream.WriteBool(value);
    }

    public static bool ReadBool(UdpKit.UdpStream stream) {
        return stream.ReadBool();
    }

    public static int RequiredBitsFloat(float minValue = float.MinValue, float maxValue = float.MaxValue, float precision = 0.0000001f) {
        int intMax = (int)((maxValue - minValue + precision) * (1f / precision));
        //Debug.Log("intmax: " + intMax);
        return (int)RequiredBitsInt(0, intMax);
    }

    ///precision is how many digits after decimal. Max is 7 (0.0000001)
    ///precision of 1 (1.1)
    ///2 (1.01)
    ///3 (1.001), etc.
    ///Lower precision means fewer bits to send
    public static void WriteFloat(UdpKit.UdpStream stream, float value, float minValue = float.MinValue, float maxValue = float.MaxValue, float precision = 0.0000001f) {
        //what is our int max value do we have from (min -> max ) with precision
        //[0->1] with 0.1 precision means
        //[0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0]
        //[0,   1,   2,   3,   4,   5,   6,   7,   8,   9,  10]
        //max value is 10!

        //[-1 ->1] with 0.1 prec
        //[-1.0, -0.9, -0.8, -0.7, -0.6, -0.5, -0.4, -0.3, -0.2, -0.1, 0.0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0]
        //lets push it to a pos rage.
        //[ 0.0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7, 1.8, 1.9, 2.0]
        //[   0,   1,   2,   3,   4,   5,   6,   7,   8,   9,  10,  11,  12,  13,  14,  15,  16,  17,  18,  19,  20]
        if(value < minValue || value > maxValue) {
            Debug.Log($"Value [{value}] is outside of range [{minValue}, {maxValue}] and will be clamped");
        }
        value = Mathf.Clamp(value, minValue, maxValue);
        //float oneOverPrecision = (1f / precision); would this be faster? probably not even worth it idk
        int intMax = (int)((maxValue - minValue + precision) * (1f / precision)); //10
        //Debug.Log("intMax: " + intMax);
        //don't round, just remove values past precision
        //if we passed in 0.4524, we should use the value 0.4 (precision 0.1)
        //and that should be int value of 4
        bool neg = (value < 0);
        value = value - minValue;
        //Debug.Log("offset value: " + value);
        if(!neg) {
            value = Mathf.Floor(value * (1 / precision)) * precision; //converts 0.452 to 0.4.
        } else {
            value = Mathf.Ceil(value * (1 / precision)) * precision; //converts 0.452 to 0.4.
        }
        //Debug.Log("Rounded value: " + value);
        float intVal = ((value) * (1f / precision)); //1.4 should be 14

        //Debug.Log("IntVal: " + intVal);
        //compress values [0->11]
        WriteInt(stream, (int)intVal, 0, intMax);
    }

    public static float ReadFloat(UdpKit.UdpStream stream, float minValue = float.MinValue, float maxValue = float.MaxValue, float precision = 0.0000001f) {
        int intMax = (int)((maxValue - minValue + precision) * (1f / precision));
        //Debug.Log("read intMax: " + intMax);
        int intVal = ReadInt(stream, 0, intMax);
        float value = (intVal * precision) + minValue;
        //need to clean it up because sometimes we're getting 1.000001 with 0.1 prec, so fix that
        value = Mathf.Round((value) * (1f / precision)) * precision;
        return value;
    }
    //don't need a custom range for bools, because they are always one bit

    //uses smallest three compression in the range of [-10, 10] by default. 
    //can't change the range as a quaterion is always [-1, 1] technically, but we can change the precision.
    public static void WriteQuaterinion(UdpKit.UdpStream stream, Quaternion rotation, float precision = 0.0000001f) {

        //find the index with the largest abs value
        int largestIndex = 0;
        float v = rotation[0];
        for(int i = 1; i < 4; i++) {
            if(Mathf.Abs(rotation[i]) > v) {
                v = rotation[i];
                largestIndex = i;
            }
        }

        //Debug.Log("largest index write: " + largestIndex + " v: " + v);
        WriteInt(stream, largestIndex, 0, 3);
        float sign = (rotation[largestIndex] < 0) ? -1 : 1;
        //if(Mathf.Approximately(rotation[largestIndex], 1f)) {
        //    //write one bit as true
        //    //because we know if any component is 1, everything else is 0 so don't bother even sending.
        //    //in *most* cases this is an extra bit as this isn't true often I imagine, so maybe it's not worth it
        //    //to save one bit infrequently, while having to send one bit more often.
        //} else {
        //    //write one bit as false
        //}
        
        switch(largestIndex) {
            case 0:
                WriteFloat(stream, rotation[1] * sign, -1f, 1f, precision);
                WriteFloat(stream, rotation[2] * sign, -1f, 1f, precision);
                WriteFloat(stream, rotation[3] * sign, -1f, 1f, precision);
                break;

            case 1:
                WriteFloat(stream, rotation[0] * sign, -1f, 1f, precision);
                WriteFloat(stream, rotation[2] * sign, -1f, 1f, precision);
                WriteFloat(stream, rotation[3] * sign, -1f, 1f, precision);
                break;

            case 2:
                WriteFloat(stream, rotation[0] * sign, -1f, 1f, precision);
                WriteFloat(stream, rotation[1] * sign, -1f, 1f, precision);
                WriteFloat(stream, rotation[3] * sign, -1f, 1f, precision);
                break;

            case 3:
                WriteFloat(stream, rotation[0] * sign, -1f, 1f, precision);
                WriteFloat(stream, rotation[1] * sign, -1f, 1f, precision);
                WriteFloat(stream, rotation[2] * sign, -1f, 1f, precision);
                break;
        }
    }

    public static Quaternion ReadQuaternion(UdpKit.UdpStream stream, float precision = 0.0000001f) {
        float x = 0f;
        float y = 0f;
        float z = 0f;
        float w = 0f;

        int largestIndex = ReadInt(stream, 0, 3);
        //Debug.Log("read largestIndex: " + largestIndex);
        //largestIndex needs to be calculated
        float a = ReadFloat(stream, -1f, 1f, precision);
        float b = ReadFloat(stream, -1f, 1f, precision);
        float c = ReadFloat(stream, -1f, 1f, precision);

        float d = Mathf.Sqrt(1f - ((a * a) + (b * b) + (c * c))); //largest

        //Debug.Log(a + " : " + b + " : " + c + " : " + d);

        switch(largestIndex) {
            case 0:
                return new Quaternion(d, a, b, c);
                break;
            case 1:
                return new Quaternion(a, d, b, c);
                break;

            case 2:
                return new Quaternion(a, b, d, c);
                break;
            case 3:
                return new Quaternion(a, b, c, d);
                break;
        }

        return new Quaternion(0f, 0f, 0f, 1f);
    }

    public static int RequiredBitsQuaternion(float precision = 0.0000001f) {
        int s = 0;
        s += RequiredBitsInt(0, 3);
        s += RequiredBitsFloat(-1f, 1f, precision);
        s += RequiredBitsFloat(-1f, 1f, precision);
        s += RequiredBitsFloat(-1f, 1f, precision);

        //Debug.Log("btf: " + RequiredBitsFloat(-1f, 1f, precision));
        return s;
    }
}
