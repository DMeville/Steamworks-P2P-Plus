using System.Collections;
using System.Collections.Generic;
using UdpKit;
using UnityEngine;


public class CubeBehaviour : NetworkGameObject {

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

    public void Awake() {
        rb = this.GetComponent<Rigidbody>();
    }

    public override void Update() {
        base.Update();
        if(!isOwner()) return;

        deg += Time.deltaTime * rotSpeed;

        float x = Mathf.Cos(deg * Mathf.Deg2Rad) * radius;
        float z = Mathf.Sin(deg * Mathf.Deg2Rad) * radius;

        this.transform.position = new Vector3(x, 0f, z);
    }
    

    //how do I do interpolation if state updates are not coming at a regular interval?
    //If I haven't got an update for more than (NetworkSim*6)ms, do I just snap?
    //Otherwise if I have got an update.  20/60 network rate is one packet every 50 ms.  
    


    public override void OnSpawn() {
        //if you own it, subscribe to the NetworkSendEvent
        if(isOwner()) {
            Core.net.NetworkSendEvent += OnNetworkSend;
        }
    }

    //triggered right before a packet is going out.  This is where you want to
    //queue the state update message
    public override void OnNetworkSend() {
        Core.net.QueueEntityMessage("StateUpdate", this, this.prefabId, this.networkId, this.owner, this.controller);
    }

    public override void OnStateUpdate(params object[] args) {
        //if(!(bool)args[0]) { //!isSleeping
            this.transform.position = new Vector3((float)args[0], (float)args[1], (float)args[2]);
        //}
    }

    public override int Peek() {
        int s = 0;
        s += SerializerUtils.RequiredBitsFloat(-10f, 10f, 0.0001f);
        s += SerializerUtils.RequiredBitsFloat(-10f, 10f, 0.0001f);
        s += SerializerUtils.RequiredBitsFloat(-10f, 10f, 0.0001f);

        return s;
    }

    public override float Priority(ulong sendTo) {
        //could check the connections[sendTo], get their player position and find out the distance between them
        //and this object.  And scale priority based on that, so it's lower the further away they are
        //requires some player metatdata to be accessed from *somewhere* though...
        return 1f;
    }

    //bolt 3 float properties compressed the same (18 bits each = 54 bits)
    //20 packets per second, means 1080 bits or 135 bytes per second or 0.135 bytes per second

    public override void Serialize(UdpStream stream) {
        SerializerUtils.WriteFloat(stream, this.transform.position.x, -10f, 10f, 0.0001f);
        SerializerUtils.WriteFloat(stream, this.transform.position.y, -10f, 10f, 0.0001f);
        SerializerUtils.WriteFloat(stream, this.transform.position.z, -10f, 10f, 0.0001f);
    }

    public override void Deserialize(UdpStream stream, int prefabId, int networkId, int owner, int controller) {
        //deserialize any state data.
        float x = SerializerUtils.ReadFloat(stream, -10f, 10f, 0.0001f);
        float y = SerializerUtils.ReadFloat(stream, -10f, 10f, 0.0001f);
        float z = SerializerUtils.ReadFloat(stream, -10f, 10f, 0.0001f);
        

        Core.net.ProcessEntityMessage(prefabId, networkId, owner, controller, x, y, z);
    }

    public void OnDestroy() {
        Core.net.NetworkSendEvent -= OnNetworkSend;
        Core.net.NetworkSendEvent -= OnNetworkSend;
    }
}
