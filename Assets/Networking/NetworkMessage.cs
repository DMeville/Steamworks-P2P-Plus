using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//wrapper for our message queue.
public class NetworkMessage {
    public int msgCode;
    public object[] args;

    public float priority; //this only makes sense for states or entity related events.
                            //this will be called directly through.  Dunno where this will be updated, but we should
                            //sort by priority when we pack the messages into the stream or SOMETHING

    public NetworkMessage(int msgCode, params object[] args) {
        this.msgCode = msgCode;
        this.args = args;
    }

  

    //should pool these.
}