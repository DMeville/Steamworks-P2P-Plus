using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UdpKit;

public class PlayerBehaviour : NetworkEntity {

    public Rigidbody rb;
    public Vector3 targetPos;
    public Quaternion targetRotation;
    public float moveSpeed = 1f;

    public void Awake() {
        rb = this.GetComponent<Rigidbody>();
    }

    public override void Update() {
        base.Update(); //!important to call this here if we're overriding. Handles migration/priority scoping 
    }

    public override void SimulateController() {
        Vector3 mov = new Vector3();
        if(Input.GetKey(KeyCode.W)) {
            mov.z = 1f;
        } else if(Input.GetKey(KeyCode.S)) {
            mov.z = -1f;
        }

        if(Input.GetKey(KeyCode.D)) {
            mov.x = 1f;
        } else if(Input.GetKey(KeyCode.A)) {
            mov.x = -1f;
        }

        this.transform.position += mov * moveSpeed * Time.deltaTime;
    }

    public override void SimulateReceiver() {
        this.transform.position = GetInterpolatedPosition(0);
        this.transform.rotation = GetInterpolatedRotation(0);
    }

    public override void SimulateEntity() {
        if(isPredictingControl) {
            SetColor(Color.green);
        } else {
            if(controller == 0) {
                SetColor(Color.red);
            } else {
                SetColor(Color.blue);
            }
        }
    }

    public void SetColor(Color c) {
        this.GetComponent<Renderer>().material.color = c;
    }

    public override void OnSpawn(params object[] args) {
        base.OnSpawn();

        //we know args has pos and rotation data, so apply it instantly when we spawn
        //so that we spawn in the correct place.  This is done here because it's not always the case
        //sometimes an entity will not have position and rotation data(?)
        this.transform.position = new Vector3((float)args[0], (float)args[1], (float)args[2]);
        this.transform.rotation = (Quaternion)args[3];
    }

    public override void OnEntityUpdate(params object[] args) {
        if(!shouldReplicate()) return; //don't want to apply anything if we're frozen (eg, dead or predicted dead)

        //if we're using interpolation, store the snapshots here to use later
        StorePositionSnapshot(0, (float)args[0], (float)args[1], (float)args[2]);
        StoreRotationSnapshot(0, (Quaternion)args[3]);
        //StoreIntSnapshot(0, (int)args[4]);
        //StoreFloatSnapshot(0, (float)args[5]);
        //if we had another float we wanted to interpolate
        //StoreFloatSnapshot(1, (float)args[6]); //then call it with GetInterpolatedFloat(1);
        
    }

    //if we're using position interpolation, and we are the controller
    //store the position after all udpates have been called on the controller
    //this is so if we ever change controller we will have our most recent positions to interpolate for
    //instead of waiting for the new controller to send us their first state updates.
    public override void SimulateControllerLate() {
        StorePositionSnapshot(0, transform.position.x, transform.position.y, transform.position.z);
        StoreRotationSnapshot(0, transform.rotation);
    }

    public override int Peek() {
        int s = 0;
        s += SerializerUtils.RequiredBitsFloat(-100f, 100f, 0.0001f);
        s += SerializerUtils.RequiredBitsFloat(-100f, 100f, 0.0001f);
        s += SerializerUtils.RequiredBitsFloat(-100f, 100f, 0.0001f);
        s += SerializerUtils.RequiredBitsQuaternion(0.001f);
        return s;
    }

  

    //we should do a priority check while sending to remove messages to people who don't want it
    //we should also do a priority check when receving to filter out messages we don't want that might have been sent
    //eg. we change scenes, still get events while the sender realizes we changed scenes.  We should ignore those events
    public override float Priority(ulong sendTo, params object[] args) {

        float x = (float)args[0];
        float y = (float)args[1];
        float z = (float)args[2];

        return LinearDistancePriority(new Vector3(x, y, z), sendTo, 25f); //
    }



    //bolt 3 float properties compressed the same (18 bits each = 54 bits)
    //20 packets per second, means 1080 bits or 135 bytes per second or 0.135 bytes per second

    public override void Serialize(UdpStream stream) {
        SerializerUtils.WriteFloat(stream, this.transform.position.x, -100f, 100f, 0.0001f);
        SerializerUtils.WriteFloat(stream, this.transform.position.y, -100f, 100f, 0.0001f);
        SerializerUtils.WriteFloat(stream, this.transform.position.z, -100f, 100f, 0.0001f);
        SerializerUtils.WriteQuaterinion(stream, this.transform.rotation, 0.001f);
    }

    public override void Deserialize(UdpStream stream, int prefabId, int networkId, int owner, int controller) {
        //deserialize any state data.
        //!important, can only use data that we deserialize here
        //we can NOT use this.prefabId.  Because in this method call (this) refers to the PREFAB 
        //NOT the instance.  And as such, will return the prefab values.
        //this is because we don't know if this entity even exists in our local world yet, (it might need to be spawned)
        //and if that's the case, it doesn't even have an instance to get values for!
        //we do know after we serialize the data though what the position WOULD be if we spawn it

        float x = SerializerUtils.ReadFloat(stream, -100f, 100f, 0.0001f);
        float y = SerializerUtils.ReadFloat(stream, -100f, 100f, 0.0001f);
        float z = SerializerUtils.ReadFloat(stream, -100f, 100f, 0.0001f);
        Quaternion rotation = SerializerUtils.ReadQuaternion(stream, 0.001f);

        
        ProcessDeserialize(prefabId, networkId, owner, controller, x, y, z, rotation);

    }
}
