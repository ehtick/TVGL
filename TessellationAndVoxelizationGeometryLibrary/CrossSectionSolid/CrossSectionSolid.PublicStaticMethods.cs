﻿// ***********************************************************************
// Assembly         : TessellationAndVoxelizationGeometryLibrary
// Author           : matth
// Created          : 04-03-2023
//
// Last Modified By : matth
// Last Modified On : 04-03-2023
// ***********************************************************************
// <copyright file="CrossSectionSolid.PublicStaticMethods.cs" company="Design Engineering Lab">
//     2014
// </copyright>
// <summary></summary>
// ***********************************************************************
using System;
using System.Collections.Generic;
using System.Linq;

namespace TVGL
{
    /// <summary>
    /// Class CrossSectionSolid.
    /// Implements the <see cref="TVGL.Solid" />
    /// </summary>
    /// <seealso cref="TVGL.Solid" />
    public partial class CrossSectionSolid : Solid
    {

        /// <summary>
        /// Creates from tessellated solid.
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="numberOfLayers">The number of layers.</param>
        /// <returns>CrossSectionSolid.</returns>
        public static CrossSectionSolid CreateFromTessellatedSolid(TessellatedSolid ts, CartesianDirections direction, int numberOfLayers)
        {
            var intDir = Math.Abs((int)direction) - 1;
            var max = intDir == 0 ? ts.Bounds[1].X : intDir == 1 ? ts.Bounds[1].Y : ts.Bounds[1].Z;
            var min = intDir == 0 ? ts.Bounds[0].X : intDir == 1 ? ts.Bounds[0].Y : ts.Bounds[0].Z;
            var lengthAlongDir = max - min;
            var stepSize = lengthAlongDir / numberOfLayers;

            var bounds = new[] { ts.Bounds[0], ts.Bounds[1] };

            var layers = ts.GetUniformlySpacedCrossSections(direction, out var stepDistances, out _, out _, min + 0.5 * stepSize, numberOfLayers, stepSize);
            var layerDict = new Dictionary<int, IList<Polygon>>();
            for (int i = 0; i < layers.Length; i++)
                layerDict.Add(i, layers[i]);
            var directionVector = Vector3.UnitVector(direction);
            var stepDistanceDictionary = new Dictionary<int, double>();
            for (int i = 0; i < stepDistances.Length; i++)
                stepDistanceDictionary.Add(i, stepDistances[i]);
            return new CrossSectionSolid(directionVector, stepDistanceDictionary, ts.SameTolerance, layerDict, bounds, ts.Units);
        }


        /// <summary>
        /// Creates from tessellated solid.
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="numberOfLayers">The number of layers.</param>
        /// <returns>CrossSectionSolid.</returns>
        public static CrossSectionSolid CreateFromTessellatedSolid(TessellatedSolid ts, Vector3 direction, int numberOfLayers)
        {
            direction = direction.Normalize();
            var (min, max) = ts.Vertices.GetDistanceToExtremeVertex(direction, out _, out _);
            var lengthAlongDir = max - min;
            var stepSize = lengthAlongDir / numberOfLayers;
            var stepDistances = new Dictionary<int, double>();
            //var stepDistances = new double[numberOfLayers];
            stepDistances.Add(0, min + 0.5 * stepSize);
            //stepDistances[0] = ts.Bounds[0][intDir] + 0.5 * stepSize;
            for (int i = 1; i < numberOfLayers; i++)
                stepDistances.Add(i, stepDistances[i - 1] + stepSize);
            //stepDistances[i] = stepDistances[i - 1] + stepSize;
            var bounds = new[] { ts.Bounds[0].Copy(), ts.Bounds[1].Copy() };

            var layers = ts.GetUniformlySpacedCrossSections(direction, out _, out _, out _, stepDistances[0], numberOfLayers, stepSize);
            var layerDict = new Dictionary<int, IList<Polygon>>();
            for (int i = 0; i < layers.Length; i++)
                layerDict.Add(i, layers[i]);
            return new CrossSectionSolid(direction, stepDistances, ts.SameTolerance, layerDict, bounds, ts.Units);
        }

        /// <summary>
        /// Creates the uniform cross section solid.
        /// </summary>
        /// <param name="buildDirection">The build direction.</param>
        /// <param name="distanceOfPlane">The distance of plane.</param>
        /// <param name="extrudeThickness">The extrude thickness.</param>
        /// <param name="shape">The shape.</param>
        /// <param name="sameTolerance">The same tolerance.</param>
        /// <param name="units">The units.</param>
        /// <returns>CrossSectionSolid.</returns>
        public static CrossSectionSolid CreateConstantCrossSectionSolid(Vector3 buildDirection, double distanceOfPlane, double extrudeThickness,
            IEnumerable<Polygon> shape, double sameTolerance, UnitType units)
        {
            var shapeList = shape as IList<Polygon> ?? shape.ToList();
            var stepDistances = new Dictionary<int, double> { { 0, distanceOfPlane }, { 1, distanceOfPlane + extrudeThickness } };
            var layers2D = new Dictionary<int, IList<Polygon>> { { 0, shapeList }, { 1, shapeList } };
            return new CrossSectionSolid(buildDirection, stepDistances, sameTolerance, layers2D, null, units);
        }


        /// <summary>
        /// Creates the uniform cross section solid.
        /// </summary>
        /// <param name="buildDirection">The build direction.</param>
        /// <param name="distanceOfPlane">The distance of plane.</param>
        /// <param name="extrudeThickness">The extrude thickness.</param>
        /// <param name="shape">The shape.</param>
        /// <param name="sameTolerance">The same tolerance.</param>
        /// <param name="units">The units.</param>
        /// <returns>CrossSectionSolid.</returns>
        public static CrossSectionSolid CreateConstantCrossSectionSolid(Vector3 buildDirection, double distanceOfPlane, double extrudeThickness,
            Polygon shape, double sameTolerance, UnitType units)
        {
            var stepDistances = new Dictionary<int, double> { { 0, distanceOfPlane }, { 1, distanceOfPlane + extrudeThickness } };
            var layers2D = new Dictionary<int, IList<Polygon>> { { 0, new[] { shape } }, { 1, new[] { shape } } };
            return new CrossSectionSolid(buildDirection, stepDistances, sameTolerance, layers2D, null, units);
        }


        /// <summary>
        /// Gets the cross sections as3 d loops.
        /// </summary>
        /// <returns>Vector3[][][].</returns>
        public Vector3[][][] GetCrossSectionsAs3DLoops()
        {
            var result = new Vector3[Layer2D.Count][][];
            int k = 0;
            foreach (var layerKeyValuePair in Layer2D)
            {
                var index = layerKeyValuePair.Key;
                var zValue = StepDistances[index];

                //Check that the loop does not contain any duplicate points
                var skipHoles = false;
                var layer2DLoops = layerKeyValuePair.Value;
                if (ContainsDuplicatePoints(layer2DLoops))
                {
                    //try offsetting in by a small amount
                    layer2DLoops = PolygonOperations.OffsetSquare(layer2DLoops, -Constants.LineSlopeTolerance, Constants.LineSlopeTolerance);
                    //Offsetting did not work, so try offsetting by a larger amount.
                    if (ContainsDuplicatePoints(layer2DLoops))
                    {
                        layer2DLoops = PolygonOperations.OffsetSquare(layer2DLoops, -Constants.LineSlopeTolerance * 10, Constants.LineSlopeTolerance);
                        //If still no luck, then skip the inner loops
                        skipHoles = ContainsDuplicatePoints(layer2DLoops);
                    }
                }

                var numLoops = layer2DLoops.Sum(poly => 1 + poly.NumberOfInnerPolygons);
                var layer = new Vector3[numLoops][];
                result[k++] = layer;
                int j = 0;
                foreach (var poly in layer2DLoops)
                {
                    if (skipHoles)
                    {
                        var loop = new Vector3[poly.Path.Count];
                        for (int i = 0; i < loop.Length; i++)
                            loop[i] = new Vector3(poly.Path[i], zValue).Multiply(BackTransform);
                        layer[j++] = loop;
                    }
                    else
                    {
                        foreach (var path in poly.AllPaths)
                        {
                            var loop = new Vector3[path.Count];
                            for (int i = 0; i < path.Count; i++)
                                loop[i] = new Vector3(path[i], zValue).Multiply(BackTransform);
                            layer[j++] = loop;
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Determines whether [contains duplicate points] [the specified layer].
        /// </summary>
        /// <param name="layer">The layer.</param>
        /// <returns><c>true</c> if [contains duplicate points] [the specified layer]; otherwise, <c>false</c>.</returns>
        private static bool ContainsDuplicatePoints(IList<Polygon> layer)
        {
            var allPoints = new HashSet<Vector2>();
            foreach (var poly in layer)
            {
                foreach (var innerPoly in poly.AllPolygons)
                {
                    foreach (var point in innerPoly.Path)
                    {
                        if (allPoints.Contains(point))
                            return true;
                        else
                            allPoints.Add(point);
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Sets the layer2 d area.
        /// </summary>
        public void SetLayer2DArea()
        {
            Layer2DArea = new Dictionary<int, double>();
            foreach (var layer in Layer2D)
            {
                Layer2DArea.Add(layer.Key, layer.Value.Sum(p => p.Area));
            }
        }

        /// <summary>
        /// Reverses the specified new step distances.
        /// </summary>
        /// <param name="newStepDistances">The new step distances.</param>
        public void Reverse(Dictionary<int, double> newStepDistances)
        {
            var direction = Direction * -1;
            var transform = direction.TransformToXYPlane(out var backTransform);
            //Get the solid offset, since Layer2D may not be the full size as the StepDistances.
            var maxD = StepDistances.Keys.Max() - Layer2D.Keys.Max();
            StepDistances = newStepDistances;
            if (Layer2D != null && Layer2D.Any())
            {
                var min = Layer2D.Keys.Min();
                var j = maxD; //Start at maxD offset, not necessarily zero.
                var newLayer2D = new Dictionary<int, IList<Polygon>>();
                for (var i = Layer2D.Keys.Max(); i >= min; i--)
                {
                    var polygons = new List<Polygon>();
                    foreach (var polygon in Layer2D[i])
                    {
                        //There is probably a better way to this, but this should work.
                        //First, convert the 2D polygons back into their 3D coordates using 
                        //the original BackTransform. Then, transform to the new 2D coordinates.
                        //The polygons are now aligned with the specified reversed direction.
                        var positiveLoop = polygon.Path.ConvertTo3DLocations(BackTransform);
                        var positivePath = positiveLoop.ProjectTo2DCoordinates(transform).ToList();
                        if (positivePath.Area() < 0) positivePath.Reverse();
                        var newPolygon = new Polygon(positivePath);
                        foreach (var path in polygon.InnerPolygons)
                        {
                            var negativeLoop = path.Path.ConvertTo3DLocations(BackTransform);
                            var negativePath = negativeLoop.ProjectTo2DCoordinates(transform).ToList();
                            if (negativePath.Area() > 0) negativePath.Reverse();
                            newPolygon.AddInnerPolygon(new Polygon(negativePath));
                        }
                        polygons.Add(newPolygon);
                    }
                    newLayer2D[j++] = polygons;
                }
                Layer2D = newLayer2D;
            }
            if (Layer2DArea != null && Layer2DArea.Any())
            {
                var j = 0;
                var min = Layer2DArea.Keys.Min();
                var newLayer2DArea = new Dictionary<int, double>();
                for (var i = Layer2DArea.Keys.Max(); i >= min; i--)
                    newLayer2DArea[j++] = Layer2DArea[i];
            }
            TransformMatrix = transform;
            BackTransform = backTransform;
            FirstIndex = Layer2D.Keys.First();
            LastIndex = Layer2D.Keys.Last();
        }
    }
}