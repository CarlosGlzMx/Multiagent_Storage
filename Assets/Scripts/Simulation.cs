/*
Simulación de un sistema multiagentes conectado con Google Colab
TC2008B. Sistemas Multiagentes y Gráficas Computacionales. Tecnológico de Monterrey

El equipo de trabajo genera en su mayoría código independiente, pero acude a
implementaciones de profesores del Tecnológico de Monterrey para las conexiones
con HTTP y el manejo de los datos que se envían y reciben.

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
    // Variables para la creación del cuarto
    private int m = 1, n = 1, roomHeight = 7;
    private Mesh roomMesh;
    private Renderer renderer;

    // Conexión con Colab
    public string url; 

    // Variables para el flujo del simulador. Robots puede ser array dado que los ids empiezan en 0
    public GameObject[] robots;
    public GameObject[] stacks;
    public Dictionary<Vector2, GameObject> boxes;
    public float stepSpeed = 5.0f;
    private float _timer, _dt;

    // Prefabs por ser colocados
    public GameObject robot, box, door, stack;

    void Start() {
        // Obtención de los componentes del cuarto base
        roomMesh = GameObject.FindGameObjectsWithTag("Room")[0].GetComponent<MeshFilter>().mesh;
        renderer = GameObject.FindGameObjectsWithTag("Room")[0].GetComponent<Renderer>();

        // Primera conexión con Colab, devuelve M, N y el grid inicial
        // De ahí mismo se llamará la construcción del cuarto y los agentes
        StartCoroutine(RequestToColab("board-init"));
        StartCoroutine(RequestToColab("robots"));
        StartCoroutine(RequestToColab("stacks"));
        StartCoroutine(RequestToColab("boxes"));
        StartCoroutine(RequestToColab("step"));
    }

    void Update() {
        
    }

    // Método para comunicación con Python, envía datos petición y recibe datos de simulación
    private IEnumerator RequestToColab(string requestName) {
        // Crea un form que se enviará en el método POST
        WWWForm form = new WWWForm();
        string requestInJSON = "{\"request\" : \"" + requestName + "\"}";
        form.AddField("request", requestInJSON);
        using (UnityWebRequest www = UnityWebRequest.Post(url, form)) {

            // Preparación de los datos y el encabezado HTTP para salir hacia Colab
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(requestInJSON);
            www.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            // Se envía el request y se actúa en caso de error o éxito
            yield return www.SendWebRequest();
            if (www.result != UnityWebRequest.Result.ConnectionError &&
                www.result != UnityWebRequest.Result.ProtocolError) {
                
                // Casos considerados de requests distintos
                if (requestName == "board-init") {
                    // Obtiene el tamaño del tablero
                    Board board = JsonUtility.FromJson<Board>(www.downloadHandler.text);
                    m = board.m;
                    n = board.n;
                    GenerateRoom(m, n);
                }
                else if (requestName == "robots") {
                    StorageRobot[] robotData = JsonHelper.FromJson<StorageRobot>(www.downloadHandler.text);
                    if (robots.Length == 0) SpawnRobots(robotData);
                }
                else if (requestName == "stacks") {
                    Stack[] stackData = JsonHelper.FromJson<Stack>(www.downloadHandler.text);
                    if (stacks.Length == 0) SpawnStacks(stackData);
                }
                else if (requestName == "boxes") {
                    Box[] boxData = JsonHelper.FromJson<Box>(www.downloadHandler.text);
                    if (boxes == null) SpawnBoxes(boxData);
                }
                else if (requestName == "step") {
                    Action[] stepActions = JsonHelper.FromJson<Action>(www.downloadHandler.text);
                    executeActions(stepActions);
                }
            }
            else {
                // Error en la conexión con Colab
                Debug.Log(www.error);
            }
        }
    }

    // Ejecuta la lista de acciones correspondiente al step actual
    private void executeActions(Action[] stepActions) {
        for (int i = 0; i < stepActions.Length; i++) {
            if (stepActions[i].type == "Moverse") {
                Debug.Log("Me muevo");
            }
            else if (stepActions[i].type == "Recoger") {
                Debug.Log("Recojo caja");
            }
            else if (stepActions[i].type == "Dejar") {
                Debug.Log("Dejo caja");
            }
        }
    }


    private void SpawnRobots(StorageRobot[] data) {
        robots = new GameObject[data.Length];
        for (int i = 0; i < data.Length; i++) {
            robots[data[i].id] = Instantiate(robot, new Vector3(data[i].x, 1, data[i].z),
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
        boxes = new Dictionary<Vector2, GameObject>();
        for (int i = 0; i < data.Length; i++) {
            boxes.Add(new Vector2(data[i].x, data[i].z), Instantiate(box,
                new Vector3(data[i].x, 1, data[i].z), Quaternion.identity) as GameObject);
        }
    }

    // Genera un almacen de manera dinámica con: luces, puertas, texturas y una camara reubicada
    void GenerateRoom(int width, int height) {
        // Obtiene los vértices del mesh que serán movidos según la parametrización
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
        Material[] roomMaterials = renderer.materials;
        roomMaterials[1].mainTextureScale = new Vector2(1.0f, (float) 1.0f / roomHeight);
        roomMaterials[2].mainTextureScale = new Vector2(height, width);
        renderer.materials = roomMaterials;

        // Ajuste de la cámara, considerando un fov horizontal de 90 grados
        float camZ = (width + 2) / 2;
        float camX = (height + 2) + camZ;
        GetComponent<Camera>().transform.position = new Vector3(camX, (float) roomHeight, camZ);
        float camRotX = Mathf.Atan((float) roomHeight / camZ) / 2 * Mathf.Rad2Deg;
        GetComponent<Camera>().transform.eulerAngles += new Vector3(camRotX, 0, 0);

        // Construcción de una cuadrícula de luces
        int SECTION_SIZE = 8;
        int horLights = width < SECTION_SIZE ? 1 : width / SECTION_SIZE;
        int verLights = height < SECTION_SIZE ? 1 : height / SECTION_SIZE;

        // Disclaimer
        Debug.Log("Unity básico no mostrará muchas fuentes de luz puntuales para no gastar recursos");

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
}