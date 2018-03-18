using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using System.Linq;

public static class SerializerUtils  {

    //Could have extension methods that also have a range, so we can compress values further
    //without using the full sizeof(T) for each type.
    //eg, if we know an int will only be between [0-8], we don't have to use 4 bytes for it, only 2.
    //so long as we read it back the same way.

    //reads are destructive

    public static string ToStringBinary(this byte[] a) {
        string s = "";
        a.ToList().ForEach(p => { s += Convert.ToString(p, 2).PadLeft(8, '0'); s += " "; });
        return s;
    }

    public static string ToStringBinary(this byte a) {
        return Convert.ToString(a, 2).PadLeft(8, '0');
    }

    public static byte[] Append(this byte[] a, byte[] b) {
        return a.Concat(b).ToArray();
    }

    //int32 methods
    public static byte[] WriteInt(int value) {
        return BitConverter.GetBytes(value);
    }

    public static int ReadInt(ref byte[] data) {
        int v = BitConverter.ToInt32(data, 0);
        data = data.Skip(sizeof(int)).ToArray();
        return v; 
    }

    //bool methods
    public static byte[] WriteBool(bool value) {
        return BitConverter.GetBytes(value);
    }

    public static bool ReadBool(ref byte[] data) {
        bool v = BitConverter.ToBoolean(data, 0);
        data = data.Skip(sizeof(bool)).ToArray();
        return v;
    }

    //string methods
    public static byte[] WriteString(string value) {
        return Encoding.UTF8.GetBytes(value);
    }

    public static string ReadString(byte[] data) {
        string v = Encoding.UTF8.GetString(data);
        data = data.Skip(sizeof(Char) * v.Length).ToArray();
        return v;
    }

    //float (single) methods
    public static byte[] WriteFloat(float value) {
        return BitConverter.GetBytes(value);
    }

    public static float ReadFloat(byte[] data) {
        float v = BitConverter.ToSingle(data, 0);
        data = data.Skip(sizeof(float)).ToArray();
        return v;
    }
}
