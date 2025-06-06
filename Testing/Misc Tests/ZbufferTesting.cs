﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TVGL;

namespace TVGLUnitTestsAndBenchmarking.Misc_Tests
{
    internal static class ZbufferTesting
    {
        internal static void Test1()
        {
            DirectoryInfo dir = IO.BackoutToFolder(Program.inputFolder);
            foreach (var fileName in dir.GetFiles("*").Skip(0))
            {
                Console.WriteLine("\n\n\nAttempting to open: " + fileName.Name);
                IO.Open(fileName.FullName, out TessellatedSolid solid);
                if (solid == null) continue;
                //Presenter.ShowAndHang(solid);
                var direction = Vector3.UnitY;
                //var direction = new Vector3(1, 1, 1).Normalize();
                var (minD, maxD) = solid.Vertices.GetDistanceToExtremeVertex(direction, out _, out _);
                var displacement = (minD - maxD) * direction;
                //Console.Write("zbuffer start...");
                var sw = Stopwatch.StartNew();
                var zbuffer = ZBuffer.Run(solid, direction, 500);
                sw.Stop();
                Console.WriteLine(sw.Elapsed.Ticks);
                //Console.WriteLine("end:  "+sw.Elapsed);
                //continue; 
                var paths = new List<List<Vector3>>();
                for (int i = 0; i < zbuffer.XCount; i++)
                {
                    var xLine = new List<Vector3>();
                    for (int j = 0; j < zbuffer.YCount; j++)
                        xLine.Add(displacement + zbuffer.Get3DPoint(i, j));
                    paths.Add(xLine);
                }
                for (int i = 0; i < zbuffer.YCount; i++)
                {
                    var yLine = new List<Vector3>();
                    for (int j = 0; j < zbuffer.XCount; j++)
                        yLine.Add(displacement + zbuffer.Get3DPoint(j, i));
                    paths.Add(yLine);
                }
                var colors = paths.Select(c => new Color(KnownColors.DodgerBlue));
                Presenter.ShowAndHang([paths], [true], [1], colors, solid);
            }
        }

        internal static void Test2()
        {
            DirectoryInfo dir = IO.BackoutToFolder(Program.inputFolder);
            foreach (var fileName in dir.GetFiles("*").Skip(20))
            {
                //Console.WriteLine("\n\n\nAttempting to open: " + fileName.Name);
                IO.Open(fileName.FullName, out TessellatedSolid solid, TessellatedSolidBuildOptions.Default);
                if (solid == null) continue;
                //Presenter.ShowAndHang(solid);
                var axis = Vector3.UnitX;
                var anchor = solid.Center;
                //var direction = new Vector3(1, 1, 1).Normalize();
                var (minD, maxD) = solid.Vertices.GetDistanceToExtremeVertex(axis, out _, out _);
                //Console.Write("zbuffer start...");
                var visibleFaces = new List<TriangleFace>();
                var walls = new List<TriangleFace>();
                foreach (var face in solid.Faces)
                {
                    if (face.Normal.IsAlignedOrReverse(axis, Constants.DotToleranceForSame) ||
                        face.Normal.IsPerpendicular(face.Center - anchor, Constants.DotToleranceOrthogonal))
                        walls.Add(face);
                    else if (face.Normal.Dot(face.Center - anchor) > 0)
                        visibleFaces.Add(face);
                }
                solid.HasUniformColor = false;
                solid.ResetDefaultColor();
                foreach (var face in walls)
                    face.Color = new Color(KnownColors.Blue);
                foreach (var face in visibleFaces)
                    face.Color = new Color(KnownColors.Green);
                visibleFaces[0].Color = new Color(KnownColors.Lime);
                //Presenter.ShowAndHang(solid);
                var sw = Stopwatch.StartNew();
                var zbuffer = CylindricalBuffer.Run(solid, axis, anchor, 500); //,visibleFaces);
                sw.Stop();
                Console.WriteLine(sw.Elapsed.Ticks);
                //Console.WriteLine("end:  "+sw.Elapsed);
                //continue; 
                var paths = new List<List<Vector3>>();
                for (int i = 0; i < zbuffer.XCount; i++)
                {
                    var xLine = new List<Vector3>();
                    for (int j = 0; j < zbuffer.YCount; j++)
                        xLine.Add(zbuffer.Get3DPoint(i, j, 0));
                    paths.Add(xLine);
                }
                for (int i = 0; i < zbuffer.YCount; i++)
                {
                    var yLine = new List<Vector3>();
                    for (int j = 0; j < zbuffer.XCount; j++)
                        yLine.Add(zbuffer.Get3DPoint(j, i, 0));
                    yLine.Add(yLine[0]);
                    paths.Add(yLine);
                }
                var colors = paths.Select(c => new Color(KnownColors.DodgerBlue));
                Presenter.ShowAndHang([paths], [true], [1], colors, solid);
            }
        }

        internal static void Test3()
        {
            DirectoryInfo dir = IO.BackoutToFolder(Program.inputFolder);
            foreach (var fileName in dir.GetFiles("").Skip(40))
            {
                //Console.WriteLine("\n\n\nAttempting to open: " + fileName.Name);
                IO.Open(fileName.FullName, out TessellatedSolid solid, TessellatedSolidBuildOptions.Default);
                if (solid == null) continue; ;
                var sw = Stopwatch.StartNew();
                var zbuffer = SphericalBuffer.Run(solid, solid.Center, 1000);
                sw.Stop();
                Console.WriteLine(sw.Elapsed.Ticks);
                //Console.WriteLine("end:  "+sw.Elapsed);
                //continue; 
                var paths = new List<List<Vector3>>();
                for (int i = 0; i < zbuffer.XCount; i++)
                {
                    var xLine = new List<Vector3>();
                    for (int j = 0; j < zbuffer.YCount; j++)
                        xLine.Add(zbuffer.Get3DPoint(i, j, 0));
                    paths.Add(xLine);
                }
                for (int i = 0; i < zbuffer.YCount; i++)
                {
                    var yLine = new List<Vector3>();
                    for (int j = 0; j < zbuffer.XCount; j++)
                        yLine.Add(zbuffer.Get3DPoint(j, i, 0));
                    yLine.Add(yLine[0]);
                    paths.Add(yLine);
                }
                var colors = paths.Select(c => new Color(KnownColors.DodgerBlue));
                Presenter.ShowAndHang([paths], [true], [1], colors, solid);
            }
        }

    }
}
