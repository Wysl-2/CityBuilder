using UnityEngine;


namespace MeshBuilder.Primitives
{
  public enum PlaneOrientation { XZ, XY, YZ }

  public static class MeshPrimitives 
  {
      /// <summary>
      /// Creates a quad in the specified plane with given size, centered at pivot.
      /// </summary>
      /// <param name="size">Width and height of the quad (x,y for XZ/XY, x,z for YZ).</param>
      /// <param name="pivot">Center point of the quad.</param>
      /// <param name="orientation">Plane of the quad (XZ, XY, YZ).</param>
      /// <param name="winding">Vertex winding order (CCW or CW).</param>
      /// <returns>Array of 4 vertices: (0,0), (w,0), (w,h), (0,h) in the specified plane.</returns>
      public static Vector3[] CreateQuad(Vector2 size, Vector3 pivot, PlaneOrientation orientation = PlaneOrientation.XZ, Winding winding = Winding.CW)
      {
          if (size.x <= 0 || size.y <= 0) throw new System.ArgumentException("Size must have positive components.", nameof(size));

          Vector3[] vertices = new Vector3[4];
          float w = size.x * 0.5f;
          float h = size.y * 0.5f;

          switch (orientation)
          {
              case PlaneOrientation.XZ:
                  vertices[0] = pivot + new Vector3(-w, 0, -h); // (0,0,0)
                  vertices[1] = pivot + new Vector3(w, 0, -h);  // (1,0,0)
                  vertices[2] = pivot + new Vector3(w, 0, h);   // (1,0,1)
                  vertices[3] = pivot + new Vector3(-w, 0, h);  // (0,0,1)
                  break;
              case PlaneOrientation.XY:
                  vertices[0] = pivot + new Vector3(-w, -h, 0);
                  vertices[1] = pivot + new Vector3(w, -h, 0);
                  vertices[2] = pivot + new Vector3(w, h, 0);
                  vertices[3] = pivot + new Vector3(-w, h, 0);
                  break;
              case PlaneOrientation.YZ:
                  vertices[0] = pivot + new Vector3(0, -h, -w);
                  vertices[1] = pivot + new Vector3(0, -h, w);
                  vertices[2] = pivot + new Vector3(0, h, w);
                  vertices[3] = pivot + new Vector3(0, h, -w);
                  break;
          }

          if (winding == Winding.CCW)
              System.Array.Reverse(vertices, 1, 3); // Swap 1,2,3 to reverse order

          return vertices;
      }

      /// <summary>
      /// Creates a triangle in the specified plane with given vertices, centered at pivot.
      /// </summary>
      /// <param name="size">Width and height to scale the triangle.</param>
      /// <param name="pivot">Center point of the triangle.</param>
      /// <param name="orientation">Plane of the triangle (XZ, XY, YZ).</param>
      /// <param name="winding">Vertex winding order (CCW or CW).</param>
      /// <returns>Array of 3 vertices forming an equilateral triangle or scaled variant.</returns>
      public static Vector3[] CreateTriangle(Vector2 size, Vector3 pivot, PlaneOrientation orientation = PlaneOrientation.XZ, Winding winding = Winding.CW)
      {
          if (size.x <= 0 || size.y <= 0) throw new System.ArgumentException("Size must have positive components.", nameof(size));

          Vector3[] vertices = new Vector3[3];
          float w = size.x * 0.5f;
          float h = size.y * 0.866f; // Approx height of equilateral triangle

          switch (orientation)
          {
              case PlaneOrientation.XZ:
                  vertices[0] = pivot + new Vector3(0, 0, h * 0.5f);      // Top
                  vertices[1] = pivot + new Vector3(-w, 0, -h * 0.5f);     // Bottom-left
                  vertices[2] = pivot + new Vector3(w, 0, -h * 0.5f);      // Bottom-right
                  break;
              case PlaneOrientation.XY:
                  vertices[0] = pivot + new Vector3(0, h * 0.5f, 0);
                  vertices[1] = pivot + new Vector3(-w, -h * 0.5f, 0);
                  vertices[2] = pivot + new Vector3(w, -h * 0.5f, 0);
                  break;
              case PlaneOrientation.YZ:
                  vertices[0] = pivot + new Vector3(0, h * 0.5f, 0);
                  vertices[1] = pivot + new Vector3(0, -h * 0.5f, -w);
                  vertices[2] = pivot + new Vector3(0, -h * 0.5f, w);
                  break;
          }

          if (winding == Winding.CCW)
              System.Array.Reverse(vertices, 1, 2); // Swap 1,2 to reverse order

          return vertices;
      }
  }
}
