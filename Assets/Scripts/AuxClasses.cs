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