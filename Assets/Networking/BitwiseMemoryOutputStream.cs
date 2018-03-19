using System;
using System.Text;

/**
 * This class is a bit-wise memory output stream implementation
 * https://github.com/M-Aghasi/Unity-UdpSocket-BitStream-Utilities
 */
public class BitwiseMemoryOutputStream {

    private int _capacity;
    private int _head;
    private byte[] _buffer;

    private static readonly int INITIAL_BUFFER_SIZE_IN_BYTE = 600;

    public BitwiseMemoryOutputStream() {
        _head = 0;
        ReallocateBuffer(INITIAL_BUFFER_SIZE_IN_BYTE * 8);
    }

    /**
     * writes an int value to buffer by 32 bits
     */
    public void WriteInt(int data) {
        WriteInt(data, 32);
    }

    /**
     * writes an int value to buffer by specified bits count
     * example: writeInt(7, 3);
     */
    public void WriteInt(int data, int bitCount) {

        if (bitCount < 1) {
            return;
        }
        if (bitCount > 32) {
            bitCount = 32;
        }
        int byteCount = bitCount / 8;
        if (bitCount % 8 > 0) {
            byteCount++;
        }
        byte[] srcByte = IntToBytes(data, byteCount);
        int bitsToWrite = (bitCount % 8 == 0) ? 8 : bitCount % 8;
        WriteBits(srcByte[0], bitsToWrite);

        int i = 1;
        //write all the bytes
        while (i < byteCount) {
            WriteBits(srcByte[i], 8);
            ++i;
        }
    }

    /**
     * writes a long value to buffer by 64 bits
     */
    public void WriteLong(long data) {
        WriteLong(data, 64);
    }

    /**
     * writes a long value to buffer by specified bits count
     * example: writeLong(7L, 3);
     */
    public void WriteLong(long data, int bitCount) {

        if (bitCount < 1) {
            return;
        }
        if (bitCount > 64) {
            bitCount = 64;
        }
        int byteCount = bitCount / 8;
        if (bitCount % 8 > 0) {
            byteCount++;
        }
        byte[] srcByte = LongToBytes(data, byteCount);
        int bitsToWrite = (bitCount % 8 == 0) ? 8 : bitCount % 8;
        WriteBits(srcByte[0], bitsToWrite);

        int i = 1;
        //write all the bytes
        while (i < byteCount) {
            WriteBits(srcByte[i], 8);
            ++i;
        }
    }

    /**
     * writes a double value to buffer by 64 bits
     */
    public void WriteDouble(double data) {
        WriteDouble(data, 64);
    }

    /**
     * as float/double representation in binary is different than int/long
     * its better to write float/double by all bits.
     */
    private void WriteDouble(double data, int bitCount) {

        if (bitCount < 1) {
            return;
        }
        if (bitCount > 64) {
            bitCount = 64;
        }
        int byteCount = bitCount / 8;
        if (bitCount % 8 > 0) {
            byteCount++;
        }

        byte[] srcByte = LongToBytes(BitConverter.DoubleToInt64Bits(data), byteCount);
        int bitsToWrite = (bitCount % 8 == 0) ? 8 : bitCount % 8;
        WriteBits(srcByte[0], bitsToWrite);

        int i = 1;
        //write all the bytes
        while (i < byteCount) {
            WriteBits(srcByte[i], 8);
            ++i;
        }
    }

    /**
     * writes a float value to buffer by 32 bits
     */
    public void WriteFloat(float data) {
        WriteFloat(data, 32);
    }

    /**
     * as float/double representation in binary is different than int/long
     * its better to write float/double by all bits.
     */
    private void WriteFloat(float data, int bitCount) {

        if (bitCount < 1) {
            return;
        }
        if (bitCount > 32) {
            bitCount = 32;
        }
        int byteCount = bitCount / 8;
        if (bitCount % 8 > 0) {
            byteCount++;
        }
        byte[] srcByte = IntToBytes(BitConverter.ToInt32(BitConverter.GetBytes(data), 0), byteCount);
        int bitsToWrite = (bitCount % 8 == 0) ? 8 : bitCount % 8;
        WriteBits(srcByte[0], bitsToWrite);

        int i = 1;
        //write all the bytes
        while (i < byteCount) {
            WriteBits(srcByte[i], 8);
            ++i;
        }
    }

    /**
     * writes a signed int value to buffer by 32 bits
     */
    public void WriteSigned(int data) {
        WriteSigned(data, 32);
    }

    /**
     * writes a signed int value to buffer by specified bits count
     * example: writeSigned(-7, 3);
     * it first writes number's sign as a bool and then writes data's absolute value as an int
     */
    public void WriteSigned(int data, int bitCount) {

        bool isNegative = (data < 0);
        if (isNegative) {
            data *= -1;
        }
        WriteBool(isNegative);
        WriteInt(data, bitCount);
    }

    /**
     * writes a signed long value to buffer by 64 bits
     */
    public void WriteSignedLong(long data) {
        WriteSignedLong(data, 64);
    }

    /**
     * writes a signed long value to buffer by specified bits count
     * example: writeSignedLong(-7L, 3);
     * it first writes number's sign as a bool and then writes data's absolute value as a long
     */
    public void WriteSignedLong(long data, int bitCount) {

        bool isNegative = (data < 0);
        if (isNegative) {
            data *= -1;
        }
        WriteBool(isNegative);
        WriteLong(data, bitCount);
    }

    /**
     * writes a byte value to buffer by 8 bits
     */
    public void WriteByte(byte data) {
        WriteBits(data, 8);
    }

    /**
     * writes a byte value to buffer by specified bits count
     * example: writeByte(0x7, 3);
     */
    public void WriteByte(byte data, int bitCount) {
        WriteBits(data, bitCount);
    }

    /**
     * writes a bool value to buffer by 1 bit
     */
    public void WriteBool(bool data) {
        WriteBits(data ? (byte)1 : (byte)0, 1);
    }

    /**
     * writes a string value to buffer based on string's length
     * it first writes string's length as an int value, then writes string's bytes to buffer
     */
    public void WriteString(string data) {
        byte[] bytes = Encoding.UTF8.GetBytes(data);
        WriteInt(bytes.Length);
        foreach (byte element in bytes) {
            WriteByte(element);
        }
    }

    private void WriteBits(byte data, int bitCount) {

        int nextHead = _head + bitCount;
        // reallocate buffer if there is no enough free space for new data
        if (nextHead > _capacity) {
            ReallocateBuffer(System.Math.Max(_capacity * 2, nextHead));
        }

        //// example for _head=9:
        // _head{00001001} >> 3 = {00000001} = 1
        //// or:
        // 9 / 8 = 1
        int byteOffset = _head >> 3;    // or -> _head / 8

        //// example for _head=9:
        // _head{00001001} & {00000111} = {00000001} = 1
        int bitOffsetInCurrentByte = _head & 0x7;

        //// example for _head=9:
        // ~({11111111} >> 1) = ~({01111111}) = {10000000}
        byte currentMask = (byte)~(0xff >> bitOffsetInCurrentByte);     // mask of currentByte, ones are written zeros are free

        //// example for _head=9 and currentByte={10000000} and data=7 and bitCount=3:
        // ((currentByte{10000000} & currentMask{10000000}) | ((data{00000111} << 8 - bitCount) >> bitOffsetInCurrentByte))
        // = (({10000000}) | ((data{00000111} << 5) >> 1))
        // = (({10000000}) | ({11100000} >> 1))
        // = ({10000000} | {01110000})
        // =  {11110000}
        _buffer[byteOffset] = (byte)((_buffer[byteOffset] & currentMask) | ((data << 8 - bitCount) >> bitOffsetInCurrentByte));

        int bitsFreeThisByte = 8 - bitOffsetInCurrentByte;

        // go to next byte if data is bigger than currentByte's free space
        if (bitsFreeThisByte < bitCount) {
            _buffer[byteOffset + 1] = (byte)((data << 8 - bitCount) << bitsFreeThisByte);
        }
        _head = nextHead;
    }

    private byte[] IntToBytes(int num, int byteCount) {

        byte[] result = new byte[byteCount];
        for (int i = 0; byteCount > 0; i++, byteCount--) {
            result[i] = (byte)(num >> ((byteCount - 1) * 8));
        }
        return result;
    }

    private byte[] LongToBytes(long num, int byteCount) {

        byte[] result = new byte[byteCount];

        for (int i = 0; byteCount > 0; i++, byteCount--) {
            result[i] = (byte)(num >> ((byteCount - 1) * 8));
        }
        return result;
    }

    private void ReallocateBuffer(int newCapacity) {

        if (_buffer == null) {
            _buffer = new byte[newCapacity >> 3];
        }
        else {
            byte[] tempBuffer = new byte[newCapacity >> 3];
            Array.Copy(_buffer, 0, tempBuffer, 0, _buffer.Length);
            _buffer = null;
            _buffer = tempBuffer;
        }
        _capacity = newCapacity;
    }

    /**
     * returns buffer as a byte array
     */
    public byte[] GetBuffer() {
        byte[] temp = new byte[GetByteLength()];
        Array.Copy(_buffer, temp, GetByteLength());
        return temp;
    }

    /**
     * returns buffer length in bits
     */
    public int GetBitLength() {
        return _head;
    }

    /**
     * returns buffer length in bytes
     */
    public int GetByteLength() {
        return (_head + 7) >> 3;
    }
}

