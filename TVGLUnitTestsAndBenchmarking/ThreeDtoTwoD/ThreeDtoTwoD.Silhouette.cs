﻿using System;
using System.IO;
using System.Linq;
using TVGL;

namespace TVGLUnitTestsAndBenchmarking
{
    public  static partial class TVGL3Dto2DTests
    {
        static Random r = new Random(10);
        static double r100 => 200.0 * r.NextDouble() - 100.0;

        //[Fact]
        public static void TestSilhouette()
        {
            var dir = new DirectoryInfo(".");
            while (!Directory.Exists(dir.FullName + Path.DirectorySeparatorChar + "TestFiles"))
                dir = dir.Parent;
            dir = new DirectoryInfo(dir.FullName + Path.DirectorySeparatorChar + "TestFiles");

            var fileNames = dir.GetFiles("*").ToArray();
            for (var i = 6; i < fileNames.Length - 0; i++)
            {
                //var filename = FileNames[i];
                var filename = fileNames[i].FullName;
                if (Path.GetExtension(filename) != ".stl") continue;
                var name = fileNames[i].Name;
                Console.WriteLine("Attempting: " + filename);
                var solid = (TessellatedSolid)IO.Open(filename);
                Presenter.ShowAndHang(solid);
                if (solid.Errors != null)
                {
                    Console.WriteLine("    ===>" + filename + " has errors: " + solid.Errors.ToString());
                    continue;
                }
                if (name.Contains("yCastin")) continue;

                for (int j = 0; j < 3; j++)
                {
                    var direction = Vector3.UnitVector((CartesianDirections)j);
                    //var direction = new Vector3(r100, r100, r100);
                    Console.WriteLine(direction[0] + ", " + direction[1] + ", " + direction[2]);

                    var silhouette = solid.CreateSilhouette(direction);
                    Presenter.ShowAndHang(silhouette);
                    solid.Vertices.GetLengthAndExtremeVertex(direction, out var btmVertex, out var topVertex);
                    var plane = new Plane(btmVertex.Coordinates.Lerp(topVertex.Coordinates, r.NextDouble()), direction);
                }
            }

        }

    }
}