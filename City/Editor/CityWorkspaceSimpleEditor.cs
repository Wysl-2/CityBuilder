// // CityWorkspaceSimpleEditor.cs
// // SceneView rectangle gizmos for Area, BuildingPlots, RoadBands, and IntersectionAreas.
// // Adds crisp visualization for road cross-sections and correct corner joins that
// // meet using per-side SideProfile widths (footpath, curb, gutter).

// using System.IO;
// using UnityEditor;
// using UnityEngine;
// using SP = CityWorkspaceSimple.SideProfile;

// [CustomEditor(typeof(CityWorkspaceSimple))]
// public class CityWorkspaceSimpleEditor : Editor
// {
//     CityWorkspaceSimple ws;

//     SerializedProperty areaYProp, areaSizeProp, makeGroundProp, groundMatProp, groundUVProp;

//     SerializedProperty groundCellSizeProp, groundAddMeshColliderProp, groundColliderRemoveIntersectionsProp;
//     SerializedProperty groundCarveUnderRoadsProp, groundCarveUnderBuildingsProp, groundRoadsMarginProp, groundBuildingsMarginProp;

//     SerializedProperty buildingPlotsProp, roadBandsProp, intersectionsProp;

//     string meshFolder = "Assets/City/Meshes";

//     void OnEnable()
//     {
//         ws = (CityWorkspaceSimple)target;

//         areaYProp      = serializedObject.FindProperty("areaY");
//         areaSizeProp   = serializedObject.FindProperty("areaSize");
//         makeGroundProp = serializedObject.FindProperty("makeGround");
//         groundMatProp  = serializedObject.FindProperty("groundMaterial");
//         groundUVProp   = serializedObject.FindProperty("groundUVMeters");

//         groundCellSizeProp                   = serializedObject.FindProperty("groundCellSize");
//         groundAddMeshColliderProp            = serializedObject.FindProperty("groundAddMeshCollider");
//         groundColliderRemoveIntersectionsProp= serializedObject.FindProperty("groundColliderRemoveIntersections");
//         groundCarveUnderRoadsProp            = serializedObject.FindProperty("groundCarveUnderRoads");
//         groundCarveUnderBuildingsProp        = serializedObject.FindProperty("groundCarveUnderBuildings");
//         groundRoadsMarginProp                = serializedObject.FindProperty("groundRoadsMargin");
//         groundBuildingsMarginProp            = serializedObject.FindProperty("groundBuildingsMargin");

//         buildingPlotsProp = serializedObject.FindProperty("buildingPlots");
//         roadBandsProp     = serializedObject.FindProperty("roadBands");
//         intersectionsProp = serializedObject.FindProperty("intersections");
//     }

//     public override void OnInspectorGUI()
//     {
//         serializedObject.Update();

//         EditorGUILayout.LabelField("Main Area", EditorStyles.boldLabel);
//         EditorGUILayout.PropertyField(areaYProp);
//         EditorGUILayout.PropertyField(areaSizeProp);

//         EditorGUILayout.PropertyField(makeGroundProp);
//         if (makeGroundProp.boolValue)
//         {
//             EditorGUILayout.PropertyField(groundMatProp);
//             EditorGUILayout.PropertyField(groundUVProp, new GUIContent("Ground UV (m/tile)"));
//         }

//         EditorGUILayout.Space();
//         EditorGUILayout.LabelField("Ground Grid (subdivision + cull)", EditorStyles.boldLabel);
//         EditorGUILayout.PropertyField(groundCellSizeProp,   new GUIContent("Cell Size (m)"));
//         EditorGUILayout.PropertyField(groundAddMeshColliderProp, new GUIContent("Add Mesh Collider"));

//         using (new EditorGUI.DisabledScope(!groundAddMeshColliderProp.boolValue))
//         {
//             EditorGUILayout.PropertyField(groundColliderRemoveIntersectionsProp, new GUIContent("Collider Removes Intersections"));
//         }

//         EditorGUILayout.PropertyField(groundCarveUnderRoadsProp,     new GUIContent("Carve Under Roads"));
//         EditorGUILayout.PropertyField(groundCarveUnderBuildingsProp, new GUIContent("Carve Under Buildings"));
//         using (new EditorGUI.IndentLevelScope())
//         {
//             EditorGUILayout.PropertyField(groundRoadsMarginProp,     new GUIContent("Roads Margin (m)"));
//             EditorGUILayout.PropertyField(groundBuildingsMarginProp, new GUIContent("Buildings Margin (m)"));
//         }

//         EditorGUILayout.Space();
//         EditorGUILayout.LabelField("Building Plots", EditorStyles.boldLabel);
//         EditorGUILayout.PropertyField(buildingPlotsProp, true);

//         EditorGUILayout.Space();
//         EditorGUILayout.LabelField("Road Bands", EditorStyles.boldLabel);
//         EditorGUILayout.PropertyField(roadBandsProp, true);

//         EditorGUILayout.Space();
//         EditorGUILayout.LabelField("Intersection Areas", EditorStyles.boldLabel);
//         EditorGUILayout.PropertyField(intersectionsProp, true);

//         EditorGUILayout.Space();
//         using (new EditorGUILayout.HorizontalScope())
//         {
//             if (GUILayout.Button("Generate Preview"))
//             {
//                 Undo.RegisterFullObjectHierarchyUndo(ws.gameObject, "Generate Preview");
//                 ws.GeneratePreview();
//             }
//             if (GUILayout.Button("Clear Preview"))
//             {
//                 Undo.RegisterFullObjectHierarchyUndo(ws.gameObject, "Clear Preview");
//                 ws.ClearPreview();
//             }
//         }

//         EditorGUILayout.Space();
//         meshFolder = EditorGUILayout.TextField("Bake Mesh Folder", meshFolder);
//         if (GUILayout.Button("Bake Preview -> Mesh Assets + MeshColliders"))
//         {
//             BakePreviewToAssets(ws, meshFolder);
//         }

//         serializedObject.ApplyModifiedProperties();
//     }

//     // -------- Scene drawing/handles --------
//     void OnSceneGUI()
//     {
//         if (!ws) return;
//         float y = ws.areaY;
//         var t = ws.transform;

//         // Draw main area rect (thin overlay)
//         Vector3 c = t.position + new Vector3(0, y - t.position.y, 0);
//         Vector2 size = ws.areaSize;
//         DrawRectWireThinOverlay(c, size, new Color(0.25f, 0.8f, 1f, 0.8f), lift: 0.02f, px: 1.0f);

//         // Simple edge resize sliders for area (keep as-is)
//         EditorGUI.BeginChangeCheck();
//         float hx = size.x * 0.5f;
//         float hz = size.y * 0.5f;
//         Vector3 rx = c + new Vector3(hx, 0, 0);
//         Vector3 rz = c + new Vector3(0, 0, hz);
//         float newHx = Handles.ScaleSlider(hx, rx, Vector3.right, Quaternion.identity, HandleUtility.GetHandleSize(rx)*0.8f, 0.1f);
//         float newHz = Handles.ScaleSlider(hz, rz, Vector3.forward, Quaternion.identity, HandleUtility.GetHandleSize(rz)*0.8f, 0.1f);
//         if (EditorGUI.EndChangeCheck())
//         {
//             Undo.RecordObject(ws, "Resize Area");
//             ws.areaSize = new Vector2(Mathf.Max(1f, newHx*2f), Mathf.Max(1f, newHz*2f));
//             EditorUtility.SetDirty(ws);
//         }

//         // Building plots
//         for (int i = 0; i < ws.buildingPlots.Count; i++)
//         {
//             var p = ws.buildingPlots[i];
//             if (p.size.x <= 0 || p.size.y <= 0) continue;

//             Vector3 pc = ws.LocalXZToWorld(p.center, y);
//             DrawRectWireThinOverlay(pc, p.size, new Color(0.2f, 1f, 0.3f, 0.9f), lift: 0.02f, px: 1.0f);

//             // Center move
//             EditorGUI.BeginChangeCheck();
//             var np = FreeMoveHandleCompat(pc, HandleUtility.GetHandleSize(pc)*0.08f, Vector3.zero, Handles.SphereHandleCap);
//             if (EditorGUI.EndChangeCheck())
//             {
//                 Undo.RecordObject(ws, "Move Plot");
//                 var local = t.InverseTransformPoint(np);
//                 Vector2 clamped = ClampCenterToArea(new Vector2(local.x, local.z), p.size, ws.areaSize);
//                 p.center = clamped;
//                 ws.buildingPlots[i] = p;
//                 EditorUtility.SetDirty(ws);
//             }

//             // NE resize
//             Vector2 half = p.size * 0.5f;
//             Vector3 ne = pc + new Vector3(half.x, 0, half.y);
//             Vector3 sw = pc - new Vector3(half.x, 0, half.y);

//             EditorGUI.BeginChangeCheck();
//             var ne2 = FreeMoveHandleCompat(ne, HandleUtility.GetHandleSize(ne)*0.08f, Vector3.zero, Handles.CubeHandleCap);
//             if (EditorGUI.EndChangeCheck())
//             {
//                 Undo.RecordObject(ws, "Resize Plot");
//                 var neLocal = t.InverseTransformPoint(ne2);
//                 Vector2 neL = new Vector2(neLocal.x, neLocal.z);
//                 var swLocal = t.InverseTransformPoint(sw);
//                 Vector2 swL = new Vector2(swLocal.x, swLocal.z);
//                 Vector2 newSize = new Vector2(Mathf.Abs(neL.x - swL.x), Mathf.Abs(neL.y - swL.y));
//                 Vector2 newCenter = (neL + swL) * 0.5f;
//                 newCenter = ClampCenterToArea(newCenter, newSize, ws.areaSize);
//                 newSize = ClampSizeToArea(newCenter, newSize, ws.areaSize);
//                 p.center = newCenter; p.size = newSize;
//                 ws.buildingPlots[i] = p;
//                 EditorUtility.SetDirty(ws);
//             }
//         }

//         // Road bands
//         for (int i = 0; i < ws.roadBands.Count; i++)
//         {
//             var r = ws.roadBands[i];
//             if (r.size.x <= 0 || r.size.y <= 0) continue;

//             Vector3 rc = ws.LocalXZToWorld(r.center, y);
//             DrawRectWireThinOverlay(rc, r.size, new Color(1f, 0.75f, 0.2f, 0.9f), lift: 0.02f, px: 1.0f);

//             // Center move
//             EditorGUI.BeginChangeCheck();
//             var np = FreeMoveHandleCompat(rc, HandleUtility.GetHandleSize(rc)*0.08f, Vector3.zero, Handles.SphereHandleCap);
//             if (EditorGUI.EndChangeCheck())
//             {
//                 Undo.RecordObject(ws, "Move Road Band");
//                 var local = t.InverseTransformPoint(np);
//                 Vector2 clamped = ClampCenterToArea(new Vector2(local.x, local.z), r.size, ws.areaSize);
//                 r.center = clamped;
//                 ws.roadBands[i] = r;
//                 EditorUtility.SetDirty(ws);
//             }

//             // NE resize
//             Vector2 half = r.size * 0.5f;
//             Vector3 ne = rc + new Vector3(half.x, 0, half.y);
//             Vector3 sw = rc - new Vector3(half.x, 0, half.y);

//             EditorGUI.BeginChangeCheck();
//             var ne2 = FreeMoveHandleCompat(ne, HandleUtility.GetHandleSize(ne)*0.08f, Vector3.zero, Handles.CubeHandleCap);
//             if (EditorGUI.EndChangeCheck())
//             {
//                 Undo.RecordObject(ws, "Resize Road Band");
//                 var neLocal = t.InverseTransformPoint(ne2);
//                 Vector2 neL = new Vector2(neLocal.x, neLocal.z);
//                 var swLocal = t.InverseTransformPoint(sw);
//                 Vector2 swL = new Vector2(swLocal.x, swLocal.z);
//                 Vector2 newSize = new Vector2(Mathf.Abs(neL.x - swL.x), Mathf.Abs(neL.y - swL.y));
//                 Vector2 newCenter = (neL + swL) * 0.5f;
//                 newCenter = ClampCenterToArea(newCenter, newSize, ws.areaSize);
//                 newSize = ClampSizeToArea(newCenter, newSize, ws.areaSize);
//                 r.center = newCenter; r.size = newSize;
//                 ws.roadBands[i] = r;
//                 EditorUtility.SetDirty(ws);
//             }

//             DrawRoadBandVisualization(ws, r);
//         }

//         // Intersection areas
//         for (int i = 0; i < ws.intersections.Count; i++)
//         {
//             var ia = ws.intersections[i];
//             if (ia.size.x <= 0 || ia.size.y <= 0) continue;

//             Vector3 ic = ws.LocalXZToWorld(ia.center, y);

//             // Base outline
//             DrawRectWireThinOverlay(ic, ia.size, new Color(0.65f, 0.55f, 1.0f, 0.95f), lift: 0.02f, px: 1.0f);

//             // Center move
//             EditorGUI.BeginChangeCheck();
//             var nip = FreeMoveHandleCompat(ic, HandleUtility.GetHandleSize(ic)*0.08f, Vector3.zero, Handles.SphereHandleCap);
//             if (EditorGUI.EndChangeCheck())
//             {
//                 Undo.RecordObject(ws, "Move Intersection");
//                 var local = t.InverseTransformPoint(nip);
//                 Vector2 clamped = ClampCenterToArea(new Vector2(local.x, local.z), ia.size, ws.areaSize);
//                 ia.center = clamped;
//                 ws.intersections[i] = ia;
//                 EditorUtility.SetDirty(ws);
//             }

//             // NE resize
//             Vector2 ihalf = ia.size * 0.5f;
//             Vector3 ine = ic + new Vector3(ihalf.x, 0, ihalf.y);
//             Vector3 isw = ic - new Vector3(ihalf.x, 0, ihalf.y);

//             EditorGUI.BeginChangeCheck();
//             var ine2 = FreeMoveHandleCompat(ine, HandleUtility.GetHandleSize(ine)*0.08f, Vector3.zero, Handles.CubeHandleCap);
//             if (EditorGUI.EndChangeCheck())
//             {
//                 Undo.RecordObject(ws, "Resize Intersection");
//                 var neLocal = t.InverseTransformPoint(ine2);
//                 Vector2 neL = new Vector2(neLocal.x, neLocal.z);
//                 var swLocal = t.InverseTransformPoint(isw);
//                 Vector2 swL = new Vector2(swLocal.x, swLocal.z);
//                 Vector2 newSize = new Vector2(Mathf.Abs(neL.x - swL.x), Mathf.Abs(neL.y - swL.y));
//                 Vector2 newCenter = (neL + swL) * 0.5f;
//                 newCenter = ClampCenterToArea(newCenter, newSize, ws.areaSize);
//                 newSize = ClampSizeToArea(newCenter, newSize, ws.areaSize);
//                 ia.center = newCenter; ia.size = newSize;
//                 ws.intersections[i] = ia;
//                 EditorUtility.SetDirty(ws);
//             }

//             DrawIntersectionVisualization(ws, ia);
//         }
//     }

//     // ========= Visualization for RoadBands (crisp, thin overlay) =========
//     static void DrawRoadBandVisualization(CityWorkspaceSimple ws, CityWorkspaceSimple.RoadBand r)
//     {
//         var colCenter     = new Color(1f, 1f, 1f, 0.18f);
//         var colCarrEdge   = new Color(0.65f, 0.65f, 0.65f, 0.95f);
//         var colGutterEdge = new Color(0.25f, 0.65f, 1.00f, 0.95f);
//         var colCurbEdge   = new Color(0.95f, 0.35f, 0.75f, 0.95f);
//         var colFootEdge   = new Color(0.35f, 1.00f, 0.45f, 1.00f);
//         var colOverflow   = new Color(1.00f, 0.30f, 0.30f, 1.00f);

//         float y = ws.areaY;
//         Vector2 size = r.size;
//         bool longX = size.x >= size.y;

//         Vector3 c = ws.LocalXZToWorld(r.center, y);
//         Vector3 lDir = ws.transform.TransformDirection(longX ? Vector3.right  : Vector3.forward).normalized;
//         Vector3 sDir = ws.transform.TransformDirection(longX ? Vector3.forward: Vector3.right ).normalized;
//         float halfL  = (longX ? size.x : size.y) * 0.5f;
//         float halfW  = (longX ? size.y : size.x) * 0.5f;

//         float halfCar = r.carriagewayWidth * 0.5f;
//         float gEdge   = halfCar + r.gutterWidth;
//         float cEdge   = gEdge   + r.curbWidth;
//         float fEdge   = cEdge   + r.footpathWidth;
//         float totalAcross = 2f * fEdge;
//         float bandWidth   = 2f * halfW;

//         DrawThinOverlayLine(c - lDir * halfL, c + lDir * halfL, colCenter, lift: 0.02f, px: 1.0f, alwaysOnTop: true, aliased: false);

//         void EdgePair(float offset, Color baseColor)
//         {
//             bool tooWide = offset > halfW + 1e-4f;
//             float o = Mathf.Min(offset, halfW);
//             var color = tooWide ? colOverflow : baseColor;

//             Vector3 a1 = c - lDir * halfL + sDir * (+o);
//             Vector3 a2 = c + lDir * halfL + sDir * (+o);
//             Vector3 b1 = c - lDir * halfL + sDir * (-o);
//             Vector3 b2 = c + lDir * halfL + sDir * (-o);

//             DrawThinOverlayLine(a1, a2, color, lift: 0.02f, px: 1.0f, alwaysOnTop: true, aliased: false);
//             DrawThinOverlayLine(b1, b2, color, lift: 0.02f, px: 1.0f, alwaysOnTop: true, aliased: false);
//         }

//         EdgePair(halfCar, colCarrEdge);
//         EdgePair(gEdge,   colGutterEdge);
//         EdgePair(cEdge,   colCurbEdge);
//         EdgePair(fEdge,   colFootEdge);

//         Vector3 left  = c - sDir * halfW;
//         Vector3 right = c + sDir * halfW;
//         DrawThinOverlayLine(left, right, new Color(1f,1f,1f,0.25f), lift: 0.02f, px: 1.0f, alwaysOnTop: true, aliased: false);

//         var style = new GUIStyle(EditorStyles.miniBoldLabel)
//         {
//             alignment = TextAnchor.MiddleCenter,
//             normal = { textColor = (totalAcross <= bandWidth) ? new Color(0.55f, 1f, 0.55f, 1f)
//                                                               : new Color(1f, 0.4f, 0.4f, 1f) }
//         };
//         string verdict = (totalAcross <= bandWidth) ? "OK" : "TOO WIDE";
//         Handles.Label(c + Vector3.up * 0.35f,
//             $"band {bandWidth:F2} m   |   cross {totalAcross:F2} m  [{verdict}]",
//             style);

//         float textLift = 0.15f;
//         var mini = new GUIStyle(EditorStyles.miniLabel)
//         {
//             alignment = TextAnchor.MiddleLeft,
//             normal = { textColor = new Color(0.92f, 0.92f, 0.92f, 0.95f) }
//         };

//         void MidLabel(float a, float b, string txt)
//         {
//             float mid = Mathf.Clamp((a + b) * 0.5f, 0, halfW);
//             Vector3 pos = c + sDir * mid + Vector3.up * textLift;
//             Handles.Label(pos, txt, mini);
//         }

//         MidLabel(0f,      halfCar, $"carriage {r.carriagewayWidth:F2}m");
//         MidLabel(halfCar, gEdge,   $"gutter {r.gutterWidth:F2}m");
//         MidLabel(gEdge,   cEdge,   $"curb {r.curbWidth:F2}m");
//         MidLabel(cEdge,   fEdge,   $"footpath {r.footpathWidth:F2}m");
//     }

//     // ========= Visualization for IntersectionAreas (per-side cross-sections + correct corner joins) =========
//  static void DrawIntersectionVisualization(CityWorkspaceSimple ws, CityWorkspaceSimple.IntersectionArea ia)
// {
//     var colCurbEdge = new Color(0.95f, 0.35f, 0.75f, 0.95f);

//     Vector3 center      = ws.LocalXZToWorld(ia.center, 0f);
//     Vector2 halfExtents = ia.size * 0.5f;

//     // --- same cross-section math as RoadBandVisualization ---
//     float halfCar = ia.carriagewayWidth * 0.5f;
//     float gEdge   = halfCar + ia.gutterWidth;
//     float cEdge   = gEdge   + ia.curbWidth;       // curb outer face (this is what you’re drawing)
//     float fEdge   = cEdge   + ia.footpathWidth;   // (optional) footpath outer for checks/labels

//     // Clamp per-axis to the available half-size
//     float curbOffsetX = Mathf.Min(cEdge, halfExtents.x); // used on North/South (offset along X)
//     float curbOffsetZ = Mathf.Min(cEdge, halfExtents.y); // used on East/West  (offset along Z)

//     // Helper (your existing version kept intact)
//     static void DrawEdgeCurbLines(Vector3 center, Vector2 halfExtents, float curbOffset, string edge, Color color)
//     {
//         switch (edge)
//         {
//             case "South":
//             {
//                 float z = center.z - halfExtents.y;
//                 float depth = halfExtents.y;
//                 Vector3 eastStart = new(center.x + curbOffset, center.y, z);
//                 Vector3 eastEnd   = new(eastStart.x, eastStart.y, eastStart.z + depth);
//                 Vector3 westStart = new(center.x - curbOffset, center.y, z);
//                 Vector3 westEnd   = new(westStart.x, westStart.y, westStart.z + depth);
//                 DrawThinOverlayLine(eastStart, eastEnd, color);
//                 DrawThinOverlayLine(westStart, westEnd, color);
//                 break;
//             }
//             case "North":
//             {
//                 float z = center.z + halfExtents.y;
//                 float depth = halfExtents.y;
//                 Vector3 eastStart = new(center.x + curbOffset, center.y, z);
//                 Vector3 eastEnd   = new(eastStart.x, eastStart.y, eastStart.z - depth);
//                 Vector3 westStart = new(center.x - curbOffset, center.y, z);
//                 Vector3 westEnd   = new(westStart.x, westStart.y, westStart.z - depth);
//                 DrawThinOverlayLine(eastStart, eastEnd, color);
//                 DrawThinOverlayLine(westStart, westEnd, color);
//                 break;
//             }
//             case "East":
//             {
//                 float x = center.x + halfExtents.x;
//                 float depth = halfExtents.x;
//                 Vector3 northStart = new(x, center.y, center.z + curbOffset);
//                 Vector3 northEnd   = new(northStart.x - depth, northStart.y, northStart.z);
//                 Vector3 southStart = new(x, center.y, center.z - curbOffset);
//                 Vector3 southEnd   = new(southStart.x - depth, southStart.y, southStart.z);
//                 DrawThinOverlayLine(northStart, northEnd, color);
//                 DrawThinOverlayLine(southStart, southEnd, color);
//                 break;
//             }
//             case "West":
//             {
//                 float x = center.x - halfExtents.x;
//                 float depth = halfExtents.x;
//                 Vector3 northStart = new(x, center.y, center.z + curbOffset);
//                 Vector3 northEnd   = new(northStart.x + depth, northStart.y, northStart.z);
//                 Vector3 southStart = new(x, center.y, center.z - curbOffset);
//                 Vector3 southEnd   = new(southStart.x + depth, southStart.y, southStart.z);
//                 DrawThinOverlayLine(northStart, northEnd, color);
//                 DrawThinOverlayLine(southStart, southEnd, color);
//                 break;
//             }
//         }
//     }

//     // Draw curb (outer) on all edges
//     DrawEdgeCurbLines(center, halfExtents, curbOffsetX, "North", colCurbEdge);
//     DrawEdgeCurbLines(center, halfExtents, curbOffsetX, "South", colCurbEdge);
//     DrawEdgeCurbLines(center, halfExtents, curbOffsetZ, "East",  colCurbEdge);
//     DrawEdgeCurbLines(center, halfExtents, curbOffsetZ, "West",  colCurbEdge);

//     // Corner markers for the curb rectangle
//     Vector3 ne = new(center.x + curbOffsetX, center.y, center.z + curbOffsetZ);
//     Vector3 nw = new(center.x - curbOffsetX, center.y, center.z + curbOffsetZ);
//     Vector3 se = new(center.x + curbOffsetX, center.y, center.z - curbOffsetZ);
//     Vector3 sw = new(center.x - curbOffsetX, center.y, center.z - curbOffsetZ);
//     DrawMarker(ne, colCurbEdge); DrawMarker(nw, colCurbEdge);
//     DrawMarker(se, colCurbEdge); DrawMarker(sw, colCurbEdge);
// }
    
//     static void DrawMarker(Vector3 p, Color color)
//     {
//     #if UNITY_EDITOR
//         var prev = Handles.color;
//         Handles.color = color;
//         float size = HandleUtility.GetHandleSize(p) * 0.06f; // tweak scale
//         Handles.SphereHandleCap(0, p, Quaternion.identity, size, EventType.Repaint);
//         Handles.color = prev;
//     #endif
//     }



//         // Vector3 southEdgeCurbEastStart = new(center.x + curbOffsetX, center.y, southEdgeZ);
//     // Vector3 southEdgeCurbEastEnd    = new(southEdgeCurbEastStart.x, southEdgeCurbEastStart.y, southEdgeCurbEastStart.z + halfDepth);
//     // DrawThinOverlayLine(southEdgeCurbEastStart, southEdgeCurbEastEnd, colCurbEdge);

//     // Vector3 southEdgeCurbWestStart = new(center.x - curbOffsetX, center.y, southEdgeZ);
//     // Vector3 southEdgeCurbWestEnd   = new(southEdgeCurbWestStart .x, southEdgeCurbWestStart .y, southEdgeCurbWestStart .z + halfDepth);
//     // DrawThinOverlayLine(southEdgeCurbWestStart, southEdgeCurbWestEnd, colCurbEdge);
//     //}



//     // ------ Helpers (drawing, handles, clamps, baking) ------
//     static void DrawRectWireThinOverlay(Vector3 center, Vector2 size, Color col, float lift = 0.02f, float px = 1.0f)
//     {
//         float hx = size.x * 0.5f, hz = size.y * 0.5f;
//         Vector3 bl = center + new Vector3(-hx, 0, -hz);
//         Vector3 br = center + new Vector3(+hx, 0, -hz);
//         Vector3 tr = center + new Vector3(+hx, 0, +hz);
//         Vector3 tl = center + new Vector3(-hx, 0, +hz);

//         DrawThinOverlayLine(bl, br, col, lift, px, alwaysOnTop: true, aliased: false);
//         DrawThinOverlayLine(br, tr, col, lift, px, alwaysOnTop: true, aliased: false);
//         DrawThinOverlayLine(tr, tl, col, lift, px, alwaysOnTop: true, aliased: false);
//         DrawThinOverlayLine(tl, bl, col, lift, px, alwaysOnTop: true, aliased: false);
//     }

//     static void DrawThinOverlayLine(Vector3 a, Vector3 b, Color c, float lift = 0.02f, float px = 1.0f, bool alwaysOnTop = true, bool aliased = false)
//     {
//         a += Vector3.up * lift;
//         b += Vector3.up * lift;

//         var oldTest = Handles.zTest;
//         if (alwaysOnTop) Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

//         var oldColor = Handles.color;
//         Handles.color = c;

//         if (aliased) Handles.DrawLine(a, b);
//         else         Handles.DrawAAPolyLine(px, a, b);

//         Handles.color = oldColor;
//         Handles.zTest = oldTest;
//     }

//     static Vector2 ClampCenterToArea(Vector2 center, Vector2 size, Vector2 areaSize)
//     {
//         Vector2 half = size * 0.5f;
//         Vector2 limit = areaSize * 0.5f - half;
//         return new Vector2(
//             Mathf.Clamp(center.x, -limit.x, +limit.x),
//             Mathf.Clamp(center.y, -limit.y, +limit.y)
//         );
//     }

//     static Vector2 ClampSizeToArea(Vector2 center, Vector2 size, Vector2 areaSize)
//     {
//         Vector2 halfArea = areaSize * 0.5f;
//         Vector2 maxHalf = new Vector2(
//             Mathf.Min(halfArea.x - Mathf.Abs(center.x), size.x * 0.5f + 9999f),
//             Mathf.Min(halfArea.y - Mathf.Abs(center.y), size.y * 0.5f + 9999f)
//         );
//         maxHalf = new Vector2(Mathf.Max(0.1f, maxHalf.x), Mathf.Max(0.1f, maxHalf.y));
//         return new Vector2(Mathf.Min(size.x * 0.5f, maxHalf.x) * 2f,
//                            Mathf.Min(size.y * 0.5f, maxHalf.y) * 2f);
//     }

//     static Vector3 FreeMoveHandleCompat(Vector3 pos, float size, Vector3 snap, Handles.CapFunction cap)
//     {
// #if UNITY_2022_1_OR_NEWER
//         return Handles.FreeMoveHandle(pos, size, snap, cap);
// #else
//         return Handles.FreeMoveHandle(pos, Quaternion.identity, size, snap, cap);
// #endif
//     }

//     static void BakePreviewToAssets(CityWorkspaceSimple ws, string folder)
//     {
//         if (!ws.previewRoot)
//         {
//             if (!EditorUtility.DisplayDialog("No Preview", "Generate Preview first?", "Generate", "Cancel"))
//                 return;
//             ws.GeneratePreview();
//         }

//         if (!AssetDatabase.IsValidFolder(folder))
//         {
//             Directory.CreateDirectory(folder);
//             AssetDatabase.Refresh();
//         }

//         var baked = new GameObject("BAKED");
//         baked.transform.SetParent(ws.transform, false);

//         int counter = 0;
//         foreach (var mf in ws.previewRoot.GetComponentsInChildren<MeshFilter>())
//         {
//             if (!mf.sharedMesh) continue;

//             var src = mf.gameObject;
//             var copy = new GameObject(src.name);
//             copy.transform.SetParent(baked.transform, false);
//             copy.transform.position = mf.transform.position;
//             copy.transform.rotation = mf.transform.rotation;
//             copy.transform.localScale = mf.transform.localScale;
//             copy.isStatic = true;

//             var mf2 = copy.AddComponent<MeshFilter>();
//             var mr2 = copy.AddComponent<MeshRenderer>();

//             var mesh = Object.Instantiate(mf.sharedMesh);
//             string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{ws.name}_m{counter++}.asset");
//             AssetDatabase.CreateAsset(mesh, path);
//             mf2.sharedMesh = mesh;

//             var mr = src.GetComponent<MeshRenderer>();
//             if (mr) mr2.sharedMaterials = mr.sharedMaterials;

//             var mc = copy.AddComponent<MeshCollider>();
//             mc.sharedMesh = mesh;
// #if UNITY_2020_2_OR_NEWER
//             mc.cookingOptions = MeshColliderCookingOptions.EnableMeshCleaning |
//                                 MeshColliderCookingOptions.WeldColocatedVertices |
//                                 MeshColliderCookingOptions.UseFastMidphase;
// #endif
//         }
//         AssetDatabase.SaveAssets();
//         Debug.Log($"Baked {counter} meshes into '{folder}' and created 'BAKED'.");
//     }
// }
