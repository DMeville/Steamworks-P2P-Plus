using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ByteStream = UdpKit.UdpStream;

public abstract class NetworkGameObject:MonoBehaviour {

    public int prefabId;
    public int networkId;

    public int owner;
    public int controller;

    public int stateType = 0;


    public float interpolationBufferTime = 100;//ms.
    public List<PositionSnapshot> positionSnapshots = new List<PositionSnapshot>();
    public List<RotationSnapshot> rotationSnapshots = new List<RotationSnapshot>();

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

    public abstract void OnStateUpdate(params object[] args);

    public virtual void Update() {
    //    SimulateOwner();
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

    public void StorePositionSnapshot(float x, float y, float z) {
        while(positionSnapshots.Count > 10) {
            positionSnapshots.RemoveAt(0); //only ever store 10
        }

        positionSnapshots.Add(new PositionSnapshot() { timeRec = Time.realtimeSinceStartup, pos = new Vector3(x, y, z) });
    }

    public Vector3 GetInterpolatedPosition() {
        float renderTime = Time.realtimeSinceStartup - (interpolationBufferTime/1000f); //1/1000 to convert ms to s
        //do we have at least two snapshots to interp between?
        if(positionSnapshots.Count == 0) {
            //hmmmmmmmmmm.  When we get our spawn, we should have at least one position stored.
            //this should never happen, unless we are trying to interpolate a state that doesn't have a transform, which you can't do
            return new Vector3(0, 0, 0);
        } else if(positionSnapshots.Count == 1) {
            //can't interp, just snap to our most recent time
            return positionSnapshots[0].pos;
        } else {
            //we can interp so long as we have one interp position on either side of 
            //we need one interp with time < renderTime, and one with time > renderTime,
            //so find where time > renderTime changes from true to false
            bool foundTransition = false;
            int left = 0;
            int right = 0;
            for(int i = positionSnapshots.Count-2; i >= 0; i--) { //start at the end (-1 because we need i and i++), because the switch should lbe closer here
                if(!positionSnapshots.WithinRange(i + 1)) continue;
                if(renderTime > positionSnapshots[i].timeRec && renderTime < positionSnapshots[i + 1].timeRec) {
                    foundTransition = true;
                    left = i;
                    right = i + 1;
                    break;
                }
            }

            if(foundTransition) {
                float lerpTime = Mathf.InverseLerp(positionSnapshots[left].timeRec, positionSnapshots[right].timeRec, renderTime);
                return Vector3.Lerp(positionSnapshots[left].pos, positionSnapshots[right].pos, lerpTime);
            } else {
                //this could happen if every snapshot position is < renderTime,
                //so we should just set it to the oldest entry
                return positionSnapshots[positionSnapshots.Count - 1].pos;
            }

            return new Vector3(0, 0, 0);
        }
    }

    public void StoreRotationSnapshot(Quaternion rotation) {
        while(rotationSnapshots.Count > 10) {
            rotationSnapshots.RemoveAt(0); //only ever store 10
        }

        rotationSnapshots.Add(new RotationSnapshot() { timeRec = Time.realtimeSinceStartup, rot = rotation });
    }

    public Quaternion GetInterpolatedRotation() {
        float renderTime = Time.realtimeSinceStartup - (interpolationBufferTime / 1000f); //1/1000 to convert ms to s
                                                                                          //do we have at least two snapshots to interp between?
        if(rotationSnapshots.Count == 0) {
            //hmmmmmmmmmm.  When we get our spawn, we should have at least one position stored.
            //this should never happen, unless we are trying to interpolate a state that doesn't have a transform, which you can't do
            return new Quaternion(0f, 0f, 0f, 1f);
        } else if(rotationSnapshots.Count == 1) {
            //can't interp, just snap to our most recent time
            return rotationSnapshots[0].rot;
        } else {
            //we can interp so long as we have one interp position on either side of 
            //we need one interp with time < renderTime, and one with time > renderTime,
            //so find where time > renderTime changes from true to false
            bool foundTransition = false;
            int left = 0;
            int right = 0;
            for(int i = rotationSnapshots.Count - 2; i >= 0; i--) { //start at the end (-1 because we need i and i++), because the switch should lbe closer here
                if(!rotationSnapshots.WithinRange(i + 1)) continue;
                if(renderTime > rotationSnapshots[i].timeRec && renderTime < rotationSnapshots[i + 1].timeRec) {
                    foundTransition = true;
                    left = i;
                    right = i + 1;
                    break;
                }
            }

            if(foundTransition) {
                float lerpTime = Mathf.InverseLerp(rotationSnapshots[left].timeRec, rotationSnapshots[right].timeRec, renderTime);
                return Quaternion.Slerp(rotationSnapshots[left].rot, rotationSnapshots[right].rot, lerpTime);
            } else {
                //this could happen if every snapshot position is < renderTime,
                //so we should just set it to the oldest entry
                return rotationSnapshots[rotationSnapshots.Count - 1].rot;
            }

            return new Quaternion(0f, 0f, 0f, 1f);
        }
    }
}

[System.Serializable]
public class PositionSnapshot {
    public float timeRec = 0f;
    public Vector3 pos;
}
[System.Serializable]
public class RotationSnapshot {
    public float timeRec = 0f;
    public Quaternion rot;
}



