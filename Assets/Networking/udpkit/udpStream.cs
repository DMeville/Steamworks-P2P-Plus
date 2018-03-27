/*
* The MIT License (MIT)
* 
* Copyright (c) 2012-2014 Fredrik Holmstrom (fredrik.johan.holmstrom@gmail.com)
* 
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
* 
* The above copyright notice and this permission notice shall be included in
* all copies or substantial portions of the Software.
* 
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
* THE SOFTWARE.
*/

using System;
using System.Text;

namespace UdpKit {
    public class UdpStream:IDisposable {
        internal bool IsPooled = true;
        internal UdpStreamPool Pool;

        internal int Ptr;
        internal int Length;
        internal byte[] Data;

        /// <summary>
        /// A user-assignable object
        /// </summary>
        public object UserToken {
            get;
            set;
        }

        public int Size {
            get { return Length; }
            set { Length = UdpMath.Clamp(value, 0, Data.Length << 3); }
        }

        public int Position {
            get { return Ptr; }
            set { Ptr = UdpMath.Clamp(value, 0, Length); }
        }

        public bool Done {
            get { return Ptr == Length; }
        }

        public bool Overflowing {
            get { return Ptr > Length; }
        }

        public byte[] ByteBuffer {
            get { return Data; }
        }

        public UdpStream(byte[] arr)
            : this(arr, arr.Length) {
        }

        public UdpStream(byte[] arr, int size) {
            Ptr = 0;
            Data = arr;
            Length = size << 3;
        }

        public bool CanWrite() {
            return CanWrite(1);
        }

        public bool CanRead() {
            return CanRead(1);
        }

        public bool CanWrite(int bits) {
            return Ptr + bits <= Length;
        }

        public bool CanRead(int bits) {
            return Ptr + bits <= Length;
        }

        public void Reset(int size) {
            Ptr = 0;
            Length = size << 3;

            Array.Clear(Data, 0, Data.Length);
        }

        public bool WriteBool(bool value) {
#if TRACE_RW
            if (UdpLog.IsEnabled(UdpLog.TRACE))
                UdpLog.Trace("Writing bool (1 bit)");
#endif
            //Core.net.bitsOut += 1;
            Core.net.AddToBandwidthOutBuffer(1);
            //UnityEngine.Debug.Log("Write Bool (1 bit");
            InternalWriteByte(value ? (byte)1 : (byte)0, 1);
            return value;
        }

        public bool ReadBool() {
#if TRACE_RW
            if (UdpLog.IsEnabled(UdpLog.TRACE))
                UdpLog.Trace("Reading bool (1 bit)");
#endif
            //Core.net.bitsIn += 1;
            Core.net.AddToBandwidthInBuffer(1);

            //UnityEngine.Debug.Log("Read Bool (1 bit");
            return InternalReadByte(1) == 1;
        }

        public void WriteByte(byte value, int bits) {
#if TRACE_RW
            if (UdpLog.IsEnabled(UdpLog.TRACE))
                UdpLog.Trace("Writing byte ({0} bits)", bits);
#endif
            InternalWriteByte(value, bits);
        }

        public byte ReadByte(int bits) {
#if TRACE_RW
            if (UdpLog.IsEnabled(UdpLog.TRACE))
                UdpLog.Trace("Reading byte ({0} bits)", bits);
#endif
            return InternalReadByte(bits);
        }

        public void WriteByte(byte value) {
            WriteByte(value, 8);
        }

        public byte ReadByte() {
            return ReadByte(8);
        }

        public void WriteSByte(sbyte value, int bits) {
#if TRACE_RW
            if (UdpLog.IsEnabled(UdpLog.TRACE))
                UdpLog.Trace("Writing sbyte ({0} bits)", bits);
#endif
            InternalWriteByte((byte)value, bits);
        }

        public sbyte ReadSByte(int bits) {
#if TRACE_RW
            if (UdpLog.IsEnabled(UdpLog.TRACE))
                UdpLog.Trace("Reading sbyte ({0} bits)", bits);
#endif
            return (sbyte)InternalReadByte(bits);
        }

        public void WriteSByte(sbyte value) {
            WriteSByte(value, 8);
        }

        public sbyte ReadSByte() {
            return ReadSByte(8);
        }

        public void WriteUShort(ushort value, int bits) {
#if TRACE_RW
            if (UdpLog.IsEnabled(UdpLog.TRACE))
                UdpLog.Trace("Writing ushort ({0} bits)", bits);
#endif
            if(bits <= 8) {
                InternalWriteByte((byte)(value & 0xFF), bits);
            } else {
                InternalWriteByte((byte)(value & 0xFF), 8);
                InternalWriteByte((byte)(value >> 8), bits - 8);
            }
        }

        public ushort ReadUShort(int bits) {
#if TRACE_RW
            if (UdpLog.IsEnabled(UdpLog.TRACE))
                UdpLog.Trace("Reading ushort ({0} bits)", bits);
#endif
            if(bits <= 8) {
                return InternalReadByte(bits);
            } else {
                return (ushort)(InternalReadByte(8) | (InternalReadByte(bits - 8) << 8));
            }
        }

        public void WriteUShort(ushort value) {
            WriteUShort(value, 16);
        }

        public ushort ReadUShort() {
            return ReadUShort(16);
        }

        public void WriteShort(short value, int bits) {
            WriteUShort((ushort)value, bits);
        }

        public short ReadShort(int bits) {
            return (short)ReadUShort(bits);
        }

        public void WriteShort(short value) {
            WriteShort(value, 16);
        }

        public short ReadShort() {
            return ReadShort(16);
        }

        public void WriteChar(char value, int bits) {
            UdpByteConverter bytes = value;
            WriteUShort(bytes.Unsigned16, bits);
        }

        public char ReadChar(int bits) {
            UdpByteConverter bytes = ReadUShort(bits);
            return bytes.Char;
        }

        public void WriteChar(char value) {
            WriteChar(value, 16);
        }

        public char ReadChar() {
            return ReadChar(16);
        }

        public void WriteUInt(uint value, int bits) {
#if TRACE_RW
            if (UdpLog.IsEnabled(UdpLog.TRACE))
                UdpLog.Trace("Writing uint ({0} bits)", bits);
#endif
            //UnityEngine.Debug.Log("Write Int (" + bits + " bits)");
            Core.net.AddToBandwidthOutBuffer(bits);
            //Core.net.bitsOut += bits;
            //UnityEngine.Debug.Log(string.Format("Writing uint ({0} bits)", bits));
            byte
                a = (byte)(value >> 0),
                b = (byte)(value >> 8),
                c = (byte)(value >> 16),
                d = (byte)(value >> 24);

            switch((bits + 7) / 8) {
                case 1:
                    InternalWriteByte(a, bits);
                    break;

                case 2:
                    InternalWriteByte(a, 8);
                    InternalWriteByte(b, bits - 8);
                    break;

                case 3:
                    InternalWriteByte(a, 8);
                    InternalWriteByte(b, 8);
                    InternalWriteByte(c, bits - 16);
                    break;

                case 4:
                    InternalWriteByte(a, 8);
                    InternalWriteByte(b, 8);
                    InternalWriteByte(c, 8);
                    InternalWriteByte(d, bits - 24);
                    break;
            }
        }

        public uint ReadUInt(int bits) {
#if TRACE_RW
            if (UdpLog.IsEnabled(UdpLog.TRACE))
                UdpLog.Trace("Reading uint ({0} bits)", bits);
#endif
            //UnityEngine.Debug.Log("Read Int (" + bits + " bits)");
            //Core.net.bitsIn += bits;
            Core.net.AddToBandwidthInBuffer(bits);
            int
                a = 0,
                b = 0,
                c = 0,
                d = 0;

            switch((bits + 7) / 8) {
                case 1:
                    a = InternalReadByte(bits);
                    break;

                case 2:
                    a = InternalReadByte(8);
                    b = InternalReadByte(bits - 8);
                    break;

                case 3:
                    a = InternalReadByte(8);
                    b = InternalReadByte(8);
                    c = InternalReadByte(bits - 16);
                    break;

                case 4:
                    a = InternalReadByte(8);
                    b = InternalReadByte(8);
                    c = InternalReadByte(8);
                    d = InternalReadByte(bits - 24);
                    break;
            }

            return (uint)(a | (b << 8) | (c << 16) | (d << 24));
        }

        public void WriteUInt(uint value) {
            WriteUInt(value, 32);
        }

        public uint ReadUInt() {
            return ReadUInt(32);
        }

        public void WriteInt(int value, int bits) {
            WriteUInt((uint)value, bits);
        }

        public int ReadInt(int bits) {
            return (int)ReadUInt(bits);
        }

        public void WriteInt(int value) {
            WriteInt(value, 32);
        }

        public int ReadInt() {
            return ReadInt(32);
        }

        public void WriteEnum32<T>(T value, int bits) where T : struct {
            WriteInt(UdpUtils.EnumToInt(value), bits);
        }

        public T ReadEnum32<T>(int bits) where T : struct {
            return UdpUtils.IntToEnum<T>(ReadInt(bits));
        }

        public void WriteEnum32<T>(T value) where T : struct {
            WriteEnum32<T>(value, 32);
        }

        public T ReadEnum32<T>() where T : struct {
            return ReadEnum32<T>(32);
        }

        public void WriteULong(ulong value, int bits) {
#if TRACE_RW
            if (UdpLog.IsEnabled(UdpLog.TRACE))
                UdpLog.Trace("Writing ulong ({0} bits)", bits);
#endif
            if(bits <= 32) {
                WriteUInt((uint)(value & 0xFFFFFFFF), bits);
            } else {
                WriteUInt((uint)(value), 32);
                WriteUInt((uint)(value >> 32), bits - 32);
            }
        }

        public ulong ReadULong(int bits) {
#if TRACE_RW
            if (UdpLog.IsEnabled(UdpLog.TRACE))
                UdpLog.Trace("Reading ulong ({0} bits)", bits);
#endif
            if(bits <= 32) {
                return ReadUInt(bits);
            } else {
                ulong a = ReadUInt(32);
                ulong b = ReadUInt(bits - 32);
                return a | (b << 32);
            }
        }

        public void WriteULong(ulong value) {
            WriteULong(value, 64);
        }

        public ulong ReadULong() {
            return ReadULong(64);
        }

        public void WriteLong(long value, int bits) {
            WriteULong((ulong)value, bits);
        }

        public long ReadLong(int bits) {
            return (long)ReadULong(bits);
        }

        public void WriteLong(long value) {
            WriteLong(value, 64);
        }

        public long ReadLong() {
            return ReadLong(64);
        }

        public void WriteHalf(float value) {
            WriteUShort(SlimMath.HalfUtilities.Pack(value), 16);
        }

        public float ReadHalf() {
            return SlimMath.HalfUtilities.Unpack(ReadUShort(16));
        }

        public void WriteFloat(float value) {
#if TRACE_RW
            if (UdpLog.IsEnabled(UdpLog.TRACE))
                UdpLog.Trace("Writing float (32 bits)");
#endif
            UdpByteConverter bytes = value;
            InternalWriteByte(bytes.Byte0, 8);
            InternalWriteByte(bytes.Byte1, 8);
            InternalWriteByte(bytes.Byte2, 8);
            InternalWriteByte(bytes.Byte3, 8);
        }

        public float ReadFloat() {
#if TRACE_RW
            if (UdpLog.IsEnabled(UdpLog.TRACE))
                UdpLog.Trace("Reading float (32 bits)");
#endif
            UdpByteConverter bytes = default(UdpByteConverter);
            bytes.Byte0 = InternalReadByte(8);
            bytes.Byte1 = InternalReadByte(8);
            bytes.Byte2 = InternalReadByte(8);
            bytes.Byte3 = InternalReadByte(8);
            return bytes.Float32;
        }

        public void WriteDouble(double value) {
#if TRACE_RW
            if (UdpLog.IsEnabled(UdpLog.TRACE))
                UdpLog.Trace("Writing double (64 bits)");
#endif
            UdpByteConverter bytes = value;
            InternalWriteByte(bytes.Byte0, 8);
            InternalWriteByte(bytes.Byte1, 8);
            InternalWriteByte(bytes.Byte2, 8);
            InternalWriteByte(bytes.Byte3, 8);
            InternalWriteByte(bytes.Byte4, 8);
            InternalWriteByte(bytes.Byte5, 8);
            InternalWriteByte(bytes.Byte6, 8);
            InternalWriteByte(bytes.Byte7, 8);
        }

        public double ReadDouble() {
#if TRACE_RW
            if (UdpLog.IsEnabled(UdpLog.TRACE))
                UdpLog.Trace("Reading double (64 bits)");
#endif
            UdpByteConverter bytes = default(UdpByteConverter);
            bytes.Byte0 = InternalReadByte(8);
            bytes.Byte1 = InternalReadByte(8);
            bytes.Byte2 = InternalReadByte(8);
            bytes.Byte3 = InternalReadByte(8);
            bytes.Byte4 = InternalReadByte(8);
            bytes.Byte5 = InternalReadByte(8);
            bytes.Byte6 = InternalReadByte(8);
            bytes.Byte7 = InternalReadByte(8);
            return bytes.Float64;
        }

        public void WriteByteArray(byte[] from) {
            WriteByteArray(from, 0, from.Length);
        }

        public void WriteByteArray(byte[] from, int count) {
            WriteByteArray(from, 0, count);
        }

        public void WriteByteArray(byte[] from, int offset, int count) {
#if TRACE_RW
            if (UdpLog.IsEnabled(UdpLog.TRACE))
                UdpLog.Trace("Writing byte array ({0} bits)", count * 8);
#endif
            int p = Ptr >> 3;
            int bitsUsed = Ptr % 8;
            int bitsFree = 8 - bitsUsed;

            if(bitsUsed == 0) {
                Buffer.BlockCopy(from, offset, Data, p, count);
            } else {
                for(int i = 0; i < count; ++i) {
                    byte value = from[offset + i];

                    Data[p] &= (byte)(0xFF >> bitsFree);
                    Data[p] |= (byte)(value << bitsUsed);

                    p += 1;

                    Data[p] &= (byte)(0xFF << bitsUsed);
                    Data[p] |= (byte)(value >> bitsFree);
                }
            }

            Ptr += (count * 8);
        }

        public void ReadByteArray(byte[] to) {
            ReadByteArray(to, 0, to.Length);
        }

        public void ReadByteArray(byte[] to, int count) {
            ReadByteArray(to, 0, count);
        }

        public void ReadByteArray(byte[] to, int offset, int count) {
#if TRACE_RW
            if (UdpLog.IsEnabled(UdpLog.TRACE))
                UdpLog.Trace("Reading byte array ({0} bits)", count * 8);
#endif

            int p = Ptr >> 3;
            int bitsUsed = Ptr % 8;

            if(bitsUsed == 0) {
                Buffer.BlockCopy(Data, p, to, offset, count);
            } else {
                int bitsNotUsed = 8 - bitsUsed;

                for(int i = 0; i < count; ++i) {
                    int first = Data[p] >> bitsUsed;

                    p += 1;

                    int second = Data[p] & (255 >> bitsNotUsed);
                    to[offset + i] = (byte)(first | (second << bitsNotUsed));
                }
            }

            Ptr += (count * 8);
        }

        public void WriteString(string value, Encoding encoding) {
            WriteString(value, encoding, int.MaxValue);
        }

        public void WriteString(string value, Encoding encoding, int length) {
            if(string.IsNullOrEmpty(value)) {
                WriteUShort(0);
            } else {
                if(length < value.Length) {
                    value = value.Substring(0, length);
                }

                WriteUShort((ushort)encoding.GetByteCount(value));
                WriteByteArray(encoding.GetBytes(value));
            }
        }

        public void WriteString(string value) {
            WriteString(value, Encoding.UTF8);
        }

        public string ReadString(Encoding encoding) {
            int byteCount = ReadUShort();

            if(byteCount == 0) {
                return "";
            }

            var bytes = new byte[byteCount];

            ReadByteArray(bytes);

            return encoding.GetString(bytes);
        }

        public string ReadString() {
            return ReadString(Encoding.UTF8);
        }

        void InternalWriteByte(byte value, int bits) {
            if(bits <= 0)
                return;

            value = (byte)(value & (0xFF >> (8 - bits)));

            int p = Ptr >> 3;
            int bitsUsed = Ptr & 0x7;
            int bitsFree = 8 - bitsUsed;
            int bitsLeft = bitsFree - bits;

            if(bitsLeft >= 0) {
                int mask = (0xFF >> bitsFree) | (0xFF << (8 - bitsLeft));
                Data[p] = (byte)((Data[p] & mask) | (value << bitsUsed));
            } else {
                Data[p] = (byte)((Data[p] & (0xFF >> bitsFree)) | (value << bitsUsed));
                Data[p + 1] = (byte)((Data[p + 1] & (0xFF << (bits - bitsFree))) | (value >> bitsFree));
            }

            Ptr += bits;
        }

        byte InternalReadByte(int bits) {
            if(bits <= 0)
                return 0;

            byte value;
            int p = Ptr >> 3;
            int bitsUsed = Ptr % 8;

            if(bitsUsed == 0 && bits == 8) {
                value = Data[p];
            } else {
                int first = Data[p] >> bitsUsed;
                int remainingBits = bits - (8 - bitsUsed);

                if(remainingBits < 1) {
                    value = (byte)(first & (0xFF >> (8 - bits)));
                } else {
                    int second = Data[p + 1] & (0xFF >> (8 - remainingBits));
                    value = (byte)(first | (second << (bits - remainingBits)));
                }
            }

            Ptr += bits;
            //UnityEngine.Debug.Log(BitTools.BitDisplay.ByteToString(value));
            return value;
        }

        public void Dispose() {
            if(Pool != null) {
                Pool.Release(this);
            }
        }
    }
}