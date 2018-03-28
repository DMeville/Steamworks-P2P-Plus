﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ByteStream = UdpKit.UdpStream;
using Sirenix.OdinInspector;

public abstract class NetworkEntity:SerializedMonoBehaviour {

    public int prefabId;
    public int networkId;

    public int owner;
    public int controller;

    public bool isHidden = false;
    public bool isPendingDestroy = false;

    public Dictionary<ulong, NetworkMessage> queuedMessage = new Dictionary<ulong, NetworkMessage>();
    //so we can update it or delete it if it's a destroy or something
    //ulong is steamId, as we can have have one networkmessage per connection queue 
    //store one network message per connection because potentially we would be sending an update to everyone.
    //it *should* be the same update, so we could reuse the networkmessage but...that's confusing
    //and maybe sometimes you'd want to send different messages

    public float interpolationBufferTime = 100;//ms. could we change this based on distance or something? or priority?
    public List<List<Vector3Snapshot>> positionSnapshots = new List<List<Vector3Snapshot>>();
    public List<List<QuaternionSnapshot>> rotationSnapshots = new List<List<QuaternionSnapshot>>();
    public List<List<FloatSnapshot>> floatSnapshots = new List<List<FloatSnapshot>>();
    public List<List<IntSnapshot>> intSnapshots = new List<List<IntSnapshot>>();
    //what else might we want to interpolate? Colour?

    //hold the packet for this time then pop it to our targetPos;
    //needs interpolation buffer, whatever that means.

    /// <summary>
    /// Called when this gameobject is created in the world via a network spawn (on both the caller and reciever)
    /// use this to set initial state data, spawn position, etc.  Any data you set here, will be used in the first 
    /// state udpate (if you're the server)
    /// </summary>
    public abstract void OnSpawn();

    public abstract void OnNetworkSend();

    public abstract int Peek();

    public abstract float Priority(ulong sendTo);
    //don't perfabId, networkId, owner, controller are serialized and deserialized automatically before calling these
    //serialize is called on the entity instance
    public abstract void Serialize(ByteStream stream);

    //deserialize is called on the prefab instance. You can NOT use instance properties in this method
    //they will cause errors because they will be the prefab values, NOT the entity values
    public abstract void Deserialize(ByteStream stream, int prefabId, int networkId, int owner, int controller);

    public abstract void OnEntityUpdate(params object[] args);

    //this is queued to all connections, but sometimes removed before they get sent out (priority sorted)
    public void QueueEntityUpdate() {
        foreach(var k in Core.net.connections) {
            if(queuedMessage[k.Key] == null) {
                Core.net.QueueEntityMessage("EntityUpdate", this, this.prefabId, this.networkId, this.owner, this.controller);
            } else {
                //don't requeue it, there's still one in the queue waiting to go out.
                //queued state updates serializes the most up to date data right before send
                //eg, if you queue a message, and it sits in the queue for 1 minute, when it finally goes out
                //it will send it's current position, not the position you queued the message with
                //this is because there's no point in sending the old data when you could send fresh stuff
                //this is also why we don't let more than one entity message state update
            }
        }
    }

    //sends an update to the owner asking to destroy it.
    //also hides the entity locally so it stops doing stuff because we think it's dead
    public void Destroy() {
        Debug.Log("Entity.Destroy(): " + isOwner());
        //process this even if we're frozen
        if(isOwner()) {
            //check to see if we have any queued entity messages waiting to go out
            //if we do, update them.
            //if we do not, queue new ones

            //we can just call QueueEntityMessage as it overwrites any pending messages
            //to EVERYONE
            Core.net.QueueEntityMessage("EntityDestroy", this, this.prefabId, this.networkId, this.owner, this.controller);
            //mark for destroy at the end of this frame/lateupdate
            Hide();
            isPendingDestroy = true; // waiting until all our destroy messages go out
            //DestroyInternal(); can't call this until the message has been sent, otherwise the entity is null already
        } else if(isController()) {
            //we only want to send a request to the owner
            ulong owner = Core.net.GetConnection(this.owner).steamID;
            Core.net.QueueEntityMessage(owner, "EntityDestroyRequest", this, this.prefabId, this.networkId, this.owner, this.controller);
            Hide(); //hide it, no more outgoing or incoming messages
        } else {
            ulong controller = Core.net.GetConnection(this.controller).steamID;
            //we are not the owner or controller, so send a request to the controller
            Core.net.QueueEntityMessage(controller, "EntityDestroyRequest", this, this.prefabId, this.networkId, this.owner, this.controller);
            Hide();
        }
    }

   public void TryDestroyInternal() {
        Debug.Log("TryDestroyInternal");
        DestroyInternal();
    }

    public virtual void DestroyInternal() {
        //when you recieve the actual destroy
        Debug.Log("Entity.DestroyInternal");
        Core.net.NetworkSendEvent -= OnNetworkSend;
        Destroy(this.gameObject);
    }

    //locally "destroys" the object when we send a request.  This is so it's instant for you, 
    //and eventually the owner will come clean it up
    //hook into this to spawn an explosion or whatever
    public virtual void Hide() {
        this.gameObject.SetActive(false); //this should stop collisions/particles/and update logic
        isHidden = true;
        Core.net.NetworkSendEvent -= OnNetworkSend; //stop sending out updates if we were registed to
    }

    //if we are frozen, we shouldn't rec or send any network events
    public bool isFrozen() {
        return isHidden || isPendingDestroy;
    }


    public virtual void Update() {
    //    SimulateOwner();
    }

    public virtual void LateUpdate() {
      
    }

    //public abstract void SimulateOwner();


    /// <summary>
    /// Are you the owner of this entity?
    /// </summary>
    /// <returns>true if you are the owner</returns>
    public bool isOwner() {
        return Core.net.me.connectionIndex == owner;
}


    /// <summary>
    /// Are you the controller of this entity?
    /// </summary>
    /// <returns>true if you are the controller</returns>
    public bool isController() {
        //I'm wondering if we really need a distinction between owner and controller though.
        //Can you ever be the owner but not the controller?
        //That would mean it would still be in your entity list, but you are not the highest auth 
        //someone else would be sending state updates, and that shouldn't happen.
        //If you spawn an object, then someone else shows up with higher auth, you should pass ownership to them
        //means they get the spawn, and you remove it from your entity list (and they remove it from their copy of your entity list)
        //and you both add it to theirs.  Now they are in control, and send updates.
        //You can still send updates about that object

        //eg,There is a physics box, you want to push it.  A has higher auth, you are B.  THey are the owner.
        //you start touching the box and INSTANTLY take control of it on your client so you can
        //push it responsively.  You start sending updates to the owner about how you are pushing this box
        //after some time of inactivity with the box they take control back.
        //if there is a disagreement, about who has control, the owner decides.

        //we would need to do logic in Update to decide if you want to try and take control
        //and send the "takeControl" message along with hooking into
        //NetworkSendEvent when we have control to send the state updates

        //So yes, there is need for a distinction.
        return Core.net.me.connectionIndex == controller;
    }

    /// <summary>
    /// Stores a position snapshot to be used for interpolation.  Store positions when you rec state data.
    /// </summary>
    /// <param name="index">The index of the snapshot list we want to add to. This is so we can store more than one pos to interpolate with per state. (eg, right hand and left hand on the same state, etc)</param>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    public void StorePositionSnapshot(int index, float x, float y, float z) {
        while(!positionSnapshots.WithinRange(index)) {
            positionSnapshots.Add(new List<Vector3Snapshot>());
        }

        while(positionSnapshots[index].Count > 10) {
            positionSnapshots[index].RemoveAt(0); //only ever store 10
        }

        positionSnapshots[index].Add(new Vector3Snapshot() { timeRec = Time.realtimeSinceStartup, pos = new Vector3(x, y, z) });
    }

    public Vector3 GetInterpolatedPosition(int index) {
        float renderTime = Time.realtimeSinceStartup - (interpolationBufferTime/1000f); //1/1000 to convert ms to s
        //do we have at least two snapshots to interp between?
        if(positionSnapshots[index].Count == 0) {
            //hmmmmmmmmmm.  When we get our spawn, we should have at least one position stored.
            //this should never happen, unless we are trying to interpolate a state that doesn't have a transform, which you can't do
            return new Vector3(0, 0, 0);
        } else if(positionSnapshots[index].Count == 1) {
            //can't interp, just snap to our most recent time
            return positionSnapshots[index][0].pos;
        } else {
            //we can interp so long as we have one interp position on either side of 
            //we need one interp with time < renderTime, and one with time > renderTime,
            //so find where time > renderTime changes from true to false
            bool foundTransition = false;
            int left = 0;
            int right = 0;
            for(int i = positionSnapshots[index].Count-2; i >= 0; i--) { //start at the end (-1 because we need i and i++), because the switch should lbe closer here
                if(!positionSnapshots[index].WithinRange(i + 1)) continue;
                if(renderTime > positionSnapshots[index][i].timeRec && renderTime < positionSnapshots[index][i + 1].timeRec) {
                    foundTransition = true;
                    left = i;
                    right = i + 1;
                    break;
                }
            }

            if(foundTransition) {
                float lerpTime = Mathf.InverseLerp(positionSnapshots[index][left].timeRec, positionSnapshots[index][right].timeRec, renderTime);
                return Vector3.Lerp(positionSnapshots[index][left].pos, positionSnapshots[index][right].pos, lerpTime);
            } else {
                //this could happen if every snapshot position is < renderTime,
                //so we should just set it to the oldest entry
                return positionSnapshots[index][positionSnapshots[index].Count - 1].pos;
            }

            return new Vector3(0, 0, 0);
        }
    }

    public void StoreRotationSnapshot(int index, Quaternion rotation) {
        while(!rotationSnapshots.WithinRange(index)) {
            rotationSnapshots.Add(new List<QuaternionSnapshot>());
        }

        while(rotationSnapshots[index].Count > 10) {
            rotationSnapshots[index].RemoveAt(0); //only ever store 10
        }

        rotationSnapshots[index].Add(new QuaternionSnapshot() { timeRec = Time.realtimeSinceStartup, rot = rotation });
    }

    public Quaternion GetInterpolatedRotation(int index) {
        float renderTime = Time.realtimeSinceStartup - (interpolationBufferTime / 1000f); //1/1000 to convert ms to s
                                                                                          //do we have at least two snapshots to interp between?
        if(rotationSnapshots[index].Count == 0) {
            //hmmmmmmmmmm.  When we get our spawn, we should have at least one position stored.
            //this should never happen, unless we are trying to interpolate a state that doesn't have a transform, which you can't do
            return new Quaternion(0f, 0f, 0f, 1f);
        } else if(rotationSnapshots[index].Count == 1) {
            //can't interp, just snap to our most recent time
            return rotationSnapshots[index][0].rot;
        } else {
            //we can interp so long as we have one interp position on either side of 
            //we need one interp with time < renderTime, and one with time > renderTime,
            //so find where time > renderTime changes from true to false
            bool foundTransition = false;
            int left = 0;
            int right = 0;
            for(int i = rotationSnapshots[index].Count - 2; i >= 0; i--) { //start at the end (-1 because we need i and i++), because the switch should lbe closer here
                if(!rotationSnapshots[index].WithinRange(i + 1)) continue;
                if(renderTime > rotationSnapshots[index][i].timeRec && renderTime < rotationSnapshots[index][i + 1].timeRec) {
                    foundTransition = true;
                    left = i;
                    right = i + 1;
                    break;
                }
            }

            if(foundTransition) {
                float lerpTime = Mathf.InverseLerp(rotationSnapshots[index][left].timeRec, rotationSnapshots[index][right].timeRec, renderTime);
                return Quaternion.Slerp(rotationSnapshots[index][left].rot, rotationSnapshots[index][right].rot, lerpTime);
            } else {
                //this could happen if every snapshot position is < renderTime,
                //so we should just set it to the oldest entry
                return rotationSnapshots[index][rotationSnapshots[index].Count - 1].rot;
            }

            return new Quaternion(0f, 0f, 0f, 1f);
        }
    }

    public void StoreFloatSnapshot(int index, float value) {
        while(!floatSnapshots.WithinRange(index)) {
            floatSnapshots.Add(new List<FloatSnapshot>());
        }

        while(floatSnapshots[index].Count > 10) {
            floatSnapshots[index].RemoveAt(0); //only ever store 10
        }

        floatSnapshots[index].Add(new FloatSnapshot() { timeRec = Time.realtimeSinceStartup, value = value });
    }

    public float GetInterpolatedFloat(int index) {
        float renderTime = Time.realtimeSinceStartup - (interpolationBufferTime / 1000f); //1/1000 to convert ms to s
        //do we have at least two snapshots to interp between?
        if(floatSnapshots[index].Count == 0) {
            //hmmmmmmmmmm.  When we get our spawn, we should have at least one position stored.
            //this should never happen, unless we are trying to interpolate a state that doesn't have a transform, which you can't do
            return 0f;
        } else if(floatSnapshots[index].Count == 1) {
            //can't interp, just snap to our most recent time
            return floatSnapshots[index][0].value;
        } else {
            //we can interp so long as we have one interp position on either side of 
            //we need one interp with time < renderTime, and one with time > renderTime,
            //so find where time > renderTime changes from true to false
            bool foundTransition = false;
            int left = 0;
            int right = 0;
            for(int i = floatSnapshots[index].Count - 2; i >= 0; i--) { //start at the end (-1 because we need i and i++), because the switch should lbe closer here
                if(!floatSnapshots[index].WithinRange(i + 1)) continue;
                if(renderTime > floatSnapshots[index][i].timeRec && renderTime < floatSnapshots[index][i + 1].timeRec) {
                    foundTransition = true;
                    left = i;
                    right = i + 1;
                    break;
                }
            }

            if(foundTransition) {
                float lerpTime = Mathf.InverseLerp(floatSnapshots[index][left].timeRec, floatSnapshots[index][right].timeRec, renderTime);
                return Mathf.Lerp(floatSnapshots[index][left].value, floatSnapshots[index][right].value, lerpTime);
            } else {
                //this could happen if every snapshot position is < renderTime,
                //so we should just set it to the oldest entry
                return floatSnapshots[index][floatSnapshots[index].Count - 1].value;
            }

            return 0f;
        }
    }

    public void StoreIntSnapshot(int index, int value) {
        while(!intSnapshots.WithinRange(index)) {
            intSnapshots.Add(new List<IntSnapshot>());
        }

        while(intSnapshots[index].Count > 10) {
            intSnapshots[index].RemoveAt(0); //only ever store 10
        }

        intSnapshots[index].Add(new IntSnapshot() { timeRec = Time.realtimeSinceStartup, value = value });
    }

    public int GetInterpolatedInt(int index) {
        float renderTime = Time.realtimeSinceStartup - (interpolationBufferTime / 1000f); //1/1000 to convert ms to s
        //do we have at least two snapshots to interp between?
        if(intSnapshots[index].Count == 0) {
            //hmmmmmmmmmm.  When we get our spawn, we should have at least one position stored.
            //this should never happen, unless we are trying to interpolate a state that doesn't have a transform, which you can't do
            return 0;
        } else if(intSnapshots[index].Count == 1) {
            //can't interp, just snap to our most recent time
            return intSnapshots[index][0].value;
        } else {
            //we can interp so long as we have one interp position on either side of 
            //we need one interp with time < renderTime, and one with time > renderTime,
            //so find where time > renderTime changes from true to false
            bool foundTransition = false;
            int left = 0;
            int right = 0;
            for(int i = intSnapshots[index].Count - 2; i >= 0; i--) { //start at the end (-1 because we need i and i++), because the switch should lbe closer here
                if(!intSnapshots[index].WithinRange(i + 1)) continue;
                if(renderTime > intSnapshots[index][i].timeRec && renderTime < intSnapshots[index][i + 1].timeRec) {
                    foundTransition = true;
                    left = i;
                    right = i + 1;
                    break;
                }
            }

            if(foundTransition) {
                float lerpTime = Mathf.InverseLerp(intSnapshots[index][left].timeRec, intSnapshots[index][right].timeRec, renderTime);
                return (int)Mathf.Lerp(intSnapshots[index][left].value, intSnapshots[index][right].value, lerpTime);
            } else {
                //this could happen if every snapshot position is < renderTime,
                //so we should just set it to the oldest entry
                return intSnapshots[index][intSnapshots[index].Count - 1].value;
            }

            return 0;
        }
    }
}

[System.Serializable]
public class Vector3Snapshot {
    public float timeRec = 0f;
    public Vector3 pos;
}
[System.Serializable]
public class QuaternionSnapshot {
    public float timeRec = 0f;
    public Quaternion rot;
}

[System.Serializable]
public class FloatSnapshot {
    public float timeRec = 0f;
    public float value;
}

[System.Serializable]
public class IntSnapshot {
    public float timeRec = 0f;
    public int value;
}


