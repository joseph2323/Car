using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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
	float minRPM = 2000;
	float maxRPM = 7000;
	//maximum Engine Torque
	public float maxTorque = 1300.0f;
	float shiftDownRPM = 2200;
	float shiftUpRPM = 6500;
	public int gears = 5;
	int gear=1;
	public float[] gearRatios;
	public float finalDriveRatio = 4.4f;
	public bool automatic = true;
	WheelFrictionCurve defaultFrictionForward;
	WheelFrictionCurve defaultFrictionSideways;
	WheelFrictionCurve roadFrictionSideways;
	WheelFrictionCurve roadFrictionForward;
	WheelFrictionCurve dirtFrictionSideways;
	WheelFrictionCurve dirtFrictionForward;
	public Vector3 lastCheck = new Vector3(965,12,347);
	private float mph;
	//Stores all live trails
	public bool onRoad = false;
	//Parameters

	public AudioSource carSound;
	public AudioClip dirtSound;
	public AudioClip roadSound;
	public AudioClip defaultSound;

	public GameObject skidMarkPrefabFL;
	public GameObject skidMarkPrefabFR;
	public GameObject skidMarkPrefabBL;
	public GameObject skidMarkPrefabBR;

	public Material roadSkidMat;
	public Material dirtSkidMat;
	public Material defaultSkidMat;
	
	public float startSideSlip;
	public float endSlip;
	float returnPower;
	Light  myLight ;
	Light  myLight2 ;
	GameObject[] lightObj;

	void Awake ()
	{


	}

	void Start ()
	{	
		carSound = this.GetComponent<AudioSource>();
		skidMarkPrefabFL.GetComponent<AudioSource>().clip = defaultSound;
		skidMarkPrefabFR.GetComponent<AudioSource>().clip = defaultSound;
		skidMarkPrefabBL.GetComponent<AudioSource>().clip = defaultSound;
		skidMarkPrefabBR.GetComponent<AudioSource>().clip = defaultSound;

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
		defaultFrictionSideways.stiffness = 1.2f;//5.0f;

		lastCheck = transform.position;
		roadFrictionForward = defaultFrictionForward;
		roadFrictionSideways = defaultFrictionSideways;
		dirtFrictionForward = defaultFrictionForward;
		dirtFrictionSideways = defaultFrictionSideways;
		dirtFrictionForward.stiffness = 1.6f;
		dirtFrictionSideways.stiffness = 1.4f;
		roadFrictionForward.stiffness = 1.8f;
		roadFrictionSideways.stiffness = 1.6f;

		skidMarkPrefabFL.GetComponent<TrailRenderer>().material = defaultSkidMat;
		skidMarkPrefabFR.GetComponent<TrailRenderer>().material = defaultSkidMat;
		skidMarkPrefabBL.GetComponent<TrailRenderer>().material = defaultSkidMat;
		skidMarkPrefabBR.GetComponent<TrailRenderer>().material = defaultSkidMat;
		lightObj = GameObject.FindGameObjectsWithTag ("light");
		myLight = lightObj[0].GetComponent<Light>();
		myLight2 = lightObj[1].GetComponent<Light>();
	
		myLight.enabled = false;
		myLight2.enabled = false;

		for (int i = 0; i < wheels.Length; i++)
		{			
			GetCollider (i).forwardFriction = defaultFrictionForward;
			GetCollider (i).sidewaysFriction = defaultFrictionSideways;

			//skidMarkPrefab = (GameObject)Instantiate(skidMarkPrefab, GetCollider(i).transform.TransformPoint(GetCollider(i).center ) - (GetCollider (i).transform.up * (GetCollider (i).suspensionDistance * 0.8f)), Quaternion.identity);
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
	
	void Update ()
	{

		if (!canMove)
			return;
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
		mph = body.velocity.magnitude * 2.237f;

		if (fuel <= 0) {
			maxTorque = 0;
		} 
		else if (fuel > 0) {
			maxTorque =1200;
		}
		 
	}

	void FixedUpdate()
	{



		if(onRoad)
		{

			for (int i = 0; i < wheels.Length; i++)
			{
				GetCollider(i).forwardFriction = defaultFrictionForward;
				GetCollider(i).sidewaysFriction = roadFrictionSideways;
			}
		}

		carSound.pitch = (engineRPM/1800);
		GripLevels (GetCollider (0), skidMarkPrefabFL);
		GripLevels (GetCollider (1), skidMarkPrefabFR);
		GripLevels (GetCollider (2), skidMarkPrefabBL);
		GripLevels (GetCollider (3), skidMarkPrefabBR);
		Skidding (GetCollider (0), skidMarkPrefabFL);
		Skidding (GetCollider (1), skidMarkPrefabFR);
		Skidding (GetCollider (2), skidMarkPrefabBL);
		Skidding (GetCollider (3), skidMarkPrefabBR);
	}

	//RETURNS A WHEELCOLLIDER 
	//0 = FRONT LEFT, 1 = FRONT RIGHT, 
	//2 = BACK LEFT,  3 = BACK RIGHT
	WheelCollider GetCollider ( int n  )
	{
		return wheels[n].gameObject.GetComponent<WheelCollider>();    
	}

	//GUI FOR DEBUGGING
	void OnGUI()
	{

		GUI.Box(new Rect((Screen.width - 200), 50, 150, 100),
		        new GUIContent(mph.ToString("F2") + "\nFuel: " + fuelText +"%\nGear: " + gear + "\n engine power: " + GetCollider(0).motorTorque + "\n curr rpm: " + engineRPM));		
	}

	void Skidding(WheelCollider wheel, GameObject skids)
	{
		RaycastHit hit;
		Vector3 ColliderCenterPoint = wheel.transform.TransformPoint( wheel.center );

		if ( Physics.Raycast( ColliderCenterPoint, -wheel.transform.up, out hit, wheel.suspensionDistance + wheel.radius ) ) 
		{
			skids.transform.position = hit.point + (new Vector3(0,0.1f,0));

		}else
		{
			skids.transform.position = ColliderCenterPoint - (wheel.transform.up * wheel.suspensionDistance);
		}

		WheelHit CorrespondingGroundHit;
		wheel.GetGroundHit( out CorrespondingGroundHit );

		if ( Mathf.Abs( CorrespondingGroundHit.sidewaysSlip ) > startSideSlip)
		{
			skids.gameObject.GetComponent<AudioSource>().volume = Mathf.Clamp((CorrespondingGroundHit.sidewaysSlip - startSideSlip) + 1.4f, 0.001f,0.25f);
			skids.gameObject.SetActive(true);
			//Debug.Log("Skid Volume" + (Mathf.Clamp(CorrespondingGroundHit.sidewaysSlip - startSideSlip, 0.001f,0.2f)));
		}
		else if (Mathf.Abs( CorrespondingGroundHit.sidewaysSlip)  <= endSlip)
		{
			StartCoroutine(KillSkids(skids));
		}
	}

	IEnumerator KillSkids(GameObject skid)
	{
		yield return new WaitForSeconds(.5f);
		skid.SetActive(false);
	}

	void Controls()
	{


		if(Input.GetAxis("Vertical") > 0)
		{
			myLight.enabled = false;
			myLight2.enabled = false;// turns off lights
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
				myLight.enabled = true;
				myLight2.enabled = true;
				myLight.color = Color.white;
				myLight2.color = Color.white;//turns on reverse light
				for (int i = 0; i < wheels.Length; i++)
				{
					GetCollider(i).motorTorque=CalcEngine(-Input.GetAxis("Vertical"));
					GetCollider(i).brakeTorque= 0.0f;

				}				
			}
			else
			{
				myLight.enabled = true;
				myLight2.enabled = true;
				myLight.color = Color.red;
				myLight2.color = Color.red;//break light
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
			myLight.enabled = false;
			myLight2.enabled = false;
			for (int i = 0; i < wheels.Length; i++)
			{
				GetCollider(i).brakeTorque=0.0f;
				GetCollider(i).motorTorque=CalcEngine(Input.GetAxis("Vertical"));
			}
		}

		if (Input.GetKeyDown (KeyCode.R))//reset button returns you to last checkpoint
		{
			body.velocity = Vector3.zero;
			body.angularVelocity = Vector3.zero;
			body.transform.position = lastCheck;
			body.transform.rotation = Quaternion.Euler(0, 0, 0);
		
			engineRPM =0;
			GetCollider(0).motorTorque = 0;
			GetCollider(1).motorTorque = 0;
			GetCollider(2).motorTorque = 0;
			GetCollider(3).motorTorque = 0;
			return;

		}
		if (Input.GetKeyDown (KeyCode.F)) {
			fuel += 25;
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
		engineRPM = wheelRPM * gearRatios[gear] * finalDriveRatio;
		if (fuel >= 0) {
			fuel -= (Mathf.Abs (0.000003f * engineRPM));
		} else if (fuel < 0) {
			fuel = 0;
		}

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
			returnPower = motor*maxTorque*torqueCurve*torqueToForceRatio;

			return returnPower;
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

			Destroy(other.gameObject);
		}

	}	
	void OnTriggerEnter (Collider other)
	{
		if (other.gameObject.tag == "Checkpoint") 
		{

		}
	}
	public void addFuel()
	{
		lastCheck = body.transform.position; //sets checkpoint for reset function
		fuel += 10;
		if(fuel>100)
			fuel=100;
	}

	void GripLevels(WheelCollider wheel, GameObject skidPrefab)
	{
		WheelHit hit;
		if(wheel.GetGroundHit(out hit))
		{
			if(hit.collider.gameObject.tag == "Road")
			{
				skidPrefab.GetComponent<AudioSource>().clip = roadSound;
				skidPrefab.GetComponent<TrailRenderer>().material = roadSkidMat;
				if(wheel.sidewaysFriction.stiffness != roadFrictionSideways.stiffness)
				{
					Debug.Log ("Road grip!");
					wheel.sidewaysFriction = roadFrictionSideways;
				}
			}
			else if(hit.collider.gameObject.tag == "Dirt")
			{
				skidPrefab.GetComponent<AudioSource>().clip = dirtSound;
				skidPrefab.GetComponent<TrailRenderer>().material = dirtSkidMat;
				if(wheel.sidewaysFriction.stiffness != roadFrictionSideways.stiffness)
				{
					Debug.Log ("Dirt grip!");
					wheel.sidewaysFriction = roadFrictionSideways;
				}
			}
			else 
			{
				skidPrefab.GetComponent<AudioSource>().clip = defaultSound;
				skidPrefab.GetComponent<TrailRenderer>().material = defaultSkidMat;
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
		Vector3 position;
		Quaternion quat;
		//(out  Vector3 position ,  out  Quaternion quat  );
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