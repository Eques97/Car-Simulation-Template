using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;

public class CarConfig : MonoBehaviour
{
    [Header("Body")]
    public float Mass;
    public Vector3 CenterOfMass;
    public Vector3 ColliderCenter;
    public Vector3 ColliderSize;
    public Vector3 BodyMeshCenter;
    public Transform BodyMesh;
    [Header("Wheel")]
    public float Track;
    public float WheelBase;
    public float WheelMass;
    public float WheelRadius;
    public float WheelDampingRate;
    public float ForwardFrictionStiffness;
    public float SideWaysFrictionStiffness;
    public Transform WheelMesh;
    [Header("Suspension")]
    public float SuspensionDistance;
    public float Spring;
    public float Damper;
    public float TargetPosition;
    [Header("Engine")]
    public AnimationCurve Power;
    private float RPM;
    private float RPMmax;
    [Header("Transmission")]
    public AnimationCurve GearRatios;
    private int Gear;
    public float FinalDriveRatio;
    public bool Auto;
    public int GearRaiseRPM;
    public int GearReduceRPM;
    [Header("Drive")]
    public bool Front;
    public float Lever;
    public float Clutch;
    public bool Brake;
    [Header("Steering")]
    [Range(20, 45)]
    public int MaxSteerAngle;
    public float SteerAngle;

    private readonly string[] Wheels = { "LeftFrontWheel", "LeftRearWheel", "RightFrontWheel", "RightRearWheel" };
    private float RPMOutRangeTime = 0;
    private float LastVelocity = 0;
    private float Acceleration;

    // Start is called before the first frame update
    void Start()
    {
        Rigidbody rigidbody = gameObject.AddComponent<Rigidbody>();
        rigidbody.mass = Mass;
        rigidbody.centerOfMass = new Vector3(0, 0, 0);
        BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
        boxCollider.size = ColliderSize;
        boxCollider.center = ColliderCenter;
        Transform bodyMesh = Instantiate(BodyMesh, transform);
        bodyMesh.name = "BodyMesh";
        bodyMesh.localPosition = BodyMeshCenter;

        for (int i = 0; i < Wheels.Length; i++)
        {
            GameObject wheel = new GameObject(Wheels[i]);
            wheel.transform.parent = transform;
            float x = i < 2 ? Track * -0.5f : Track * 0.5f;
            float z = i % 2 == 0 ? WheelBase * 0.5f : WheelBase * -0.5f;
            wheel.transform.localPosition = new Vector3(x, 0, z);
            WheelCollider wheelCollider = wheel.AddComponent<WheelCollider>();
            wheelCollider.mass = WheelMass;
            wheelCollider.radius = WheelRadius;
            wheelCollider.wheelDampingRate = WheelDampingRate;
            wheelCollider.suspensionDistance = SuspensionDistance;
            JointSpring jointSpring = wheelCollider.suspensionSpring;
            jointSpring.spring = Spring;
            jointSpring.damper = Damper;
            jointSpring.targetPosition = TargetPosition;
            wheelCollider.suspensionSpring = jointSpring;
            WheelFrictionCurve forwardFrictionCurve = wheelCollider.forwardFriction;
            forwardFrictionCurve.stiffness = ForwardFrictionStiffness;
            wheelCollider.forwardFriction = forwardFrictionCurve;
            WheelFrictionCurve sidwaysFrictionCurve = wheelCollider.sidewaysFriction;
            sidwaysFrictionCurve.stiffness = SideWaysFrictionStiffness;
            wheelCollider.sidewaysFriction = sidwaysFrictionCurve;
            Transform wheelMesh = Instantiate(WheelMesh, wheel.transform);
            wheelMesh.name = "WheelMesh";

        }

        RPMmax = Power.keys[Power.keys.Length - 2].time; //initialized engine max RPM from curve
    }

    // Update is called once per frame
    void Update()
    {
        MapWheelMesh();
    }

    private void MapWheelMesh()
    {
        for (int i = 0; i < 4; i++)
        {
            Transform wheel = transform.Find(Wheels[i]);
            Transform wheelMesh = wheel.Find("WheelMesh");
            WheelCollider wheelCollider = wheel.GetComponent<WheelCollider>(); 
            wheelCollider.GetWorldPose(out Vector3 pos, out Quaternion quat);
            if (i > 1) quat *= Quaternion.Euler(0.0f, 180.0f, 0.0f);
            wheelMesh.SetPositionAndRotation(pos, quat);

        }
    }

    private void FixedUpdate()
    {
        ControlPedals();
        ShiftGears();
        Steer();
        if (Front) Drive(0, 2);
        else Drive(1, 3);
        ComputeAccelerationG();
    }

    private void ControlPedals()
    {
        //Clutch
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
        //Lever
        float idleLever = 0.3f;
        float vInput = Input.GetAxis("Vertical");
        float deltaLever = Lever + vInput * Time.deltaTime;
        if (deltaLever <= 1 && deltaLever >= idleLever) Lever = deltaLever;

        //Brake
        Brake = Input.GetKey(KeyCode.Space) ? true : false;
        if (Auto && Brake) Clutch = 0;

        //Ignition
        if (Input.GetKey(KeyCode.I) && RPM == 0) RPM = Lever * RPMmax;
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
            int maxGear = (int)GearRatios.keys[GearRatios.keys.Length - 1].time;
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

        float hAxis = Input.GetAxis("Horizontal");
        //float deltaSteerAngle = SteerAngle + hAxis * Time.fixedDeltaTime * 1.5f;
        //if (deltaSteerAngle < 1 && deltaSteerAngle > -1) SteerAngle = deltaSteerAngle;
        SteerAngle = hAxis * hAxis * hAxis / Mathf.Abs(hAxis);

        if (SteerAngle < 0)
        {
            lw.steerAngle = SteerAngle * MaxSteerAngle;
            float r = WheelBase / Mathf.Tan(lw.steerAngle * Mathf.Deg2Rad) - Track / 2;
            rw.steerAngle = Mathf.Atan(WheelBase / (r - Track / 2)) * Mathf.Rad2Deg;
        }
        else if (SteerAngle > 0)
        {
            rw.steerAngle = SteerAngle * MaxSteerAngle;
            float r = WheelBase / Mathf.Tan(rw.steerAngle * Mathf.Deg2Rad) + Track / 2;
            lw.steerAngle = Mathf.Atan(WheelBase / (r + Track / 2)) * Mathf.Rad2Deg;
        }
    }

    private void Drive(int indexL, int indexR)
    {
        for (int i = 0; i < 4; i++) if (transform.Find(Wheels[i]).GetComponent<WheelCollider>() == null) return;
        WheelCollider wcL = transform.Find(Wheels[indexL]).GetComponent<WheelCollider>();
        WheelCollider wcR = transform.Find(Wheels[indexR]).GetComponent<WheelCollider>();
        float brakeTorque = Brake ? 1000 : 0;
        float gearRatio = GearRatios.Evaluate(Gear);
        if (gearRatio == 0) Clutch = 0;
        float RPMi = gearRatio * FinalDriveRatio * 0.5f * (wcL.rpm + wcR.rpm);
        float pullDelta = -Time.fixedDeltaTime * Clutch * (RPM - RPMi) * 10f;
        float pushDelta = Time.fixedDeltaTime * (RPMmax * Lever - RPM) * 10f;
        if (RPMi < RPM) RPM += pullDelta + pushDelta;
        else RPM += pushDelta;
        float power = Power.Evaluate(RPM);
        float torque = power * 30000 / (Mathf.PI * RPM);
        if (power == 0 && RPM < RPMmax)
        {
            RPM = 0;
            torque = 0;
        }
        float torqueW = Clutch * gearRatio * FinalDriveRatio * 0.5f * torque;
        wcL.motorTorque = torqueW;
        wcR.motorTorque = torqueW;
        wcL.wheelDampingRate = (RPMi < RPM || RPM == 0) ? WheelDampingRate : 10 * wcL.motorTorque / (RPM / gearRatio / FinalDriveRatio);
        wcR.wheelDampingRate = (RPMi < RPM || RPM == 0) ? WheelDampingRate : 10 * wcL.motorTorque / (RPM / gearRatio / FinalDriveRatio);
        for (int i = 0; i < 4; i++)
        {
            WheelCollider wc = transform.Find(Wheels[i]).GetComponent<WheelCollider>();
            wc.brakeTorque = brakeTorque;
            if (i != indexL && i != indexR)
            {
                wc.motorTorque = 0;
                wc.wheelDampingRate = WheelDampingRate;
            }
        }
    }

    public void ComputeAccelerationG()
    {
        float currentVelocity = GetComponent<Rigidbody>().velocity.magnitude;
        Acceleration = (currentVelocity - LastVelocity) / Time.fixedDeltaTime / Physics.gravity.magnitude;
        LastVelocity = currentVelocity;
    }

    public string GetCurrentGear()
    {
        if (Gear == 0) return "N";
        else if (Gear == -1) return "R";
        else return Gear.ToString();
    }

    public int GetEngineRPM()
    {
        return (int)RPM;
    }

    public int GetSpeed()
    {
        return (int)(GetComponent<Rigidbody>().velocity.magnitude * 3.6f);
    }

    public float GetAcceleration()
    {
        return Acceleration;
    }
}
