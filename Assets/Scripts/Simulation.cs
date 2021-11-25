/*
Simulación de un sistema multiagentes conectado con Google Colab
TC2008B. Sistemas Multiagentes y Gráficas Computacionales. Tecnológico de Monterrey

El equipo de trabajo genera en su mayoría código independiente, pero acude a
implementaciones de profesores del Tecnológico de Monterrey para las conexiones
con HTTP y el manejo de los datos que se envían y reciben.

https://colab.research.google.com/drive/1hcRyf_sUnMaF551BBNDEeXwyokZ5cswo?usp=sharing

- Versión para la actividad integradora del equipo 2, Carlos G. del Rosal, 11/22/2021
- Adapted by [Jorge Cruz](https://jcrvz.co) on November 2021
- Original implementation: C# client to interact with Unity, Sergio Ruiz, July 2021
*/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public class Simulation : MonoBehaviour {
    // Variables para la creación del cuarto - base
    private int m = 1, n = 1, roomHeight = 8;
    private Mesh roomMesh;
    private Renderer roomRenderer;

    // Conexión con Colab
    public string url; 

    // Variables para el flujo del simulador. Robots puede ser array dado que los ids empiezan en 0
    private GameObject[] robots;
    private GameObject[] stacks;
    private Dictionary<string, GameObject> boxes;
    private List<Move> ongoingMoves = new List<Move>();

    // Controles de duración y activación de la simulación
    public float stepDuration = 5.0f;
    private float timer, parametrizedT;
    private bool active = false;

    // Prefabs por ser colocados
    public GameObject robot, box, door, stack;

    void Start() {
        // Incialización definida por Colab
        StartCoroutine(RequestToColab("board-init"));
        StartCoroutine(RequestToColab("robots"));
        StartCoroutine(RequestToColab("stacks"));
        StartCoroutine(RequestToColab("boxes"));

        // Inicialización del cronometro
        timer = stepDuration;
    }

    void Update() {
        // Permite tener la simulación inactiva sin tener que dejar/destruir la escena
        if (active) {
            // Cuenta el tiempo hacia 0 restando el tiempo por frame
            timer -= Time.deltaTime;
            parametrizedT = 1.0f - (timer / stepDuration);
            if (timer < 0) {
                // Acciones por realizar cada stepDuration
                ongoingMoves.Clear();
                StartCoroutine(RequestToColab("step"));

                // Reinicia el timer al cumplirse la duración
                timer = stepDuration;
            }

            // Realiza los movimientos pendientes
            if (ongoingMoves.Count > 0) {
                foreach (Move robotMove in ongoingMoves) {
                    robots[robotMove.robotID].transform.position = Vector3.Lerp(
                        robotMove.origin, robotMove.destination, parametrizedT);
                    robots[robotMove.robotID].transform.LookAt(robotMove.destination);
                }
            }
        }
    }

    // Método para comunicación con Python, envía datos petición y recibe datos de simulación
    private IEnumerator RequestToColab(string requestName) {
        // Crea un form que se enviará en el método POST
        WWWForm form = new WWWForm();
        string requestInJSON = "{\"request\" : \"" + requestName + "\"}";
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(requestInJSON);
        form.AddField("request", requestInJSON);
        using (UnityWebRequest www = UnityWebRequest.Post(url, form)) {

            // Preparación de los datos y el encabezado HTTP para salir hacia Colab
            www.uploadHandler.Dispose();
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.SetRequestHeader("Content-Type", "application/json");

            // Se envía el request y se actúa en caso de error o éxito
            yield return www.SendWebRequest();
            if (www.result != UnityWebRequest.Result.ConnectionError &&
                www.result != UnityWebRequest.Result.ProtocolError) {
                string colabResponse = www.downloadHandler.text;
                
                // Casos considerados de requests distintos
                if (colabResponse == "{\"order\": \"stop\"}") {
                    // Con la primera request que diga que Python se ha detenido deja de actualizar
                    if (active) Debug.Log("Colab ha detenido el envío de datos");
                    active = false;
                }
                else if (requestName == "board-init") {
                    // Obtiene el tamaño del tablero
                    Board board = JsonUtility.FromJson<Board>(colabResponse);
                    m = board.m;
                    n = board.n;
                    GenerateRoom(m, n);
                    active = true;
                }
                else if (requestName == "robots") {
                    // Obtiene los robots, los instancia como un prefab
                    StorageRobot[] robotData = JsonHelper.FromJson<StorageRobot>(colabResponse);
                    if (robots == null) SpawnRobots(robotData);
                }
                else if (requestName == "stacks") {
                    // Obtiene las pilas, las instancia como un prefab
                    Stack[] stackData = JsonHelper.FromJson<Stack>(colabResponse);
                    if (stacks == null) SpawnStacks(stackData);
                }
                else if (requestName == "boxes") {
                    // Obtiene las cajas, las instancia como un prefab
                    Box[] boxData = JsonHelper.FromJson<Box>(colabResponse);
                    if (boxes == null) SpawnBoxes(boxData);
                }
                else if (requestName == "step") {
                    // Obtiene las acciones por realizar en el siguiente tiempo de acción
                    Action[] stepActions = JsonHelper.FromJson<Action>(colabResponse);
                    if (stepActions.Length > 0) executeActions(stepActions);
                }
            }
            else {
                // Error en la conexión con Colab
                Debug.Log("No hay comunicación con Colab");
                active = false;
            }
        }
    }

    // Ejecuta la lista de acciones correspondiente al step actual
    private void executeActions(Action[] stepActions) {
        foreach (Action action in stepActions) {
            if (action.type == "Moverse") {
                // Registra el movimiento solo como el desplazamiento que se interpolará
                ongoingMoves.Add(new Move(action.robotID,
                    robots[action.robotID].transform.position, action.dx, action.dy));
            }
            else if (action.type == "Recoger") {
                // "Recoge" la caja al activar la caja encima y destruir la caja debajo
                robots[action.robotID].transform.Find("Box").GetComponent<Renderer>().enabled = true;
                Destroy(boxes[GetPickedBoxKey(action)]);
            }
            else if (action.type == "Dejar") {
                // "Deja" la caja al desactivar la caja encima e instanciar una caja en la pila
                robots[action.robotID].transform.Find("Box").GetComponent<Renderer>().enabled = false;
                Instantiate(box, new Vector3(robots[action.robotID].transform.position.x + action.dy,
                    action.stackSize + 1, robots[action.robotID].transform.position.z + action.dx),
                    Quaternion.identity);
            }
        }
    }

    private void SpawnRobots(StorageRobot[] data) {
        robots = new GameObject[data.Length];
        for (int i = 0; i < data.Length; i++) {
            robots[data[i].id] = Instantiate(robot, new Vector3(data[i].x + 0.5f, 1, data[i].z + 0.5f),
                Quaternion.identity) as GameObject;
            robots[data[i].id].transform.Find("Box").GetComponent<Renderer>().enabled = false;
        }
    }

    private void SpawnStacks(Stack[] data) {
        stacks = new GameObject[data.Length];
        for (int i = 0; i < data.Length; i++) {
            // El prefab de Blender trae una rotación fija y el pivote mal, se ajusta manualmente
            stacks[i] = Instantiate(stack) as GameObject;
            stacks[i].transform.position = new Vector3(data[i].x + 0.5f, 1, data[i].z + 0.5f);
        }
    }

    private void SpawnBoxes(Box[] data) {
        boxes = new Dictionary<string, GameObject>();
        for (int i = 0; i < data.Length; i++) {
            boxes.Add(data[i].x + "-" + data[i].z, Instantiate(box,
                new Vector3(data[i].x, 1, data[i].z), Quaternion.identity) as GameObject);
        }
    }

    // Genera un almacen de manera dinámica con: luces, puertas, texturas y una camara reubicada
    private void GenerateRoom(int width, int height) {
        // Obtiene los vértices del mesh que serán movidos según la parametrización
        roomMesh = GameObject.FindGameObjectsWithTag("Room")[0].GetComponent<MeshFilter>().mesh;
        roomRenderer = GameObject.FindGameObjectsWithTag("Room")[0].GetComponent<Renderer>();
        Vector3[] vertices = roomMesh.vertices;

        // Realiza correcciones de tamaño
        for (int i = 0; i < vertices.Length; i++) {
            // Modificaciones conociendo las dimensiones del caso base
            if (vertices[i][0] > 1) vertices[i][0] = height + vertices[i][0] - 1;
            if (vertices[i][1] > 1) vertices[i][1] = (float) roomHeight;
            if (vertices[i][2] > 1) vertices[i][2] = width + vertices[i][2] - 1;
        }
        roomMesh.vertices = vertices;

        // Ajuste del tiling de las texturas
        Material[] roomMaterials = roomRenderer.materials;
        roomMaterials[1].mainTextureScale = new Vector2(1.0f, (float) 1.0f / roomHeight);
        roomMaterials[2].mainTextureScale = new Vector2(height, width);
        roomRenderer.materials = roomMaterials;

        // Ajuste de la cámara, considerando un fov horizontal de 90 grados
        float camZ = (width + 2) / 2;
        float camX = (height + 2) + camZ;
        Camera.main.transform.position = new Vector3(camX, (float) roomHeight, camZ);
        float camRotX = Mathf.Atan((float) roomHeight / camZ) / 2 * Mathf.Rad2Deg;
        Camera.main.transform.eulerAngles += new Vector3(camRotX, 0, 0);

        // Construcción de una cuadrícula de luces
        int SECTION_SIZE = 8;
        int horLights = width < SECTION_SIZE ? 1 : width / SECTION_SIZE;
        int verLights = height < SECTION_SIZE ? 1 : height / SECTION_SIZE;

        // Creación de luces y personalización
        for (int i = 0; i < horLights; i++) {
            for (int j = 0; j < verLights; j++) {
                // Creación de un objeto vacío, con transform, que tenga la luz como componente
                GameObject lightGameObject = new GameObject("Room Light " + (i * verLights + j + 1));
                Light roomLight = lightGameObject.AddComponent<Light>();

                // Colocación de la nueva luz
                float lightX = (float) height / (verLights * 2) * (j * 2 + 1) + 1;
                float lightZ = (float) width / (horLights * 2) * (i * 2 + 1) + 1;
                lightGameObject.transform.position = new Vector3(lightX, (float) roomHeight, lightZ);
                
                // Personalización de la intensidad, rango y persistencia. Valores probados
                roomLight.intensity = 1.2f;
                roomLight.range = Mathf.Sqrt(Mathf.Pow((float) roomHeight, 2) + Mathf.Pow((float) SECTION_SIZE * 1.5f, 2));
                roomLight.renderMode = LightRenderMode.ForcePixel;
            }
        }

        // Colocación de las puertas - Se elige una puerta doble en cada pared lateral
        Instantiate(door, new Vector3((float) height / 2 + 1.5f, 2.0f, 1.1f), Quaternion.Euler(90, 180, 90));
        Instantiate(door, new Vector3((float) height / 2 + 2.5f, 2.0f, 1.0f), Quaternion.Euler(90, 0, 90));
        Instantiate(door, new Vector3((float) height / 2 + 1.5f, 2.0f, (float) width + 1.0f), Quaternion.Euler(90, 180, 90));
        Instantiate(door, new Vector3((float) height / 2 + 2.5f, 2.0f, (float) width + 0.9f), Quaternion.Euler(90, 0, 90));
    }

    private string GetPickedBoxKey(Action action) {
        int roundedX = (int)Mathf.Round(robots[action.robotID].transform.position.x) + action.dy;
        int roundedZ = (int)Mathf.Round(robots[action.robotID].transform.position.z) + action.dx;
        return roundedX + "-" + roundedZ;
    }
}