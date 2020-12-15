﻿using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class LevelController : MonoBehaviour
{
    [Header("Level Settings")]
    [Tooltip("Seed for random generation")] public int roomSeed = 123;
    int startPlayerId = 0;
    Vector3 startPlayerPos = new Vector3(0, 1.28f, 0);
    [Tooltip("Amount of rooms to be generated")] public int roomAmount = 20;
    [HideInInspector] public int currentRoomAmount = 0;
    [Tooltip("Shape of level: 0-> Corridor shaped level, each room generates a single room, 1-> Square shaped level, each room generates various rooms")] [Range(0, 1)] public float levelShape = 0.5f;
    [Tooltip("Aditional level interconectivity")] [Range(0, 1)] public float interconectivity = 0.2f;

    [Header("Room Settings")]
    [Tooltip("Minimal size of room, used as base for bigger rooms")] public static int baseRoomeSize = 20;
    [Tooltip("Initial room, player spawns in this rooms at posiiton (0,0,0)")] public GameObject initRoom;
    [Tooltip("Basic conector room, used when a room random room doesnt fit when generated by a door that must always generate something")] public GameObject basicConector;
    [Tooltip("Empty room prefabs to create in level")] public List<GameObject> roomsEmpty = new List<GameObject>();
    [Tooltip("Enemy room prefabs to create in level")] public List<GameObject> roomsEnemy = new List<GameObject>();
    [Tooltip("Healing room prefabs to create in level")] public List<GameObject> roomsHealing = new List<GameObject>();
    [Tooltip("Trap room prefabs to create in level")] public List<GameObject> roomsTrap = new List<GameObject>();
    [Tooltip("Fragment room prefabs to create in level")] public List<GameObject> roomsFragment = new List<GameObject>();
    [Tooltip("Boss room prefabs to create in level")] public List<GameObject> roomsBoss = new List<GameObject>();
    [HideInInspector] public GameObject lastRoom;
    //[HideInInspector] public HashSet<Vector2Int> ocupiedSpaces = new HashSet<Vector2Int>(new spaceComparer());
    [HideInInspector] public Dictionary<Vector2Int, int> ocupiedSpaces = new Dictionary<Vector2Int, int>(new spaceComparer());
    [HideInInspector] public GameObject[] roomArray;
    [HideInInspector] public int[,] roomMatrix;            //DEBUG

    public Queue<Vector2Int> roomsToSpawn = new Queue<Vector2Int>();
    float[] randValues;
    int randCounter = 0;

    int trySpawn = 0;
    bool iterationCompleted = false;

    [Header("Optimization Settings")]
    [Tooltip("Only show room that player is in")] public bool optimization = false;
    [Tooltip("Only show room that player is in + its neighbors")] public bool showExtraRooms = false;
    [Tooltip("Only turn lights in room that player is in")] public bool lightOptimiation = false;
    [Tooltip("Only turn lights that are a certain distance from player")] public bool lightDistanceOptimization = false;
    [Tooltip("Distance for Light Distance Optimization")] public float lightUpDistance = 10f;

    [Header("Debug Settings")]
    [Tooltip("Show room connections")] public bool debug = false;
    [SerializeField] float nodeSize = 3f;
    Color startColor = Color.black;
    Color emptyColor = Color.white;
    Color enemyColor = Color.red;
    Color trapColor = Color.magenta;
    Color fragmentColor = Color.cyan;
    Color bossColor = Color.yellow;
    Color healingColor = Color.green;
    Color baseLineColor = Color.white;
    Color interconectedLineColor = Color.blue;
    [SerializeField] float lineWidth = 5;
    [SerializeField] float debugOffset = 5f;

    GameController gameController;

    class spaceComparer : IEqualityComparer<Vector2Int> { 
        public bool Equals(Vector2Int a, Vector2Int b)
        {
            //print("Comparing: (" + a.x + "," + b.x + ") with (" + a.y + "," + b.y + ")");
            return ((a.x == b.x) && (a.y == b.y));
        }
        public int GetHashCode(Vector2Int vec)
        {
            return vec.GetHashCode();
        }
    }

    //Initialize all values with a leveinfowrapper
    public void initAllLevelValues(GameController.LevelInfoWrapper lvlInfo)
    {
        roomSeed = lvlInfo.levelSeed;
        if (roomSeed != -1)
            Random.InitState(roomSeed);

        initRandValues();

        roomAmount = lvlInfo.levelRoomsAmount;
        startPlayerId = lvlInfo.playerRoomId;
        startPlayerPos = lvlInfo.playerPos;
    }

    //custom methods for random values
    public float randomValue()
    {
        randCounter = (randCounter + 1) % randValues.Length;
        return randValues[randCounter];
    }
    public float randomFloat(float min, float max)
    {
        randCounter = (randCounter+1)% randValues.Length;
        return min + (randValues[randCounter] * (max - min));
    }
    public int randomInt(int min, int max)
    {
        randCounter = (randCounter + 1) % randValues.Length;
        return (int)(min + (randValues[randCounter] * (max - min)));
    }

    void initRandValues()
    {
        randValues = new float[roomAmount * 6];
        for (int i = 0; i < randValues.Length; i++)
        {
            randValues[i] = Random.value;
        }
    }

    private void Start()
    {
        gameController = GameObject.Find("GameController").GetComponent<GameController>();
        gameController.readyForInitialization(this); 
    }

    //start generating the level
    public void startLevelGeneration()
    {
        print("level controler start");

        //array with all generated room instances
        roomArray = new GameObject[roomAmount];

        //adjecency matrix with room connections
        roomMatrix = new int[roomAmount, roomAmount];
        for (int i = 0; i < roomAmount; i++)
        {
            for (int j = 0; j < roomAmount; j++)
            {
                roomMatrix[i, j] = 0;
            }
        }


        //initialize first room
        foreach (Transform centerP in initRoom.GetComponent<RoomController>().getCenterPos())
        {
            ocupiedSpaces.Add(new Vector2Int(Mathf.RoundToInt(centerP.transform.position.x / LevelController.baseRoomeSize), Mathf.RoundToInt(centerP.transform.position.z / LevelController.baseRoomeSize)), 0);
        }

        currentRoomAmount++;

        var roomAux = Instantiate(initRoom, transform.position, Quaternion.identity, transform);
        roomAux.GetComponent<RoomController>().adjustId();
        roomAux.GetComponent<RoomController>().levelInitialize();

        this.enabled = true;
    }

    //constantly spawn rooms if there are rooms to spawn
    void FixedUpdate()
    {
        if (roomsToSpawn.Count != 0)
        {
            if (currentRoomAmount < roomAmount)
            {
                Vector2Int auxRoomSpawner = roomsToSpawn.Dequeue();
                roomArray[auxRoomSpawner.x].GetComponent<RoomController>().trySpawnRoom(auxRoomSpawner.y);
            }
            else
            {
                roomsToSpawn.Clear();
            }
            trySpawn = 0;
        }
        else if (!iterationCompleted)
        {
            trySpawn++;
            if (trySpawn > 20)
            {
                iterationCompleted = true;
                endFirstGenerationWave();
            }
        }
    }

    //add room to queue to be spawned next fixedUpdate()
    public void addRoomToSpawn(Vector2Int gateIdToAdd)
    {
        roomsToSpawn.Enqueue(gateIdToAdd);
    }

    //called when all rooms are generated
    public void endFirstGenerationWave()
    {
        if(currentRoomAmount == roomAmount)
        {
            print("Succesfull generation");
            foreach (GameObject room in roomArray)
            {
                room.GetComponent<RoomController>().secondGeneration();
            }
            levelFinished();
        }
        //if desired room amount isnt achieved, level generation is tried again
        else if (currentRoomAmount < roomAmount)
        {
            print("Generation self trapped!, atempting another iteration");
            levelShape = 1;
            for (int i=0; i<currentRoomAmount; i++)
            {
                roomArray[i].GetComponent<RoomController>().prepareFreeGates();
            }
            iterationCompleted = false;
        }
        else
        {
            Debug.LogError("Something went really wrong, major ERROR!");
        }
    }

    //called when rooms are generated and desired room amount is achieved
    void levelFinished()
    {   
        //if optimization is enabled most rooms get disabled
        if (optimization)
        {
            for(int i=0; i< roomArray.Length; i++)
            {
                roomArray[i].SetActive(false);
            }
            roomArray[startPlayerId].SetActive(true);
        }

        RoomController.resetLastId();
        gameController.initializePlayerWhenReady(startPlayerPos, startPlayerId, startPlayerId);

        randValues = null;
        //DEBUG -> LIBERAR MEMORIA DE RESTO DE ESTRUCTURAS
    }

    //create edge in adjecency matrix
    public void createEdge(int idA, int idB, int value)
    {
        roomMatrix[idA, idB] = value;
        roomMatrix[idB, idA] = value;
    }

    //debug method
    private void OnDrawGizmos()
    {
        if (debug && Application.isPlaying && roomMatrix != null)
        {
            for(int i=0; i<roomAmount; i++)
            {
                for(int j=i+1; j<roomAmount; j++)
                {
                    if (roomMatrix[i, j] == 1) {

                        Vector3 aux_i = new Vector3(roomArray[i].transform.position.x, roomArray[i].transform.position.y + debugOffset + nodeSize / 2, roomArray[i].transform.position.z);
                        Vector3 aux_j = new Vector3(roomArray[j].transform.position.x, roomArray[j].transform.position.y + debugOffset + nodeSize / 2, roomArray[j].transform.position.z);
                        Handles.DrawBezier(aux_i, aux_j, aux_i, aux_j, baseLineColor, null, lineWidth);
                    }
                    else if (roomMatrix[i, j] == 2)
                    {
                        Vector3 aux_i = new Vector3(roomArray[i].transform.position.x, roomArray[i].transform.position.y + debugOffset + nodeSize / 2, roomArray[i].transform.position.z);
                        Vector3 aux_j = new Vector3(roomArray[j].transform.position.x, roomArray[j].transform.position.y + debugOffset + nodeSize / 2, roomArray[j].transform.position.z);
                        Handles.DrawBezier(aux_i, aux_j, aux_i, aux_j, interconectedLineColor, null, lineWidth);
                    }
                }
            }
            foreach (GameObject room in roomArray)
            {   
                if(room != null)
                {
                    switch (room.GetComponent<RoomController>().roomType)
                    {
                        case RoomController.RoomeTypes.Start:
                            Gizmos.color = startColor;
                            break;
                        case RoomController.RoomeTypes.Empty:
                            Gizmos.color = emptyColor;
                            break;
                        case RoomController.RoomeTypes.Enemy:
                            Gizmos.color = enemyColor;
                            break;
                        case RoomController.RoomeTypes.Trap:
                            Gizmos.color = trapColor;
                            break;
                        case RoomController.RoomeTypes.Fragment:
                            Gizmos.color = fragmentColor;
                            break;
                        case RoomController.RoomeTypes.Boss:
                            Gizmos.color = bossColor;
                            break;
                        case RoomController.RoomeTypes.Healing:
                            Gizmos.color = healingColor;
                            break;
                        default:
                            Gizmos.color = startColor;
                            break;
                    }
                    Gizmos.DrawSphere(new Vector3(room.transform.position.x, room.transform.position.y + debugOffset, room.transform.position.z), nodeSize);
                }
            }
        }
    }
}
