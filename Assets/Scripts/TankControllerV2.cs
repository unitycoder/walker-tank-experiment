using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using TankExtensions;
using IkSolver;
using ValueDriver;

public class TankControllerV2: MonoBehaviour{
	[System.Serializable]
	public struct Part{
		public string name;
		public GameObject obj;
		public HingeJoint hinge;
		public Rigidbody body;
		public DefaultTransform defaultTransform;		

		public Vector3 objWorldPos{
			get => obj.transform.position;
		}

		public Part(GameObject obj_){
			obj = obj_;
			name = obj.name;
			hinge = null;
			body = null;
			defaultTransform = null;
			if (obj){
				hinge = obj.GetComponent<HingeJoint>();
				body = obj.GetComponent<Rigidbody>();
				defaultTransform = obj.GetComponent<DefaultTransform>();
			}
		}
	}

	[System.Serializable]
	public struct Leg{
		public Part hip;
		public Part upper;
		public Part lower;
		public Part tip;
		public bool right;
		public bool front;
	}

	[System.Serializable]
	public class LegControl{
		public float legYaw = 0.0f;
		public float upperLeg = 0.0f;
		public float lowerLeg = 0.0f;
	}

	[System.Serializable]
	public class Parts{
		public Part turret;
		public Part barrel;
		public Part body;

		public Leg legRF;
		public Leg legLF;
		public Leg legRB;
		public Leg legLB;
	}

	public Parts parts = new Parts();

	[System.Serializable]
	public class DirectControl{
		public float turretControlAngle = 0.0f;
		public float barrelControlAngle = 0.0f;
		public LegControl legControlRF = new LegControl();
		public LegControl legControlLF = new LegControl();
		public LegControl legControlRB = new LegControl();
		public LegControl legControlLB = new LegControl();
	}
	public DirectControl directControl = new DirectControl();

	[System.Serializable]
	public class IkControl{
		public Transform legRFTarget = null;
		public Transform legRBTarget = null;
		public Transform legLFTarget = null;
		public Transform legLBTarget = null;
		
		public Transform getLegIkTarget(int legIndex){
			switch(legIndex){
				case(0):
					return legLFTarget;
				case(1):
					return legRFTarget;
				case(2):
					return legLBTarget;
				case(3):
					return legRBTarget;
				default:
					return legLFTarget;
			}
		}
	}
	public IkControl ikControl = new IkControl();


	bool findTankPart(out Part result, string name){
		var obj = gameObject.findObjectWithLowerName(name);
		if (obj){
			result = new Part(obj);
			return true;
		}
		else{
			result = new Part();
			Debug.LogWarning($"Part {name} not found in {gameObject}");
			return false;
		}
	}

	void findTankLeg(out Leg leg, bool front, bool right, string suffix){
		leg = new Leg();
		leg.right = right;
		leg.front = front;

		findTankPart(out leg.hip, $"hip{suffix}");
		findTankPart(out leg.upper, $"upperleg{suffix}");
		findTankPart(out leg.lower, $"lowerleg{suffix}");
		findTankPart(out leg.tip, $"tip{suffix}");
	}

	void drawConnectionLine(Part a, Part b){
		drawConnectionLine(a.obj, b.obj);
	}

	void drawGizmoPoint(Vector3 pos, float size){
		var x = transform.right * size * 0.5f;
		var y = transform.up * size * 0.5f;
		var z = transform.forward * size * 0.5f;
		Gizmos.DrawLine(pos - x, pos + x);
		Gizmos.DrawLine(pos - y, pos + y);
		Gizmos.DrawLine(pos - z, pos + z);
	}

	void drawConnectionLine(GameObject a, GameObject b){
		if (!a || !b)
			return;
		Gizmos.DrawLine(a.transform.position, b.transform.position);
	}

	void drawCenterOfMass(Part part){
		if (!part.body)
			return;
		drawGizmoPoint(part.body.worldCenterOfMass, 0.25f);
	}

	void drawLegGizmo(Leg leg){
		drawConnectionLine(parts.body, leg.hip);
		drawConnectionLine(leg.hip, leg.upper);
		drawConnectionLine(leg.upper, leg.lower);
		drawConnectionLine(leg.lower, leg.tip);

		drawCenterOfMass(leg.hip);
		drawCenterOfMass(leg.upper);
		drawCenterOfMass(leg.lower);
	}

	void addCenterOfMass(ref Vector3 center, ref float mass, Part part){
		if (!part.body)
			return;
		var worldCenter = part.body.worldCenterOfMass;
		var partMass = part.body.mass;
		center += worldCenter * partMass;
		mass += partMass;
	}

	void addCenterOfMass(ref Vector3 center, ref float mass, Leg leg){
		addCenterOfMass(ref center, ref mass, leg.hip);
		addCenterOfMass(ref center, ref mass, leg.upper);
		addCenterOfMass(ref center, ref mass, leg.lower);
	}

	(Vector3, float) getCenterOfMass(){
		Vector3 centerOfMass = Vector3.zero;
		float mass = 0.0f;

		addCenterOfMass(ref centerOfMass, ref mass, parts.turret);
		addCenterOfMass(ref centerOfMass, ref mass, parts.barrel);
		addCenterOfMass(ref centerOfMass, ref mass, parts.body);
		addCenterOfMass(ref centerOfMass, ref mass, parts.legLB);
		addCenterOfMass(ref centerOfMass, ref mass, parts.legLF);
		addCenterOfMass(ref centerOfMass, ref mass, parts.legRB);
		addCenterOfMass(ref centerOfMass, ref mass, parts.legRF);

		if (mass != 0.0f){
			centerOfMass /= mass;
		}

		return (centerOfMass, mass);
	}

	void drawGizmos(Color c){
		var oldColor = Gizmos.color;
		Gizmos.color = c;

		drawLegGizmo(parts.legLF);
		drawLegGizmo(parts.legRF);
		drawLegGizmo(parts.legLB);
		drawLegGizmo(parts.legRB);

		drawConnectionLine(parts.body, parts.turret);
		drawConnectionLine(parts.turret, parts.barrel);
		drawCenterOfMass(parts.body);
		drawCenterOfMass(parts.turret);
		drawCenterOfMass(parts.barrel);

		var cmData = getCenterOfMass();
		var mass = cmData.Item2;
		var centerOfMass = cmData.Item1;

		if (mass != 0.0f){
			drawGizmoPoint(centerOfMass, 1.0f);
			var massVec = Vector3.Project(centerOfMass - transform.position, Vector3.up);
			var groundMass = centerOfMass - massVec; 
			Gizmos.DrawLine(groundMass, centerOfMass);
			//drawGizmoPoint(groundMass, 1.0f);

			var groundPlane = Vector3.up;
			var projectedCenterOfMass = Vector3.ProjectOnPlane(centerOfMass, groundPlane);
			var projectedLF = Vector3.ProjectOnPlane(parts.legLF.tip.obj.transform.position, groundPlane);
			var projectedRF = Vector3.ProjectOnPlane(parts.legRF.tip.obj.transform.position, groundPlane);
			var projectedLB = Vector3.ProjectOnPlane(parts.legLB.tip.obj.transform.position, groundPlane);
			var projectedRB = Vector3.ProjectOnPlane(parts.legRB.tip.obj.transform.position, groundPlane);

			drawGizmoPoint(projectedCenterOfMass, 1.0f);
			Gizmos.DrawLine(projectedLB, projectedRB);
			Gizmos.DrawLine(projectedLF, projectedRF);
			Gizmos.DrawLine(projectedLB, projectedLF);
			Gizmos.DrawLine(projectedRB, projectedRF);
			Gizmos.DrawLine(projectedLB, projectedRF);
			Gizmos.DrawLine(projectedLF, projectedRB);
		}

		Gizmos.color = oldColor;
	}

	void OnDrawGizmos(){
		drawGizmos(Color.yellow);
	}

	void OnDrawSelected(){
		drawGizmos(Color.white);
	}

	Coroutine controlCoroutineObject = null;

	void Start(){
		findTankPart(out parts.turret, "turret");
		findTankPart(out parts.barrel, "barrels");
		//if (!findTankPart(out body, "body"))
		parts.body = new Part(gameObject);

		findTankLeg(out parts.legRF, true, true, "rf");
		findTankLeg(out parts.legLF, true, false, "lf");
		findTankLeg(out parts.legRB, false, true, "rb");
		findTankLeg(out parts.legLB, false, false, "lb");

		if (ikControl.legRFTarget && parts.legRF.tip.obj)
			ikControl.legRFTarget.transform.position = parts.legRF.tip.obj.transform.position;

		if (ikControl.legRBTarget && parts.legRB.tip.obj)
			ikControl.legRBTarget.transform.position = parts.legRB.tip.obj.transform.position;

		if (ikControl.legLFTarget && parts.legLF.tip.obj)
			ikControl.legLFTarget.transform.position = parts.legLF.tip.obj.transform.position;

		if (ikControl.legLBTarget && parts.legLB.tip.obj)
			ikControl.legLBTarget.transform.position = parts.legLB.tip.obj.transform.position;

		controlCoroutineObject = StartCoroutine(controlCoroutine());
	}

	void applyHingeAngle(HingeJoint hinge, float angle){
		if (!hinge)
			return;
		var spring = hinge.spring;
		spring.targetPosition = angle;
		hinge.spring = spring;
	}

	void applyHingeAngle(Part part, float angle){
		applyHingeAngle(part.hinge, angle);
	}

	void applyLegControl(Leg leg, LegControl legControl){
		applyHingeAngle(leg.hip, legControl.legYaw);
		applyHingeAngle(leg.upper, legControl.upperLeg);
		applyHingeAngle(leg.lower, legControl.lowerLeg);
	}


	void applyControl(){
		applyHingeAngle(parts.turret, directControl.turretControlAngle);
		applyHingeAngle(parts.barrel, directControl.barrelControlAngle);
		applyLegControl(parts.legRF, directControl.legControlRF);
		applyLegControl(parts.legLF, directControl.legControlLF);
		applyLegControl(parts.legRB, directControl.legControlRB);
		applyLegControl(parts.legLB, directControl.legControlLB);
	}	

	void solveLegKinematics(Leg leg, LegControl legControl, Vector3 worldTargetPos){
		if (!parts.body.obj || !leg.hip.obj || !leg.upper.obj || !leg.lower.obj || !leg.tip.obj)
			return;

		var origBodyCoord = Coord.fromTransform(parts.body.defaultTransform);
		var worldBodyCoord = Coord.fromTransform(parts.body.obj.transform, false);
		var hipCoord = Coord.fromTransform(leg.hip.defaultTransform);
		var upperCoord = Coord.fromTransform(leg.upper.defaultTransform);
		var lowerCoord = Coord.fromTransform(leg.lower.defaultTransform);
		var tipCoord = Coord.fromTransform(leg.tip.defaultTransform);

		hipCoord = origBodyCoord.inverseTransformCoord(hipCoord);
		upperCoord = origBodyCoord.inverseTransformCoord(upperCoord);
		lowerCoord = origBodyCoord.inverseTransformCoord(lowerCoord);
		tipCoord = origBodyCoord.inverseTransformCoord(tipCoord);

		var hipNode = new IkNode(hipCoord, leg.hip.hinge);
		var upperNode = new IkNode(upperCoord, leg.upper.hinge);
		var lowerNode = new IkNode(lowerCoord, leg.lower.hinge);
		var tipNode = new IkNode(tipCoord, leg.tip.hinge);

		var nodes = new List<IkNode>();
		nodes.Add(hipNode);
		nodes.Add(upperNode);
		nodes.Add(lowerNode);
		nodes.Add(tipNode);

		for(int i = nodes.Count-1; i > 0; i--){
			nodes[i].moveToParentSpace(nodes[i-1], true);
		}

		var targetPos = worldBodyCoord.inverseTransformPoint(worldTargetPos);
		Solver.solveIkChain(nodes, targetPos, true);

		//updateIkChain(nodes, 0, false, false);
		var debugCoord = worldBodyCoord;
		for(int i = 0; (i + 1) < nodes.Count; i++){
			var node = nodes[i];
			var nextNode = nodes[i+1];
			Debug.DrawLine(
				debugCoord.transformPoint(node.objWorld.pos), 
				debugCoord.transformPoint(nextNode.objWorld.pos)
			);
		}
		/*
		if (nodes.Count > 0){
			Debug.DrawLine(debugCoord.transformPoint(nodes[0].objWorld.pos), debugCoord.transformPoint(targetPos));
			Debug.DrawLine(debugCoord.transformPoint(nodes[nodes.Count-1].objWorld.pos), debugCoord.transformPoint(targetPos));
		}
		*/

		legControl.legYaw = hipNode.jointState.xRotDeg;
		legControl.upperLeg = upperNode.jointState.xRotDeg;
		legControl.lowerLeg = lowerNode.jointState.xRotDeg;
	}

	void solveKinematics(){
		if (ikControl.legRFTarget && ikControl.legRFTarget.gameObject.activeInHierarchy)
			solveLegKinematics(parts.legRF, directControl.legControlRF, ikControl.legRFTarget.position);
		if (ikControl.legRBTarget && ikControl.legRBTarget.gameObject.activeInHierarchy)
			solveLegKinematics(parts.legRB, directControl.legControlRB, ikControl.legRBTarget.position);
		if (ikControl.legLFTarget && ikControl.legLFTarget.gameObject.activeInHierarchy)
			solveLegKinematics(parts.legLF, directControl.legControlLF, ikControl.legLFTarget.position);
		if (ikControl.legLBTarget && ikControl.legLBTarget.gameObject.activeInHierarchy)
			solveLegKinematics(parts.legLB, directControl.legControlLB, ikControl.legLBTarget.position);
	}

	void setRelLegIk(int legIndex, float right, float forward, float height){
		var target = ikControl.getLegIkTarget(legIndex);
		var rightVec = parts.body.obj.transform.right;
		var forwardVec = parts.body.obj.transform.forward;
		var upVec = parts.body.obj.transform.up;
		var pos = parts.body.objWorldPos;

		target.transform.position = pos 
			+ right * rightVec + forward * forwardVec 
			+ upVec * height;
	}

	void setRelLegIk(int legIndex, Vector3 coord){
		setRelLegIk(legIndex, coord.x, coord.z, coord.y);
	}

	void setRelLegIk(Vector3 lf, Vector3 rf, Vector3 lb, Vector3 rb){
		setRelLegIk(0, lf);
		setRelLegIk(1, rf);
		setRelLegIk(2, lb);
		setRelLegIk(3, rb);
	}

	void setRelLegIk(LegRelIk relIk){
		setRelLegIk(relIk.lf, relIk.rf, relIk.lb, relIk.rb);
	}

	Vector3 invRelTransformNoScale(Transform t, Vector3 p){
		var diff = p - t.position;
		return new Vector3(
			Vector3.Dot(diff, t.right),
			Vector3.Dot(diff, t.up),
			Vector3.Dot(diff, t.forward)
		);
	}

	LegRelIk getLegRelIk(bool fromTargets){
		var result = new LegRelIk();
		result.rf = fromTargets ? ikControl.legRFTarget.position: parts.legRF.tip.objWorldPos;
		result.lf = fromTargets ? ikControl.legLFTarget.position: parts.legLF.tip.objWorldPos;
		result.rb = fromTargets ? ikControl.legRBTarget.position: parts.legRB.tip.objWorldPos;
		result.lb = fromTargets ? ikControl.legLBTarget.position: parts.legLB.tip.objWorldPos;

		var bodyT = parts.body.obj.transform;
		result.rf = invRelTransformNoScale(bodyT, result.rf);
		result.rb = invRelTransformNoScale(bodyT, result.rb);
		result.lf = invRelTransformNoScale(bodyT, result.lf);
		result.lb = invRelTransformNoScale(bodyT, result.lb);

		return result;
	}

	float sawFunc(float t){
		t = Mathf.Repeat(t, 1.0f);
		float t0 = 0.25f;
		if (t <= t0)
			return Mathf.Lerp(0.0f, 1.0f, Mathf.Clamp01(t/t0));
		return Mathf.Lerp(1.0f, 0.0f, Mathf.Clamp01((t - t0)/(1.0f - t0)));
	}

	void combineIks(LegRelIk result, LinkedList<LegRelIk> iks, LegRelIk untilIk){
		result.setVec(Vector3.zero);
		bool first = true;
		foreach(var cur in iks){
			if (cur == untilIk)
				break;
			if (first){
				result.assign(cur);
				first = false;
			}
			else
				result.addIk(cur);
		}
	}

	[System.Serializable]
	public class GaitGenerator{
		public int numSectors{
			get => 4;
		}
		public float timer = 0.0f;
		public float period = 4.0f;
		public float relT = 0.0f;
		public float relSectorT = 0.0f;
		public float angleDeg = 0.0f;
		public float angleRad = 0.0f;
		public int currentSector = 0;
		public float[] raisePulses = new float[0];
		public float[] lerpPulses = new float[0];
		public float circleX = 0.0f;
		public float circleY = 0.0f;

		public float saw(float t, int numSectors){
			var sectorDur = 1.0f / (float)numSectors;
			t = Mathf.Repeat(t, 1.0f);
			if (t < sectorDur)
				return Mathf.Lerp(-1.0f, 0.0f, t/sectorDur);
			return Mathf.Lerp(0.0f, 1.0f, (t - sectorDur)/(1.0f - sectorDur));
		}

		public void update(){
			if ((raisePulses?.Length ?? 0) != numSectors)
				raisePulses = new float[numSectors];
			if ((lerpPulses?.Length ?? 0) != numSectors)
				lerpPulses = new float[numSectors];

			float sectorDur = 1/(float)numSectors;

			timer += Time.deltaTime;
			timer = Mathf.Repeat(timer, period);
			relT = timer/period;

			angleDeg = Mathf.Lerp(0.0f, 360.0f, relT);
			angleRad = angleDeg * Mathf.Deg2Rad;
			circleX = Mathf.Cos(angleRad);
			circleY = Mathf.Sin(angleRad);

			var pulseAngle = angleRad * 0.5f * (float)numSectors;
			var pulseValue = Mathf.Abs(Mathf.Sin(pulseAngle));
			
			relSectorT = Mathf.Repeat(relT/sectorDur, 1.0f);
			currentSector = Mathf.Clamp(Mathf.FloorToInt(relT/sectorDur), 0, numSectors - 1);

			for(int sectorIndex = 0; sectorIndex < numSectors; sectorIndex++){
				raisePulses[sectorIndex] = (sectorIndex == currentSector) ? pulseValue : 0.0f;
				lerpPulses[sectorIndex] = saw(relT - sectorDur * (float)sectorIndex, numSectors);
			}
		}
	}

	public GaitGenerator gaitGenerator = new GaitGenerator();

	IEnumerator controlCoroutine(){
		var legIk = getLegRelIk(false);

		var sideOffset = MathTools.seq(legIk.rf, legIk.rb, legIk.lf, legIk.lb).Select(v => Mathf.Abs(v.x)).Average();
		var forwardOffset = MathTools.seq(legIk.rf, legIk.lf).Select(v => v.z).Average();
		var backOffset = MathTools.seq(legIk.rb, legIk.lb).Select(v => v.z).Average();
		var height = MathTools.seq(legIk.rf, legIk.rb, legIk.lf, legIk.lb).Select(v => v.y).Average();

		legIk = new LegRelIk(sideOffset, forwardOffset, backOffset, height);
		LinkedList<LegRelIk> iks = new LinkedList<LegRelIk>();

		var baseIk = legIk;
		iks.AddLast(baseIk);

		var heightIk = new LegRelIk();
		iks.AddLast(heightIk);

		var extendIk = new LegRelIk();
		iks.AddLast(extendIk);

		var circleIk = new LegRelIk();
		iks.AddLast(circleIk);

		var legRaiseIk = new LegRelIk();
		iks.AddLast(legRaiseIk);

		var legMoveIk = new LegRelIk();
		iks.AddLast(legMoveIk);

		setRelLegIk(legIk);

		var combinedIk = new LegRelIk();
		System.Action updRelIk = () => {
			combineIks(combinedIk, iks, null);
			setRelLegIk(combinedIk);
		};

		yield return new WaitForSeconds(1.0f);

		/*
		yield return heightIk.shiftY(2.0f, 1.0f, updRelIk);

		float extX = 0.0f;
		float extY = 0.0f;
		float extZ = 3.0f;
		extendIk.addVec(0, -extX, extY, extZ);
		extendIk.addVec(1, +extX, extY, extZ);
		extendIk.addVec(2, -extX, extY, -extZ);
		extendIk.addVec(3, +extX, extY, -extZ);
		extendIk.weight = 0.0f;
		yield return extendIk.driveWeight(0.0f, 1.0f, 2.0f, updRelIk);
		*/

		yield return heightIk.shiftY(-5.0f, 2.0f, updRelIk);

		circleIk.weight = 0.0f;

		int[] quadrantToLeg = new int[]{
			1, 0, 2, 3
		};

		float rx = 1.0f;
		float rz = 1.0f;

		float stepHeight = 3.0f;

		circleIk.weight = 0.0f;
		legRaiseIk.weight = 0.0f;

		int lastQuadrant = -1;

		Vector3[] legPrev = new Vector3[gaitGenerator.numSectors];
		Vector3[] legNext = new Vector3[gaitGenerator.numSectors];
		bool[] legNextFlags = new bool[gaitGenerator.numSectors];
		float[] lastSawValue = new float[gaitGenerator.numSectors];
		for(int i = 0; i < gaitGenerator.numSectors; i++){
			lastSawValue[i] = 0.0f;
			legPrev[i] = legNext[i] = Vector3.zero;
			legNextFlags[i] = false;
		}

		float stepVal = 3.0f;
		float extValX = 2.0f;
		float extValZ = 2.0f;
		circleIk.weight = 0.0f;
		legRaiseIk.weight = 0.0f;
		while(true){
			circleIk.setVec(gaitGenerator.circleX * rx, 0.0f, gaitGenerator.circleY * rz);
			for(int secIndex = 0; secIndex < gaitGenerator.numSectors; secIndex++){
				int legIndex = quadrantToLeg[secIndex];
				legRaiseIk.setVec(legIndex, 0.0f, gaitGenerator.raisePulses[secIndex] * stepHeight, 0.0f);
			}
			if (circleIk.weight < 1.0f){
				circleIk.weight = Mathf.Clamp01(circleIk.weight + Time.deltaTime * 0.5f);
				updRelIk();
				yield return null;
				continue;
			}
			if (legRaiseIk.weight < 1.0f){
				legRaiseIk.weight = Mathf.Clamp01(legRaiseIk.weight + Time.deltaTime * 0.5f);
				updRelIk();
				yield return null;
				continue;
			}

			for(int secIndex = 0; secIndex < gaitGenerator.numSectors; secIndex++){
				int legIndex = quadrantToLeg[secIndex];
				//legRaiseIk.setVec(legIndex, 0.0f, gaitGenerator.raisePulses[secIndex] * stepHeight, 0.0f);
				//legRaiseIk.setVec(legIndex, 0.0f, gaitGenerator.lerpPulses[secIndex] * stepHeight, 0.0f);
				var sawValue = gaitGenerator.lerpPulses[secIndex];

				bool leftLeg = ((legIndex & 0x1) == 0);
				bool frontLeg= legIndex < 2;

				var legBaseCoord = new Vector3(
					(leftLeg ? -extValX: extValX),
					0.0f, 
					(frontLeg ? extValZ: -extValZ)
				);

				if ((lastSawValue[legIndex] < 0.0f) && (sawValue >= 0.0f)){
					if (!legNextFlags[legIndex])
						legPrev[legIndex] = legNext[legIndex];
					else{
						legPrev[legIndex] = legBaseCoord - Vector3.forward * stepVal;
					}
				}

				if ((lastSawValue[legIndex] > 0.0f) && (sawValue <= 0.0f)){
					legNext[legIndex] = legBaseCoord + Vector3.forward * stepVal;
					legNextFlags[legIndex] = true;
				}

				var lerpFactor = Mathf.Repeat(sawValue + 1.0f, 1.0f);
				var legValue = (sawValue < 0.0f) ?  
					Vector3.Lerp(legPrev[legIndex], legNext[legIndex], lerpFactor):
					Vector3.Lerp(legNext[legIndex], legPrev[legIndex], lerpFactor);
				
				legMoveIk.setVec(legIndex, legValue);

				lastSawValue[legIndex] = sawValue;
			}
			if (lastQuadrant != gaitGenerator.currentSector){
				lastQuadrant = gaitGenerator.currentSector;
			}

			updRelIk();
			yield return null;
		}
	}

	void Update(){
		gaitGenerator.update();
		solveKinematics();
		applyControl();		
	}
}
