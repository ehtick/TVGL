﻿// Copyright 2015-2020 Design Engineering Lab
// This file is a part of TVGL, Tessellation and Voxelization Geometry Library
// https://github.com/DesignEngrLab/TVGL
// It is licensed under MIT License (see LICENSE.txt for details)
using Priority_Queue;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using TVGL.Numerics;

namespace TVGL.TwoDimensional
{
    /// <summary>
    /// A set of general operation for points and paths
    /// </summary>
    public static partial class PolygonOperations
    {
        #region Simplify

        /// <summary>
        /// Simplifies the specified polygons no more than the allowable change in area fraction.
        /// </summary>
        /// <param name="polygons">The polygons.</param>
        /// <param name="allowableChangeInAreaFraction">The allowable change in area fraction.</param>
        /// <returns>IEnumerable&lt;Polygon&gt;.</returns>
        public static IEnumerable<Polygon> Simplify(this IEnumerable<Polygon> polygons)
        {
            return polygons.Select(poly => poly.Simplify());
        }

        /// <summary>
        /// Simplifies the specified polygons no more than the allowable change in area fraction.
        /// </summary>
        /// <param name="polygons">The polygons.</param>
        /// <param name="allowableChangeInAreaFraction">The allowable change in area fraction.</param>
        /// <returns>IEnumerable&lt;Polygon&gt;.</returns>
        public static IEnumerable<Polygon> Simplify(this IEnumerable<Polygon> polygons, double allowableChangeInAreaFraction)
        {
            return polygons.Select(poly => poly.Simplify(allowableChangeInAreaFraction));
        }

        /// <summary>
        /// Simplifies the specified polygon no more than the allowable change in area fraction.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="allowableChangeInAreaFraction">The allowable change in area fraction.</param>
        /// <returns>Polygon.</returns>
        public static Polygon Simplify(this Polygon polygon)
        {
            var simplifiedPositivePolygon = new Polygon(polygon.Path.Simplify());
            foreach (var polygonHole in polygon.InnerPolygons)
                simplifiedPositivePolygon.AddInnerPolygon(new Polygon(polygonHole.Path.Simplify()));
            return simplifiedPositivePolygon;
        }


        /// <summary>
        /// Simplifies the specified polygon no more than the allowable change in area fraction.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="allowableChangeInAreaFraction">The allowable change in area fraction.</param>
        /// <returns>Polygon.</returns>
        public static Polygon Simplify(this Polygon polygon, double allowableChangeInAreaFraction)
        {
            var simplifiedPositivePolygon = new Polygon(polygon.Path.Simplify(allowableChangeInAreaFraction));
            foreach (var polygonHole in polygon.InnerPolygons)
                simplifiedPositivePolygon.AddInnerPolygon(new Polygon(polygonHole.Path.Simplify(allowableChangeInAreaFraction)));
            return simplifiedPositivePolygon;
        }

        /// <summary>
        /// Simplifies the specified polygons no more than the allowable change in area fraction.
        /// </summary>
        /// <param name="paths">The paths.</param>
        /// <param name="allowableChangeInAreaFraction">The allowable change in area fraction.</param>
        /// <returns>List&lt;List&lt;Vector2&gt;&gt;.</returns>
        public static List<List<Vector2>> Simplify(this IEnumerable<IEnumerable<Vector2>> paths)
        {
            return paths.Select(p => Simplify(p)).ToList();
        }


        /// <summary>
        /// Simplifies the specified polygons no more than the allowable change in area fraction.
        /// </summary>
        /// <param name="paths">The paths.</param>
        /// <param name="allowableChangeInAreaFraction">The allowable change in area fraction.</param>
        /// <returns>List&lt;List&lt;Vector2&gt;&gt;.</returns>
        public static List<List<Vector2>> Simplify(this IEnumerable<IEnumerable<Vector2>> paths, double allowableChangeInAreaFraction)
        {
            return paths.Select(p => Simplify(p, allowableChangeInAreaFraction)).ToList();
        }

        /// <summary>
        /// Simplifies the specified polygons no more than the allowable change in area fraction.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static List<Vector2> Simplify(this IEnumerable<Vector2> path)
        {
            var polygon = path.ToList();
            var forwardPoint = polygon[0];
            var currentPoint = polygon[^1];
            for (int i = polygon.Count - 1; i >= 0; i--)
            {
                var backwardPoint = i == 0 ? polygon[^1] : polygon[i - 1];
                var cross = (currentPoint - backwardPoint).Cross(forwardPoint - currentPoint);
                if (cross.IsNegligible()) polygon.RemoveAt(i);
                else forwardPoint = currentPoint;
                currentPoint = backwardPoint;
            }
            return polygon;
        }


        /// <summary>
        /// Simplifies the specified polygons no more than the allowable change in area fraction.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static List<Vector2> Simplify(this IEnumerable<Vector2> path, double allowableChangeInAreaFraction)
        {
            var polygon = path.Simplify();
            var numPoints = polygon.Count;
            var origArea = Math.Abs(polygon.Area());
            if (origArea.IsNegligible()) return polygon;

            #region build initial list of cross products

            // queue is sorted on the cross-product at the polygon corner (requiring knowledge of the previous and next points)
            // Here we are using the SimplePriorityQueue from BlueRaja (https://github.com/BlueRaja/High-Speed-Priority-Queue-for-C-Sharp)
            var convexCornerQueue = new SimplePriorityQueue<int, double>(new ForwardSort());
            var concaveCornerQueue = new SimplePriorityQueue<int, double>(new ReverseSort());

            // cross-products which are kept in the same order as the corners they represent. This is solely used with the above
            // dictionary - to essentially do the reverse lookup. given a corner-index, crossProductsArray will instanly tell us the
            // cross-product. The cross-product is used as the key in the dictionary - to find corner-indices.
            var crossProductsArray = new double[numPoints];

            // make the cross-products. this is a for-loop that is preceded with the first element (requiring the last element, "^1" in
            // C# 8 terms) and succeeded by one for the last corner
            AddCrossProductToOneOfTheLists(polygon[^1], polygon[0], polygon[1], convexCornerQueue, concaveCornerQueue,
                crossProductsArray, 0);
            for (int i = 1; i < numPoints - 1; i++)
                AddCrossProductToOneOfTheLists(polygon[i - 1], polygon[i], polygon[i + 1], convexCornerQueue, concaveCornerQueue,
                crossProductsArray, i);
            AddCrossProductToOneOfTheLists(polygon[^2], polygon[^1], polygon[0], convexCornerQueue, concaveCornerQueue,
                crossProductsArray, numPoints - 1);

            #endregion build initial list of cross products

            // after much thought, the idea to split up into positive and negative sorted lists is so that we don't over remove vertices
            // by bouncing back and forth between convex and concave while staying with the target deltaArea. So, we do as many convex corners
            // before reaching a reduction of deltaArea - followed by a reduction of concave edges so that no more than deltaArea is re-added
            for (int sign = 1; sign >= -1; sign -= 2)
            {
                var deltaArea = 2 * allowableChangeInAreaFraction * origArea; //multiplied by 2 in order to reduce all the divide by 2
                                                                              // that happens when we change cross-product to area of a triangle
                var relevantSortedList = (sign == 1) ? convexCornerQueue : concaveCornerQueue;
                // first we remove any convex corners that would reduce the area
                while (relevantSortedList.Count > 0)
                {
                    var index = relevantSortedList.Dequeue();
                    var smallestArea = crossProductsArray[index];
                    if (deltaArea < sign * smallestArea)
                    { //this was one tricky little bug! in order to keep this fast, we first dequeue before examining
                      // the result. if the resulting index produces more area than we need we switch to the
                      // concave queue. That dequeuing and updating will want this last index on the queues
                      // if it is a neighbor to a new one being removing. Confusing, eh? So, we need to put it
                      // back in. Looks kludge-y but this only happens once, and it's better to do this once
                      // then add more logic to the above statements that would slow it down.
                        relevantSortedList.Enqueue(index, smallestArea);
                        break;
                    }
                    deltaArea -= sign * smallestArea;
                    //  set the corner to null. we'll remove null corners at the end. for now, just set to null.
                    // this is for speed and keep the indices correct in the various collections
                    polygon[index] = Vector2.Null;
                    // find the four neighbors - two on each side. the closest two (prevIndex and nextIndex) need to be updated
                    // which requires each other (now that the corner in question has been removed) and their neighbors on the other side
                    // (nextnextIndex and prevprevIndex)
                    int nextIndex = FindValidNeighborIndex(index, true, polygon, numPoints);
                    int nextnextIndex = FindValidNeighborIndex(nextIndex, true, polygon, numPoints);
                    int prevIndex = FindValidNeighborIndex(index, false, polygon, numPoints);
                    int prevprevIndex = FindValidNeighborIndex(prevIndex, false, polygon, numPoints);
                    // if the polygon has been reduced to 2 points, then we're going to delete it
                    if (nextnextIndex == prevIndex || nextIndex == prevprevIndex) // then reduced to two points.
                        continue;

                    // now, add these new crossproducts both to the dictionary and to the sortedLists. Note, that nothing is
                    // removed from the sorted lists here. it is more efficient to just remove them if they bubble to the top of the list,
                    // which is done in PopNextSmallestArea
                    UpdateCrossProductInQueues(polygon[prevIndex], polygon[nextIndex], polygon[nextnextIndex], convexCornerQueue, concaveCornerQueue,
                        crossProductsArray, nextIndex);
                    UpdateCrossProductInQueues(polygon[prevprevIndex], polygon[prevIndex], polygon[nextIndex], convexCornerQueue, concaveCornerQueue,
                            crossProductsArray, prevIndex);
                }
            }
            return polygon.Where(v => !v.IsNull()).ToList();
        }

        public static Polygon SimplifyFuzzy(this Polygon polygon,
            double lengthTolerance = Constants.LineLengthMinimum,
            double slopeTolerance = Constants.LineSlopeTolerance)
        {
            var simplifiedPositivePolygon = new Polygon(polygon.Path.SimplifyFuzzy(lengthTolerance, slopeTolerance));
            foreach (var polygonHole in polygon.InnerPolygons)
                simplifiedPositivePolygon.AddInnerPolygon(new Polygon(polygonHole.Path.SimplifyFuzzy(lengthTolerance, slopeTolerance)));
            return simplifiedPositivePolygon;
        }

        /// <summary>
        /// Simplifies the specified polygons by reducing the number of points in the polygon
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static List<Vector2> SimplifyFuzzy(this IEnumerable<Vector2> path,
        double lengthTolerance = Constants.LineLengthMinimum,
        double slopeTolerance = Constants.LineSlopeTolerance)
        {
            if (lengthTolerance.IsNegligible()) lengthTolerance = Constants.LineLengthMinimum;
            var squareLengthTolerance = lengthTolerance * lengthTolerance;
            var simplePath = new List<Vector2>(path);
            var n = simplePath.Count;
            if (n < 4) return simplePath;

            //Remove negligible length lines and combine collinear lines.
            var i = 0;
            var j = 1;
            var k = 2;
            var iX = simplePath[i].X;
            var iY = simplePath[i].Y;
            var jX = simplePath[j].X;
            var jY = simplePath[j].Y;
            var kX = simplePath[k].X;
            var kY = simplePath[k].Y;
            while (i < n)
            {
                //We only check line I-J in the first iteration, since later we
                //check line J-K instead.
                if (i == 0 && NegligibleLine(iX, iY, jX, jY, squareLengthTolerance))
                {
                    simplePath.RemoveAt(j);
                    n--;
                    j = (i + 1) % n; //Next position in path. Goes to 0 when i = n-1; 
                    k = (j + 1) % n; //Next position in path. Goes to 0 when j = n-1; 
                                     //Current stays the same.
                                     //j moves to k, k moves forward but has the same index.
                    jX = kX;
                    jY = kY;
                    var kPoint = simplePath[k];
                    kX = kPoint.X;
                    kY = kPoint.Y;
                }
                else if (NegligibleLine(jX, jY, kX, kY, squareLengthTolerance))
                {
                    continue;
                    n--;
                    j = (i + 1) % n; //Next position in path. Goes to 0 when i = n-1; 
                    k = (j + 1) % n; //Next position in path. Goes to 0 when j = n-1; 
                                     //Current and Next stay the same.
                                     //k moves forward but has the same index.
                    var kPoint = simplePath[k];
                    kX = kPoint.X;
                    kY = kPoint.Y;
                }
                //Use an even looser tolerance to determine if slopes are equal.
                else if (LineSlopesEqual(iX, iY, jX, jY, kX, kY, slopeTolerance))
                {
                    simplePath.RemoveAt(j);
                    n--;
                    j = (i + 1) % n; //Next position in path. Goes to 0 when i = n-1; 
                    k = (j + 1) % n; //Next position in path. Goes to 0 when j = n-1; 

                    //Current stays the same.
                    //j moves to k, k moves forward but has the same index.
                    jX = kX;
                    jY = kY;
                    var kPoint = simplePath[k];
                    kX = kPoint.X;
                    kY = kPoint.Y;
                }
                else
                {
                    //Everything moves forward
                    i++;
                    j = (i + 1) % n; //Next position in path. Goes to 0 when i = n-1; 
                    k = (j + 1) % n; //Next position in path. Goes to 0 when j = n-1; 
                    iX = jX;
                    iY = jY;
                    jX = kX;
                    jY = kY;
                    var kPoint = simplePath[k];
                    kX = kPoint.X;
                    kY = kPoint.Y;
                }
            }

            var area1 = Area(path);
            var area2 = Area(simplePath);

            //If the simplification destroys a polygon, do not simplify it.
            if (area2.IsNegligible() ||
                !area1.IsPracticallySame(area2, Math.Abs(area1 * (1 - Constants.HighConfidence))))
            {
                return path.ToList();
            }

            return simplePath;
        }

        private static bool NegligibleLine(double p1X, double p1Y, double p2X, double p2Y, double squaredTolerance)
        {
            var dX = p1X - p2X;
            var dY = p1Y - p2Y;
            return (dX * dX + dY * dY).IsNegligible(squaredTolerance);
        }

        private static bool LineSlopesEqual(double p1X, double p1Y, double p2X, double p2Y, double p3X, double p3Y,
            double tolerance = Constants.LineSlopeTolerance)
        {
            var value = (p1Y - p2Y) * (p2X - p3X) - (p1X - p2X) * (p2Y - p3Y);
            return value.IsNegligible(tolerance);
        }


        /// <summary>
        /// Simplifies the specified polygon to the target number of points.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="targetNumberOfPoints">The target number of points.</param>
        /// <returns>Polygon.</returns>
        public static Polygon Simplify(this Polygon polygon, int targetNumberOfPoints)
        {
            var simplifiedPaths = polygon.AllPolygons.Select(poly => poly.Path).Simplify(targetNumberOfPoints);
            return CreateShallowPolygonTreesOrderedListsAndVertices(simplifiedPaths).First();
        }

        /// <summary>
        /// Simplifies the specified polygons to the target number of points.
        /// </summary>
        /// <param name="polygons">The polygons.</param>
        /// <param name="targetNumberOfPoints">The target number of points.</param>
        /// <returns>IEnumerable&lt;Polygon&gt;.</returns>
        public static IEnumerable<Polygon> Simplify(this IEnumerable<Polygon> polygons, int targetNumberOfPoints)
        {
            var simplifiedPaths = polygons.SelectMany(poly => poly.AllPolygons.Select(p => p.Path)).Simplify(targetNumberOfPoints);
            return CreateShallowPolygonTreesOrderedListsAndVertices(simplifiedPaths);
        }

        /// <summary>
        /// Simplifies the specified polygons to the target number of points.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="targetNumberOfPoints">The target number of points.</param>
        /// <returns>List&lt;List&lt;Vector2&gt;&gt;.</returns>
        public static List<List<Vector2>> Simplify(this IEnumerable<Vector2> path, int targetNumberOfPoints)
        { return Simplify(new[] { path }, targetNumberOfPoints); }

        /// <summary>
        /// Simplifies the specified polygon to the target number of points.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="targetNumberOfPoints">The target number of points.</param>
        /// <returns>List&lt;List&lt;Vector2&gt;&gt;.</returns>
        /// <exception cref="ArgumentOutOfRangeException">targetNumberOfPoints - The number of points to remove in PolygonOperations.Simplify"
        ///                   + " is more than the total number of points in the polygon(s).</exception>
        public static List<List<Vector2>> Simplify(this IEnumerable<IEnumerable<Vector2>> paths, int targetNumberOfPoints)
        {
            var polygons = paths.Select(p => p.Simplify()).ToList();
            var numPoints = polygons.Select(p => p.Count).ToList();
            var numToRemove = numPoints.Sum() - targetNumberOfPoints;

            #region build initial list of cross products

            var cornerQueue = new SimplePriorityQueue<int, double>(new AbsoluteValueSort());
            var crossProductsArray = new double[numPoints.Sum()];
            var index = 0;
            for (int j = 0; j < polygons.Count; j++)
            {
                AddCrossProductToQueue(polygons[j][^1], polygons[j][0], polygons[j][1], cornerQueue, crossProductsArray, index++);
                for (int i = 1; i < numPoints[j] - 1; i++)
                    AddCrossProductToQueue(polygons[j][i - 1], polygons[j][i], polygons[j][i + 1], cornerQueue, crossProductsArray, index++);
                AddCrossProductToQueue(polygons[j][^2], polygons[j][^1], polygons[j][0], cornerQueue, crossProductsArray, index++);
            }

            #endregion build initial list of cross products

            if (numToRemove <= 0) throw new ArgumentOutOfRangeException(nameof(targetNumberOfPoints),
                "The number of points to remove in PolygonOperations.Simplify is more than the total number of points in the polygon(s).");
            while (numToRemove-- > 0)
            {
                index = cornerQueue.Dequeue();
                var cornerIndex = index;
                var polygonIndex = 0;
                // the index is from stringing together all the original polygons into one long array
                while (cornerIndex >= polygons[polygonIndex].Count) cornerIndex -= polygons[polygonIndex++].Count;
                polygons[polygonIndex][cornerIndex] = Vector2.Null;

                // find the four neighbors - two on each side. the closest two (prevIndex and nextIndex) need to be updated
                // which requires each other (now that the corner in question has been removed) and their neighbors on the other side
                // (nextnextIndex and prevprevIndex)
                int nextIndex = FindValidNeighborIndex(cornerIndex, true, polygons[polygonIndex], numPoints[polygonIndex]);
                int nextnextIndex = FindValidNeighborIndex(nextIndex, true, polygons[polygonIndex], numPoints[polygonIndex]);
                int prevIndex = FindValidNeighborIndex(cornerIndex, false, polygons[polygonIndex], numPoints[polygonIndex]);
                int prevprevIndex = FindValidNeighborIndex(prevIndex, false, polygons[polygonIndex], numPoints[polygonIndex]);
                // if the polygon has been reduced to 2 points, then we're going to delete it
                if (nextnextIndex == prevIndex || nextIndex == prevprevIndex) // then reduced to two points.
                {
                    polygons[polygonIndex][nextIndex] = Vector2.Null;
                    polygons[polygonIndex][nextnextIndex] = Vector2.Null;
                    numToRemove -= 2;
                }
                var polygonStartIndex = index - cornerIndex;
                // like the AddCrossProductToQueue function used above, we need a global index from stringing together all the polygons.
                // So, polygonStartIndex is used to find the start of this particular polygon's index and then add prevIndex and nextIndex to it.
                UpdateCrossProductInQueue(polygons[polygonIndex][prevprevIndex], polygons[polygonIndex][prevIndex], polygons[polygonIndex][nextIndex],
                    cornerQueue, crossProductsArray, polygonStartIndex + prevIndex);
                UpdateCrossProductInQueue(polygons[polygonIndex][prevIndex], polygons[polygonIndex][nextIndex], polygons[polygonIndex][nextnextIndex],
                    cornerQueue, crossProductsArray, polygonStartIndex + nextIndex);
            }

            var result = new List<List<Vector2>>();
            foreach (var polygon in polygons)
            {
                var resultPolygon = new List<Vector2>();
                foreach (var corner in polygon)
                    if (!corner.IsNull()) resultPolygon.Add(corner);
                if (resultPolygon.Count > 2)
                    result.Add(resultPolygon);
            }
            return result;
        }

        private static int FindValidNeighborIndex(int index, bool forward, IList<Vector2> polygon, int numPoints)
        {
            int increment = forward ? 1 : -1;
            var hitLimit = false;
            do
            {
                index += increment;
                if (index < 0)
                {
                    index = numPoints - 1;
                    if (hitLimit)
                    {
                        index = -1;
                        break;
                    }
                    hitLimit = true;
                }
                else if (index == numPoints)
                {
                    index = 0;
                    if (hitLimit)
                    {
                        index = -1;
                        break;
                    }
                    hitLimit = true;
                }
            }
            while (polygon[index].IsNull());
            return index;
        }

        private static void AddCrossProductToOneOfTheLists(Vector2 fromPoint, Vector2 currentPoint, Vector2 nextPoint,
            SimplePriorityQueue<int, double> convexCornerQueue, SimplePriorityQueue<int, double> concaveCornerQueue,
            double[] crossProducts, int index)
        {
            var cross = (currentPoint - fromPoint).Cross(nextPoint - currentPoint);
            crossProducts[index] = cross;
            if (cross < 0) concaveCornerQueue.Enqueue(index, (float)cross);
            else convexCornerQueue.Enqueue(index, (float)cross);
        }

        private static void UpdateCrossProductInQueues(Vector2 fromPoint, Vector2 currentPoint, Vector2 nextPoint,
            SimplePriorityQueue<int, double> convexCornerQueue, SimplePriorityQueue<int, double> concaveCornerQueue,
            double[] crossProducts, int index)
        {
            var oldCross = crossProducts[index];
            var newCross = (currentPoint - fromPoint).Cross(nextPoint - currentPoint);
            crossProducts[index] = newCross;
            if (newCross < 0)
            {
                if (oldCross < 0)
                    concaveCornerQueue.UpdatePriority(index, newCross);
                else //then it used to be positive and needs to be removed from the convexCornerQueue
                {
                    convexCornerQueue.Remove(index);
                    concaveCornerQueue.Enqueue(index, newCross);
                }
            }
            // else newCross is positive and should be on the convexCornerQueue
            else if (oldCross >= 0)
                convexCornerQueue.UpdatePriority(index, newCross);
            else //then it used to be negative and needs to be removed from the concaveCornerQueue
            {
                concaveCornerQueue.Remove(index);
                convexCornerQueue.Enqueue(index, newCross);
            }
        }

        private static void AddCrossProductToQueue(Vector2 fromPoint, Vector2 currentPoint,
            Vector2 nextPoint, SimplePriorityQueue<int, double> cornerQueue,
            double[] crossProducts, int index)
        {
            var cross = Math.Abs((currentPoint - fromPoint).Cross(nextPoint - currentPoint));
            crossProducts[index] = cross;
            cornerQueue.Enqueue(index, cross);
        }

        private static void UpdateCrossProductInQueue(Vector2 fromPoint, Vector2 currentPoint, Vector2 nextPoint,
            SimplePriorityQueue<int, double> cornerQueue, double[] crossProducts, int index)
        {
            var newCross = (currentPoint - fromPoint).Cross(nextPoint - currentPoint);
            crossProducts[index] = newCross;
            cornerQueue.UpdatePriority(index, newCross);
        }

        #endregion Simplify



        #region Complexify

        /// <summary>
        /// Complexifies the specified polygons so that no edge is longer than the maxAllowableLength.
        /// </summary>
        /// <param name="polygons">The polygons.</param>
        /// <param name="maxAllowableLength">Maximum length of the allowable.</param>
        /// <returns>IEnumerable&lt;Polygon&gt;.</returns>
        /// <exception cref="NotImplementedException"></exception>
        public static IEnumerable<Polygon> ComplexifyToNewPolygons(this IEnumerable<Polygon> polygons, double maxAllowableLength)
        {
            var copiedPolygons = polygons.Select(p => p.Copy(true, false));
            Complexify(copiedPolygons, maxAllowableLength);
            return copiedPolygons;
        }

        /// <summary>
        /// Complexifies the specified polygon so that no edge is longer than the maxAllowableLength.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="maxAllowableLength">Maximum length of the allowable.</param>
        /// <returns>Polygon.</returns>
        public static Polygon ComplexifyToNewPolygon(this Polygon polygon, double maxAllowableLength)
        {
            var copiedPolygon = polygon.Copy(true, false);
            Complexify(copiedPolygon, maxAllowableLength);
            return copiedPolygon;
        }

        /// <summary>
        /// Complexifies the specified polygons so that no edge is longer than the maxAllowableLength.
        /// </summary>
        /// <param name="polygons">The polygons.</param>
        /// <param name="maxAllowableLength">Maximum length of the allowable.</param>
        /// <returns>IEnumerable&lt;Polygon&gt;.</returns>
        public static void Complexify(this IEnumerable<Polygon> polygons, double maxAllowableLength)
        {
            foreach (var polygon in polygons)
                polygon.Complexify(maxAllowableLength);
        }

        /// <summary>
        /// Complexifies the specified polygon so that no edge is longer than the maxAllowableLength.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="maxAllowableLength">Maximum length of the allowable.</param>
        /// <returns>Polygon.</returns>
        public static void Complexify(this Polygon polygon, double maxAllowableLength)
        {
            var loopID = polygon.Index;
            for (int i = 0; i < polygon.Edges.Length; i++)
            {
                var thisLine = polygon.Edges[i];
                if (thisLine.Length > maxAllowableLength)
                {
                    var numNewPoints = (int)thisLine.Length / maxAllowableLength;
                    for (int j = 0; j < numNewPoints; j++)
                    {
                        var fraction = j / (double)numNewPoints;
                        var newCoordinates = fraction * thisLine.FromPoint.Coordinates + ((1 - fraction) * thisLine.ToPoint.Coordinates);
                        polygon.Vertices.Insert(i, new Vertex2D(newCoordinates, 0, loopID));
                    }
                }
            }
            polygon.Reset();
            foreach (var polygonHole in polygon.InnerPolygons)
                polygonHole.Complexify(maxAllowableLength);
        }

        /// <summary>
        /// Complexifies the specified polygons so that no edge is longer than the maxAllowableLength.
        /// </summary>
        /// <param name="polygons">The polygons.</param>
        /// <param name="targetNumberOfPoints">The target number of points.</param>
        /// <returns>IEnumerable&lt;Polygon&gt;.</returns>
        /// <exception cref="NotImplementedException"></exception>
        public static IEnumerable<Polygon> ComplexifyToNewPolygons(this IEnumerable<Polygon> polygons, int targetNumberOfPoints)
        {
            var copiedPolygons = polygons.Select(p => p.Copy(true, false));
            Complexify(copiedPolygons, targetNumberOfPoints);
            return copiedPolygons;
        }

        /// <summary>
        /// Complexifies the specified polygon so that no edge is longer than the maxAllowableLength.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="targetNumberOfPoints">The target number of points.</param>
        /// <returns>Polygon.</returns>
        public static Polygon ComplexifyToNewPolygon(this Polygon polygon, int targetNumberOfPoints)
        {
            var copiedPolygon = polygon.Copy(true, false);
            Complexify(copiedPolygon, targetNumberOfPoints);
            return copiedPolygon;
        }
        /// <summary>
        /// Complexifies the specified polygons so that no edge is longer than the maxAllowableLength.
        /// </summary>
        /// <param name="polygons">The polygons.</param>
        /// <param name="targetNumberOfPoints">The target number of points.</param>
        /// <returns>IEnumerable&lt;Polygon&gt;.</returns>
        public static void Complexify(this IEnumerable<Polygon> polygons, int targetNumberOfPoints)
        {
            //   throw new NotImplementedException();
        }

        /// <summary>
        /// Complexifies the specified polygon so that no edge is longer than the maxAllowableLength.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="targetNumberOfPoints">The target number of points.</param>
        /// <returns>Polygon.</returns>
        public static void Complexify(this Polygon polygon, int targetNumberOfPoints)
        {
            // throw new NotImplementedException();
        }
        #endregion
    }
}