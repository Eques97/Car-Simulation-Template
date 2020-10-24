using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShowOnCanvas : MonoBehaviour
{
    public Transform Car;
    private readonly string Manual = "Use arrow keys to accelerate, brake and steer\nUse leftshift to press clutch" +
        "\nUse number keys, \"r\" and \"n\" to shift gears\nPress \"i\" to ignite the engine";

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        CarConfig cc = Car.GetComponent<CarConfig>();
        string status = "Speed : " + cc.GetSpeed() + " km/h\nRPM : " + cc.GetEngineRPM() + "\nGear : " + cc.GetCurrentGear();
        transform.Find("Status").GetComponent<Text>().text = status;
        transform.Find("Manual").GetComponent<Text>().text = Manual;
    }
}
