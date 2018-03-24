using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//wrapper for our message queue.
public class NetworkMessage {
    public int msgCode;
    public object[] args;

    public int skipped = 0; //incremented every time this message is in the queue and is skipped because it's too low of priority
    public float priority = 0f; //stored when sending, this is what we sort by

    //can we store a reference to the entity (if it's an entity event or state)
    //then we can just update this networkMessage if it's been in the queue for so long
    //that we have a new state update we want to send?  That way we send only 
    //the most recent state, and not the older one first (as we don't need the older one if we have a newer one..)
    //?

    public NetworkMessage(int msgCode, params object[] args) {
        this.msgCode = msgCode;
        this.args = args;
    }

  

    //should pool these.
}