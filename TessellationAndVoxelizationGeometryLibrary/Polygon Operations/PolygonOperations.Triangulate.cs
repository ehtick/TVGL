﻿// Copyright 2015-2020 Design Engineering Lab
// This file is a part of TVGL, Tessellation and Voxelization Geometry Library
// https://github.com/DesignEngrLab/TVGL
// It is licensed under MIT License (see LICENSE.txt for details)
using System;
using System.Collections.Generic;
using System.Linq;
using Priority_Queue;
using TVGL.Numerics;

namespace TVGL.TwoDimensional
{
    /// <summary>
    /// Triangulates a Polygon into faces in O(n log n) time.
    /// </summary>
    ///  <References>
    ///     The new approach is based on how it is presented in 
    ///     the book
    ///     "Computational geometry: algorithms and applications". 2000
    ///     Authors: de Berg, Mark and van Kreveld, Marc and Overmars, Mark and Schwarzkopf, Otfried and Overmars, M
    /// </References>
    /// A good summary of how the monotone polygons are created can be seen in the video: https://youtu.be/IkA-2Y9lBvM
    /// and the algorithm for triangulating the monotone polygons can be found here: https://youtu.be/pfXXgV9u6cw
    public static partial class PolygonOperations
    {
        /// <summary>
        /// Triangulates the specified loop of 3D vertices using the projection from the provided normal.
        /// </summary>
        /// <param name="vertexLoop">The vertex loop.</param>
        /// <param name="normal">The normal direction.</param>
        /// <returns>IEnumerable&lt;Vertex[]&gt; where each represents a triangular polygonal face.</returns>
        /// <exception cref="ArgumentException">The vertices must all have a unique IndexInList value - vertexLoop</exception>
        public static IEnumerable<Vertex[]> Triangulate(this IEnumerable<Vertex> vertexLoop, Vector3 normal)
        {
            var transform = normal.TransformToXYPlane(out _);
            var coords = new List<Vertex2D>();
            var indexToVertexDict = new Dictionary<int, Vertex>();
            foreach (var vertex in vertexLoop)
            {
                coords.Add(new Vertex2D(vertex.ConvertTo2DCoordinates(transform), vertex.IndexInList, -1));
                if (indexToVertexDict.ContainsKey(vertex.IndexInList))
                    throw new ArgumentException("The vertices must all have a unique IndexInList value", nameof(vertexLoop));
                indexToVertexDict.Add(vertex.IndexInList, vertex);
            }
            var polygon = new Polygon(coords);
            foreach (var triangleIndices in polygon.TriangulateToIndices(false))
                yield return new[]
                    {indexToVertexDict[triangleIndices[0]], indexToVertexDict[triangleIndices[1]],
                        indexToVertexDict[triangleIndices[2]]};
        }
        /// <summary>
        /// Triangulates the specified loop of 3D vertices using the projection from the provided normal.
        /// </summary>
        /// <param name="vertexLoops">The vertex loops.</param>
        /// <param name="normal">The normal direction.</param>
        /// <returns>IEnumerable&lt;Vertex[]&gt; where each represents a triangular polygonal face.</returns>
        /// <exception cref="ArgumentException">The vertices must all have a unique IndexInList value - vertexLoop</exception>
        public static IEnumerable<Vertex[]> Triangulate(this IEnumerable<IList<Vertex>> vertexLoops, Vector3 normal)
        {
            var transform = normal.TransformToXYPlane(out _);
            var polygons = new List<Polygon>();
            var indexToVertexDict = new Dictionary<int, Vertex>();
            foreach (var vertexLoop in vertexLoops)
            {
                var coords = new List<Vertex2D>();
                foreach (var vertex in vertexLoop)
                {
                    coords.Add(new Vertex2D(vertex.ConvertTo2DCoordinates(transform), vertex.IndexInList, -1));
                    if (indexToVertexDict.ContainsKey(vertex.IndexInList))
                        throw new ArgumentException("The vertices must all have a unique IndexInList value", nameof(vertexLoops));
                    indexToVertexDict.Add(vertex.IndexInList, vertex);
                }
                polygons.Add(new Polygon(coords));
            }
            polygons = polygons.CreateShallowPolygonTrees(false);
            foreach (var polygon in polygons)
            {
                foreach (var triangleIndices in polygon.TriangulateToIndices(false))
                    yield return new[]
                        {indexToVertexDict[triangleIndices[0]], indexToVertexDict[triangleIndices[1]],
                        indexToVertexDict[triangleIndices[2]]};
            }
        }


        /// <summary>
        /// Triangulates the specified polygons which may include holes. However, the .
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="reIndexPolygons">if set to <c>true</c> [re index polygons].</param>
        /// <returns>List&lt;System.Int32[]&gt;.</returns>
        public static IEnumerable<Vector2[]> TriangulateToCoordinates(this Polygon polygon, bool reIndexPolygons = true)
        {
            var triIndices = polygon.TriangulateToIndices(reIndexPolygons);
            var index2CoordsDict = polygon.AllPolygons.SelectMany(p => p.Vertices).ToDictionary(v => v.IndexInList, v => v.Coordinates);
            return triIndices.Select(ti => new[] { index2CoordsDict[ti[0]], index2CoordsDict[ti[1]], index2CoordsDict[ti[2]] });
        }

        /// <summary>
        /// Triangulates the specified polygons which may include holes. However, the .
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="reIndexPolygons">if set to <c>true</c> [re index polygons].</param>
        /// <returns>List&lt;System.Int32[]&gt;.</returns>
        public static List<int[]> TriangulateToIndices(this Polygon polygon, bool reIndexPolygons = true)
        {
            if (!polygon.IsPositive)
                throw new ArgumentException("Triangulate Polygon requires a positive polygon. A negative one was provided.", nameof(polygon));

            int numVertices;
            if (reIndexPolygons)
            {
                var index = 0;
                foreach (var subPolygon in polygon.AllPolygons)
                    foreach (var vertex in subPolygon.Vertices)
                        vertex.IndexInList = index++;
                numVertices = index;
            }
            else numVertices = polygon.AllPolygons.Sum(p => p.Vertices.Count);

            if (numVertices <= 2) return new List<int[]>();
            if (numVertices == 3) return new List<int[]> { polygon.Vertices.Select(v => v.IndexInList).ToArray() };
            if (numVertices == 4)
            {
                polygon.MakePolygonEdgesIfNonExistent();
                var verts = polygon.Vertices.Select(v => v.IndexInList).ToList();
                var concaveEdge = polygon.Vertices.FirstOrDefault(v => v.EndLine.Vector.Cross(v.StartLine.Vector) < 0);
                if (concaveEdge != null)
                {
                    while (verts[0] != concaveEdge.IndexInList)
                    {
                        verts.Add(verts[0]);
                        verts.RemoveAt(0);
                    }
                }
                return new List<int[]> { new[] { verts[0], verts[1], verts[2] }, new[] { verts[0], verts[2], verts[3] } };
            }
            var triangleFaceList = new List<int[]>();
            // this is the returned list of triangles. Well, not actually triangles but three integers each - corresponding
            // to the 3 indices of the input polygon's Vertex2D

            // in case this is a deep polygon tree - recurse down and solve for the inner positive polygons
            foreach (var hole in polygon.InnerPolygons)
                foreach (var smallInnerPolys in hole.InnerPolygons)
                    triangleFaceList.AddRange(TriangulateToIndices(smallInnerPolys, false));

            const int maxNumberOfAttempts = 10;
            var attempts = 0;
            var random = new Random(0);
            var successful = false;
            var angle = random.NextDouble() * 2 * Math.PI;
            var localTriangleFaceList = new List<int[]>();
            do
            {
                var c = Math.Cos(angle);
                var s = Math.Sin(angle);
                try
                {
                    localTriangleFaceList.Clear();
                    if (angle != 0)
                    {
                        var rotateMatrix = new Matrix3x3(c, s, -s, c, 0, 0);
                        polygon.Transform(rotateMatrix);
                    }
                    foreach (var monoPoly in CreateXMonotonePolygons(polygon))
                        localTriangleFaceList.AddRange(TriangulateMonotonePolygon(monoPoly));
                    successful = true;
                    if (angle != 0)
                    {
                        var rotateMatrix = new Matrix3x3(c, -s, s, c, 0, 0);
                        polygon.Transform(rotateMatrix);
                    }
                }
                catch
                {
                    angle = random.NextDouble() * 2 * Math.PI;
                }
            } while (!successful && attempts++ < maxNumberOfAttempts);
            if (!successful)
                throw new Exception("Unable to triangulate polygon. Consider simplifying to remove negligible edges or"
                    + " check for self-intersections.");
            triangleFaceList.AddRange(localTriangleFaceList);
            return triangleFaceList;
        }

        public static IEnumerable<Polygon> CreateXMonotonePolygons(this Polygon polygon)
        {
            polygon.MakePolygonEdgesIfNonExistent();
            var connections = FindInternalDiagonalsForMonotone(polygon);
            foreach (var edge in polygon.Edges)
                AddNewConnection(connections, edge.FromPoint, edge.ToPoint);
            foreach (var edge in polygon.InnerPolygons.SelectMany(p => p.Edges))
                AddNewConnection(connections, edge.FromPoint, edge.ToPoint);
            while (connections.Any())
            {
                var startingConnectionKVP = connections.First();
                var start = startingConnectionKVP.Key;
                var newVertices = new List<Vertex2D> { start };
                var current = start;
                var nextConnections = startingConnectionKVP.Value;
                Vertex2D next = nextConnections[0];
                while (next != start)
                {
                    newVertices.Add(next);
                    RemoveConnection(connections, current, next);
                    current = next;
                    nextConnections = connections[current];
                    if (nextConnections.Count == 1) next = nextConnections[0];
                    else next = ChooseTightestLeftTurn(nextConnections, current,
                        current.Coordinates - newVertices[^2].Coordinates);
                }
                RemoveConnection(connections, current, next);
                yield return new Polygon(newVertices.Select(v => v.Copy()));
            }
        }

        private static Vertex2D ChooseTightestLeftTurn(List<Vertex2D> nextVertices, Vertex2D current, Vector2 lastVector)
        {
            var minAngle = double.PositiveInfinity;
            Vertex2D bestVertex = null;
            foreach (var vertex in nextVertices)
            {
                if (vertex == current) continue;
                var angle = lastVector.SmallerAngleBetweenVectors(vertex.Coordinates - current.Coordinates);
                if (minAngle > angle && !angle.IsNegligible())
                {
                    minAngle = angle;
                    bestVertex = vertex;
                }
            }
            return bestVertex;
        }

        private static Dictionary<Vertex2D, List<Vertex2D>> FindInternalDiagonalsForMonotone(Polygon polygon)
        {
            var orderedListsOfVertices = new List<Vertex2D[]>();
            orderedListsOfVertices.Add(polygon.OrderedXVertices);
            foreach (var hole in polygon.InnerPolygons)
                orderedListsOfVertices.Add(hole.OrderedXVertices);
            var sortedVertices = CombineXSortedVerticesIntoOneCollection(orderedListsOfVertices);
            var connections = new Dictionary<Vertex2D, List<Vertex2D>>();
            // the edgeDatums are the current edges in the sweep. The Vertex is the past polygon point (aka helper)
            // that is often connected to the current vertex in the sweep. The boolean is only true when the vertex
            // was a merge vertex.
            var edgeDatums = new Dictionary<PolygonEdge, (Vertex2D, bool)>();
            var tolerance = polygon.GetToleranceForPolygon();
            foreach (var vertex in sortedVertices)
            {
                var monoChange = GetMonotonicityChange(vertex, tolerance);
                var cornerCross = vertex.EndLine.Vector.Cross(vertex.StartLine.Vector);
                if (monoChange == MonotonicityChange.SameAsPrevious || monoChange == MonotonicityChange.Neither || monoChange == MonotonicityChange.Y)
                // then it's regular
                {
                    if (vertex.StartLine.Vector.X.IsPositiveNonNegligible(tolerance) || vertex.EndLine.Vector.X.IsPositiveNonNegligible(tolerance) ||  //headed in the positive x direction (enclosing along the bottom)
                        (vertex.StartLine.Vector.X.IsNegligible() && vertex.EndLine.Vector.X.IsNegligible() && vertex.StartLine.Vector.Y.IsPositiveNonNegligible(tolerance)))
                    {   // in the CCW direction or along the bottom
                        MakeNewDiagonalEdgeIfMerge(connections, edgeDatums, vertex.EndLine, vertex);
                        edgeDatums.Remove(vertex.EndLine);
                        edgeDatums.Add(vertex.StartLine, (vertex, false));
                    }
                    else // then in the CW direction along the top
                    {
                        var closestDatumEdge = FindClosestLowerDatum(edgeDatums.Keys, vertex.Coordinates);
                        MakeNewDiagonalEdgeIfMerge(connections, edgeDatums, closestDatumEdge, vertex);
                        edgeDatums[closestDatumEdge] = (vertex, false);
                    }
                }
                else if (!cornerCross.IsNegativeNonNegligible(tolerance)) //then either start or end
                {
                    if ((vertex.StartLine.Vector.X.IsPositiveNonNegligible(tolerance) && vertex.EndLine.Vector.X.IsNegativeNonNegligible(tolerance)) || // then start
                        (vertex.StartLine.Vector.X.IsPositiveNonNegligible(tolerance) && vertex.EndLine.Vector.X.IsNegligible() && vertex.EndLine.Vector.Y.IsNegativeNonNegligible(tolerance)))
                        edgeDatums.Add(vertex.StartLine, (vertex, false));
                    else // then it's an end
                    {
                        MakeNewDiagonalEdgeIfMerge(connections, edgeDatums, vertex.EndLine, vertex);
                        edgeDatums.Remove(vertex.EndLine);
                    }
                }
                else //then either split or merge
                {
                    if ((vertex.StartLine.Vector.X.IsPositiveNonNegligible(tolerance) && vertex.EndLine.Vector.X.IsNegativeNonNegligible(tolerance)) || // then split
                       (vertex.StartLine.Vector.Y.IsPositiveNonNegligible(tolerance) && vertex.EndLine.Vector.Y.IsPositiveNonNegligible(tolerance)))
                    {   // it's a split
                        var closestDatumEdge = FindClosestLowerDatum(edgeDatums.Keys, vertex.Coordinates);
                        var helperVertex = edgeDatums[closestDatumEdge].Item1;
                        AddNewConnection(connections, vertex, helperVertex);
                        AddNewConnection(connections, helperVertex, vertex);
                        edgeDatums[closestDatumEdge] = (vertex, false);
                        edgeDatums.Add(vertex.StartLine, (vertex, false));
                    }
                    else //then it's a merge
                    {
                        MakeNewDiagonalEdgeIfMerge(connections, edgeDatums, vertex.EndLine, vertex);
                        edgeDatums.Remove(vertex.EndLine);
                        PolygonEdge closestDatum = FindClosestLowerDatum(edgeDatums.Keys, vertex.Coordinates);
                        if (closestDatum != null)
                        {
                            MakeNewDiagonalEdgeIfMerge(connections, edgeDatums, closestDatum, vertex);
                            edgeDatums[closestDatum] = (vertex, true);
                        }
                    }
                }
            }
            return connections;
        }

        private static void MakeNewDiagonalEdgeIfMerge(Dictionary<Vertex2D, List<Vertex2D>> connections,
            Dictionary<PolygonEdge, (Vertex2D, bool)> edgeDatums, PolygonEdge datum, Vertex2D vertex)
        {
            if (!edgeDatums.ContainsKey(datum)) return;
            var prevLineHelperData = edgeDatums[datum];
            var helperVertex = prevLineHelperData.Item1;
            var isMergePoint = prevLineHelperData.Item2;
            if (isMergePoint) //if this was a merge point
            {
                AddNewConnection(connections, vertex, helperVertex);
                AddNewConnection(connections, helperVertex, vertex);
            }
        }

        private static void AddNewConnection(Dictionary<Vertex2D, List<Vertex2D>> connections, Vertex2D fromVertex, Vertex2D toVertex)
        {
            if (connections.ContainsKey(fromVertex))
                connections[fromVertex].Add(toVertex);
            else
            {
                var newToVertices = new List<Vertex2D> { toVertex };
                connections.Add(fromVertex, newToVertices);
            }
        }
        private static void RemoveConnection(Dictionary<Vertex2D, List<Vertex2D>> connections, Vertex2D fromVertex, Vertex2D toVertex)
        {
            var toVertices = connections[fromVertex];
            if (toVertices.Count == 1)
            {
                if (toVertices[0] == toVertex)
                    connections.Remove(fromVertex);
                else throw new Exception();
            }
            else toVertices.Remove(toVertex);
        }


        private static PolygonEdge FindClosestLowerDatum(IEnumerable<PolygonEdge> edges, Vector2 point, double minfeasible = 0.0)
        {
            var numEdges = 0;
            var closestDistance = double.PositiveInfinity;
            PolygonEdge closestEdge = null;
            foreach (var edge in edges)
            {
                numEdges++;
                var intersectionYValue = edge.FindYGivenX(point.X, out var betweenPoints);
                if (!betweenPoints) continue;
                var delta = point.Y - intersectionYValue;
                if (delta >= minfeasible && delta < closestDistance)
                {
                    closestDistance = delta;
                    closestEdge = edge;
                }
            }

            //if (closestEdge == null && numEdges > 0) return FindClosestLowerDatum(edges, point, double.NegativeInfinity);
            return closestEdge;
        }


        private static IEnumerable<Vertex2D> CombineXSortedVerticesIntoOneCollection(List<Vertex2D[]> orderedListsOfVertices)
        {
            var numLists = orderedListsOfVertices.Count;
            var currentIndices = new int[numLists];
            var priorityQueue = new SimplePriorityQueue<int, Vertex2D>(new VertexSortedByXFirst());
            for (int j = 0; j < orderedListsOfVertices.Count; j++)
                priorityQueue.Enqueue(j, orderedListsOfVertices[j][0]);
            // the following code is written verbosely. I'm trusting the compiler optimization
            // to ensure that it speed things up.
            while (priorityQueue.Count > 0)
            {
                var listWithLowestEntry = priorityQueue.First;
                var vertexList = orderedListsOfVertices[listWithLowestEntry];
                var indexInThatList = currentIndices[listWithLowestEntry];
                var nextVertex = vertexList[currentIndices[listWithLowestEntry]];
                yield return nextVertex;
                indexInThatList++;
                currentIndices[listWithLowestEntry] = indexInThatList;
                if (indexInThatList < vertexList.Length)
                    priorityQueue.UpdatePriority(listWithLowestEntry, vertexList[indexInThatList]);
                else priorityQueue.Dequeue();
            }
        }



        private static IEnumerable<Vertex2D> CombineYSortedVerticesIntoOneCollection(List<Vertex2D[]> orderedListsOfVertices)
        {
            var numLists = orderedListsOfVertices.Count;
            var currentIndices = new int[numLists];
            var priorityQueue = new SimplePriorityQueue<int, Vertex2D>(new VertexSortedByYFirst());
            for (int j = 0; j < orderedListsOfVertices.Count; j++)
                priorityQueue.Enqueue(j, orderedListsOfVertices[j][0]);
            // the following code is written verbosely. I'm trusting the compiler optimization
            // to ensure that it speed things up.
            while (priorityQueue.Count > 0)
            {
                var listWithLowestEntry = priorityQueue.Dequeue();
                var vertexList = orderedListsOfVertices[listWithLowestEntry];
                var indexInThatList = currentIndices[listWithLowestEntry];
                var nextVertex = vertexList[currentIndices[listWithLowestEntry]];
                yield return nextVertex;
                indexInThatList++;
                currentIndices[listWithLowestEntry] = indexInThatList;
                if (indexInThatList < vertexList.Length)
                    priorityQueue.Enqueue(listWithLowestEntry, vertexList[indexInThatList]);
            }
        }


        private static IEnumerable<int[]> TriangulateMonotonePolygon(Polygon monoPoly)
        {
            monoPoly.MakePolygonEdgesIfNonExistent();
            if (monoPoly.Vertices.Count < 3) yield break;
            if (monoPoly.Vertices.Count == 3)
            {
                yield return new[] { monoPoly.Vertices[0].IndexInList, monoPoly.Vertices[1].IndexInList, monoPoly.Vertices[2].IndexInList };
                yield break;
            }
            Vertex2D bottomVertex = monoPoly.Vertices[0]; // Q: why is this called bottom and not leftmost?
            // A: because in the loop below it becomes the vertex on the bottom branch of the polygon
            foreach (var vertex in monoPoly.Vertices.Skip(1))
                if (bottomVertex.X > vertex.X || (bottomVertex.X == vertex.X && bottomVertex.Y > vertex.Y))
                    bottomVertex = vertex;
            var topVertex = bottomVertex; //initialize top to the same as bottom
            var concaveFunnelStack = new Stack<Vertex2D>();
            concaveFunnelStack.Push(bottomVertex);
            var nextVertex = NextXVertex(ref bottomVertex, ref topVertex, out var belongsToBottom);
            concaveFunnelStack.Push(nextVertex);

            do
            {
                nextVertex = NextXVertex(ref bottomVertex, ref topVertex, out var newVertexIsOnBottom);
                if (newVertexIsOnBottom == belongsToBottom)
                {
                    Vertex2D vertex1 = concaveFunnelStack.Pop();
                    Vertex2D vertex2 = concaveFunnelStack.Pop();
                    while (vertex2 != null && newVertexIsOnBottom ==
                        (vertex1.Coordinates - nextVertex.Coordinates).Cross(vertex2.Coordinates - vertex1.Coordinates) < 0)
                    {
                        if (newVertexIsOnBottom)
                            yield return new[] { nextVertex.IndexInList, vertex2.IndexInList, vertex1.IndexInList };
                        else yield return new[] { nextVertex.IndexInList, vertex1.IndexInList, vertex2.IndexInList };
                        vertex1 = vertex2;
                        vertex2 = concaveFunnelStack.Any() ? concaveFunnelStack.Pop() : null;
                    }
                    if (vertex2 != null) concaveFunnelStack.Push(vertex2);
                    concaveFunnelStack.Push(vertex1);
                    concaveFunnelStack.Push(nextVertex);
                }
                else //connect this to all on the stack
                {
                    Vertex2D topOfStackVertex = null;
                    Vertex2D prevVertex2 = null;
                    while (concaveFunnelStack.Any())
                    {
                        var prevVertex1 = concaveFunnelStack.Pop();
                        topOfStackVertex ??= prevVertex1;
                        if (prevVertex2 != null)
                        {
                            if (newVertexIsOnBottom)
                                yield return new[] { nextVertex.IndexInList, prevVertex2.IndexInList, prevVertex1.IndexInList };
                            else yield return new[] { nextVertex.IndexInList, prevVertex1.IndexInList, prevVertex2.IndexInList };
                        }
                        prevVertex2 = prevVertex1;
                    }
                    concaveFunnelStack.Push(topOfStackVertex);
                    concaveFunnelStack.Push(nextVertex);
                    belongsToBottom = newVertexIsOnBottom;
                }
            } while (bottomVertex != null);
        }

        private static Vertex2D NextXVertex(ref Vertex2D bottomVertex, ref Vertex2D topVertex, out bool belongsToBottom)
        {
            var nextTopVertex = topVertex.EndLine.FromPoint;
            var nextBottomVertex = bottomVertex.StartLine.ToPoint;
            if (nextTopVertex == nextBottomVertex)
            {
                topVertex = bottomVertex = null;
                belongsToBottom = false;
                return nextTopVertex;
            }
            if (nextBottomVertex.X <= nextTopVertex.X)
            {
                belongsToBottom = true;
                bottomVertex = nextBottomVertex;
                return bottomVertex;
            }
            belongsToBottom = false;
            topVertex = nextTopVertex;
            return topVertex;
        }

    }
}