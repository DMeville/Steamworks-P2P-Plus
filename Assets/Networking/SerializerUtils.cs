using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.Linq;
using InputStream = BitwiseMemoryInputStream;
using OutputStream = BitwiseMemoryOutputStream;

public static class SerializerUtils  {

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

        uint c = (uint)(maxValue - maxValue);
        uint d = (uint)(maxValue - minValue);

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
    public static void WriteInt(this OutputStream stream, int value, int minValue = int.MinValue, int maxValue = int.MaxValue) {
        int reqBit = (int)RequiredBits(minValue, maxValue);

        if(value < minValue || value > maxValue) {
            Debug.Log($"Value [{value}] is outside of range [{minValue}, {maxValue}] and will be clamped");
        }

        value = Mathf.Clamp(value, minValue, maxValue);
        value = value - minValue;

        stream.WriteInt(value, reqBit);
    }


    public static int ReadInt(this InputStream stream, int minValue = int.MinValue, int maxValue = int.MaxValue) {
        int reqBit = (int)RequiredBits(minValue, maxValue);
        return stream.ReadInt(reqBit) + minValue;
    }

    //don't need a custom range for bools, because they are always one bit
    //need a custom range for floats
}
