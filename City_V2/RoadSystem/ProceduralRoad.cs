using System.Collections.Generic;
using UnityEngine;

public class ProceduralRoad : MonoBehaviour
{
    [Header("Render")]
    public Material material;

    [Header("Dimensions")]
    public Vector2 Size = new Vector2(12, 12);
    public float RoadHeight = 0f;

    [Header("Road Profile Settings")]
    // Same fields as the intersection config
    public CurbGutter curb = CurbGutter.Default();
    public FootpathDepthSet footpathDepths = FootpathDepthSet.FromSingle(3f);

    void Start()
    {
        PBMeshBuilder builder = new PBMeshBuilder();
        var sinkTag = gameObject.AddComponent<FaceSinkTag>();
        builder.Sink = sinkTag;

        // Build footpaths on all four sides
        BuildFootpathSide(builder, Side.South);
        BuildFootpathSide(builder, Side.East);
        BuildFootpathSide(builder, Side.North);
        BuildFootpathSide(builder, Side.West);

        // Build the road fill inside the inner rectangle bounded by curb+gutter offsets
        BuildRoadFill(builder);

        if (material)
        {
            Material[] materials = new Material[] { material };
            builder.Build(materials, this.transform);
        }
    }

    void BuildRoadFill(PBMeshBuilder builder)
    {
        // Inner offsets from each boundary using footpath depth + skirtOut + gutterWidth
        float join = curb.skirtOut + curb.gutterWidth;

        float southDepth = Mathf.Max(0f, footpathDepths.South);
        float northDepth = Mathf.Max(0f, footpathDepths.North);
        float westDepth  = Mathf.Max(0f, footpathDepths.West);
        float eastDepth  = Mathf.Max(0f, footpathDepths.East);

        float xL = westDepth  + join;
        float xR = Size.x - (eastDepth + join);
        float zB = southDepth + join;
        float zT = Size.y - (northDepth + join);

        if (xL >= xR || zB >= zT) return; // degenerate, skip

        var face = QuadXZ(xL, xR, zB, zT, RoadHeight);
        face = VertexOperations.Translate(face, transform.position);
        builder.AddQuadFace(face);
    }

    // Build one footpath ribbon with curb skirt, gutter apron, and gutter skirt-to-road.
    void BuildFootpathSide(PBMeshBuilder builder, Side side)
    {
        float depth = DepthFor(side);
        if (depth <= 0f) return;

        // Canonical local for a horizontal edge where +Z goes inward from boundary
        // We'll rotate/translate into place per side.
        // Edge length = Size.x for South/North, Size.y for East/West.
        float edgeLength = (side == Side.South || side == Side.North) ? Size.x : Size.y;

        float xL = 0f;
        float xR = edgeLength;
        float z0 = 0f;               // outer edge at boundary
        float z1 = depth;            // inner edge toward road
        float y  = 0f;

        var slab = new Quad(new[]
        {
            new Vector3(xL, y, z0), // v0
            new Vector3(xR, y, z0), // v1
            new Vector3(xR, y, z1), // v2  inner/top edge
            new Vector3(xL, y, z1), // v3
        });

        // Curb skirt: push inner edge outward (+Z) and down
        var curbSkirt = new Quad(vertices: slab.ExtrudeEdgeOutDown(2, Vector3.forward, curb.skirtOut, curb.skirtDown));

        // Gutter apron: continue down from the skirt’s inner/upper edge
        var apron = ExtrusionUtil.ExtrudeEdgeOutDown(
            curbSkirt.Vertices[1], curbSkirt.Vertices[2],
            Vector3.forward, 0f, curb.gutterDepth);

        // Gutter skirt to road Y: horizontal push by gutterWidth, snap to RoadHeight
        var gutterSkirtToRoad = ExtrusionUtil.ExtrudeEdgeOutToWorldY(
            apron[1], apron[2], Vector3.forward, curb.gutterWidth, RoadHeight);

        // Rotate/translate into actual side placement
        var sets = new Vector3[][]
        {
            slab.Vertices,
            curbSkirt.Vertices,
            apron,
            gutterSkirtToRoad
        };

        var (rotY, tx) = PlacementFor(side, Size);
        var rotated = VertexOperations.RotateMany(sets, new Vector3(0f, rotY, 0f), Vector3.zero);
        var placedLocal = VertexOperations.TranslateMany(rotated, tx);
        var placedWorld = VertexOperations.TranslateMany(placedLocal, transform.position);

        builder.AddQuadFace(placedWorld[0]); // slab
        builder.AddQuadFace(placedWorld[1]); // curb skirt
        builder.AddQuadFace(placedWorld[2]); // gutter apron
        builder.AddQuadFace(placedWorld[3]); // gutter skirt to road
    }

    float DepthFor(Side s) => s switch
    {
        Side.South => footpathDepths.South,
        Side.East  => footpathDepths.East,
        Side.North => footpathDepths.North,
        _          => footpathDepths.West,
    };

    static (float rotYdeg, Vector3 tx) PlacementFor(Side side, Vector2 size) => side switch
    {
        Side.South => (  0f, new Vector3(0f,    0f,    0f)),          // along +X at z=0
        Side.East  => (-90f, new Vector3(size.x,0f,    0f)),          // along +Z at x=Size.x
        Side.North => (-180f,new Vector3(size.x,0f, size.y)),         // along -X at z=Size.y
        _          => (-270f,new Vector3(0f,    0f, size.y)),         // along -Z at x=0
    };

    // CCW quad on XZ at y
    static Vector3[] QuadXZ(float x0, float x1, float z0, float z1, float y)
    {
        return new[]
        {
            new Vector3(x1, y, z1), // NE
            new Vector3(x0, y, z1), // NW
            new Vector3(x0, y, z0), // SW
            new Vector3(x1, y, z0), // SE
        };
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        // Outline
        Gizmos.DrawLine(new Vector3(0, 0, 0),        new Vector3(Size.x, 0, 0));
        Gizmos.DrawLine(new Vector3(Size.x, 0, 0),   new Vector3(Size.x, 0, Size.y));
        Gizmos.DrawLine(new Vector3(Size.x, 0, Size.y), new Vector3(0, 0, Size.y));
        Gizmos.DrawLine(new Vector3(0, 0, Size.y),   new Vector3(0, 0, 0));
    }
}
