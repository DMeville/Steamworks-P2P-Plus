using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BandwidthData {
    public int bits = 0;
    public float timeInBuffer = 0f;

    public BandwidthData(int bits) {
        this.bits = bits;
    }
}