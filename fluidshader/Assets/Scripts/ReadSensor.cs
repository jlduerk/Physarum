using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO.Ports;

public class ReadSensor : MonoBehaviour
{
    SerialPort stream = new SerialPort("COM3", 9600);
    public float lightSensorVal;

    // Start is called before the first frame update
    void Start()
    {
        stream.Open(); //Open the Serial Stream.
    }

    // Update is called once per frame
    void Update()
    {
        string value = stream.ReadLine(); //Read the information
        lightSensorVal = (float)int.Parse(value);
        lightSensorVal -= 200;
        if (lightSensorVal < 0) lightSensorVal = 0;
        lightSensorVal /= 600;
        if (lightSensorVal > 1) lightSensorVal = 1;


        //Debug.Log(lightSensorVal);
    }
}
