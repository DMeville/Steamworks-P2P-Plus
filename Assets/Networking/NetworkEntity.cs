using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ByteStream = UdpKit.UdpStream;
using Sirenix.OdinInspector;

public abstract class NetworkEntity:SerializedMonoBehaviour {

    public int prefabId;
    public int networkId;

    public int owner;
    public int controller;

    public bool canMigrate = true; //set this to false for objects that should be destroyed when the owner leaves
    public bool ignoreZones = false;
    //like a player character.

    public bool isHidden = false;
    public bool isPendingDestroy = false;
    public bool isPredictingControl = false; //set 
    public bool isPendingMigration = false;

    public int entityStatusRequestedCount = 0; //how many times have you asked the owner
    public float entityDestroyTimeout = 10f;//
    public float lastStateUpdateRecTime = 0f;


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
    public virtual void OnSpawn(params object[] args) {
        lastStateUpdateRecTime = Time.realtimeSinceStartup;
    }

    //dunno if we need this.  Might be able to detect when an entity hasn't been
    //receiving updates on the receiver and ask "are you dead, or just out of scope"
    //if no response, instead of destroying it we try and take control
    public virtual void OnOwnerDisconnect() {
        //
    }

    public virtual bool inSameZone(int controller) {
        if(ignoreZones) return true;
        if(Core.net.me.inSameZone(Core.net.GetConnection(controller))) return true;
        else return false;
    }

    public abstract void OnNetworkSend();

    public abstract int Peek();

    //container method. This is what the networkloop calls on this entity to get the priority
    //this is so you can define what data you need in the priority call (like xyz)
    //and it passes in that data.  Use only entity data that you are replicating via state
    //see CubeBehaviour for an example of this
    public virtual float PriorityCaller(ulong sendTo) {
        return 1f;
    }

    //entity priority takes xyz values, either from the entity on send, or the deserialized xyz from the message
    //so we can always sort priority based on position (which is what we want to do like 99% of the time)
    public abstract float Priority(ulong sendTo, params object[] args);
    //don't perfabId, networkId, owner, controller are serialized and deserialized automatically before calling these
    //serialize is called on the entity instance
    public abstract void Serialize(ByteStream stream);

    //deserialize is called on the prefab instance. You can NOT use instance properties in this method
    //they will cause errors because they will be the prefab values, NOT the entity values
    public abstract void Deserialize(ByteStream stream, int prefabId, int networkId, int owner, int controller);

    public virtual void TakeControl() {
        if(!isController()) { //don't need to ask for control if you're already the controller
            isPredictingControl = true;
            //send an event asking to take control
            ulong cId = Core.net.GetConnection(controller).steamID;
            Debug.Log("sending entity control request");
            Core.net.QueueMessage(cId, Core.net.GetMessageCode("EntityControlRequest"), this.prefabId, this.networkId, this.owner, this.controller);
            //we can now do stuff with hasControl() and this will return true.

        }
    }

    //takes control instantly in a situation where we know the request will never come back (because the old controller disconnected, etc)
    public virtual void TakeControlInternal() {
        isPredictingControl = false;
        this.controller = Core.net.me.connectionIndex;
    }

    public virtual void OnEntityUpdate(params object[] args) {
        lastStateUpdateRecTime = Time.realtimeSinceStartup;
        
    }

    //this is queued to all connections, but sometimes removed before they get sent out (priority sorted)
    public void QueueEntityUpdate() {
        foreach(var k in Core.net.connections) {
            if(!queuedMessage.ContainsKey(k.Key)) {
                queuedMessage[k.Key] = null; //if you don't have this connection in our queuedMessages dict, add it
            }

            if(queuedMessage[k.Key] == null) { //check if this entity already has a message queued to this connection
                bool r = Core.net.QueueEntityMessage(k.Key, "EntityUpdate", this, this.prefabId, this.networkId, this.owner, this.controller);
                Debug.Log("Queued Entity.Update");
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
        //process this even if we're frozen
        if(isPendingDestroy) return; //already in the process of being destroyed

        bool s = true;
        if(isOwner()) {
            //check to see if we have any queued entity messages waiting to go out
            //if we do, update them.
            //if we do not, queue new ones

            //we can just call QueueEntityMessage as it overwrites any pending messages
            //to EVERYONE
            s = Core.net.QueueEntityMessage("EntityDestroy", this, this.prefabId, this.networkId, this.owner, this.controller);
            //mark for destroy at the end of this frame/lateupdate
            if(s) isPendingDestroy = true; // waiting until all our destroy messages go out
            //DestroyInternal(); can't call this until the message has been sent, otherwise the entity is null already
        } else if(isController()) {
            //we only want to send a request to the owner
            ulong owner = Core.net.GetConnection(this.owner).steamID;
            s = Core.net.QueueEntityMessage(owner, "EntityDestroyRequest", this, this.prefabId, this.networkId, this.owner, this.controller);
        } else {
            ulong controller = Core.net.GetConnection(this.controller).steamID;
            //we are not the owner or controller, so send a request to the controller
            s = Core.net.QueueEntityMessage(controller, "EntityDestroyRequest", this, this.prefabId, this.networkId, this.owner, this.controller);
        }

        if(s) {
            Hide();
        } else {
            Invoke("Destroy", 1f); //try again in a second so even if our message queue was full, the destroy should go out eventually
        }
    }

    //only destroy the object if all destroy messages have been broadcast to all players
    //or at least all the players who care about the message currently (if they don't care about the message they've already deleted the object)

    public void TryDestroyInternal() {

        bool allDone = true;

        foreach(var m in queuedMessage) {
            if(m.Value != null) {
                allDone = false;
            }
        }
        if(allDone) DestroyInternal();
    }

    //silently destroys.  Hook into hide to show explosion on destroy or something.
    //if you hook into this, you will get an explosion when an entity falls out of scope and you don't want that
    public virtual void DestroyInternal() {
        //when you recieve the actual destroy
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

    /// <summary>
    /// should we apply state updates we receive, or do we want to ignore them (eg, we're controller or predicting we're the controller)
    /// or we are frozen/hidden/pendingControl
    /// </summary>
    /// <returns></returns>
    public bool shouldReplicate() {
        return !isController() || !isFrozen(); //|| isPredictingControl; we don't want to send updates until we're sure we're the owner.
    }


    public virtual void Update() {
        if(!hasControl()) {
            //how long has it been since you received a state update?
            float ts = Time.realtimeSinceStartup - lastStateUpdateRecTime;
            if(ts > entityDestroyTimeout) {
                //ask the controller what's going on.
                if(entityStatusRequestedCount > 3) {
                    //just destroy me
                    DestroyInternal();
                } else {
                    lastStateUpdateRecTime = Time.realtimeSinceStartup;
                    entityStatusRequestedCount++;
                    RequestEntityStatus(); //what if we never get a response? after x tries, just destroy it?
                }
            }
        }
        //    SimulateOwner();
    }

    //sends a message to the controller asking if we can destroy this entity, or if we should
    public void RequestEntityStatus() {
        //Core.net.QueueMessage)
        Core.net.QueueMessage(Core.net.GetConnection(this.controller).steamID, Core.net.GetMessageCode("EntityScopeRequest"), this.prefabId, this.networkId, this.owner, this.controller);
    }

    public virtual void LateUpdate() {

    }

    public virtual void OnChangeOwner(int newOwner) {
        if(isOwner()) {
            //you are currently the owner, so you are sending this message, you need to apply the change first
            //or should we apply the change when the event goes out? What if it never does...?
            //if we do it instantly we have "local prediction" but we won't receieve any updates for it..
            //additionally, what if we have a state udpate already queued that goes out AFTER?
            //what if we queue a destroy while this is destroyed on this end, but not the other end?
            //this event needs to go to everyone saying "hey, this player is the new owner"
            //or at least people scoped into this entity
            //the change request only needs to go to the current owner though.

            //we can't predict this. We shouldn't.  Just wait until the owner stops sending updates.
            //maybe we should only be able to take control if we are already the controller?
            //there's an issue here:
            //1) A is owner, B is nonthing.  B wants ownership. B sends request, A rec's and stop sending state. 
            //Sets local entity to new owner, sends final event to everyone informing them of new owner. Makese sense

            //2) A is owner, B is controller, C is nothing.  C wants ownership.  SEnds req to A.  A ISN"T sending state so it can't stop.
            //A passes ownership to C. B still sends update.  Everyone rec's B's update, and updates the wrong (or a non-existant entity). ERROR

            //3) A is owner, B is controller, C is nothing.  C wants ownership.  Sends req to C for control.  B relinquishes control to C, stops sending updates
            //and sends C "you're in control now".  Once C gets control, asks A for ownership. Maybe at this point C can just take ownership since we're already sending updates.
            //send everyone an event saying "hey, I took control of entity update your lists"
        }
        //do the transfer.

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
        return Core.net.me.connectionIndex == controller; //
    }

    /// <summary>
    /// returns true if you are the controller, or you are predicting control.  
    /// </summary>
    /// <returns></returns>
    public bool hasControl() {
        return Core.net.me.connectionIndex == controller || isPredictingControl;
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
        if(!positionSnapshots.WithinRange(index)) return this.transform.position;

        float renderTime = Time.realtimeSinceStartup - (interpolationBufferTime / 1000f); //1/1000 to convert ms to s
        //do we have at least two snapshots to interp between?
        if(positionSnapshots[index].Count == 0) {
            //hmmmmmmmmmm.  When we get our spawn, we should have at least one position stored.
            //this should never happen, unless we are trying to interpolate a state that doesn't have a transform, which you can't do
            return this.transform.position;
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
            for(int i = positionSnapshots[index].Count - 2; i >= 0; i--) { //start at the end (-1 because we need i and i++), because the switch should lbe closer here
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
        if(!rotationSnapshots.WithinRange(index)) return this.transform.rotation;

        float renderTime = Time.realtimeSinceStartup - (interpolationBufferTime / 1000f); //1/1000 to convert ms to s
                                                                                          //do we have at least two snapshots to interp between?
        if(rotationSnapshots[index].Count == 0) {
            //hmmmmmmmmmm.  When we get our spawn, we should have at least one position stored.
            //this should never happen, unless we are trying to interpolate a state that doesn't have a transform, which you can't do
            return this.transform.rotation;
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
        if(!floatSnapshots.WithinRange(index)) return 0f;

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
        if(!intSnapshots.WithinRange(index)) return 0;
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



