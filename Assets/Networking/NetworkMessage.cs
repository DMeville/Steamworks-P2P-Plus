using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//wrapper for our message queue.
public class NetworkMessage {
    public int msgCode;
    public object[] args;

    public int skipped = 0; //incremented every time this message is in the queue and is skipped because it's too low of priority
    public float priority = 0f; //stored when sending, this is what we sort by

    public float random = 0f;

    public NetworkMessage(int msgCode, params object[] args) {
        this.msgCode = msgCode;
        this.args = args;
        random = Random.value * 10f;
    }

  

    //should pool these.
}