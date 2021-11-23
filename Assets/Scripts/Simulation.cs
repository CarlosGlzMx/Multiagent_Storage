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
    private float roomHeight = 7.0f;
    private Mesh roomMesh;
    private Renderer renderer;

    // Conexión con Colab
    public string url; 

    // Variables para el flujo del simulador. Robots puede ser array dado que los ids empiezan en 0
    private GameObject[] robots;
    private Dictionary<Vector2, GameObject> boxes;
    public float updateInterval = 5.0f;
    private float _timer;
    private float _dt;

    // Prefabs por ser colocados
    public GameObject robot;
    public GameObject box;
    public GameObject door;
    public GameObject stack;

    void Start() {
        // Obtención del mesh del cuarto base
        roomMesh = GameObject.FindGameObjectsWithTag("Room")[0].GetComponent<MeshFilter>().mesh;
        renderer = GameObject.FindGameObjectsWithTag("Room")[0].GetComponent<Renderer>();

        // Variables que provienen de Colab para la generación del cuarto
        int M = 16;
        int N = 12;
        GenerateRoom(M, N);
    }

    void Update() {
        
    }

    // Genera un almacen de manera dinámica con: luces, puertas, texturas y una camara reubicada
    void GenerateRoom(int width, int height) {
        // Obtiene los vértices del mesh que serán movidos según la parametrización
        Vector3[] vertices = roomMesh.vertices;

        // Realiza correcciones de tamaño
        for (int i = 0; i < vertices.Length; i++) {
            // Modificaciones conociendo las dimensiones del caso base
            if (vertices[i][0] > 1) {
                vertices[i][0] = height + vertices[i][0] - 1;
            }
            if (vertices[i][1] > 1) {
                vertices[i][1] = roomHeight;
            }
            if (vertices[i][2] > 1) {
                vertices[i][2] = width + vertices[i][2] - 1;
            } 
        }

        // Reajuste del cuarto
        roomMesh.vertices = vertices;

        // Ajuste del tiling de las texturas
        Material[] roomMaterials = renderer.materials;
        roomMaterials[1].mainTextureScale = new Vector2(1.0f, (float) 1.0f / roomHeight);
        roomMaterials[2].mainTextureScale = new Vector2(height, width);
        renderer.materials = roomMaterials;

        // Ajuste de la cámara, considerando un fov horizontal de 90 grados
        float camZ = (width + 2) / 2;
        float camX = (height + 2) + camZ;
        GetComponent<Camera>().transform.position = new Vector3(camX, roomHeight, camZ);
        float camRotX = Mathf.Atan(roomHeight / camZ) / 2 * Mathf.Rad2Deg;
        GetComponent<Camera>().transform.eulerAngles += new Vector3(camRotX, 0, 0);

        // Construcción de una cuadrícula de luces
        int sectionSize = 8;
        int horLights = width / sectionSize;
        int verLights = height / sectionSize;
        horLights = horLights == 0 ? 1 : horLights;
        verLights = verLights == 0 ? 1 : verLights;

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
                lightGameObject.transform.position = new Vector3(lightX, roomHeight, lightZ);
                
                // Personalización de la intensidad, rango y persistencia. Valores probados
                roomLight.intensity = 1.2f;
                roomLight.range = Mathf.Sqrt(Mathf.Pow((float) roomHeight, 2) + Mathf.Pow((float) sectionSize * 1.5f, 2));
                roomLight.renderMode = LightRenderMode.ForcePixel;
            }
        }

        // Colocación de las puertas - Se elige una puerta doble en cada pared lateral
        Instantiate(door, new Vector3((float) height / 2 + 1.5f, 2.0f, 1.1f), Quaternion.Euler(90, 180, 90));
        Instantiate(door, new Vector3((float) height / 2 + 2.5f, 2.0f, 1.0f), Quaternion.Euler(90, 0, 90));
        Instantiate(door, new Vector3((float) height / 2 + 1.5f, 2.0f, (float) width + 1.0f), Quaternion.Euler(90, 180, 90));
        Instantiate(door, new Vector3((float) height / 2 + 2.5f, 2.0f, (float) width + 0.9f), Quaternion.Euler(90, 0, 90));
    }

    // Método para comunicación con Python, envía datos petición y recibe datos de simulación
    private IEnumerator SendData(string requestedData) {
        // Crea un form que se enviará en el método POST
        WWWForm form = new WWWForm();
        form.AddField("request", requestedData);
        using (UnityWebRequest www = UnityWebRequest.Post(url, form)) {

            // Preparación de los datos y el encabezado HTTP para salir hacia Colab
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(requestedData);
            www.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = (DownloadHandler)new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            // Se envía el request y se actúa en caso de error o éxito
            yield return www.SendWebRequest();
            if (www.result != UnityWebRequest.Result.ConnectionError &&
                www.result != UnityWebRequest.Result.ProtocolError) {
                
                // Confirmación del texto recibido
                Debug.Log("Respuesta de Python:");
                Debug.Log(www.downloadHandler.text);

                // Casos considerados de requests distintos
                if (requestedData == "init") {
                    Debug.Log("Era un inicial");
                }
                else if (requestedData == "step") {
                    Debug.Log("Era un step");
                }
            }
            else {
                // Error en la conexión con Colab
                Debug.Log(www.error);
            }
        }
    }
}