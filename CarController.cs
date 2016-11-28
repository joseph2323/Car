using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Trails;
using UnityEngine.UI;

public class CarController : MonoBehaviour 
{
	public bool canMove = true;
	public Transform[] wheels;
	private float engineRPM;
	float steer = 0.0f;
	public Rigidbody body;
	float maxSteer = 25.0f;
	string mphDisplay;
	float motor = 0.0f;
	float fuel=100;
	string fuelText;
	public float AntiRollA;
	private float wheelRPM;
	//engine powerband
	float minRPM = 1500;
	float maxRPM = 7000;
	//maximum Engine Torque
	public float maxTorque = 1600.0f;
	//automatic transmission shift points
	float shiftDownRPM = 2200;
	float shiftUpRPM = 6300;
	public int gears = 5;
	int gear=1;
	public float[] gearRatios;
	public float finalDriveRatio = 4.4f;
	public bool automatic = true;
	WheelFrictionCurve defaultFrictionForward;
	WheelFrictionCurve defaultFrictionSideways;
	WheelFrictionCurve roadFrictionSideways;
	WheelFrictionCurve roadFrictionForward;
	private float mph;
	//Stores all live trails
	private LinkedList<Trail> trails = new LinkedList<Trail>();
	public bool onRoad = false;
	//Parameters
	public float width = 0.1f;
	public float decayTime = 1f;
	public Material material;
	public int roughness = 0;
	public bool softSourceEnd = false;
	private AudioSource carSound;
	
	//the range for audio source pitch
	private const float lowPtich = 0.5f;
	private const float highPitch = 5f;
	
	//change the reductionFactor to 0.1f if you are using the rigidbody velocity as parameter to determine the pitch
	private const float reductionFactor = .1f;
	





	void Awake ()
	{
		//get the Audio Source component attached to the car
		carSound = GetComponent<AudioSource>();
		//get the wheelJoint2D component attached to the car
		//wj = GetComponent<WheelJoint2D>();
		//carRigidbody = GetComponent<Rigidbody2D>();
	}

	void Start ()
	{	    



		defaultFrictionForward.extremumSlip = 0.25f;//0.4f;
		defaultFrictionForward.extremumValue = 2.5f;//1.0f;
		defaultFrictionForward.asymptoteSlip = 0.9f;//0.8f;
		defaultFrictionForward.asymptoteValue = 1.0f;//0.5f;
		defaultFrictionForward.stiffness = 1.5f;//5.0f;
				
		//Set default friction values of wheels
		defaultFrictionSideways.extremumSlip = 0.25f;//0.4f;
		defaultFrictionSideways.extremumValue = 2.2f;//1.0f;
		defaultFrictionSideways.asymptoteSlip = 0.8f;//0.5f;
		defaultFrictionSideways.asymptoteValue = 1.0f;//0.75f;
		defaultFrictionSideways.stiffness = 1.4f;//5.0f;


		roadFrictionForward = defaultFrictionForward;
		roadFrictionSideways = defaultFrictionSideways;
		roadFrictionForward.stiffness = 1.5f;
		roadFrictionSideways.stiffness = 1.6f;




		for (int i = 0; i < wheels.Length; i++)
		{			
			GetCollider (i).forwardFriction = defaultFrictionForward;
			GetCollider (i).sidewaysFriction = defaultFrictionSideways;
		}
		body.centerOfMass = new Vector3(0,-0.5f,0.4f);
		for (int i = 0; i < wheels.Length; i++)
		{

			GetCollider(i).mass = 20.0f;
			GetCollider(i).radius = 0.51f;
			GetCollider(i).wheelDampingRate =0.64f;
			GetCollider(i).forceAppPointDistance = 0.2f;			
		}
	}

	void FixedUpdate()
	{
		if (!canMove) {
			return;
		}
		
		if (automatic)
		{
			AutomaticTransmission();
		}
		else
		{
			bool changeUp = Input.GetKeyDown (KeyCode.Q);
			bool changeDown = Input.GetKeyDown (KeyCode.A);
			if (changeUp || changeDown)
				ChangeGear(changeUp);
		}
		Controls();
		TrailUpdater();
		
		NewTrail (GetCollider(0));
		NewTrail (GetCollider(1));
		NewTrail (GetCollider(2));
		NewTrail (GetCollider(3));
		mph = body.velocity.magnitude * 2.237f;
		 	
		for (int i = 0; i < wheels.Length; i++)
		{
			GripLevels(GetCollider(i));
		}
		if(onRoad)
		{
			/*wheel.forwardFriction = defaultFrictionForward;
			wheel.sidewaysFriction = roadFrictionSideways;*/
			for (int i = 0; i < wheels.Length; i++)
			{
				GetCollider(i).forwardFriction = defaultFrictionForward;
				GetCollider(i).sidewaysFriction = roadFrictionSideways;
			}
		}

		carSound.pitch = (engineRPM/2000);
	}


	//RETURNS A WHEELCOLLIDER 
	//0 = FRONT LEFT, 1 = FRONT RIGHT, 
	//2 = BACK LEFT,  3 = BACK RIGHT
	WheelCollider GetCollider ( int n  )
	{
		return wheels[n].gameObject.GetComponent<WheelCollider>();    
	}

	public bool Active
	{
		get { return (trails.Count == 0?false:(!trails.Last.Value.Finished)); }
	}

	public void EndTrail()
	{
		if(!Active) return;
		trails.Last.Value.Finish();
	}


	//GUI FOR DEBUGGING
	void Update()
	{	
		if (GetComponent<CarNetworking> ().isLocalPlayer) {
			GameObject.Find("MPH").GetComponent<Text>().text = mph.ToString("F2") + "MPH";
			GameObject.Find("FUEL").GetComponent<Text>().text = "FUEL " + fuelText;
			GameObject.Find("GEAR").GetComponent<Text>().text = "GEAR " + gear.ToString();
			GameObject.Find("ENGINE").GetComponent<Text>().text = "POWER " + GetCollider(0).motorTorque;
			GameObject.Find("RPM").GetComponent<Text>().text = engineRPM + " RPM";
		}
		//GUI.Box(new Rect((Screen.width - 200), 50, 150, 100),
		//new GUIContent(mph.ToString("F2") + "\nFuel: " + fuelText +"\nGear: " + gear + "\n engine power: " + GetCollider(0).motorTorque + "\n curr rpm: " + engineRPM));		
	}


	void TrailUpdater()
	{
		//Don't update if there are no trails
		if(trails.Count == 0) return;
		
		//Essentially a foreach loop, allowing trails to be removed from the list if they are finished
		LinkedListNode<Trail> t = trails.First;
		LinkedListNode<Trail> n;
		do
		{
			n = t.Next;
			t.Value.Update();
			if(t.Value.Dead)
				trails.Remove(t);
			t = n;
		}while(n != null);
	}


	public void NewTrail(WheelCollider coll)
	{
		WheelHit hit = new WheelHit();
		coll.GetGroundHit(out hit);

		//Stops emitting the last trail and passes the parameters onto a new one
		EndTrail();
		//trails.AddLast(new Trail(hit.collider.transform,  material, decayTime, roughness, softSourceEnd, width));
	}

	void Controls()
	{
		if(Input.GetAxis("Vertical") > 0)
		{
			for (int i = 0; i < wheels.Length; i++)
			{
				GetCollider(i).brakeTorque=0.0f;
				GetCollider(i).motorTorque=CalcEngine(Input.GetAxis("Vertical"));
			}
		}
		else if (Input.GetAxis("Vertical") < 0)
		{
			if(gear == 0)
			{
				
				for (int i = 0; i < wheels.Length; i++)
				{
					GetCollider(i).motorTorque=CalcEngine(-Input.GetAxis("Vertical"));
					GetCollider(i).brakeTorque= 0.0f;
				}
				
			}
			else
			{
				for (int i = 0; i < wheels.Length; i++)
				{
					GetCollider(i).motorTorque=CalcEngine(0.0f);
				}
				GetCollider(0).brakeTorque= -Input.GetAxis("Vertical")* 0.6f;
				GetCollider(1).brakeTorque= -Input.GetAxis("Vertical")* 0.6f;
				GetCollider(2).brakeTorque= -Input.GetAxis("Vertical")* 0.8f;
				GetCollider(3).brakeTorque= -Input.GetAxis("Vertical")* 0.8f;
			}
		} 
		else
		{
			for (int i = 0; i < wheels.Length; i++)
			{
				GetCollider(i).brakeTorque=0.0f;
				GetCollider(i).motorTorque=CalcEngine(Input.GetAxis("Vertical"));
			}
		}
		
		
		wheelRPM= body.velocity.magnitude*60.0f*0.5f;
		steer=Input.GetAxis("Horizontal") * maxSteer;	
		fuelText = fuel.ToString("F2");		
		GetCollider(0).steerAngle=steer;
		GetCollider(1).steerAngle=steer;	
		for (int i = 0; i < wheels.Length; i++)
		{
			ApplyLocalPositionToVisuals(GetCollider(i));
		}
		
		
		
		//ANTIROLL = MAKE SURE 
		//0 = FRONT LEFT, 1 = FRONT RIGHT, 
		//2 = BACK LEFT,  3 = BACK RIGHT
		AntiRoll(GetCollider(0),GetCollider(1));
		AntiRoll(GetCollider(2),GetCollider(3));
	}

	//AUTO TRANSMISSION
	void AutomaticTransmission ()
	{
		if (gear == 0 && Input.GetAxis ("Vertical") > 0) 
		{
			gear = 1;
		} 
		else if (body.velocity.magnitude < 0.05f && gear != 0 && Input.GetAxis ("Vertical") < 0)
		{
			gear = 0;
		}
		else
		{
			if(engineRPM>shiftUpRPM&&gear<gearRatios.Length-1)
				gear++;
			if(engineRPM<shiftDownRPM&&gear>1)
				gear--;
		}


	}
	//mANUAL gEARcHANGES FOR KEYBOARD/360 CONTROLLER.
	void ChangeGear(bool changeUp)
	{
		if ((changeUp && gear < gearRatios.Length-1) || (!changeUp && gear > 0))
		{
			gear += (changeUp ? 1 : -1);
			//currentRPM *= (changeUp ? 0.5f : 2);
		}
	}

	//RUDIMENTARY POWER CALCULATOR
	float CalcEngine(float power)
	{

		motor = power * Time.deltaTime * 10; //controller right trigger (360-win10)
		fuel -= 0.01f  * power;	



				
		engineRPM = wheelRPM * gearRatios[gear] * finalDriveRatio;		
		if(engineRPM<minRPM)
		{
			engineRPM=minRPM;//KEEPIDLE
		}		
		if(engineRPM<maxRPM)
		{
			//fake a basic torque curve
			float x=(2*(engineRPM/maxRPM)-1);
			float torqueCurve = 0.5f*(-x*x+2);
			float torqueToForceRatio = gearRatios[gear]*finalDriveRatio;
			return motor*maxTorque*torqueCurve*torqueToForceRatio;
		}	      
		else
			//rpmdelimiter
			return 0;
	}
	
	//COLLISION HANDLING
	void OnCollisionEnter ( Collision other  )
	{
		if(other.gameObject.name=="Fuel")
		{
			fuel += 25;
			Destroy(other.gameObject);
		}


	}	
	void GripLevels(WheelCollider wheel)
	{
		WheelHit hit;
		if(wheel.GetGroundHit(out hit))
		{

			if(hit.collider.gameObject.tag == "Road")
			{
				if(wheel.sidewaysFriction.stiffness != roadFrictionSideways.stiffness)
				{
					Debug.Log ("Road grip!");
					wheel.sidewaysFriction = roadFrictionSideways;
				}
			}
			else 
			{
				if (wheel.sidewaysFriction.stiffness != defaultFrictionSideways.stiffness)
				{
					wheel.sidewaysFriction = defaultFrictionSideways;
				}
			}
			
		}
		else
		{
		
			for (int i = 0; i < wheels.Length; i++)
			{			
				wheel.forwardFriction = defaultFrictionForward;
				wheel.sidewaysFriction = defaultFrictionSideways;
			}
		}
	}




	//ROTATE WHEEL MESHES WITH WHEELCOLLIDERS
	void ApplyLocalPositionToVisuals ( WheelCollider coll  )
	{
		if (coll.transform.childCount == 0) 
		{
			return;
		}
		Transform visualWheel = coll.transform.GetChild(0);
		Vector3 position = gameObject.transform.position;
		Quaternion quat = gameObject.transform.rotation;
		coll.GetWorldPose(out position, out quat);
		visualWheel.transform.position = position;
		visualWheel.transform.rotation = quat;		
	}

	//"ROLLBARS" (STOPS CAR ROLLING OVER, OTHERWISE FRICTION MUST BE TOO SLIPPY
	void AntiRoll(WheelCollider WheelL, WheelCollider WheelR)
	{		
		WheelHit hit; 
		float travelL = 1.0f; 
		float travelR = 1.0f;		
		bool groundedL= WheelL.GetGroundHit(out hit);   
		if (groundedL) 
			travelL = (-WheelL.transform.InverseTransformPoint(hit.point).y - WheelL.radius) 
				/ WheelL.suspensionDistance;
		
		bool groundedR= WheelR.GetGroundHit(out hit); 
		if (groundedR) 
			travelR = (-WheelR.transform.InverseTransformPoint(hit.point).y - WheelR.radius) 
				/ WheelR.suspensionDistance;
		
		float antiRollForce= (travelL - travelR) * AntiRollA;		
		if (groundedL) 
			body.AddForceAtPosition(WheelL.transform.up * -antiRollForce, WheelL.transform.position); 
		if (groundedR) 
			body.AddForceAtPosition(WheelR.transform.up * antiRollForce, WheelR.transform.position); 
	}

}