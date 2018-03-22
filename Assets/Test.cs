using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

using InputStream = BitwiseMemoryInputStream;
using OutputStream = BitwiseMemoryOutputStream;

public class Test : MonoBehaviour {

	void Start () {

        //[0-15] or [0-f] can be stored in 4 bits (1111) = f
        //guid = 32 (16bit) digits [00000000 0000 0000 0000 000000000000]
        //min bits = 128.  Probably doesn't neeeed to be unique and 128bits, we can be smarter.

        //8 bit would allow us to have 255 active entities.
        //12 bit would allow us to have 4095 active entities.
        //16 bit 65535.  this is 18 hours of creating one new entity per second before we reach the cap.
        //computer would be before this anyways probably.  Good luck having 65k entities on screeeen
        //[0000 0000 0000 0000].  Substantially less than  128 bit.  So lets not use guid for object ids.
        //could be smarter about this too and keep a list of 4095 and "findnext" available. so if we rollover we start filling empty spots.
        //this means we have a max entity count though

        //we need a way to know which gameobject to modify the state of when we get a state update. 
        //so we need to id them somehow.  We can't just "add to a list" because we can't guarentee order.
        //uint16 would give us [0-> 65535] values in[00000000 00000000] bits. Which is probably a small enough overhead.
        //we could combine state updates too so we don't send networkid per property update.  State updates will probably be atomic.
        int maxValue = 10;
        int minValue = -10;
        float precision = 0.001f;

        int intMax = (int)((maxValue - minValue + precision) * (1f / precision));
        Debug.Log("bits: " + SerializerUtils.RequiredBits(0, intMax));

    }
}
