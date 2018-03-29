using System.Collections;
using System.Collections.Generic;
using UdpKit;
using UnityEngine;


public class CubeBehaviour : NetworkEntity {

    //this generally should not be faster than the network simulation rate.
    //if it is you can end up queueing too many messages and things spiral out of control.
    //might need somethign else to control if we're sending too much messages too...
    //send faster?
    //idk?
    public float updateRate = 0.3f;
    public float _updateRate = 0f;
    public Rigidbody rb;

    public float deg = 0f;
    public float rotSpeed = 90f;
    public float radius = 1f;
    public float lerpSpeed = 10f;

    public Vector3 targetPos;
    public Quaternion targetRotation;
    public float floatValue = 0f;
    public int intValue = 0;

    public float targetFloat = 0f;
    public int targetInt = 0;
    public bool p = false;

    public void Awake() {
        rb = this.GetComponent<Rigidbody>();
    }

    public override void Update() {
        base.Update();

        if(Input.GetKeyDown(KeyCode.Space)) {
            Destroy();
        }

        if(Input.GetKeyDown(KeyCode.A)) {
            p = !p;
        }

        //if we don't care about this object anymore, because we're too far away and have stopped getting state updates for it
        //just destroy it now.
        //what if we're sitting on the threshold, will we get spawn/despawn/spawn/despawn?
        //if(!isOwner() && Priority(Core.net.me.steamID) <= 0f) {
        //    Hide();
        //    DestroyInternal(); 
        //}

        if(!isOwner()) {

            //update the position with the interpolated version (using our interp time
            this.transform.position = GetInterpolatedPosition(0); //we should come up with a more modular way to store these
                                                                 //what if we want more than one position per state (for whatever reason)
            this.transform.rotation = GetInterpolatedRotation(0); //or we want to interpolate a float (for colour or something) idk

            this.floatValue = GetInterpolatedFloat(0);
            this.intValue = GetInterpolatedInt(0);

        } else {

            //deg += Time.deltaTime * rotSpeed;
            if(deg >= 360f) {
                deg = 0;
                targetPos = new Vector3(Random.Range(-3f, 3f), Random.Range(0f, 1f), Random.Range(-3f, 3f));
                targetRotation = Random.rotation;
                targetFloat = Random.Range(-100f, 100f);
                targetInt = Random.Range(-100, 100);
            }

            this.transform.position = Vector3.Lerp(this.transform.position, targetPos, lerpSpeed * Time.deltaTime);
            this.transform.rotation = Quaternion.Slerp(this.transform.rotation, targetRotation, lerpSpeed * Time.deltaTime);
            floatValue = Mathf.Lerp(floatValue, targetFloat, lerpSpeed * Time.deltaTime);
            intValue = (int)Mathf.Lerp(intValue, targetInt, lerpSpeed * Time.deltaTime);
            //float x = Mathf.Cos(deg * Mathf.Deg2Rad) * radius;
            //float z = Mathf.Sin(deg * Mathf.Deg2Rad) * radius;

            //this.transform.position = new Vector3(x, 0f, z);
        }
    }


    //how do I do interpolation if state updates are not coming at a regular interval?
    //If I haven't got an update for more than (NetworkSim*6)ms, do I just snap?
    //Otherwise if I have got an update.  20/60 network rate is one packet every 50 ms.  



    public override void OnSpawn(params object[] args) {
        base.Update();
        //if you own it, subscribe to the NetworkSendEvent
        if(args.Length != 0) {
            //are we spawing because it entered scope? if so, we have a state
            //otherwise we do not
            this.transform.position = new Vector3((float)args[0], (float)args[1], (float)args[2]);
            this.transform.rotation = (Quaternion)args[3];
        } else {
            //normal spawn, no state yet?
        }

        if(isOwner()) {
            Core.net.NetworkSendEvent += OnNetworkSend;
        }
    }

    //triggered right before a packet is going out.  This is where you want to
    //queue the state update message
    public override void OnNetworkSend() {
        if(!isFrozen()) {
            QueueEntityUpdate();
        }
    }

    public override void OnEntityUpdate(params object[] args) {
        //if(!(bool)args[0]) { //!isSleeping
        if(isFrozen()) return; //don't want to apply anything if we're frozen (eg, dead or predicted dead)

        StorePositionSnapshot(0, (float)args[0], (float)args[1], (float)args[2]);
        StoreRotationSnapshot(0, (Quaternion)args[3]);
        StoreIntSnapshot(0, (int)args[4]);
        StoreFloatSnapshot(0, (float)args[5]);
        //if we had another float we wanted to interpolate
        //StoreFloatSnapshot(1, (float)args[6]); //then call it with GetInterpolatedFloat(1);
        //}
    }

    public override int Peek() {
        int s = 0;
        s += SerializerUtils.RequiredBitsFloat(-10f, 10f, 0.0001f);
        s += SerializerUtils.RequiredBitsFloat(-10f, 10f, 0.0001f);
        s += SerializerUtils.RequiredBitsFloat(-10f, 10f, 0.0001f);
        s += SerializerUtils.RequiredBitsInt(-100, 100);
        s += SerializerUtils.RequiredBitsFloat(-100f, 100f, 0.001f);
        s += SerializerUtils.RequiredBitsQuaternion(0.001f);
        return s;
    }

    public override float Priority(ulong sendTo) {
        float r = 0f;
        if(p) r = 0f;
        else r = 1f;

        if(this.transform.position.x > 2f) {
            r = 0f;
        } else {
            r = 1f;
        }

        Debug.Log("Entity.Priority: " + r);
        //could check the connections[sendTo], get their player position and find out the distance between them
        //and this object.  And scale priority based on that, so it's lower the further away they are
        //requires some player metatdata to be accessed from *somewhere* though...
        return r;        //return 0f;
    }

    //bolt 3 float properties compressed the same (18 bits each = 54 bits)
    //20 packets per second, means 1080 bits or 135 bytes per second or 0.135 bytes per second

    public override void Serialize(UdpStream stream) {
        SerializerUtils.WriteFloat(stream, this.transform.position.x, -10f, 10f, 0.0001f);
        SerializerUtils.WriteFloat(stream, this.transform.position.y, -10f, 10f, 0.0001f);
        SerializerUtils.WriteFloat(stream, this.transform.position.z, -10f, 10f, 0.0001f);
        SerializerUtils.WriteInt(stream, intValue, -100, 100);
        SerializerUtils.WriteFloat(stream, floatValue, -100f, 100f, 0.001f);
        SerializerUtils.WriteQuaterinion(stream, this.transform.rotation, 0.001f);
    }

    public override void Deserialize(UdpStream stream, int prefabId, int networkId, int owner, int controller) {
        //deserialize any state data.
        float x = SerializerUtils.ReadFloat(stream, -10f, 10f, 0.0001f);
        float y = SerializerUtils.ReadFloat(stream, -10f, 10f, 0.0001f);
        float z = SerializerUtils.ReadFloat(stream, -10f, 10f, 0.0001f);
        int iValue = SerializerUtils.ReadInt(stream, -100, 100);
        float fValue = SerializerUtils.ReadFloat(stream, -100f, 100f, 0.001f);
        Quaternion rotation = SerializerUtils.ReadQuaternion(stream, 0.001f);

        Core.net.ProcessEntityMessage(prefabId, networkId, owner, controller, x, y, z, rotation, iValue, fValue);
    }

    public void OnDestroy() {
        Core.net.NetworkSendEvent -= OnNetworkSend;
    }
}
