using System.Collections.Generic;
using TVGL;



namespace TVGLUnitTestsAndBenchmarking
{

    internal static class AllPolygonMethods
    {
        private static void Run()
        {
            #region Polygon Functions
            var poly1 = new Polygon(TestCases.MakeCircularPolygon(4, 4));
            double area = poly1.Area;
            var poly2 = new Polygon(TestCases.MakeCircularPolygon(3, 3));
            poly1.AddInnerPolygon(new Polygon(TestCases.MakeCircularPolygon(3, 3)));
            poly1.BoundingRectangle();
            poly1.ConvertTo3DLocations(Vector3.UnitX, 1.0);
            poly1.CreateConvexHull(out _);
            poly1.ConvertTo3DLocations(Vector3.UnitX, 1.0);
            var tessFaces = poly1.ExtrusionFacesFrom2DPolygons(Vector3.UnitX, 1.0, 4.3);
            poly1.GetPolygonInteraction(poly2);

            bool isItTrueThat = poly1.IsCircular(out var minCircle);
            isItTrueThat = poly1.IsConvex;
            isItTrueThat = poly1.IsPositive;
            var poly3 = new Polygon(TestCases.MakeCircularPolygon(5, 5));
            var intersections = poly1.GetPolygonInteraction(poly2);
            var lines = poly1.Edges;
            var extrema = poly1.MaxX;
            extrema = poly1.MaxY;
            extrema = poly1.MinX;
            extrema = poly1.MinY;
            List<Vertex2D> points = poly1.Vertices;
            poly1.Reverse();
            #endregion

            List<Vector2> a = poly1.Path;

            #region IEnumerable<Vector2>
            a.Area();
            a.BoundingRectangle();
            a.ConvertTo3DLocations(Vector3.UnitX, 1.0);
            ConvexHull2D.Create(a, out _);
            a.CreateConvexHullMaximal(out _);
            var b = TestCases.MakeCircularPolygon(5, 5);
            //a.Difference(b);
            var length = a.GetLengthAndExtremePoints(new Vector2(1, 1), out List<Vector2> bottomPoints,
                out List<Vector2> topPoints);
            //a.Intersection(b);
            a.IsRectangular(out var dimensions);
            a.MinimumCircle();
            //a.OffsetMiter(5.0);
            //a.OffsetRound(5.0);
            //a.OffsetSquare(5.0);
            a.Perimeter();
            //a.SimplifyFast();
            //a.SimplifyMinLength(1e-3);
            //a.SimplifyMinLength(10);
            //a.SimplifyByAreaChange(1e-3);
            //a.SimplifyByAreaChange(10);
            //a.Union(b);
            //a.Xor(b);
            #endregion

            #region IEnumerable<IEnumerable<Vector2>>
            var c = (IEnumerable<IEnumerable<Vector2>>)(new[] { TestCases.MakeCircularPolygon(4, 4) });
            var d = (IEnumerable<IEnumerable<Vector2>>)(new[] { TestCases.MakeCircularPolygon(5, 5) });
            c.Area();
            c.Create2DMedialAxis();
            //c.Difference(d);
            //c.Intersection(d);
            //c.OffsetMiter(5.0);
            //c.OffsetRound(5.0);
            //c.OffsetSquare(5.0);
            c.Perimeter();
            c.ExtrusionFacesFrom2DPolygons(Vector3.UnitX, 1.0, 4.0);
            //a.SimplifyFast();
            //a.SimplifyMinLength(1e-3);
            //a.SimplifyMinLength(10);
            //a.SimplifyByAreaChange(1e-3);
            //a.SimplifyByAreaChange(10);
            c.SliceAtLine(Vector2.UnitX, 1.0, out var negativeSidePolys, out var positiveSidePolys);
            //c.Triangulate(out var groupOfLoops, out var isPositive);
            //c.Union(b);
            //c.Xor(b);
            #endregion


        }
    }
}