using UnityEngine;
using System.Collections.Generic;


[CreateAssetMenu(fileName = "RoadNetworkAsset", menuName = "Scriptable Objects/RoadNetworkAsset")]
public class RoadNetworkAsset : ScriptableObject
{
  public List<RoadSection> sections = new();
}

[System.Serializable]
public class RoadSection
{
    public string name = "Section";
    public Vector3 position;
    public float rotationY;                  // degrees
    public Vector2 size = new(12, 48);      // X=width, Y=length
    public float roadHeight = 0f;

    public bool connectedN, connectedE, connectedS, connectedW;

    public float footSouth = 3, footEast = 3, footNorth = 3, footWest = 3;
    public CurbGutter curb = CurbGutter.Default();
    public Material material;

    public string bakeFolder = "Assets/BakedMeshes";
    public string prefabNameOverride = "";  // optional
}
