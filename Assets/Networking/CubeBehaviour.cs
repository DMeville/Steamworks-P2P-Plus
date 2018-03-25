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

    public void Update() {
        if(owner != Core.net.me.connectionIndex) return;

        _updateRate -= Time.deltaTime;
        if(_updateRate <= 0f) {
            _updateRate = updateRate;
            SendState();
        }
    }

    public void SendState() {
        Core.net.QueueEntityMessage("StateUpdate", this, this.prefabId, this.networkId, this.owner, this.controller);
    }

    public override void OnSpawn() {

    }

    public override void OnStateUpdate(params object[] args) {
        this.transform.position = new Vector3((float)args[0], (float)args[1], (float)args[2]);
    }

    public override int Peek() {
        int s = 0;

        s += SerializerUtils.RequiredBitsFloat(-10f, 10f, 0.0001f); 
        s += SerializerUtils.RequiredBitsFloat(-10f, 10f, 0.0001f);
        s += SerializerUtils.RequiredBitsFloat(-10f, 10f, 0.0001f);

        return 0;
    }

    public override float Priority(ulong sendTo) {
        //could check the connections[sendTo], get their player position and find out the distance between them
        //and this object.  And scale priority based on that, so it's lower the further away they are
        //requires some player metatdata to be accessed from *somewhere* though...
        return 1f;
    }

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
}
