using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//I use singletons too much. This is for ease of access and I'm lazy. 
//Easier to type Core.net.SendMessage() vs NetworkManager.instance.SendMessage()
//just an easy way to store and access any manager objects
public class Core : MonoBehaviour {

    public static NetworkManager net;
    //public static OtherManager other; etc etc
}
