using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;

public class CarConfig : MonoBehaviour
{
    [Header("Wheel")]
    public float Track;
    public float WheelBase;
    public bool WheelPhysics;
    public float WheelWidth;
    public float WheelRadius;
    public float BaseWheelDampingRate;
    [Header("Suspension")]
    public float SuspensionDistance;
    public float Spring;
    public float Damper;
    [Header("Engine")]
    public AnimationCurve Performance;
    private int RPM;
    public int MaxRPM;
    private bool Dead;
    [Header("Transmission")]
    public AnimationCurve GearRatios;
    private int Gear;
    public float FinalDriveRatio;
    public bool Auto;
    public int GearRaiseRPM;
    public int GearReduceRPM;
    [Header("Drive")]
    public bool Front;
    private float Lever;
    private float Clutch;
    private bool Brake;
    [Header("Steering")]
    [Range(20, 45)]
    public int MaxSteerAngle;

    private readonly string[] Wheels = { "LeftFrontWheel", "LeftRearWheel", "RightFrontWheel", "RightRearWheel" };
    private float RPMOutRangeTime = 0;

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        ConfigWheelCollider();
        ConfigWheel();
        ConfigSuspension();
        //if (WheelPhysics) Debug.Log("Speed " + GetComponent<Rigidbody>().velocity.magnitude * 3.6f + " WheelRPM " + transform.Find("LeftFrontWheel").GetComponent<WheelCollider>().rpm);
    }

    private void FixedUpdate()
    {
        ControlPedals();
        ShiftGears();
        Steer();
        Drive();
    }

    private void ConfigWheelCollider()
    {
        for (int i = 0; i < 4; i++)
        {
            Transform wheel = transform.Find(Wheels[i]);
            WheelCollider wc = wheel.GetComponent<WheelCollider>();
            if (WheelPhysics && wc == null) wheel.gameObject.AddComponent<WheelCollider>();
            else if (WheelPhysics && wc != null) wc.wheelDampingRate = BaseWheelDampingRate;
            else if (!WheelPhysics && wc != null) Destroy(wheel.GetComponent<WheelCollider>());
        }
    }

    private void ConfigWheel()
    {
        for (int i = 0; i < 4; i++)
        {
            Transform wheel = transform.Find(Wheels[i]);
            Transform wheelMesh = wheel.Find("DefaultWheelMesh");
            wheelMesh.localScale = new Vector3(WheelWidth, WheelRadius * 2, WheelRadius * 2);
            WheelCollider wc = wheel.GetComponent<WheelCollider>();
            if (wc == null)
            {
                float x = Track / 2;
                float z = WheelBase / 2;
                if (i % 2 != 0) z *= -1;
                if (i < 2) x *= -1;
                else wheelMesh.localRotation = Quaternion.Euler(0, 180, 0);
                wheel.localPosition = new Vector3(x, 0, z);
            }
            else
            {
                wc.radius = WheelRadius;
                wc.GetWorldPose(out Vector3 pos, out Quaternion quat);
                if (i > 1) quat *= Quaternion.Euler(0.0f, 180.0f, 0.0f);
                wheelMesh.SetPositionAndRotation(pos, quat);
            }
        }
    }

    private void ConfigSuspension()
    {
        for (int i = 0; i < 4; i++)
        {
            Transform wheel = transform.Find(Wheels[i]);
            Transform wheelMesh = wheel.Find("DefaultWheelMesh");
            WheelCollider wc = wheel.GetComponent<WheelCollider>();
            if (wc == null) wheelMesh.localPosition = new Vector3(0, -SuspensionDistance, 0);
            else
            {
                wc.suspensionDistance = SuspensionDistance;                
                wc.suspensionSpring = new JointSpring
                {
                    spring = Spring,
                    damper = Damper
                };
            }            
        }
    }

    private void ControlPedals()
    {
        if (Input.GetKey(KeyCode.LeftShift))
        {
            float deltaClutch = Clutch - Time.fixedDeltaTime * 5;
            Clutch = deltaClutch > 0 ? deltaClutch : 0;
        }
        else
        {
            float deltaClutch = Clutch + Time.fixedDeltaTime * 1;
            Clutch = deltaClutch < 1 ? deltaClutch : 1;
        }
        if (Input.GetKey(KeyCode.I)) Dead = false;
        float vInput = Input.GetAxis("Vertical");
        if (vInput > 0.15) Lever = vInput;
        else if (vInput < 0) Brake = true;
        else
        {
            Lever = 0.15f;
            Brake = false;
        }
        if (Brake && Auto)
        {
            Clutch = 0;
        }
    }

    private void ShiftGears()
    {
        if (Auto)
        {
            if (Input.GetKey(KeyCode.R) || Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.R)) Clutch = 0;
            if (Input.GetKey(KeyCode.N)) Gear = 0;
            else if (Input.GetKey(KeyCode.R)) Gear = -1;
            else if (Input.GetKey(KeyCode.D)) Gear = 1;
            if (Gear < 1) return;
            float threshold = 1;
            int minGear = 1;
            int maxGear = 1;
            foreach (Keyframe kf in GearRatios.keys) maxGear = (int)kf.time - 1;
            if (RPM > GearRaiseRPM)
            {
                if (RPMOutRangeTime < threshold) RPMOutRangeTime += Time.fixedDeltaTime;
                else
                {
                    RPMOutRangeTime = 0;
                    if (Gear < maxGear) Gear += 1;
                    else Gear = maxGear;
                }
            }
            else if (RPM < GearReduceRPM)
            {
                if (RPMOutRangeTime > -threshold) RPMOutRangeTime -= Time.fixedDeltaTime;
                else
                {
                    RPMOutRangeTime = 0;
                    if (Gear > minGear) Gear -= 1;
                    else Gear = minGear;
                }
            }
            else
            {
                if (RPMOutRangeTime < 0) RPMOutRangeTime += Time.fixedDeltaTime;
                else if (RPMOutRangeTime > 0) RPMOutRangeTime -= Time.fixedDeltaTime;
            }            
        }
        else if (Input.GetKey(KeyCode.LeftShift) && Clutch == 0)
        {
            if (Input.GetKey(KeyCode.N)) Gear = 0;
            else if (Input.GetKey(KeyCode.R)) Gear = -1;
            else if (Input.GetKey(KeyCode.Alpha1)) Gear = 1;
            else if (Input.GetKey(KeyCode.Alpha2)) Gear = 2;
            else if (Input.GetKey(KeyCode.Alpha3)) Gear = 3;
            else if (Input.GetKey(KeyCode.Alpha4)) Gear = 4;
            else if (Input.GetKey(KeyCode.Alpha5)) Gear = 5;
            else if (Input.GetKey(KeyCode.Alpha6)) Gear = 6;
        }
    }

    private void Steer()
    {
        WheelCollider lw = transform.Find("LeftFrontWheel").GetComponent<WheelCollider>();
        WheelCollider rw = transform.Find("RightFrontWheel").GetComponent<WheelCollider>();

        if (lw == null || rw == null) return;

        float steer = Input.GetAxis("Horizontal");

        float l = WheelBase;
        float t = Track;

        float r;
        float la = lw.steerAngle;
        float ra = rw.steerAngle;

        if (steer < 0)
        {
            la -= MaxSteerAngle * Time.deltaTime;
            if (la > -MaxSteerAngle)
            {
                lw.steerAngle = la;
                r = l / Mathf.Tan(la * Mathf.Deg2Rad) - t / 2;
                rw.steerAngle = Mathf.Atan(l / (r - t / 2)) * Mathf.Rad2Deg;
            }
        }
        else if (steer > 0)
        {
            ra += MaxSteerAngle * Time.deltaTime;
            if (ra < MaxSteerAngle)
            {
                rw.steerAngle = ra;
                r = l / Mathf.Tan(ra * Mathf.Deg2Rad) + t / 2;
                lw.steerAngle = Mathf.Atan(l / (r + t / 2)) * Mathf.Rad2Deg;
            }
        }
        else
        {
            la -= MaxSteerAngle * Time.deltaTime * la / Mathf.Abs(la);
            if (la > -0.1f && la < 0.1f) lw.steerAngle = 0;
            else lw.steerAngle = la;
            r = l / Mathf.Tan(la * Mathf.Deg2Rad) - t / 2;
            rw.steerAngle = Mathf.Atan(l / (r - t / 2)) * Mathf.Rad2Deg;
        }
    }

    private void Drive()
    {
        int indexL, indexR;
        if (Front)
        {
            indexL = 0;
            indexR = 2;
        }
        else
        {
            indexL = 1;
            indexR = 3;
        }
        WheelCollider wcL = transform.Find(Wheels[indexL]).GetComponent<WheelCollider>();
        WheelCollider wcR = transform.Find(Wheels[indexR]).GetComponent<WheelCollider>();
        float brakeTorque = Brake ? 5000 : 0;
        for (int i = 0; i < 4; i++)
        {
            WheelCollider wc = transform.Find(Wheels[i]).GetComponent<WheelCollider>();
            if (wc == null) return;
            wc.wheelDampingRate = BaseWheelDampingRate;
            wc.brakeTorque = brakeTorque;
            wc.motorTorque = 0;
        }
        if (Dead)
        {
            wcL.wheelDampingRate = 100;
            wcR.wheelDampingRate = 100;
            RPM = 0;
            return;
        }
        float rpmS = FinalDriveRatio * 0.5f * (wcL.rpm + wcR.rpm);
        float gearRatio = GearRatios.Evaluate(Gear);
        float rpmC = rpmS * gearRatio;
        float rpmEC0 = MaxRPM * Lever;
        if (gearRatio == 0)
        {
            RPM = (int)rpmEC0;
            return;
        }
        float rpmE = (rpmEC0 * (1 - 0.5f * Clutch) + rpmC * 0.5f * Clutch);
        RPM = (int)rpmE;
        float pE = Performance.Evaluate(RPM) * 1000;
        if (pE == 0 && RPM < MaxRPM) Dead = true;
        float tS = pE * Clutch * 60 / (2 * Mathf.PI * rpmE / gearRatio / FinalDriveRatio);
        float tW = tS * 0.5f;
        wcL.motorTorque = tW;
        wcR.motorTorque = tW;
        if (rpmC > rpmEC0)
        {
            float rpmWLimit = rpmEC0 / gearRatio / FinalDriveRatio;
            float engineDragDamp = 10 * tW / rpmWLimit;
            wcL.wheelDampingRate = engineDragDamp + BaseWheelDampingRate;
            wcR.wheelDampingRate = engineDragDamp + BaseWheelDampingRate;
        }
        if (RPM > MaxRPM)
        {
            wcL.wheelDampingRate = 100;
            wcR.wheelDampingRate = 100;
        }
    }

    public string GetCurrentGear()
    {
        if (Gear == 0) return "N";
        else if (Gear == -1) return "R";
        else return Gear.ToString();
    }

    public int GetEngineRPM()
    {
        return RPM;
    }

    public int GetSpeed()
    {
        return (int)(GetComponent<Rigidbody>().velocity.magnitude * 3.6f);
    }
}
