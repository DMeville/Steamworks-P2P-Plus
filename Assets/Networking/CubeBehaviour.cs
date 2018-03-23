using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class CubeBehaviour : NetworkGameObject {

    public float updateRate = 0.3f;
    private float _updateRate = 0f;

    public float radius = 1f;
    public float orbitSpeed = 1f;
    public float deg = 0f;

    public Vector3 targetPosition = new Vector3();
    public float lerpSpeed = 10f;

    public bool toggleColor = false;
    public float toggleTimer = 5f;

    public void Update() {
        //this.transform.position = Vector3.Lerp(this.transform.position, targetPosition, Time.deltaTime * lerpSpeed);

        if(Core.net.me.connectionIndex != owner) return; //need simulate owner separate from update

        deg += Time.deltaTime * orbitSpeed;

        float x = Mathf.Cos(Mathf.Deg2Rad * deg) * radius;
        float y = Mathf.Sin(Mathf.Deg2Rad * deg) * radius;

        //this.transform.position = new Vector3(x, 0f, y);

        _updateRate -= Time.deltaTime;
        if(_updateRate <= 0f) {
            _updateRate = updateRate;
            SendState();
        }

        toggleTimer -= Time.deltaTime;
        if(toggleTimer<= 0f) {
            toggleTimer = 5f;
            ToggleColor();
        }
    }

    private  void ToggleColor() {
        toggleColor = !toggleColor;
        SetToggleColor();
    }

    private void SetToggleColor() {
        if(toggleColor) {
            this.gameObject.GetComponent<Renderer>().material.color = Color.red;
        } else {
            this.gameObject.GetComponent<Renderer>().material.color = Color.blue;
        }
    }

    public void SendState() {
        //to everyone interested? need priority check per object. idk
        //should never replicate state to ourselves
        foreach(SteamConnection c in Core.net.connections.Values) {
            //this values always need to be in the same order.
            //you can't choose to omit a value just because, otherwise the deseralization will break (because it doesn't *know* what you're sending, it just assumes data should be in a specific order)
            //you can bypass this though through some trickery.
            //eg, if you wanted to save on bandwith and not send position data when the cube is at rest
            //you can add an extra bool BEFORE the data you want to omit isAtRest
            //then when you deserialize it read that value first, and if it is at rest we know we wouldn't have sent position data
            bool isSleeping = this.GetComponent<Rigidbody>().IsSleeping();
            if(isSleeping) {
                Core.net.QueueMessage(c.steamID, "StateUpdate", owner, networkId, stateType, isSleeping, toggleColor);
            } else {
                Core.net.QueueMessage(c.steamID, "StateUpdate", owner, networkId, stateType, isSleeping, this.transform.position.x, this.transform.position.y, this.transform.position.z, toggleColor, this.transform.rotation.w, this.transform.rotation.x, this.transform.rotation.y, this.transform.rotation.z);

            }
        }
    }

    public override void OnSpawn() {
        _updateRate = updateRate;

    }

    public override void OnStateUpdateReceived(int owner, int networkId, int stateType, params object[] args) {
        //Debug.Log("Received state update for cube");
        bool isSleeping = (bool)args[3];
        if(!isSleeping) {
            float x = (float)args[4];
            float y = (float)args[5];
            float z = (float)args[6];
            bool tc = (bool)args[7];
            float qw = (float)args[8];
            float qx = (float)args[9];
            float qy = (float)args[10];
            float qz = (float)args[11];

            this.toggleColor = tc;
            SetToggleColor();

            this.transform.position = new Vector3(x, y, z);
            this.transform.rotation = new Quaternion(qw, qx, qy, qz);
        } else {
            bool tc = (bool)args[4];
            this.toggleColor = tc;
            SetToggleColor();
        }
    }

    public static int GetSerializeStateSize() {
        return 0;
    }

    //public new static byte[] SerializeState(ulong receiver, int msgCode, int owner, int networkId, int stateCode, OutputStream stream, params object[] args) {
    //    bool isSleeping = (bool)args[3];
    //    stream.WriteBool(isSleeping);

    //    if(!isSleeping) {
    //        float x = (float)args[4];
    //        float y = (float)args[5];
    //        float z = (float)args[6];
    //        bool tc = (bool)args[7];
    //        float qw = (float)args[8];
    //        float qx = (float)args[9];
    //        float qy = (float)args[10];
    //        float qz = (float)args[11];
       
    //        //stream.WriteFloat(x, -10f, 10f, 0.001f);
    //        //stream.WriteFloat(y, -10f, 10f, 0.001f);
    //        //stream.WriteFloat(z, -10f, 10f, 0.001f);
    //        //stream.WriteBool(tc);
    //        //stream.WriteFloat(qw, -1f, 1f, 0.001f);
    //        //stream.WriteFloat(qx, -1f, 1f, 0.001f);
    //        //stream.WriteFloat(qy, -1f, 1f, 0.001f);
    //        //stream.WriteFloat(qz, -1f, 1f, 0.001f);
    //        //116 bits per update max * 10 cubes / 8bitsperbyte
    //        //145bytes per state update (10 cubes) every frame
    //        //
    //    } else {
    //        stream.WriteBool((bool)args[4]);
    //    }

    //    return stream.GetBuffer();
    //}

    //public new static void DeserializeState(ulong sender, int msgCode, int owner, int networkId, int stateCode, InputStream stream) {
    //    bool isSleeping = stream.ReadBool();
    //    if(!isSleeping) {
    //        //float x = stream.ReadFloat(-10f, 10f, 0.001f);
    //        //float y = stream.ReadFloat(-10f, 10f, 0.001f);
    //        //float z = stream.ReadFloat(-10f, 10f, 0.001f);
    //        //bool tc = stream.ReadBool();
    //        //float qw = stream.ReadFloat(-1f, 1f, 0.001f);
    //        //float qx = stream.ReadFloat(-1f, 1f, 0.001f);
    //        //float qy = stream.ReadFloat(-1f, 1f, 0.001f);
    //        //float qz = stream.ReadFloat(-1f, 1f, 0.001f);

    //        //Core.net.Process(sender, msgCode, owner, networkId, stateCode, isSleeping, x, y, z, tc, qw, qx, qy, qz);
    //    } else {
    //        bool tc = stream.ReadBool();
    //        //Core.net.Process(sender, msgCode, owner, networkId, stateCode, isSleeping, tc);

    //    }
    //}
}
