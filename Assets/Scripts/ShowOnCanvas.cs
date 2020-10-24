using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShowOnCanvas : MonoBehaviour
{
    public Transform Car;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        CarConfig cc = Car.GetComponent<CarConfig>();
        string gSign = (cc.GetAcceleration() > 0) ? "+ " : "- ";
        string status = "Speed : " + 
            cc.GetSpeed() + " km/h\nG : " + 
            gSign + Mathf.Abs(cc.GetAcceleration()).ToString("0.00") + "\nRPM " +
            cc.GetEngineRPM() + "\nGear : " + 
            cc.GetCurrentGear();
        transform.Find("Status").GetComponent<Text>().text = status;
    }
}
