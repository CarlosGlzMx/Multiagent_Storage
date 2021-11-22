using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Simulation : MonoBehaviour
{
    // Variables para la creación del cuarto
    public float roomHeight = 7.0f;
    private Mesh roomMesh;
    private Renderer renderer;

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
        int M = 20;
        int N = 20;
        GenerateRoom(M, N);
    }

    void Update() {
        
    }

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

        // Construcción de una cuadrilla de luces
        int sectionSize = 8;
        int horLights = width / sectionSize;
        int verLights = height / sectionSize;
        horLights = horLights == 0 ? 1 : horLights;
        verLights = verLights == 0 ? 1 : verLights;

        // Disclaimer
        if (horLights * verLights > 2) {
            Debug.Log("Unity básico no mostrará muchas fuentes de luz puntuales");
        }

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

        // EJEMPLO DE INSTANCIA DESDE CÓDIGO
        Instantiate(robot, new Vector3(1,1,1), Quaternion.identity);
    }
}
