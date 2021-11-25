using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DinoCollision : MonoBehaviour
{
    // Incrementa la luz del robot por la colision
    void OnTriggerEnter(Collider other) {
        if (other.gameObject.name == "Dino(Clone)") {
            other.gameObject.transform.Find("Siren/Redlight").GetComponent<Light>().intensity = 5.0f;
        }
    }

    // Decrementa la luz al terminar la colisión
    void OnTriggerExit(Collider other) {
        if (other.gameObject.name == "Dino(Clone)") {
            other.gameObject.transform.Find("Siren/Redlight").GetComponent<Light>().intensity = 1.5f;
        }
    }
}
