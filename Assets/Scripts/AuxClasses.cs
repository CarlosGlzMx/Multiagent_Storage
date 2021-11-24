using System;
using UnityEngine;

// Clases fáciles de deserializar para JSONUtility
[Serializable]
public class Board {
    public int m, n;
}

[Serializable]
public class StorageRobot {
    public int id, x, z;
}

[Serializable]
public class Stack {
    public int x, z, boxes;
}

[Serializable]
public class Box {
    public int x, z;
}

[Serializable]
public class Action {
    public string type;
    public int dx, dy, robotID, stackSize;
}

public class Move {
    public int robotID;
    public Vector3 origin;
    public Vector3 destination;
    public Move (int id, Vector3 origin, int dx, int dy) {
        // Hace el cambio de coordenadas de Colab a de Unity
        this.robotID = id;
        this.origin = origin;
        this.destination = origin + new Vector3(dy, 0, dx);
    }
}