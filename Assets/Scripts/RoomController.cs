﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class RoomController : MonoBehaviour
{
    static int lastId = 0;
    public int id = 0;
    public List<GameObject> gates = new List<GameObject>();
    public List<Transform> centerPoints = new List<Transform>();
    public List<GameObject> lights = new List<GameObject>();
    [SerializeField] bool commitChanges = false;
    //[SerializeField] Vector2Int position = new Vector2Int(0, 0);
    LevelController controller;
    public bool firstSpawn = true;

    [CustomEditor(typeof(RoomController))]
    public class ObjectBuilderEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            RoomController myScript = (RoomController)target;
            if (GUILayout.Button("Update Values"))
            {
                myScript.updateValues();
                myScript.commitChanges = false;
            }

            if (GUILayout.Button("Adjust Gates"))
            {
                myScript.adjustGates();
                myScript.commitChanges = false;
            }

            if (GUILayout.Button("Adjust Lights"))
            {
                myScript.adjustLightsInit();
                myScript.commitChanges = false;
            }

            if (GUILayout.Button("TurnOnLights"))
            {
                myScript.turnOnLights();
                myScript.commitChanges = false;
            }

            if (GUILayout.Button("TurnOffLights"))
            {
                myScript.turnOffLights();
                myScript.commitChanges = false;
            }
        }
    }

    void updateValues()
    {
        gates.Clear();
        centerPoints.Clear();

        Transform perRoom = transform.Find("PerRoom");
        foreach(Transform tr in perRoom)
        {
            if (tr.tag.Equals("gate"))
            {
                gates.Add(tr.gameObject);
            }
            if (tr.tag.Equals("centerPoint"))
            {
                centerPoints.Add(tr);
            }
        }
    }

    void adjustGates()
    {
        foreach (GameObject gateObj in gates)
        {   
            if (gateObj.transform.tag.Equals("gate"))
            {   
                foreach (Transform cp in centerPoints)
                {
                    Vector3 dir = gateObj.transform.position - cp.position;
                    if (dir.magnitude < 1.1f * LevelController.baseRoomeSize/2)
                    {
                        if(dir.magnitude != LevelController.baseRoomeSize/2 || dir.x*dir.y != 0)
                        {
                            Debug.LogError("ERROR, puerta (" + gateObj.transform.name + ") o centerPoint (" + cp.name + ") mal posicionado");
                            goto innerLoop;
                        }
                        else
                        {
                            dir.Normalize();
                            gateObj.GetComponent<GateController>().setDirection(new Vector2Int(Mathf.RoundToInt(dir.x), Mathf.RoundToInt(dir.z)));
                        }
                    }
                }
            }
            innerLoop:;
        }
    }

    void adjustLights(Transform t)
    {
        if (t.tag.Equals("lightComponent"))
        {
            lights.Add(t.gameObject);
        }
        if(t.childCount > 0)
        {
            foreach (Transform child in t)
            {
                adjustLights(child);
            }
        }
    }

    void adjustLightsInit()
    {
        lights.Clear();
        Transform t = transform;
        adjustLights(t);
    }

    //start
    private void Start()
    {
        controller = transform.parent.gameObject.GetComponent<LevelController>();
        controller.roomArray[id] = this.gameObject;

        prepareGates();

        if (!controller.lightOptimiation)
        {
            turnOnLights();
        }
    }

    //turn lights on
    public void levelInitialize()
    {
        turnOnLights();
    }

    //add gates to room generation queue
    void prepareGates()
    {
        shuffleGates();
        for (int i = 0; i < gates.Count; i++)
        {
            controller.addRoomToSpawn(new Vector2Int(id, i));
        }
    }

    //add only gates that havent been yet connected to generation queue
    public void prepareFreeGates()
    {
        for (int i = 0; i < gates.Count; i++)
        {
            if (!gates[i].GetComponent<GateController>().isGate)
                controller.addRoomToSpawn(new Vector2Int(id, i));
        }
    }

    //randomize gates list
    void shuffleGates()
    {
        for(int i=0; i<gates.Count; i++)
        {
            GameObject aux = gates[i];
            int randPos = controller.randomInt(0, gates.Count);
            gates[i] = gates[randPos];
            gates[randPos] = aux;
        }
    }

    public void trySpawnRoom(int gateCounter)
    {
        GateController gate = gates[gateCounter].GetComponent<GateController>();
        Vector2Int dir = gate.getDirection();
       
        if (gate.spawnAlways || firstSpawn || controller.randomValue() <= controller.levelShape)
        {
            GameObject roomPrefab = controller.rooms[controller.randomInt(0, controller.rooms.Count)];
            spawnRoom(gate, dir, roomPrefab, gate.spawnAlways);
        }
    }

    //create room, after adjusting position and rotation, if it doesnt fit, eliminate it
    void spawnRoom(GateController gate, Vector2Int dir, GameObject roomPrefab, bool tryAgain)
    {

        RoomController roomPrefabController = roomPrefab.GetComponent<RoomController>();

        int nextEntranceGate = controller.randomInt(0, roomPrefabController.gates.Count);

        var roomInstance = Instantiate(roomPrefab,
                                  Vector3.zero,
                                  /*Quaternion.Euler(0, nextRoomAngle, 0), */
                                  Quaternion.identity,
                                  transform.parent);

        RoomController roomInstanceController = roomInstance.GetComponent<RoomController>();

        int nextRoomAngle = uVecAngle(-dir, roomInstanceController.gates[nextEntranceGate].GetComponent<GateController>().getDirection());
        roomInstanceController.adjustRotation(nextRoomAngle);

        //adjusting physical position
        Vector3 newPos = gate.transform.position + (roomInstance.transform.position - roomInstanceController.gates[nextEntranceGate].transform.position);
        roomInstance.transform.position = newPos;
        //if (roomInstanceController.checkAvailableSpace(controller))
        if (roomInstanceController.checkAvailableSpace(controller))
        {
            roomInstanceController.ocupyAvailableSpace(controller);
            firstSpawn = false;
            gate.initializeGate();
            roomInstanceController.adjustId();
            controller.lastRoom = roomInstance;
            controller.currentRoomAmount++;
            controller.createEdge(id, roomInstanceController.id, 1);         
            roomInstanceController.gates[nextEntranceGate].GetComponent<GateController>().initializeGate();
            //print("Room: " + id + " (dir="+dir+") --> Room: " + roomAux.GetComponent<RoomController>().id+ " (dir=" + roomAux.GetComponent<RoomController>().gates[nextEntranceGate].GetComponent<GateController>().getDirection() + ")");
        }
        else
        {
            Destroy(roomInstance);
            if (tryAgain)
            {
                spawnRoom(gate, dir, controller.basicConector, false);
            }
        }
    }

    //aux methods
    public void adjustId()
    {
        id = RoomController.lastId;
        RoomController.lastId++;
    }

    public void adjustRotation(int angle)
    {
        transform.Rotate(0, angle, 0);
        foreach(GameObject gateToAdjust in gates)
        {
            gateToAdjust.GetComponent<GateController>().adjustDirection(angle);
        }
    }

    public int getRandGate()
    {
        return (controller.randomInt(0, gates.Count));
    }

    int uVecAngle(Vector2Int a, Vector2Int b)
    {
        Vector2 a_aux= new Vector2(a.x, a.y);
        Vector2 b_aux = new Vector2(b.x, b.y);
        return Mathf.RoundToInt(Vector2.SignedAngle(a_aux, b_aux));
    }

    bool previousCheck(Vector2Int pos, LevelController controllerAux)
    {
        return !controllerAux.ocupiedSpaces.ContainsKey(pos);
    }

    bool checkAvailableSpace(LevelController controllerAux)
    {
        if(centerPoints.Count < 2)
        {
            return !controllerAux.ocupiedSpaces.ContainsKey(new Vector2Int(Mathf.RoundToInt(transform.position.x/LevelController.baseRoomeSize), Mathf.RoundToInt(transform.position.z / LevelController.baseRoomeSize)));
        }
        else
        {
            foreach (Transform centerP in centerPoints)
            {
                if (controllerAux.ocupiedSpaces.ContainsKey(new Vector2Int(Mathf.RoundToInt(centerP.transform.position.x / LevelController.baseRoomeSize), Mathf.RoundToInt(centerP.transform.position.z / LevelController.baseRoomeSize))))
                {
                    return false;
                }
            }
            return true;
        }
    }

    void ocupyAvailableSpace(LevelController controllerAux)
    {
         foreach (Transform centerP in centerPoints)
        {
            controllerAux.ocupiedSpaces.Add(new Vector2Int(Mathf.RoundToInt(centerP.transform.position.x / LevelController.baseRoomeSize), Mathf.RoundToInt(centerP.transform.position.z / LevelController.baseRoomeSize)), RoomController.lastId);
        }
    }

    public List<Transform> getCenterPos()
    {
        return centerPoints;
    }

    void printPoints()
    {
        foreach (Transform centerP in centerPoints)
        {
            print(centerP.position);
        }
        print("______________________________________");
    }

    //secondary generation, used for aditional interconectivity and creating walls in all non connected gates
    public void secondGeneration()
    {
        foreach(GameObject gate in gates)
        {
            GateController gateController = gate.GetComponent<GateController>();
            Vector2Int dirr = gateController.getDirection();
            Vector2Int key = new Vector2Int(Mathf.RoundToInt(gate.transform.position.x/LevelController.baseRoomeSize + ((float)dirr.x/2)), Mathf.RoundToInt(gate.transform.position.z / LevelController.baseRoomeSize + ((float)dirr.y / 2)));
            if (controller.ocupiedSpaces.ContainsKey(key))
            {
                int neighborRoomId = -123123;
                bool tryGet = controller.ocupiedSpaces.TryGetValue(key, out neighborRoomId);
                if(tryGet && neighborRoomId != -123123)
                {
                    if (!gateController.isGate)
                    {
                        GameObject conectingGate = controller.roomArray[neighborRoomId].GetComponent<RoomController>().findGateInRoom(gate.transform.position, 1);
                        if (conectingGate != null && controller.randomValue() < controller.interconectivity)
                        {
                            controller.createEdge(id, neighborRoomId, 2);            //DEBUG
                            gateController.initializeGate();
                            conectingGate.GetComponent<GateController>().initializeGate();
                        }
                    }
                }
                else
                {
                    print("something went wrong with tryGet of dictionary");
                }
            }
        }

        foreach(GameObject gate in gates)
        {
            GateController gateController2 = gate.GetComponent<GateController>();
            if (!gateController2.isGate)
            {
                gateController2.initializeWall();
            }
        }
    }

    GameObject findGateInRoom(Vector3 pos, float tolerance)
    {
        foreach (GameObject gate in gates)
        {
            if (Vector3.Distance(gate.transform.position, pos) < tolerance)
            {
                return gate;
            }
        }
        return null;
    }

    //aux methods called from PlayerTracker

    public void enteredRoom()
    {
        if (controller.lightOptimiation)
            turnOnLights();
    }
    public void exitedRoom()
    {
        if (controller.lightOptimiation)
            turnOffLights();
    }

    void turnOnLights()
    {
        //lightParent.gameObject.SetActive(true);
        foreach (GameObject light in lights)
        {
            light.SetActive(true);
        }
    }

    void turnOffLights()
    {
        foreach (GameObject light in lights)
        {
            light.SetActive(false);
        }
    }

    public void turnOnCloseLights(Vector3 playerPos)
    {
        foreach (GameObject light in lights)
        {
            if(Vector3.Distance(light.transform.position, playerPos) <= controller.lightUpDistance)
                light.SetActive(true);
            else
                light.SetActive(false);
        }
    }
    
}
