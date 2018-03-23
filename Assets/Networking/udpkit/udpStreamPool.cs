using System.Collections.Generic;

namespace UdpKit {
    public class UdpStreamPool {
        //readonly UdpSocket socket;
        readonly Stack<UdpStream> pool = new Stack<UdpStream>();
        const int PacketSize = 1024; //default is 1024bytes (1kb)

        internal UdpStreamPool(/*UdpSocket s*/) {
            //socket = s;
        }

        internal void Release(UdpStream stream) {
            UdpAssert.Assert(stream.IsPooled == false);

            lock(pool) {
                stream.Size = 0;
                stream.Position = 0;
                stream.IsPooled = true;

                pool.Push(stream);
            }
        }

        public UdpStream Acquire() {
            UdpStream stream = null;

            lock(pool) {
                if(pool.Count > 0) {
                    stream = pool.Pop();
                }
            }

            if(stream == null) {
                stream = new UdpStream(new byte[PacketSize * 2]);
                stream.Pool = this;
            }

            UdpAssert.Assert(stream.IsPooled);

            stream.IsPooled = false;
            stream.Position = 0;
            //stream.Size = (PacketSize - UdpMath.BytesRequired(UdpSocket.HeaderBitSize)) << 3;
            stream.Size = (PacketSize - 0) << 3; //we're not using this for it's intended purpose,
            //just hijacking it to use it's bitpacking methods.

            return stream;
        }

        public void Free() {
            lock(pool) {
                while(pool.Count > 0) {
                    pool.Pop();
                }
            }
        }
    }
}