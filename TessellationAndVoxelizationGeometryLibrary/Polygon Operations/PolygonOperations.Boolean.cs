﻿// Copyright 2015-2020 Design Engineering Lab
// This file is a part of TVGL, Tessellation and Voxelization Geometry Library
// https://github.com/DesignEngrLab/TVGL
// It is licensed under MIT License (see LICENSE.txt for details)
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace TVGL.TwoDimensional
{
    /// <summary>
    /// A set of general operation for points and paths
    /// </summary>
    public static partial class PolygonOperations
    {
        // while the main executing methods are provided in this file (all of which can be invoked as Extensions), the code that perform the new polygon creation
        // is provided in the following four non-Static classes. These are non-static because they all inherit from the BooleanBase class. Each of these only needs
        // to be instantiated once as no data is stored in the class objects. So, this is a sort of singleton model but it's too bad we can have static classes inherit from
        // other static classes.
        private static PolygonUnion polygonUnion;
        private static PolygonIntersection polygonIntersection;
        private static PolygonRemoveIntersections polygonRemoveIntersections;

        #region Union Public Methods

        /// <summary>
        /// Returns the list of polygons that exist in either A OR B.
        /// </summary>
        /// <param name="polygonA">The polygon a.</param>
        /// <param name="polygonB">The polygon b.</param>
        /// <param name="outputAsCollectionType">Type of the output as collection.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>System.Collections.Generic.List&lt;TVGL.TwoDimensional.Polygon&gt;.</returns>
        public static List<Polygon> Union(this Polygon polygonA, Polygon polygonB,
            PolygonCollection outputAsCollectionType = PolygonCollection.PolygonWithHoles, double tolerance = double.NaN)
        {
            if (CLIPPER)
                return BooleanViaClipper(ClipperLib.PolyFillType.pftPositive, ClipperLib.ClipType.ctUnion, new[] { polygonA },
                    new[] { polygonB });
            var relationship = GetPolygonInteraction(polygonA, polygonB, tolerance);
            return Union(polygonA, polygonB, relationship, outputAsCollectionType, tolerance);

        }

        /// <summary>
        /// Returns the list of polygons that exist in either A OR B.By providing the intersections
        /// between the two polygons, the operation will be performed with less time and memory.
        /// </summary>
        /// <param name="polygonA">The polygon a.</param>
        /// <param name="polygonB">The polygon b.</param>
        /// <param name="polygonInteraction">The polygon relationship.</param>
        /// <param name="outputAsCollectionType">Type of the output as collection.</param>
        /// <param name="tolerance">The minimum allowable area.</param>
        /// <returns>System.Collections.Generic.List&lt;TVGL.TwoDimensional.Polygon&gt;.</returns>
        /// <exception cref="ArgumentException">A negative polygon (i.e. hole) is provided to Union which results in infinite shape. - polygonA</exception>
        /// <exception cref="ArgumentException">A negative polygon (i.e. hole) is provided to Union which results in infinite shape. - polygonB</exception>
        public static List<Polygon> Union(this Polygon polygonA, Polygon polygonB, PolygonInteractionRecord polygonInteraction,
            PolygonCollection outputAsCollectionType = PolygonCollection.PolygonWithHoles, double tolerance = double.NaN)
        {
            if (!polygonA.IsPositive) throw new ArgumentException("A negative polygon (i.e. hole) is provided to Union which results in infinite shape.", nameof(polygonA));
            if (!polygonB.IsPositive) throw new ArgumentException("A negative polygon (i.e. hole) is provided to Union which results in infinite shape.", nameof(polygonB));
            if (!polygonInteraction.CoincidentEdges && (polygonInteraction.Relationship == PolygonRelationship.Separated ||
                polygonInteraction.Relationship == PolygonRelationship.AIsInsideHoleOfB ||
                polygonInteraction.Relationship == PolygonRelationship.BIsInsideHoleOfA))
                return new List<Polygon> { polygonA.Copy(true, false), polygonB.Copy(true, false) };
            if (polygonInteraction.Relationship == PolygonRelationship.BInsideA ||
               polygonInteraction.Relationship == PolygonRelationship.Equal)
                return new List<Polygon> { polygonA.Copy(true, false) };
            if (polygonInteraction.Relationship == PolygonRelationship.AInsideB)
                return new List<Polygon> { polygonB.Copy(true, false) };
            polygonUnion ??= new PolygonUnion();
            return polygonUnion.Run(polygonA, polygonB, polygonInteraction, outputAsCollectionType, tolerance);
        }

        /// <summary>
        /// Returns the list of polygons that are the subshapes of ANY of the provided polygons. Notice this is called UnionPolygons here to distinguish
        /// it from the LINQ function Union, which is also a valid extension for any IEnumerable collection.
        /// </summary>
        /// <param name="polygons">The polygons.</param>
        /// <param name="outputAsCollectionType">Type of the output as collection.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>System.Collections.Generic.List&lt;TVGL.TwoDimensional.Polygon&gt;.</returns>
        public static List<Polygon> UnionPolygons(this IEnumerable<Polygon> polygons, PolygonCollection outputAsCollectionType = PolygonCollection.PolygonWithHoles,
            double tolerance = double.NaN)
        {
            if (CLIPPER)
                return BooleanViaClipper(ClipperLib.PolyFillType.pftPositive, ClipperLib.ClipType.ctUnion, polygons);

            var polygonList = polygons.ToList();
            if (double.IsNaN(tolerance))
                tolerance = GetTolerancesFromPolygons(polygonList);
            for (int i = polygonList.Count - 1; i > 0; i--)
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    var interaction = GetPolygonInteraction(polygonList[i], polygonList[j], tolerance);
                    if (interaction.Relationship == PolygonRelationship.BInsideA
                        || interaction.Relationship == PolygonRelationship.Equal)
                    {  // remove polygon B
                        polygonList.RemoveAt(j);
                        i--;
                    }
                    else if (interaction.Relationship == PolygonRelationship.AInsideB)
                    {                            // remove polygon A
                        polygonList.RemoveAt(i);
                        break; // to stop the inner loop
                    }
                    else if (interaction.CoincidentEdges || interaction.Relationship == PolygonRelationship.Intersection)
                    {
                        //if (i == 1 && j == 0)
                        //Presenter.ShowAndHang(new[] { polygonList[i], polygonList[j] });
                        var newPolygons = Union(polygonList[i], polygonList[j], interaction, outputAsCollectionType, tolerance);
                        //Console.WriteLine("i = {0}, j = {1}", i, j);
                        //if (i == 1 && j == 0)
                        //Presenter.ShowAndHang(newPolygons);
                        polygonList.RemoveAt(i);
                        polygonList.RemoveAt(j);
                        polygonList.AddRange(newPolygons);
                        i = polygonList.Count; // to restart the outer loop
                        break; // to stop the inner loop
                    }
                }
            }
            return polygonList;
        }

        /// <summary>
        /// Returns the list of polygons that are the subshapes of the two collections of polygons. Notice this is called UnionPolygons 
        /// here to distinguish it from the LINQ function Union, which is also a valid extension for any IEnumerable collection.
        /// </summary>
        /// <param name="polygonsA">The polygons a.</param>
        /// <param name="polygonsB">The polygons b.</param>
        /// <param name="outputAsCollectionType">Type of the output as collection.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>System.Collections.Generic.List&lt;TVGL.TwoDimensional.Polygon&gt;.</returns>
        public static List<Polygon> UnionPolygons(this IEnumerable<Polygon> polygonsA, IEnumerable<Polygon> polygonsB, PolygonCollection outputAsCollectionType = PolygonCollection.PolygonWithHoles,
            double tolerance = double.NaN)
        {
            if (CLIPPER)
                return BooleanViaClipper(ClipperLib.PolyFillType.pftPositive, ClipperLib.ClipType.ctUnion, polygonsA, polygonsB);

            var unionedPolygons = polygonsA.ToList();
            if (polygonsB is null) return UnionPolygons(unionedPolygons, outputAsCollectionType, tolerance);
            var polygonBList = polygonsB.ToList();
            if (double.IsNaN(tolerance))
                tolerance = GetTolerancesFromPolygons(unionedPolygons, polygonBList);

            for (int i = unionedPolygons.Count - 1; i >= 0; i--)
            {
                for (int j = polygonBList.Count - 1; j >= 0; j--)
                {
                    var interaction = GetPolygonInteraction(unionedPolygons[i], polygonBList[j], tolerance);
                    if (interaction.Relationship == PolygonRelationship.BInsideA
                        || interaction.Relationship == PolygonRelationship.Equal)
                    {  // remove polygon B
                        polygonBList.RemoveAt(j);
                    }
                    else if (interaction.Relationship == PolygonRelationship.AInsideB)
                    {                            // remove polygon A
                        unionedPolygons[i] = polygonBList[j];
                        polygonBList.RemoveAt(j);
                        break; // to stop the inner loop
                    }
                    else if (interaction.CoincidentEdges || interaction.Relationship == PolygonRelationship.Intersection)
                    {
                        //if (i == 1 && j == 0)
                        //Presenter.ShowAndHang(new[] { polygonList[i], polygonList[j] });
                        var newPolygons = Union(unionedPolygons[i], polygonBList[j], interaction, outputAsCollectionType, tolerance);
                        //Console.WriteLine("i = {0}, j = {1}", i, j);
                        //if (i == 1 && j == 0)
                        //Presenter.ShowAndHang(newPolygons);
                        unionedPolygons.RemoveAt(i);
                        polygonBList.RemoveAt(j);
                        unionedPolygons.AddRange(newPolygons);
                        i = unionedPolygons.Count; // to restart the outer loop
                        break; // to stop the inner loop
                    }
                }
            }
            return UnionPolygons(unionedPolygons.Where(p => p.IsPositive), outputAsCollectionType, tolerance);
        }

        private static double GetTolerancesFromPolygons(IEnumerable<Polygon> polygonsA, IEnumerable<Polygon> polygonsB = null)
        {
            double tolerance;
            var xMin = double.PositiveInfinity;
            var xMax = double.NegativeInfinity;
            var yMin = double.PositiveInfinity;
            var yMax = double.NegativeInfinity;
            foreach (var polygon in polygonsA)
            {
                if (xMin > polygon.MinX) xMin = polygon.MinX;
                if (yMin > polygon.MinY) yMin = polygon.MinY;
                if (xMax < polygon.MaxX) xMax = polygon.MaxX;
                if (yMax < polygon.MaxY) yMax = polygon.MaxY;
            }
            if (polygonsB != null)
                foreach (var polygon in polygonsB)
                {
                    if (xMin > polygon.MinX) xMin = polygon.MinX;
                    if (yMin > polygon.MinY) yMin = polygon.MinY;
                    if (xMax < polygon.MaxX) xMax = polygon.MaxX;
                    if (yMax < polygon.MaxY) yMax = polygon.MaxY;
                }
            tolerance = Math.Min(xMax - xMin, yMax - yMin) * Constants.BaseTolerance;
            return tolerance;
        }
        /// <summary>
        /// Gets the tolerance for polygon.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <returns>System.Double.</returns>
        public static double GetToleranceForPolygon(this Polygon polygon)
        {
            return Math.Min(polygon.MaxX - polygon.MinX, polygon.MaxY - polygon.MinY) * Constants.BaseTolerance;
        }

        #endregion Union Public Methods

        #region Intersect Public Methods

        /// <summary>
        /// Returns the list of polygons that result from the subshapes common to both A and B.
        /// </summary>
        /// <param name="polygonA">The polygon a.</param>
        /// <param name="polygonB">The polygon b.</param>
        /// <param name="outputAsCollectionType">Type of the output as collection.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>System.Collections.Generic.List&lt;TVGL.TwoDimensional.Polygon&gt;.</returns>
        public static List<Polygon> Intersect(this Polygon polygonA, Polygon polygonB,
            PolygonCollection outputAsCollectionType = PolygonCollection.PolygonWithHoles, double tolerance = double.NaN)
        {
            if (CLIPPER)
                return BooleanViaClipper(ClipperLib.PolyFillType.pftPositive, ClipperLib.ClipType.ctIntersection, new[] { polygonA },
                    new[] { polygonB });
            var relationship = GetPolygonInteraction(polygonA, polygonB, tolerance);
            return Intersect(polygonA, polygonB, relationship, outputAsCollectionType, tolerance);
        }

        /// <summary>
        /// Returns the list of polygons that result from the subshapes common to both A and B. By providing the intersections
        /// between the two polygons, the operation will be performed with less time and memory.
        /// </summary>
        /// <param name="polygonA">The polygon a.</param>
        /// <param name="polygonB">The polygon b.</param>
        /// <param name="interaction">The interaction.</param>
        /// <param name="outputAsCollectionType">Type of the output as collection.</param>
        /// <param name="tolerance">The minimum allowable area.</param>
        /// <returns>System.Collections.Generic.List&lt;TVGL.TwoDimensional.Polygon&gt;.</returns>
        public static List<Polygon> Intersect(this Polygon polygonA, Polygon polygonB, PolygonInteractionRecord interaction,
            PolygonCollection outputAsCollectionType = PolygonCollection.PolygonWithHoles, double tolerance = double.NaN)
        {
            if (interaction.IntersectionWillBeEmpty())
            {
                if (polygonB.IsPositive) return new List<Polygon>();
                else return new List<Polygon> { polygonA.Copy(true, false) };
            }
            else
            {
                polygonIntersection ??= new PolygonIntersection();
                return polygonIntersection.Run(polygonA, polygonB, interaction, outputAsCollectionType, tolerance);
            }
        }

        /// <summary>
        /// Returns the list of polygons that are the sub-shapes of the two collections of polygons. Notice this is called IntersectPolygons here 
        /// to distinguish it from the LINQ function Intersect, which is also a valid extension for any IEnumerable collection.
        /// Notice also that any overlap between the polygons in A or the polygons in B are ignored. Finally, all inputs must be positive.
        /// 
        /// </summary>
        /// <param name="polygonsA">The polygons a.</param>
        /// <param name="polygonsB">The polygons b.</param>
        /// <param name="outputAsCollectionType">Type of the output as collection.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>System.Collections.Generic.List&lt;TVGL.TwoDimensional.Polygon&gt;.</returns>
        public static List<Polygon> IntersectPolygons(this IEnumerable<Polygon> polygonsA, IEnumerable<Polygon> polygonsB, PolygonCollection outputAsCollectionType = PolygonCollection.PolygonWithHoles,
            double tolerance = double.NaN)
        {
            if (CLIPPER)
                return BooleanViaClipper(ClipperLib.PolyFillType.pftPositive, ClipperLib.ClipType.ctIntersection, polygonsA, polygonsB);
            var polygonAList = new List<Polygon>(polygonsA);
            var polygonBList = polygonsB as List<Polygon> ?? polygonsB.ToList();
            if (double.IsNaN(tolerance))
                tolerance = GetTolerancesFromPolygons(polygonAList, polygonBList);

            foreach (var polyB in polygonBList)
            {
                for (int i = polygonAList.Count - 1; i >= 0; i--)
                {
                    var newPolygons = polygonAList[i].Intersect(polyB, outputAsCollectionType, tolerance);
                    polygonAList.RemoveAt(i);
                    foreach (var newPoly in newPolygons)
                        polygonAList.Insert(i, newPoly);
                }
            }
            return polygonAList;
        }


        /// <summary>
        /// Returns the list of polygons that are the sub-shapes of ALL of the provided polygons. Notice this is called IntersectPolygons here 
        /// to distinguish it from the LINQ function Intersect, which is also a valid extension for any IEnumerable collection.
        /// </summary>
        /// <param name="polygons">The polygons.</param>
        /// <param name="outputAsCollectionType">Type of the output as collection.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>System.Collections.Generic.List&lt;TVGL.TwoDimensional.Polygon&gt;.</returns>
        public static List<Polygon> IntersectPolygons(this IEnumerable<Polygon> polygons, PolygonCollection outputAsCollectionType = PolygonCollection.PolygonWithHoles,
            double tolerance = double.NaN)
        {
            if (CLIPPER)
                return BooleanViaClipper(ClipperLib.PolyFillType.pftPositive, ClipperLib.ClipType.ctIntersection, polygons);
            var polygonList = polygons.ToList();
            for (int i = polygonList.Count - 2; i > 0; i--)
            {
                var clippingPoly = polygonList[i];
                polygonList.RemoveAt(i);
                for (int j = polygonList.Count - 1; j >= i; j--)
                {
                    var interaction = GetPolygonInteraction(clippingPoly, polygonList[j], tolerance);
                    if (interaction.IntersectionWillBeEmpty())
                        polygonList.RemoveAt(j);
                    else
                    {
                        var newPolygons = Intersect(clippingPoly, polygonList[j], interaction, outputAsCollectionType, tolerance);
                        foreach (var newPolygon in newPolygons)
                            polygonList.Insert(j, newPolygon);
                    }
                }
            }
            return polygonList;
        }

        #endregion Intersect Public Methods

        #region Subtract Public Methods

        /// <summary>
        /// Returns the list of polygons that result from A-B (subtracting polygon B from polygon A).
        /// </summary>
        /// <param name="minuend">The polygon a.</param>
        /// <param name="subtrahend">The polygon b.</param>
        /// <param name="outputAsCollectionType">Type of the output as collection.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>System.Collections.Generic.List&lt;TVGL.TwoDimensional.Polygon&gt;.</returns>
        public static List<Polygon> Subtract(this Polygon minuend, Polygon subtrahend,
            PolygonCollection outputAsCollectionType = PolygonCollection.PolygonWithHoles, double tolerance = double.NaN)
        {
            if (CLIPPER)
                return BooleanViaClipper(ClipperLib.PolyFillType.pftPositive, ClipperLib.ClipType.ctDifference, new[] { minuend },
                    new[] { subtrahend });
            var polygonBInverted = subtrahend.Copy(true, true);
            var relationship = GetPolygonInteraction(minuend, polygonBInverted, tolerance);
            return Intersect(minuend, polygonBInverted, relationship, outputAsCollectionType, tolerance);
        }

        /// <summary>
        /// Returns the list of polygons that result from A-B (subtracting polygon B from polygon A). By providing the intersections
        /// between the two polygons, the operation will be performed with less time and memory.
        /// </summary>
        /// <param name="minuend">The polygon a.</param>
        /// <param name="subtrahend">The polygon b.</param>
        /// <param name="interaction">The polygon relationship.</param>
        /// <param name="outputAsCollectionType">Type of the output as collection.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>System.Collections.Generic.List&lt;TVGL.TwoDimensional.Polygon&gt;.</returns>
        /// <exception cref="ArgumentException">The minuend is already a negative polygon (i.e. hole). Consider another operation"
        /// +" to accomplish this function, like Intersect. - polygonA</exception>
        public static List<Polygon> Subtract(this Polygon minuend, Polygon subtrahend, PolygonInteractionRecord interaction,
            PolygonCollection outputAsCollectionType = PolygonCollection.PolygonWithHoles, double tolerance = double.NaN)
        {
            interaction = interaction.InvertPolygonInRecord(subtrahend, out var invertedPolygonB);
            return Intersect(minuend, invertedPolygonB, interaction, outputAsCollectionType, tolerance);
        }


        /// <summary>
        /// Returns the list of polygons that are the sub-shapes of the minuends and that are not part of the subtrahends. 
        /// Notice also that any overlap between the polygons in A or the polygons in B are ignored. Finally, all inputs must be positive.
        /// 
        /// </summary>
        /// <param name="minuends">The polygons a.</param>
        /// <param name="subtrahends">The polygons b.</param>
        /// <param name="outputAsCollectionType">Type of the output as collection.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>System.Collections.Generic.List&lt;TVGL.TwoDimensional.Polygon&gt;.</returns>
        public static List<Polygon> Subtract(this IEnumerable<Polygon> minuends, IEnumerable<Polygon> subtrahends,
            PolygonCollection outputAsCollectionType = PolygonCollection.PolygonWithHoles, double tolerance = double.NaN)
        {
            if (CLIPPER)
                return BooleanViaClipper(ClipperLib.PolyFillType.pftPositive, ClipperLib.ClipType.ctDifference, minuends,
                    subtrahends);
            var minuendsList = minuends.ToList();
            var subtrahendsList = subtrahends as List<Polygon> ?? subtrahends.ToList();
            if (double.IsNaN(tolerance))
                tolerance = GetTolerancesFromPolygons(minuendsList, subtrahendsList);

            foreach (var polyB in subtrahendsList)
            {
                for (int i = minuendsList.Count - 1; i >= 0; i--)
                {
                    var newPolygons = minuendsList[i].Subtract(polyB, outputAsCollectionType, tolerance);
                    minuendsList.RemoveAt(i);
                    foreach (var newPoly in newPolygons)
                        minuendsList.Insert(i, newPoly);
                }
            }
            return minuendsList;
        }


        #endregion Subtract Public Methods

        #region Exclusive-OR Public Methods

        /// <summary>
        /// Returns the list of polygons that are the Exclusive-OR of the two input polygons. Exclusive-OR are the regions where one polgyon
        /// resides but not both.
        /// </summary>
        /// <param name="polygonA">The polygon a.</param>
        /// <param name="polygonB">The polygon b.</param>
        /// <param name="outputAsCollectionType">Type of the output as collection.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>System.Collections.Generic.List&lt;TVGL.TwoDimensional.Polygon&gt;.</returns>
        public static List<Polygon> ExclusiveOr(this Polygon polygonA, Polygon polygonB,
            PolygonCollection outputAsCollectionType = PolygonCollection.PolygonWithHoles, double tolerance = double.NaN)
        {
            if (CLIPPER)
                return BooleanViaClipper(ClipperLib.PolyFillType.pftPositive, ClipperLib.ClipType.ctXor, new[] { polygonA },
                    new[] { polygonB });
            var relationship = GetPolygonInteraction(polygonA, polygonB, tolerance);
            return ExclusiveOr(polygonA, polygonB, relationship, outputAsCollectionType, tolerance);
        }

        /// <summary>
        /// Returns the list of polygons that are the Exclusive-OR of the two input polygons. Exclusive-OR are the regions where one polgyon
        /// resides but not both. By providing the intersections between the two polygons, the operation will be performed with less time and memory.
        /// </summary>
        /// <param name="polygonA">The polygon a.</param>
        /// <param name="polygonB">The polygon b.</param>
        /// <param name="interactionRecord">The interaction record.</param>
        /// <param name="outputAsCollectionType">Type of the output as collection.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>System.Collections.Generic.List&lt;TVGL.TwoDimensional.Polygon&gt;.</returns>
        public static List<Polygon> ExclusiveOr(this Polygon polygonA, Polygon polygonB, PolygonInteractionRecord interactionRecord,
           PolygonCollection outputAsCollectionType = PolygonCollection.PolygonWithHoles, double tolerance = double.NaN)
        {
            if (interactionRecord.IntersectionWillBeEmpty())
                return new List<Polygon> { polygonA.Copy(true, false), polygonB.Copy(true, false) };
            else if (interactionRecord.Relationship == PolygonRelationship.BInsideA &&
                !interactionRecord.CoincidentEdges && !interactionRecord.CoincidentVertices)
            {
                var polygonACopy1 = polygonA.Copy(true, false);
                polygonACopy1.AddInnerPolygon(polygonB.Copy(true, true));
                return new List<Polygon> { polygonACopy1 };
            }
            else if (interactionRecord.Relationship == PolygonRelationship.AInsideB &&
                !interactionRecord.CoincidentEdges && !interactionRecord.CoincidentVertices)
            {
                var polygonBCopy2 = polygonB.Copy(true, false);
                polygonBCopy2.AddInnerPolygon(polygonA.Copy(true, true));
                return new List<Polygon> { polygonBCopy2 };
            }
            else
            {
                var result = polygonA.Subtract(polygonB, interactionRecord, outputAsCollectionType, tolerance);
                result.AddRange(polygonB.Subtract(polygonA, interactionRecord, outputAsCollectionType, tolerance));
                return result;
            }
        }

        #endregion Exclusive-OR Public Methods

        #region RemoveSelfIntersections Public Method

        /// <summary>
        /// Removes the self intersections.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="resultType">Type of the result.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="knownWrongPoints">The known wrong points.</param>
        /// <param name="maxNumberOfPolygons">The maximum number of polygons.</param>
        /// <returns>List&lt;Polygon&gt;.</returns>
        public static List<Polygon> RemoveSelfIntersections(this Polygon polygon, ResultType resultType,
            double tolerance = double.NaN, List<bool> knownWrongPoints = null, int maxNumberOfPolygons = int.MaxValue)
        {
            if (double.IsNaN(tolerance)) tolerance = polygon.GetToleranceForPolygon();
            var intersections = polygon.GetSelfIntersections(tolerance).Where(intersect => intersect.Relationship != SegmentRelationship.NoOverlap).ToList();
            if (intersections.Count == 0)
                return new List<Polygon> { polygon };
            polygonRemoveIntersections ??= new PolygonRemoveIntersections();
            return polygonRemoveIntersections.Run(polygon, intersections, resultType, tolerance, knownWrongPoints, maxNumberOfPolygons);
        }

        #endregion RemoveSelfIntersections Public Method
    }
}