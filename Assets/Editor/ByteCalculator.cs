using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ByteCalculator : EditorWindow {
    
    [MenuItem("Networking/ByteCalculator")]

    public static void ShowWindow() {
        EditorWindow.GetWindow(typeof(ByteCalculator));
    }

    int selected = 0;
    string[] options = new string[] { "Int", "Float", "Quaternion" };
    int result = 0;
    int intMin = 0;
    int intMax = 0;

    float floatMin = 0;
    float floatMax = 0f;
    float floatPrecision = 0.1f;

    float quatPrecision = 0.1f;

    private void OnGUI() {

        EditorGUILayout.LabelField("Use this to calculate how many bits a property will send");
        EditorGUILayout.LabelField("Valid precision values: 1, 0.1, ... 0.0000001");

        selected = EditorGUILayout.Popup("Type: ", selected, options);

        switch(selected) {
            case 0:
                intMin = EditorGUILayout.IntField("Min: ", intMin);
                intMax = EditorGUILayout.IntField("Max: ", intMax);
                if(GUILayout.Button("Calculate Required Bits")) {
                    result = SerializerUtils.RequiredBitsInt(intMin, intMax);
                }
                EditorGUILayout.LabelField("Required bits: " + result.ToString());
                EditorGUILayout.LabelField("Required bytes: " + ((float)result / 8f).ToString());

                break;

            case 1:
                floatMin = EditorGUILayout.FloatField("Min: ", floatMin);
                floatMax = EditorGUILayout.FloatField("Max: ", floatMax);
                floatPrecision = EditorGUILayout.FloatField("Precision: ", floatPrecision);
                if(GUILayout.Button("Calculate Required Bits")) {
                    result = SerializerUtils.RequiredBitsFloat(floatMin, floatMax, floatPrecision);
                }
                EditorGUILayout.LabelField("Required bits: " + result.ToString());
                EditorGUILayout.LabelField("Required bytes: " + ((float)result / 8f).ToString());

                break;

            case 2:

                quatPrecision = EditorGUILayout.FloatField("Precision: ", quatPrecision);
                if(GUILayout.Button("Calculate Required Bits")) {
                    result = SerializerUtils.RequiredBitsQuaternion(quatPrecision);
                }
                EditorGUILayout.LabelField("Required bits: " + result.ToString());
                EditorGUILayout.LabelField("Required bytes: " + ((float)result / 8f).ToString());
                break;
        }
    }
}
