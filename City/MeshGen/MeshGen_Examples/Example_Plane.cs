using System.Collections;
using System.Collections.Generic;
using ProcGen;
using UnityEngine;

public class Example_Plane : MonoBehaviour
{
    public Material material;

    [Header("UVs")]
    public float metersPerTile = 1f;
    // Start is called before the first frame update
    void Start()
    {
        var b = new MeshBuilder();

        var tile = Quad.FromXZRect(new Vector3(0, 0, 0), widthX: 1f, depthZ: 1f, y: 0);
        b.Add(tile);

        
        var tile2 = Quad.FromXZRect(new Vector3(1, 0, 0), widthX: 1f, depthZ: 1f, y: 0);
        b.Add(tile2);
        
        // Bake: 1 UV = metersPerTile meters; ShareByPlane vertex reuse (default)
        var mesh = b.ToMesh(metersPerTile: metersPerTile);
        GetComponent<MeshFilter>().sharedMesh = mesh;
        if (material != null) GetComponent<MeshRenderer>().sharedMaterial = material;

    }

}
