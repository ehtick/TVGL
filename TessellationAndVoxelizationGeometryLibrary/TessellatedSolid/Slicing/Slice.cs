﻿// ***********************************************************************
// Assembly         : TessellationAndVoxelizationGeometryLibrary
// Author           : matth
// Created          : 04-03-2023
//
// Last Modified By : matth
// Last Modified On : 04-14-2023
// ***********************************************************************
// <copyright file="Slice.cs" company="Design Engineering Lab">
//     2014
// </copyright>
// <summary></summary>
// ***********************************************************************
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TVGL
{
    /// <summary>
    /// The Slice class includes static functions for cutting a tessellated solid.
    /// Slice4 Performs the slicing operation on the prescribed flat plane. This is a NON-Destructive
    /// operation, and returns two of more new tessellated solids  in the "out" parameter
    /// lists.
    /// However, it does reference the solid's faces. So this may conflict with parallel processing.
    /// </summary>
    public static class Slice
    {
        #region Define Contact at a Flat Plane

        /// <summary>
        /// This slice function makes a seperate cut for the positive and negative side,
        /// at a specified offset in both directions. It rebuilds straddle triangles,
        /// but only uses one of the two straddle edge intersection vertices to prevent
        /// tiny triangles from being created.
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="solids">The resulting solids</param>
        /// <param name="contactData">The contact data.</param>
        /// <param name="setIntersectionGroups">Determines whether to output the intersections (2D cross sections and other info)</param>
        /// <param name="undoPlaneOffset">Determines whether to construct new faces exactly on the cutting plane</param>
        public static void SliceOnInfiniteFlat(this TessellatedSolid ts, Plane plane,
            out List<TessellatedSolid> solids, out ContactData contactData, bool setIntersectionGroups = false,
            bool undoPlaneOffset = false)
        {
            if (!GetSliceContactData(ts, plane, out contactData, setIntersectionGroups, undoPlaneOffset: undoPlaneOffset))
            {
                solids = new List<TessellatedSolid>();
                Message.output("CuttingPlane does not cut through the given solid.", 2);
                return;
            }
            MakeSolids(contactData, ts.Units, out solids);
            var totalVolume1 = solids.Sum(solid => solid.Volume);
            var totalVolume2 = contactData.SolidContactData.Sum(solidContactData => solidContactData.Volume(ts.SameTolerance));
            if (!totalVolume2.IsPracticallySame(totalVolume1, 100))
            {
                Message.output("Error with Volume function calculation in TVGL. SolidContactData Volumes and Solid Volumes should match, since they use all the same faces.", 2);
                Message.output("Contact Data Total Volume = " + totalVolume2 + ". Solid Total Volume = " + totalVolume1, 2);
            }
        }

        /// <summary>
        /// This slice function makes a seperate cut for the positive and negative side,
        /// at a specified offset in both directions. It rebuilds straddle triangles,
        /// but only uses one of the two straddle edge intersection vertices to prevent
        /// tiny triangles from being created.
        /// This version allows the user to input IntersectionGroups ignore by index.
        /// These are set by useing OnInfiniteFlat with setIntersectionGroups = true.
        /// Limitation: The finite plane is limited in that it can only ignore entire groups of loops,
        /// so if two positive side groups connect with one negative side loop, they cannot be seperated
        /// individually (e.g., if two pegs are connected to a large face, you cannot remove only one peg
        /// with a flat exactly on the large face. However, you could cut it off by moving the plane
        /// slightly toward the peg. That would make two IntersectionGroups rather than one).
        /// This is because Slice was written to re-triangulate exposed surfaces from the intersection loops.
        /// This cannot currently be done for partial intersection loops.
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="solids">The solids.</param>
        /// <param name="intersectionsToIgnore">The intersections to ignore.</param>
        /// <param name="newContactData">The new contact data.</param>
        public static void SliceOnFiniteFlatByIngoringIntersections(this TessellatedSolid ts, Plane plane,
            out List<TessellatedSolid> solids, ICollection<IntersectionGroup> intersectionsToIgnore, out ContactData newContactData)
        {
            var loopsToIgnore = new List<int>();
            foreach (var intersectionGroup in intersectionsToIgnore)
            {
                loopsToIgnore.AddRange(intersectionGroup.GetLoopIndices());
            }

            if (!GetSliceContactData(ts, plane, out newContactData, false, loopsToIgnore))
            {
                solids = new List<TessellatedSolid>();
                Message.output("CuttingPlane does not cut through the given solid.", 2);
                return;
            }
            MakeSolids(newContactData, ts.Units, out solids);
            var totalVolume1 = solids.Sum(solid => solid.Volume);
            var totalVolume2 = newContactData.SolidContactData.Sum(solidContactData => solidContactData.Volume(ts.SameTolerance));
            if (!totalVolume2.IsPracticallySame(totalVolume1, 100))
            {
                Message.output("Error with Volume function calculation in TVGL. SolidContactData Volumes and Solid Volumes should match, since they use all the same faces.", 2);
                Message.output("Contact Data Total Volume = " + totalVolume2 + ". Solid Total Volume = " + totalVolume1, 2);
            }
        }

        /// <summary>
        /// This slice function makes a seperate cut for the positive and negative side,
        /// at a specified offset in both directions. It rebuilds straddle triangles,
        /// but only uses one of the two straddle edge intersection vertices to prevent
        /// tiny triangles from being created.
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="positiveSideSolid">The solid that is on the positive side of the plane
        /// This means that are on the side that the normal faces.</param>
        /// <param name="negativeSideSolid">The solid on the negative side of the plane.</param>
        public static void SliceOnFlatAsSingleSolids(this TessellatedSolid ts, Plane plane,
            out TessellatedSolid positiveSideSolid, out TessellatedSolid negativeSideSolid)
        {
            if (!GetSliceContactData(ts, plane, out var contactData, false))
            {
                positiveSideSolid = null;
                negativeSideSolid = null;
                Message.output("CuttingPlane does not cut through the given solid.", 2);
                return;
            }
            //MakeSingleSolidOnEachSideOfInfitePlane(contactData, ts.Units, out positiveSideSolid, out negativeSideSolid);
            List<TriangleFace> positiveSideFaces = new List<TriangleFace>(contactData.PositiveSideContactData.SelectMany(solidContactData => solidContactData.AllFaces));
            positiveSideSolid = new TessellatedSolid(positiveSideFaces, null, TessellatedSolidBuildOptions.Default, units: ts.Units);
            var negativeSideFaces = new List<TriangleFace>(contactData.NegativeSideContactData.SelectMany(solidContactData => solidContactData.AllFaces));
            negativeSideSolid = new TessellatedSolid(negativeSideFaces, null, TessellatedSolidBuildOptions.Default, units: ts.Units);
        }

        /// <summary>
        /// Gets the contact data for a slice, without making the individual solids.
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="contactData">The contact data.</param>
        /// <param name="setIntersectionGroups">if set to <c>true</c> [set intersection groups].</param>
        /// <param name="loopsToIgnore">The loops to ignore.</param>
        /// <param name="undoPlaneOffset">if set to <c>true</c> [undo plane offset].</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        public static bool GetSliceContactData(this TessellatedSolid ts, Plane plane, out ContactData contactData,
            bool setIntersectionGroups, ICollection<int> loopsToIgnore = null, bool undoPlaneOffset = false)
        {
            var posPlaneShift = 0.0;
            var negPlaneShift = 0.0;
            var distancesToPlane = ts.Vertices.Select(v => v.Coordinates.Dot(plane.Normal)).ToList();
            distancesToPlane.SetPositiveAndNegativeShifts(plane.DistanceToOrigin, ts.SameTolerance,
                ref posPlaneShift, ref negPlaneShift);

            DivideUpFaces(ts, new Plane(plane.DistanceToOrigin + posPlaneShift, plane.Normal),
                out var positiveSideLoops, 1, new List<double>(distancesToPlane), posPlaneShift, loopsToIgnore, undoPlaneOffset);
            DivideUpFaces(ts, new Plane(plane.DistanceToOrigin + negPlaneShift, plane.Normal),
                out var negativeSideLoops, -1, new List<double>(distancesToPlane), negPlaneShift, loopsToIgnore, undoPlaneOffset);
            #endregion

            var groupOfLoops = GroupLoops(positiveSideLoops, negativeSideLoops, plane, setIntersectionGroups, ts.SameTolerance,
                out var intersectionLoops);
            var contactDataForEachSolid = MakeContactDataForEachSolid(ts, groupOfLoops);
            contactData = new ContactData(contactDataForEachSolid, intersectionLoops, plane);
            return true;
        }

        /// <summary>
        /// Returns lists of solids, given contact data for this slice
        /// </summary>
        /// <param name="contactData">The contact data.</param>
        /// <param name="unitType">Type of the unit.</param>
        /// <param name="solids">The solids.</param>
        public static void MakeSolids(this ContactData contactData, UnitType unitType, out List<TessellatedSolid> solids)
        {
            solids = contactData.SolidContactData.Select(solidContactData => new TessellatedSolid(solidContactData.AllFaces, null,
                TessellatedSolidBuildOptions.Default, units: unitType)).ToList();
        }


        /// <summary>
        /// Groups the loops and creates triangles for the newly exposed surfaces.
        /// The direction of each loop is not necessary as it can be inferred.
        /// </summary>
        /// <param name="posSideLoops">The on side loops.</param>
        /// <param name="negSideLoops">The neg side loops.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="setIntersectionGroups">if set to <c>true</c> [set intersection groups].</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="intersectionGroups">The intersection groups.</param>
        /// <returns>IEnumerable&lt;SolidContactData&gt;.</returns>
        /// <exception cref="System.Exception">This loop should always be positive. Check to may sure the group was created correctly in 'OrderLoops'
        /// or
        /// This loop should always be negative. Check to may sure the group was created correctly in 'OrderLoops'
        /// or
        /// The face should be in this list. Otherwise, it should not have been selected with face wrapping</exception>
        private static ISet<GroupOfLoops> GroupLoops(IList<Loop> posSideLoops, IList<Loop> negSideLoops,
            Plane plane, bool setIntersectionGroups, double tolerance, out List<IntersectionGroup> intersectionGroups)
        {
            //Process the positive and negative side loops to create List<GroupOfLoops>. This requires the 
            //directionallity (hole vs. filled) and pairing of loops into groups, and the triangulation of
            //those groups.
            //Since the positive side loops actually need to look in reverse to see the new faces, use the reverse direction.
            //Vise-versa for the negative side.
            var groupsOfLoops = new HashSet<GroupOfLoops>();
            var posSideGroups = new List<GroupOfLoops>();
            var negSideGroups = new List<GroupOfLoops>();
            for (var k = -1; k <= 1; k += 2) //-1 for positive side and 1 for negative side.
            {
                var direction = plane.Normal * k;
                var transform = direction.TransformToXYPlane(out _);
                var loops = k == -1 ? posSideLoops : negSideLoops;
                var allPolygons = new List<Polygon>();
                var j = 0;
                var allVertices = new List<Vertex>();
                for (int i = 0; i < loops.Count; i++)
                {
                    allVertices.AddRange(loops[i].VertexLoop);
                    var vertices = new List<Vertex2D>();
                    foreach (var vertex in loops[i].VertexLoop)
                        vertices.Add(new Vertex2D(vertex.ConvertTo2DCoordinates(transform), j++, i));
                    allPolygons.Add(new Polygon(vertices, i));
                }
                foreach (var polygon in allPolygons.CreateShallowPolygonTrees(false))
                {
                    var indicesOfTriangles = polygon.TriangulateToIndices();
                    var positiveLoop = loops[polygon.Index];
                    var negativeLoops = polygon.InnerPolygons.Select(p => loops[p.Index]).ToList();
                    var planeFaces = new List<TriangleFace>();
                    var groupOfOnPlaneFaces = indicesOfTriangles.Select(triIndices => new TriangleFace(
                        allVertices[triIndices.A], allVertices[triIndices.B], allVertices[triIndices.C], false));
                    var groupOfLoops = new GroupOfLoops(positiveLoop, negativeLoops, groupOfOnPlaneFaces);
                    groupsOfLoops.Add(groupOfLoops);
                    if (k == -1) posSideGroups.Add(groupOfLoops);
                    else negSideGroups.Add(groupOfLoops);
                }
            }

            intersectionGroups = new List<IntersectionGroup>();
            if (!setIntersectionGroups) return groupsOfLoops;

            //Pair up the groups of loops
            //Note: since projecting and intersection are time intensive. Do not perform this next operation unless
            //considering finite planes. This information is not necessary for infinite planes.
            var g = 0;
            foreach (var group in groupsOfLoops) group.SetCrossSection2D(plane);
            foreach (var posGroup in posSideGroups)
            {
                //Find all the negative side groups that it intersects with.
                foreach (var negGroup in negSideGroups)
                {
                    var intersection = posGroup.CrossSection2D.Intersect(negGroup.CrossSection2D);
                    if (intersection == null || !intersection.Any() ||
                        intersection.Sum(p => p.Area).IsNegligible(tolerance)) continue;
                    //Check if this intersection should be paired with an existing intersection group.
                    var intersectionGroupFound = false;
                    foreach (var intersectionGroup in intersectionGroups)
                    {
                        if (!intersectionGroup.GroupOfLoops.Contains(posGroup) &&
                            !intersectionGroup.GroupOfLoops.Contains(negGroup)) continue;
                        //Add this intersection to the already existing group.
                        intersectionGroup.GroupOfLoops.Add(posGroup); //This is a hash, so we can add the same item again without issue.
                        intersectionGroup.GroupOfLoops.Add(negGroup);
                        foreach (var poly in intersection)
                        {
                            intersectionGroup.Intersection2D.Add(poly);
                        }
                        intersectionGroupFound = true;
                    }

                    if (intersectionGroupFound) continue;
                    intersectionGroups.Add(new IntersectionGroup(posGroup, negGroup, intersection, g));
                    g++;
                }
            }
            return groupsOfLoops;
        }

        /// <summary>
        /// Uses face wrapping on the groups of loops to identigy multiple solids.
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <param name="groupsOfLoops">The groups of loops.</param>
        /// <returns>IEnumerable&lt;SolidContactData&gt;.</returns>
        private static IEnumerable<SolidContactData> MakeContactDataForEachSolid(TessellatedSolid ts, ISet<GroupOfLoops> groupsOfLoops)
        {

            //Perform face wrapping (using adjacency to build up a list of all the faces on a solid) -- Similar to 'GetMultipleSolids'
            //The straddle faces form the barrier for the wrapping procedure.
            //Note: Since this function has been updated for finite planes, it is now possible that a negSideLoop is part
            //of the same solid as a posSideLoop (e.g., consider chopping an "S" verically but not through the middle).
            //For this reason, the group of loops from both sides need to be considering at the same time.
            var contactDataForEachSolid = new List<SolidContactData>();
            while (groupsOfLoops.Any())
            {
                var groupOfLoops = groupsOfLoops.First();
                var onPlaneFaces = new List<TriangleFace>(groupOfLoops.OnPlaneFaces);
                var allLoopsBelongingToSolid = new List<GroupOfLoops> { groupOfLoops };
                groupsOfLoops.Remove(groupOfLoops);
                //Push all the adjacent onside faces to a stack
                //Note that blind pockets and holes are also included in this loop, since the onside faces for every loop in the group are included
                var straddleFaceIndices = new HashSet<int>(groupOfLoops.StraddleFaceIndices);
                var facesBelongingToSolid = new HashSet<TriangleFace>();
                var stack = new Stack<TriangleFace>();
                var usedFaces = new HashSet<TriangleFace>();
                foreach (var adjOnsideFaceIndex in groupOfLoops.AdjOnsideFaceIndices)
                {
                    stack.Push(ts.Faces[adjOnsideFaceIndex]);
                    usedFaces.Add(ts.Faces[adjOnsideFaceIndex]);
                }
                while (stack.Any())
                {
                    var face = stack.Pop();
                    if (facesBelongingToSolid.Contains(face)) continue;
                    facesBelongingToSolid.Add(face);
                    foreach (var adjacentFace in face.AdjacentFaces)
                    {
                        if (usedFaces.Contains(adjacentFace)) continue;
                        usedFaces.Add(adjacentFace);
                        if (adjacentFace == null) continue; //This is an error. Handle it in the error function.
                        if (straddleFaceIndices.Contains(adjacentFace.IndexInList)) continue; //Don't add the straddle face. 
                        //If the wrapping gets to any faces that are straddle faces for other loops, then that group of loops is part of the same solid.
                        //Note that blind pockets and holes are also included in this loop, since the onside faces for every loop in the group are included
                        //If the groups have been paired with the intersection loops, do not add group2.
                        var notStraddleEdge = true;
                        var groupsToRemove = new List<GroupOfLoops>();
                        foreach (var group2 in groupsOfLoops)
                        {
                            if (!group2.StraddleFaceIndices.Contains(adjacentFace.IndexInList)) continue;
                            //This is a straddle edge for the current group of loops. Since we are considering both sides at the same time, 
                            //this gets a bit tricky because both loops could have the same straddle faces. To solve this, we have added
                            //a vertex hashset that contains the vertices from the straddle faces that are on the positive/negative sides.
                            //If the face (not the adjacentFace/straddleFace) contains a vertex that matches with this loop, add the loop.
                            //Otherwise, it is on the wrong side of approach. Skip it. 
                            if (!group2.StraddleEdgeOnSideVertices.Contains(face.A) &&
                                !group2.StraddleEdgeOnSideVertices.Contains(face.B) &&
                                !group2.StraddleEdgeOnSideVertices.Contains(face.B)) continue;
                            notStraddleEdge = false;
                            //Update the straddleFaceIndexList
                            foreach (var faceIndex in group2.StraddleFaceIndices)
                            {
                                straddleFaceIndices.Add(faceIndex);
                            }
                            //Don't add the straddle face. 
                            //Add all the adjacenet onside face indexes to the stack
                            foreach (var adjOnsideFaceIndex in group2.AdjOnsideFaceIndices)
                            {
                                if (usedFaces.Contains(ts.Faces[adjOnsideFaceIndex])) continue;
                                usedFaces.Add(ts.Faces[adjOnsideFaceIndex]);
                                stack.Push(ts.Faces[adjOnsideFaceIndex]);
                            }
                            //Add this the loops in this group to this solid
                            allLoopsBelongingToSolid.Add(group2);
                            //Add the onPlane faces 
                            onPlaneFaces.AddRange(group2.OnPlaneFaces);
                            //Remove that group from the list of groups
                            groupsToRemove.Add(group2);
                        }
                        foreach (var group2 in groupsToRemove) groupsOfLoops.Remove(group2);
                        if (notStraddleEdge) stack.Push(adjacentFace);
                    }
                }
                contactDataForEachSolid.Add(new SolidContactData(allLoopsBelongingToSolid, facesBelongingToSolid.ToList(), onPlaneFaces));
            }
            return contactDataForEachSolid;
        }


        /// <summary>
        /// Divides up faces.
        /// Returns a list of onSideFaces from the ts (not including straddle faces), and a list of all the new faces that make up the
        /// halves of the straddle faces that are on this side.
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="loops">The loops.</param>
        /// <param name="isPositiveSide">The is positive side.</param>
        /// <param name="distancesToPlane">The distances to plane.</param>
        /// <param name="planeOffset">The plane offset.</param>
        /// <param name="loopsToIgnore">The loops to ignore.</param>
        /// <param name="undoPlaneOffset">if set to <c>true</c> [undo plane offset].</param>
        /// <exception cref="System.Exception">These should be equal for closed geometry</exception>
        /// <exception cref="System.Exception">This should never happen and will cause errors down the line. Prevent it.</exception>
        /// <exception cref="System.Exception">While loop was unable to complete.</exception>
        /// <exception cref="System.Exception">No good starting edge found. Rewrite the function to find a better edge</exception>
        /// <exception cref="System.Exception">pick a different starting edge</exception>
        /// <exception cref="System.Exception">No shared face exists between these two straddle edges</exception>
        /// <exception cref="System.Exception">This could be a knife edge. But this error will likely cause errors down the line</exception>
        private static void DivideUpFaces(TessellatedSolid ts, Plane plane, out List<Loop> loops, int isPositiveSide,
            IList<double> distancesToPlane, double planeOffset, ICollection<int> loopsToIgnore = null,
            bool undoPlaneOffset = false)
        {
            for (var i = 0; i < distancesToPlane.Count; i++)
                distancesToPlane[i] -= planeOffset;

            //Find all the straddle edges and add the new intersect vertices to both the pos and neg loops.
            var straddleEdges = new List<StraddleEdge>();
            var straddleEdgesDict = new Dictionary<int, Edge>();
            foreach (var edge in ts.Edges)
            {
                var toDistance = distancesToPlane[edge.To.IndexInList];
                var fromDistance = distancesToPlane[edge.From.IndexInList];
                //Check for a straddle edge (Signs are different)
                if (Math.Sign(toDistance) == Math.Sign(fromDistance)) continue;

                //If it is a straddle edge, then figure out which vertex is the offSideVertex (the one we aren't keeping)
                Vertex offSideVertex = isPositiveSide * toDistance > 0 ? edge.From : edge.To;
                straddleEdges.Add(undoPlaneOffset
                    ? new StraddleEdge(edge, plane, offSideVertex, planeOffset)
                    : new StraddleEdge(edge, plane, offSideVertex));
                straddleEdgesDict.Add(edge.IndexInList, edge);
            }

            //Also, find which faces are on the current side of the plane, by using edges.
            //Every face should have either 2 or 0 straddle edges, but never just 1.
            var straddleFaces = new Dictionary<int, TriangleFace>();
            //Set the straddle faces and onSide faces
            foreach (var face in ts.Faces)
            {
                var d1 = distancesToPlane[face.A.IndexInList];
                var d2 = distancesToPlane[face.B.IndexInList];
                var d3 = distancesToPlane[face.C.IndexInList];
                //If all the same signs, then this is on either the positive or negative side 
                if (Math.Sign(d1) == Math.Sign(d2) && Math.Sign(d1) == Math.Sign(d3)) continue;
                //else, it must be a straddle face
                straddleFaces.Add(face.IndexInList, face);
            }
            if (straddleFaces.Count != straddleEdges.Count) throw new Exception("These should be equal for closed geometry");

            //Get loops of straddleEdges 
            var loopsOfStraddleEdges = new List<List<StraddleEdge>>();
            var loopsOfStraddleFaceIndices = new List<HashSet<int>>();
            var maxAttempts = straddleEdges.Count / 3;
            var attempts = 0;
            while (straddleEdges.Any() && attempts < maxAttempts)
            {
                attempts++;
                var loopOfStraddleEdges = new List<StraddleEdge>();
                var loopOfStraddleFaceIndices = new HashSet<int>();
                var straddleEdge = straddleEdges[0];
                loopOfStraddleEdges.Add(straddleEdge);
                straddleEdges.RemoveAt(0);
                var startFace = straddleEdge.Edge.OwnedFace;
                loopOfStraddleFaceIndices.Add(startFace.IndexInList);
                var newStartFace = straddleEdge.NextFace(startFace);
                do
                {
                    loopOfStraddleFaceIndices.Add(newStartFace.IndexInList);
                    var possibleStraddleEdges = new List<StraddleEdge>();
                    foreach (var edge in newStartFace.Edges)
                    {
                        var possibleStraddleEdge = straddleEdges.FirstOrDefault(e => e.Edge == edge);
                        if (possibleStraddleEdge != null)
                        {
                            possibleStraddleEdges.Add(possibleStraddleEdge);
                        }
                    }

                    //Only two straddle edges are possible per face, and the other has already been removed from straddleEdges.
                    if (possibleStraddleEdges.Count != 1) throw new Exception("This should never happen and will cause errors down the line. Prevent it.");
                    straddleEdge = possibleStraddleEdges[0];
                    loopOfStraddleEdges.Add(straddleEdge);
                    straddleEdges.Remove(straddleEdge);
                    var currentFace = newStartFace;
                    newStartFace = straddleEdge.NextFace(currentFace);
                } while (newStartFace != startFace);
                if (loopOfStraddleEdges.Count < 3) continue; //Ignore this loop, since it seems to be a knife edge 
                loopsOfStraddleEdges.Add(loopOfStraddleEdges);
                loopsOfStraddleFaceIndices.Add(loopOfStraddleFaceIndices);
            }
            if (straddleEdges.Any()) throw new Exception("While loop was unable to complete.");

            //Get loops of vertices, adding newly creates faces as you go
            //This is the brains of this function. It loops through the straddle edges to 
            //create new faces. This function avoids creating two new points that are 
            //extremely close together, which should avoid neglible edges and faces.
            //It also keeps track of how many new vertices should be created.
            var newVertexIndex = ts.NumberOfVertices;
            var tolerance = Math.Sqrt(ts.SameTolerance);
            loops = new List<Loop>();
            for (var i = 0; i < loopsOfStraddleEdges.Count; i++)
            {
                var loopIndex = (i + 1) * isPositiveSide;
                if (loopsToIgnore != null && loopsToIgnore.Contains(loopIndex)) continue;
                var loopOfStraddleEdges = loopsOfStraddleEdges[i];
                var straddleEdgeOnSideVertices = loopOfStraddleEdges.Select(e => e.OnSideVertex);
                var straddleFaceIndices = loopsOfStraddleFaceIndices[i];
                var newFaces = new List<TriangleFace>();
                var newEdges = new List<Edge>();
                var loopOfVertices = new List<Vertex>();
                var adjOnsideFaceIndices = new HashSet<int>();
                //Find a good starting edge. One with an intersect vertex far enough away from other intersection vertices.
                var k = 0;
                var length1 = loopOfStraddleEdges.Last().IntersectVertex.Coordinates.Distance(
                            loopOfStraddleEdges[k].IntersectVertex.Coordinates);
                while (length1.IsNegligible(tolerance) && k + 1 != loopOfStraddleEdges.Count - 1)
                {
                    k++;
                    length1 = loopOfStraddleEdges[k - 1].IntersectVertex.Coordinates.Distance(
                        loopOfStraddleEdges[k].IntersectVertex.Coordinates);
                }
                if (k + 1 == loopOfStraddleEdges.Count - 1) throw new Exception("No good starting edge found. Rewrite the function to find a better edge");
                var firstStraddleEdge = loopOfStraddleEdges[k];
                var previousStraddleEdge = firstStraddleEdge;
                var successfull = false;
                do
                {
                    //ToDo: this function allows loops of two vertices if created vertices are too close together
                    k++; //Update the index
                    if (k > loopOfStraddleEdges.Count - 1) k = 0; //Set back to start if necessary
                    var currentStraddleEdge = loopOfStraddleEdges[k];
                    var length = currentStraddleEdge.IntersectVertex.Coordinates.Distance(
                            previousStraddleEdge.IntersectVertex.Coordinates);

                    //If finished, then create the final face and end
                    if (currentStraddleEdge == firstStraddleEdge)
                    {
                        if (length.IsNegligible(tolerance)) throw new Exception("pick a different starting edge");
                        if (loopOfVertices.Last() != previousStraddleEdge.IntersectVertex)
                        {
                            previousStraddleEdge.IntersectVertex.IndexInList = newVertexIndex++;
                            loopOfVertices.Add(previousStraddleEdge.IntersectVertex);
                        }
                        newFaces.AddRange(NewFace(previousStraddleEdge, currentStraddleEdge, straddleEdgesDict, straddleFaces, newEdges, adjOnsideFaceIndices, true));
                        successfull = true;
                    }
                    //If too close together for a good triangle
                    else if (length.IsNegligible(tolerance))
                    {
                        currentStraddleEdge.IntersectVertex = previousStraddleEdge.IntersectVertex;
                        if (!loopOfVertices.Any() || loopOfVertices.Last() != previousStraddleEdge.IntersectVertex)
                        {
                            previousStraddleEdge.IntersectVertex.IndexInList = newVertexIndex++;
                            loopOfVertices.Add(previousStraddleEdge.IntersectVertex);
                        }
                        if (currentStraddleEdge.OnSideVertex == previousStraddleEdge.OnSideVertex)
                        {
                            if (currentStraddleEdge.OwnedFace == previousStraddleEdge.OwnedFace)
                                previousStraddleEdge.OwnedFace = currentStraddleEdge.OtherFace;
                            else if (currentStraddleEdge.OwnedFace == previousStraddleEdge.OtherFace)
                                previousStraddleEdge.OtherFace = currentStraddleEdge.OtherFace;
                            else if (currentStraddleEdge.OtherFace == previousStraddleEdge.OwnedFace)
                                previousStraddleEdge.OwnedFace = currentStraddleEdge.OwnedFace;
                            else if (currentStraddleEdge.OtherFace == previousStraddleEdge.OtherFace)
                                previousStraddleEdge.OtherFace = currentStraddleEdge.OwnedFace;
                            else throw new Exception("No shared face exists between these two straddle edges");
                            previousStraddleEdge.OffSideVertex = currentStraddleEdge.OffSideVertex;
                        }
                        else
                        {
                            newFaces.AddRange(NewFace(previousStraddleEdge, currentStraddleEdge, straddleEdgesDict, straddleFaces, newEdges, adjOnsideFaceIndices));
                            previousStraddleEdge = currentStraddleEdge;
                        }
                    }
                    else
                    {
                        if (!loopOfVertices.Any() || loopOfVertices.Last() != previousStraddleEdge.IntersectVertex)
                        {
                            previousStraddleEdge.IntersectVertex.IndexInList = newVertexIndex++;
                            loopOfVertices.Add(previousStraddleEdge.IntersectVertex);
                        }
                        newFaces.AddRange(NewFace(previousStraddleEdge, currentStraddleEdge, straddleEdgesDict, straddleFaces, newEdges, adjOnsideFaceIndices));
                        previousStraddleEdge = currentStraddleEdge;
                    }
                } while (!successfull);
                if (loopOfVertices.Count < 3) throw new Exception("This could be a knife edge. But this error will likely cause errors down the line");
                //The loop index for negative side loops are negative.
                loops.Add(new Loop(loopOfVertices, newFaces, plane.Normal, straddleFaceIndices, adjOnsideFaceIndices,
                    loopIndex, isPositiveSide == 1, straddleEdgeOnSideVertices));
            }
        }

        /// <summary>
        /// Creates a new face given two straddle edges
        /// </summary>
        /// <param name="st1">The ST1.</param>
        /// <param name="st2">The ST2.</param>
        /// <param name="straddleEdgesDict">The straddle edges dictionary.</param>
        /// <param name="straddleFaces">The straddle faces.</param>
        /// <param name="newEdges">The new edges.</param>
        /// <param name="adjOnsideFaceIndices">The adj onside face indices.</param>
        /// <param name="lastNewFace">if set to <c>true</c> [last new face].</param>
        /// <returns>List&lt;TriangleFace&gt;.</returns>
        /// <exception cref="System.Exception">No shared face exists between these two straddle edges</exception>
        /// <exception cref="System.Exception">There should only be one boundary edge. There must be 2 straddle edges for this shared face.</exception>
        /// <exception cref="System.Exception">All edges of the shared face are straddle edges. This cannot be.</exception>
        /// <exception cref="System.Exception">This should never be the case. The boundary edge should be have the sharedFace as owned or other</exception>
        /// <exception cref="System.Exception">Error, the straddle edges do not match up at a common vertex</exception>
        private static List<TriangleFace> NewFace(StraddleEdge st1, StraddleEdge st2, Dictionary<int, Edge> straddleEdgesDict,
            Dictionary<int, TriangleFace> straddleFaces, List<Edge> newEdges, HashSet<int> adjOnsideFaceIndices, bool lastNewFace = false)
        {
            TriangleFace sharedFace;
            if (st1.OwnedFace == st2.OwnedFace || st1.OwnedFace == st2.OtherFace) sharedFace = st1.OwnedFace;
            else if (st1.OtherFace == st2.OwnedFace || st1.OtherFace == st2.OtherFace) sharedFace = st1.OtherFace;
            else throw new Exception("No shared face exists between these two straddle edges");

            //Make an extra edge if the first new face
            if (!newEdges.Any())
            {
                var newEdge = new Edge(st1.IntersectVertex, st1.OnSideVertex, false);
                newEdges.Add(newEdge);
            }

            if (st1.IntersectVertex == st2.IntersectVertex)
            {
                //Make one new edge and one new face. Set the ownership of this edge.
                var newFace =
                    new TriangleFace(new[] { st1.OnSideVertex, st1.IntersectVertex, st2.OnSideVertex },
                        sharedFace.Normal, false);
                newEdges.Last().OtherFace = newFace;
                if (!lastNewFace)
                    newEdges.Add(new Edge(st2.IntersectVertex, st2.OnSideVertex, false) { OwnedFace = newFace });
                else newEdges.First().OwnedFace = newFace;

                #region Store index of face on boundary edge.
                //First, find the boundary edge
                Edge boundaryEdge = null;
                foreach (var edge in sharedFace.Edges.Where(edge => !straddleEdgesDict.ContainsKey(edge.IndexInList)))
                {
                    if (boundaryEdge != null) throw new Exception("There should only be one boundary edge. There must be 2 straddle edges for this shared face.");
                    boundaryEdge = edge;
                }
                if (boundaryEdge == null) throw new Exception("All edges of the shared face are straddle edges. This cannot be.");

                //Second, find the boundary face
                //No duplicates are to be included in the adjOnsideFaceIndices HashSet.
                if (boundaryEdge.OwnedFace == sharedFace)
                {
                    //Check if the other face is a straddle face. If it is, it is not needed for face wrapping. 
                    //Note: It is a straddle edge when the boundary edge is above the cutting plane, but both faces are straddling the cutting plane.
                    //This is common. Consider cutting a box at an angle near one of its edges.
                    if (!straddleFaces.ContainsKey(boundaryEdge.OtherFace.IndexInList) && !adjOnsideFaceIndices.Contains(boundaryEdge.OtherFace.IndexInList))
                    {
                        adjOnsideFaceIndices.Add(boundaryEdge.OtherFace.IndexInList);
                    }
                }
                else if (boundaryEdge.OtherFace == sharedFace)
                {
                    if (!straddleFaces.ContainsKey(boundaryEdge.OwnedFace.IndexInList) && !adjOnsideFaceIndices.Contains(boundaryEdge.OwnedFace.IndexInList))
                    {
                        adjOnsideFaceIndices.Add(boundaryEdge.OwnedFace.IndexInList);
                    }
                }
                else throw new Exception("This should never be the case. The boundary edge should be have the sharedFace as owned or other");
                #endregion

                return new List<TriangleFace> { newFace };
            }
            //If not the same intersect vertex, then the same offSideVertex denotes 
            //two Consecutive curved edges, so this creates two new faces
            if (st1.OffSideVertex == st2.OffSideVertex ||
                st1.OriginalOffSideVertex == st2.OffSideVertex ||
                st1.OffSideVertex == st2.OriginalOffSideVertex)
            {
                //Create two new faces
                var newFace1 =
                    new TriangleFace(new[] { st1.OnSideVertex, st1.IntersectVertex, st2.IntersectVertex },
                        sharedFace.Normal, false);
                var newFace2 =
                    new TriangleFace(new[] { st1.OnSideVertex, st2.IntersectVertex, st2.OnSideVertex },
                        sharedFace.Normal, false);
                //Update ownership of most recently created edge
                newEdges.Last().OtherFace = newFace1;
                //Create new edges and update their ownership 
                var newEdge1 = new Edge(st1.IntersectVertex, st2.IntersectVertex, false) { OwnedFace = newFace1 };
                var newEdge2 = new Edge(st1.OnSideVertex, st2.IntersectVertex, false) { OwnedFace = newFace2, OtherFace = newFace1 };
                newEdges.AddRange(new List<Edge> { newEdge1, newEdge2 });
                //Create the last edge, if this is not the last new face
                if (!lastNewFace) newEdges.Add(new Edge(st2.IntersectVertex, st2.OnSideVertex, false) { OwnedFace = newFace2 });
                else newEdges.First().OwnedFace = newFace2;

                #region Store index of face on boundary edge.
                //First, find the boundary edge
                Edge boundaryEdge = null;
                foreach (var edge in sharedFace.Edges.Where(edge => !straddleEdgesDict.ContainsKey(edge.IndexInList)))
                {
                    if (boundaryEdge != null) throw new Exception("There should only be one boundary edge. There must be 2 straddle edges for this shared face.");
                    boundaryEdge = edge;
                }
                if (boundaryEdge == null) throw new Exception("All edges of the shared face are straddle edges. This cannot be.");

                //Second, find the boundary face
                //No duplicates are to be included in the adjOnsideFaceIndices HashSet.
                if (boundaryEdge.OwnedFace == sharedFace)
                {
                    //Check if the other face is a straddle face. If it is, it is not needed for face wrapping. 
                    //Note: It is a straddle edge when the boundary edge is above the cutting plane, but both faces are straddling the cutting plane.
                    //This is common. Consider cutting a box at an angle near one of its edges.
                    if (!straddleFaces.ContainsKey(boundaryEdge.OtherFace.IndexInList) && !adjOnsideFaceIndices.Contains(boundaryEdge.OtherFace.IndexInList))
                    {
                        adjOnsideFaceIndices.Add(boundaryEdge.OtherFace.IndexInList);
                    }
                }
                else if (boundaryEdge.OtherFace == sharedFace)
                {
                    if (!straddleFaces.ContainsKey(boundaryEdge.OwnedFace.IndexInList) && !adjOnsideFaceIndices.Contains(boundaryEdge.OwnedFace.IndexInList))
                    {
                        adjOnsideFaceIndices.Add(boundaryEdge.OwnedFace.IndexInList);
                    }
                }
                else throw new Exception("This should never be the case. The boundary edge should be have the sharedFace as owned or other");
                #endregion

                return new List<TriangleFace> { newFace1, newFace2 };
            }
            if (st1.OnSideVertex == st2.OnSideVertex)
            {
                //Make two new edges and one new face. Set the ownership of the edges.
                var newFace =
                    new TriangleFace(new[] { st1.OnSideVertex, st1.IntersectVertex, st2.IntersectVertex },
                        sharedFace.Normal, false);
                //Update ownership of most recently created edge
                newEdges.Last().OtherFace = newFace;
                //Create new edges and update their ownership 
                newEdges.Add(new Edge(st1.IntersectVertex, st2.IntersectVertex, false) { OwnedFace = newFace });
                if (!lastNewFace) newEdges.Add(new Edge(st2.IntersectVertex, st2.OnSideVertex, false) { OwnedFace = newFace });
                else newEdges.First().OwnedFace = newFace;
                return new List<TriangleFace> { newFace };
            }
            throw new Exception("Error, the straddle edges do not match up at a common vertex");
        }

        /// <summary>
        /// Straddle edge references original edge and an intersection vertex.
        /// </summary>
        internal class StraddleEdge
        {
            /// <summary>
            /// Point of edge / plane intersection
            /// </summary>
            public Vertex IntersectVertex;

            /// <summary>
            /// Vertex on side of plane that will not be kept
            /// </summary>
            public Vertex OffSideVertex;

            /// <summary>
            /// Vertex on side of plane that will not be kept (Used when collapsing an edge)
            /// </summary>
            public Vertex OriginalOffSideVertex;

            /// <summary>
            /// Vertex on side of plane that will be kept
            /// </summary>
            public Vertex OnSideVertex;

            /// <summary>
            /// Connect back to the base edge
            /// </summary>
            public Edge Edge;

            /// <summary>
            /// OwnedFace (may change if collapsed into another straddle edge)
            /// </summary>
            public TriangleFace OwnedFace;

            /// <summary>
            /// OtherFace (may change if collapsed into another straddle edge)
            /// </summary>
            public TriangleFace OtherFace;

            /// <summary>
            /// Initializes a new instance of the <see cref="StraddleEdge"/> class.
            /// </summary>
            /// <param name="edge">The edge.</param>
            /// <param name="plane">The plane.</param>
            /// <param name="offSideVertex">The off side vertex.</param>
            /// <param name="planeOffset">The plane offset.</param>
            /// <exception cref="System.Exception">Cannot Be Empty</exception>
            internal StraddleEdge(Edge edge, Plane plane, Vertex offSideVertex, double planeOffset = 0D)
            {
                OwnedFace = edge.OwnedFace;
                OtherFace = edge.OtherFace;
                Edge = edge;
                OffSideVertex = offSideVertex;
                OriginalOffSideVertex = offSideVertex;
                OnSideVertex = Edge.OtherVertex(OffSideVertex);
                IntersectVertex = new Vertex(MiscFunctions.PointOnPlaneFromIntersectingLineSegment(plane.Normal,
                    plane.DistanceToOrigin - planeOffset, edge.To.Coordinates, edge.From.Coordinates, out _));
                if (IntersectVertex == null) throw new Exception("Cannot Be Empty");
            }

            /// <summary>
            /// Gets the next face in the loop from this edge, given the current face
            /// </summary>
            /// <param name="face">The face.</param>
            /// <returns>TriangleFace.</returns>
            public TriangleFace NextFace(TriangleFace face)
            {
                return Edge.GetMatingFace(face);
            }
        }

        #region Get Cross Sections
        /// <summary>
        /// Gets many uniformly spaced cross-sections through the tessellated solid.
        /// </summary>
        /// <param name="ts"></param>
        /// <param name="direction"></param>
        /// <param name="sliceOffsets"></param>
        /// <param name="startDistanceAlongDirection"></param>
        /// <param name="numSlices"></param>
        /// <param name="stepSize"></param>
        /// <returns></returns>
        public static List<Polygon>[] GetUniformlySpacedCrossSections(this TessellatedSolid ts, Vector3 direction, out double[] sliceOffsets,
            double startDistanceAlongDirection = double.NaN, int numSlices = -1, double stepSize = double.NaN)
            => GetUniformlySpacedCrossSections(ts.Vertices, direction, out sliceOffsets, out _, out _, startDistanceAlongDirection, numSlices, stepSize);

        /// <summary>
        /// Gets many uniformly spaced cross-sections through the tessellated solid.
        /// </summary>
        /// <param name="ts"></param>
        /// <param name="direction"></param>
        /// <param name="sliceOffsets"></param>
        /// <param name="vertex2DToEdges"></param>
        /// <param name="startDistanceAlongDirection"></param>
        /// <param name="numSlices"></param>
        /// <param name="stepSize"></param>
        /// <returns></returns>
        public static List<Polygon>[] GetUniformlySpacedCrossSections(this TessellatedSolid ts, Vector3 direction, out double[] sliceOffsets,
            out Dictionary<Vertex2D, Edge>[] vertex2DToEdges,
            double startDistanceAlongDirection = double.NaN, int numSlices = -1, double stepSize = double.NaN)
            => GetUniformlySpacedCrossSections(ts.Vertices, direction, out sliceOffsets, out vertex2DToEdges, out _, startDistanceAlongDirection, numSlices, stepSize);

        /// <summary>
        /// Gets many uniformly spaced cross-sections through the tessellated solid.
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="startDistanceAlongDirection">The start distance along direction.</param>
        /// <param name="numSlices">The number slices.</param>
        /// <param name="stepSize">Size of the step.</param>
        /// <returns>List&lt;Polygon&gt;[].</returns>
        /// <exception cref="System.ArgumentException">Either a valid stepSize or a number of slices greater than zero must be specified.</exception>
        public static List<Polygon>[] GetUniformlySpacedCrossSections(this TessellatedSolid ts, Vector3 direction, out double[] sliceOffsets,
            out Dictionary<Vertex2D, Edge>[] vertex2DToEdges, out int[] numCompletePolygonsPerLayer,
            double startDistanceAlongDirection = double.NegativeInfinity, int numSlices = -1, double stepSize = double.NaN)
            => GetUniformlySpacedCrossSections(ts.Vertices, direction, out sliceOffsets, out vertex2DToEdges, out numCompletePolygonsPerLayer, startDistanceAlongDirection, numSlices, stepSize);

        /// <summary>
        /// Gets many uniformly spaced cross-sections through the tessellated solid.
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="direction"></param>
        /// <param name="sliceOffsets"></param>
        /// <param name="startDistanceAlongDirection"></param>
        /// <param name="numSlices"></param>
        /// <param name="stepSize"></param>
        /// <returns></returns>
        public static List<Polygon>[] GetUniformlySpacedCrossSections(this IEnumerable<Vertex> vertices, Vector3 direction, out double[] sliceOffsets,
           double startDistanceAlongDirection = double.NaN,
        int numSlices = -1, double stepSize = double.NaN)
        => GetUniformlySpacedCrossSections(vertices, direction, out sliceOffsets, out _, out _, startDistanceAlongDirection, numSlices, stepSize);

        /// <summary>
        /// Gets many uniformly spaced cross-sections through the tessellated solid.
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="direction"></param>
        /// <param name="sliceOffsets"></param>
        /// <param name="completePolygons"></param>
        /// <param name="startDistanceAlongDirection"></param>
        /// <param name="numSlices"></param>
        /// <param name="stepSize"></param>
        /// <returns></returns>
        public static List<Polygon>[] GetUniformlySpacedCrossSections(this IEnumerable<Vertex> vertices, Vector3 direction, out double[] sliceOffsets,
           out int[] completePolygons, double startDistanceAlongDirection = double.NaN,
        int numSlices = -1, double stepSize = double.NaN)
        => GetUniformlySpacedCrossSections(vertices, direction, out sliceOffsets, out _, out completePolygons, startDistanceAlongDirection, numSlices, stepSize);

        /// <summary>
        /// Gets many uniformly spaced cross-sections through the tessellated solid.
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="direction"></param>
        /// <param name="sliceOffsets"></param>
        /// <param name="vertex2DToEdges"></param>
        /// <param name="startDistanceAlongDirection"></param>
        /// <param name="numSlices"></param>
        /// <param name="stepSize"></param>
        /// <returns></returns>
        public static List<Polygon>[] GetUniformlySpacedCrossSections(this IEnumerable<Vertex> vertices, Vector3 direction, out double[] sliceOffsets,
           out Dictionary<Vertex2D, Edge>[] vertex2DToEdges, double startDistanceAlongDirection = double.NaN,
        int numSlices = -1, double stepSize = double.NaN)
        => GetUniformlySpacedCrossSections(vertices, direction, out sliceOffsets, out vertex2DToEdges, out _, startDistanceAlongDirection, numSlices, stepSize);

        /// <summary>
        /// Gets many uniformly spaced cross-sections through the tessellated solid.
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="direction"></param>
        /// <param name="sliceOffsets"></param>
        /// <param name="vertex2DToEdges"></param>
        /// <param name="numCompletePolygonsPerLayer"></param>
        /// <param name="startDistanceAlongDirection"></param>
        /// <param name="numSlices"></param>
        /// <param name="stepSize"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static List<Polygon>[] GetUniformlySpacedCrossSections(this IEnumerable<Vertex> vertices, Vector3 direction, out double[] sliceOffsets,
           out Dictionary<Vertex2D, Edge>[] vertex2DToEdges, out int[] numCompletePolygonsPerLayer, double startDistanceAlongDirection = double.NegativeInfinity,
        int numSlices = -1, double stepSize = double.NaN)
        {
            //First, sort the vertices along the given axis. Duplicate distances are not important.
            var sortedVertices = vertices.OrderBy(v => v.Dot(direction)).ToArray();
            if (double.IsNaN(stepSize) && numSlices < 1) throw new ArgumentException("Either a valid stepSize or a number of slices greater than zero must be specified.");
            direction = direction.Normalize();
            var transform = direction.TransformToXYPlane(out _);
            var plane = new Plane(0.0, direction);

            var firstDistance = Math.Max(startDistanceAlongDirection, sortedVertices[0].Dot(direction));
            var lastDistance = sortedVertices[^1].Dot(direction);
            //if (double.IsNaN(startDistanceAlongDirection))
            //    startDistanceAlongDirection = firstDistance;
            startDistanceAlongDirection = firstDistance + 0.5 * stepSize;
            var lengthAlongDir = lastDistance - firstDistance;
            stepSize = Math.Abs(stepSize);
            if (double.IsNaN(stepSize)) stepSize = lengthAlongDir / numSlices;
            if (numSlices < 1) numSlices = (int)(lengthAlongDir / stepSize);

            var result = new List<Polygon>[numSlices];
            sliceOffsets = new double[numSlices];

            vertex2DToEdges = new Dictionary<Vertex2D, Edge>[numSlices];
            numCompletePolygonsPerLayer = new int[numSlices];
            var currentEdges = new HashSet<Edge>();
            var nextDistance = sortedVertices.First().Dot(direction);
            var vIndex = 0;
            for (int step = 0; step < numSlices; step++)
            {
                var d = startDistanceAlongDirection + step * stepSize;
                sliceOffsets[step] = d;
                var thisVertex = sortedVertices[vIndex];
                var thisVertexDot = thisVertex.Dot(direction);
                var needToOffset = false;
                while (thisVertexDot <= d)
                {
                    if (d.IsPracticallySame(thisVertexDot)) needToOffset = true;
                    foreach (var edge in thisVertex.Edges)
                    {
                        if (currentEdges.Contains(edge)) currentEdges.Remove(edge);
                        else currentEdges.Add(edge);
                    }
                    vIndex++;
                    if (vIndex == sortedVertices.Length) break;
                    thisVertex = sortedVertices[vIndex];
                    thisVertexDot = thisVertex.Dot(direction);
                }
                if (needToOffset)
                {
                    if (vIndex < sortedVertices.Length)
                        d += 0.001 * Math.Min(stepSize, sortedVertices[vIndex].Dot(direction) - d);
                    else
                        d += 0.001 * stepSize;
                }
                plane.DistanceToOrigin = d;
                if (currentEdges.Any())
                {
                    result[step] = CreatePolygons(currentEdges.ToDictionary(ce => ce, ce =>
                       MiscFunctions.PointOnPlaneFromIntersectingLineSegment(plane, ce.From.Coordinates, ce.To.Coordinates, out _)
                           .ConvertTo2DCoordinates(transform)), plane.Normal, plane.DistanceToOrigin, out var v2EDictionary, out var numComplete);
                    vertex2DToEdges[step] = v2EDictionary;
                    numCompletePolygonsPerLayer[step] = numComplete;
                }
                else result[step] = new List<Polygon>();
            }
            return result;
        }

        /// <summary>
        /// Gets many uniformly spaced cross-sections through the tessellated solid.
        /// </summary>
        /// <param name="ts"></param>
        /// <param name="direction"></param>
        /// <param name="sliceOffsets"></param>
        /// <param name="startDistanceAlongDirection"></param>
        /// <param name="numSlices"></param>
        /// <param name="stepSize"></param>
        /// <returns></returns>
        public static List<Polygon>[] GetUniformlySpacedCrossSections(this TessellatedSolid ts, CartesianDirections direction, out double[] sliceOffsets,
           double startDistanceAlongDirection = double.NaN, int numSlices = -1, double stepSize = double.NaN)
        => GetUniformlySpacedCrossSections(ts, direction, out sliceOffsets, out _, out _, startDistanceAlongDirection, numSlices, stepSize);

        /// <summary>
        /// Gets many uniformly spaced cross-sections through the tessellated solid.
        /// </summary>
        /// <param name="ts"></param>
        /// <param name="direction"></param>
        /// <param name="sliceOffsets"></param>
        /// <param name="vertex2DToEdges"></param>
        /// <param name="startDistanceAlongDirection"></param>
        /// <param name="numSlices"></param>
        /// <param name="stepSize"></param>
        /// <returns></returns>
        public static List<Polygon>[] GetUniformlySpacedCrossSections(this TessellatedSolid ts, CartesianDirections direction, out double[] sliceOffsets,
            out Dictionary<Vertex2D, Edge>[] vertex2DToEdges, double startDistanceAlongDirection = double.NaN, int numSlices = -1, double stepSize = double.NaN)
        => GetUniformlySpacedCrossSections(ts, direction, out sliceOffsets, out vertex2DToEdges, out _, startDistanceAlongDirection, numSlices, stepSize);


        /// <summary>
        /// Gets many uniformly spaced cross-sections through the tessellated solid.
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="startDistanceAlongDirection">The start distance along direction.</param>
        /// <param name="numSlices">The number slices.</param>
        /// <param name="stepSize">Size of the step.</param>
        /// <returns>List&lt;Polygon&gt;[].</returns>
        /// <exception cref="System.ArgumentException">Either a valid stepSize or a number of slices greater than zero must be specified.</exception>
        public static List<Polygon>[] GetUniformlySpacedCrossSections(this TessellatedSolid ts, CartesianDirections direction, out double[] sliceOffsets,
            out Dictionary<Vertex2D, Edge>[] vertex2DToEdges, out int[] numCompletedPolygonsPerLayer, double startDistanceAlongDirection = double.NaN, int numSlices = -1, double stepSize = double.NaN)
        {
            if (double.IsNaN(stepSize) && numSlices < 1) throw new ArgumentException("Either a valid stepSize or a number of slices greater than zero must be specified.");
            var intDir = Math.Abs((int)direction) - 1;

            if ((ts.Bounds[0] - ts.Bounds[1]).IsNegligible(ts.SameTolerance))
                ts.DefineAxisAlignedBoundingBoxAndTolerance();
            var lengthAlongDir = ts.Bounds[1][intDir] - ts.Bounds[0][intDir];
            stepSize = Math.Abs(stepSize);
            if (double.IsNaN(stepSize)) stepSize = lengthAlongDir / numSlices;
            if (numSlices < 1) numSlices = (int)(lengthAlongDir / stepSize);
            if (double.IsNaN(startDistanceAlongDirection))
            {
                if (direction < 0)
                    startDistanceAlongDirection = ts.Bounds[1][intDir] - 0.5 * stepSize;
                else startDistanceAlongDirection = ts.Bounds[0][intDir] + 0.5 * stepSize;
            }
            switch (direction)
            {
                case CartesianDirections.XPositive:
                    return AllSlicesAlongX(ts, startDistanceAlongDirection, numSlices, stepSize, out sliceOffsets, out vertex2DToEdges, out numCompletedPolygonsPerLayer);
                case CartesianDirections.YPositive:
                    return AllSlicesAlongY(ts, startDistanceAlongDirection, numSlices, stepSize, out sliceOffsets, out vertex2DToEdges, out numCompletedPolygonsPerLayer);
                case CartesianDirections.ZPositive:
                    return AllSlicesAlongZ(ts, startDistanceAlongDirection, numSlices, stepSize, out sliceOffsets, out vertex2DToEdges, out numCompletedPolygonsPerLayer);
                case CartesianDirections.XNegative:
                    var slicesX = AllSlicesAlongX(ts, startDistanceAlongDirection, numSlices, stepSize, out sliceOffsets, out vertex2DToEdges, out numCompletedPolygonsPerLayer);
                    Array.Reverse(sliceOffsets);
                    Array.Reverse(slicesX);
                    Array.Reverse(numCompletedPolygonsPerLayer);
                    Array.Reverse(vertex2DToEdges);
                    return slicesX;
                case CartesianDirections.YNegative:
                    var slicesY = AllSlicesAlongY(ts, startDistanceAlongDirection, numSlices, stepSize, out sliceOffsets, out vertex2DToEdges, out numCompletedPolygonsPerLayer);
                    Array.Reverse(sliceOffsets);
                    Array.Reverse(slicesY);
                    Array.Reverse(numCompletedPolygonsPerLayer);
                    Array.Reverse(vertex2DToEdges);
                    return slicesY;
                default:
                    var slicesZ = AllSlicesAlongZ(ts, startDistanceAlongDirection, numSlices, stepSize, out sliceOffsets, out vertex2DToEdges, out numCompletedPolygonsPerLayer);
                    Array.Reverse(sliceOffsets);
                    Array.Reverse(slicesZ);
                    Array.Reverse(numCompletedPolygonsPerLayer);
                    Array.Reverse(vertex2DToEdges);
                    return slicesZ;
            }
        }

        /// <summary>
        /// Gets the cross-section through the tessellated solid at the specified distance from the origin in the specified direction.
        /// </summary>
        /// <param name="tessellatedSolid"></param>
        /// <param name="direction"></param>
        /// <param name="distanceToOrigin"></param>
        /// <returns></returns>
        public static List<Polygon> GetCrossSection(this TessellatedSolid tessellatedSolid, CartesianDirections direction,
            double distanceToOrigin)
      => tessellatedSolid.GetCrossSection(direction, distanceToOrigin, out _, out _);

        /// <summary>
        /// Gets the cross-section through the tessellated solid at the specified distance from the origin in the specified direction.
        /// </summary>
        /// <param name="tessellatedSolid"></param>
        /// <param name="direction"></param>
        /// <param name="distanceToOrigin"></param>
        /// <param name="v2EDictionary"></param>
        /// <returns></returns>
        public static List<Polygon> GetCrossSection(this TessellatedSolid tessellatedSolid, CartesianDirections direction,
            double distanceToOrigin, out Dictionary<Vertex2D, Edge> v2EDictionary)
      => tessellatedSolid.GetCrossSection(direction, distanceToOrigin, out v2EDictionary, out _);


        /// <summary>
        /// Gets the cross-section through the tessellated solid at the specified distance from the origin in the specified direction.
        /// </summary>
        /// <param name="tessellatedSolid">The tessellated solid.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="distanceToOrigin">The distance to origin.</param>
        /// <param name="v2EDictionary">The v2 e dictionary.</param>
        /// <returns>List&lt;Polygon&gt;.</returns>
        public static List<Polygon> GetCrossSection(this TessellatedSolid tessellatedSolid, CartesianDirections direction,
            double distanceToOrigin, out Dictionary<Vertex2D, Edge> v2EDictionary, out int numCompletedPolygons)
        {
            var intDir = Math.Abs((int)direction) - 1;
            var signDir = Math.Sign((int)direction);
            var distances = tessellatedSolid.Vertices.Select(v => v.Coordinates[intDir]).ToList();
            var positiveShift = 0.0;
            var negativeShift = 0.0;
            distances.SetPositiveAndNegativeShifts(distanceToOrigin, tessellatedSolid.SameTolerance, ref positiveShift, ref negativeShift);
            var planeDistance = signDir * (distanceToOrigin + ((positiveShift < -negativeShift) ? positiveShift : negativeShift));

            var transform = direction.TransformToXYPlane(out _);
            tessellatedSolid.MakeEdgesIfNonExistent();
            var e2VDict = new Dictionary<Edge, Vector2>();
            foreach (var edge in tessellatedSolid.Edges)
            {
                var fromDistance = distances[edge.From.IndexInList];
                var toDistance = distances[edge.To.IndexInList];
                if ((fromDistance > planeDistance && toDistance < planeDistance) || (fromDistance < planeDistance && toDistance > planeDistance))
                {
                    Vector2 ip = Vector2.Null;
                    if (intDir == 0)
                    {
                        var ip3D = MiscFunctions.PointOnXPlaneFromLineSegment(planeDistance, edge.From.Coordinates, edge.To.Coordinates);
                        ip = new Vector2(-signDir * ip3D.Z, ip3D.Y);
                    }
                    else if (intDir == 1)
                    {
                        var ip3D = MiscFunctions.PointOnYPlaneFromLineSegment(planeDistance, edge.From.Coordinates, edge.To.Coordinates);
                        ip = new Vector2(ip3D.X, -signDir * ip3D.Z);
                    }
                    else
                    {
                        var ip3D = MiscFunctions.PointOnZPlaneFromLineSegment(planeDistance, edge.From.Coordinates, edge.To.Coordinates);
                        ip = new Vector2(ip3D.X, signDir * ip3D.Y);
                    }
                    e2VDict.Add(edge, ip);
                }
            }
            return CreatePolygons(e2VDict, signDir * Vector3.UnitVector(intDir), signDir * planeDistance, out v2EDictionary, out numCompletedPolygons);
        }

        /// <summary>
        /// Gets the cross-section through the tessellated solid at the specified plane.
        /// </summary>
        /// <param name="tessellatedSolid"></param>
        /// <param name="plane"></param>
        /// <returns></returns>
        public static List<Polygon> GetCrossSection(this TessellatedSolid tessellatedSolid, Plane plane)
        => tessellatedSolid.GetCrossSection(plane, out _, out _);

        /// <summary>
        /// Gets the cross-section through the tessellated solid at the specified plane.
        /// 
        /// </summary>
        /// <param name="tessellatedSolid"></param>
        /// <param name="plane"></param>
        /// <param name="v2EDictionary"></param>
        /// <returns></returns>
        public static List<Polygon> GetCrossSection(this TessellatedSolid tessellatedSolid, Plane plane, out Dictionary<Vertex2D, Edge> v2EDictionary)
            => tessellatedSolid.GetCrossSection(plane, out v2EDictionary, out _);

        /// <summary>
        /// Gets the cross-section through the tessellated solid at the specified plane.
        /// </summary>
        /// <param name="tessellatedSolid">The tessellated solid.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="v2EDictionary">The v2 e dictionary.</param>
        /// <returns>List&lt;Polygon&gt;.</returns>
        public static List<Polygon> GetCrossSection(this TessellatedSolid tessellatedSolid, Plane plane,
            out Dictionary<Vertex2D, Edge> v2EDictionary, out int numCompletedPolygons)
        {
            var direction = plane.Normal;
            var closestCartesianDirection = direction.SnapDirectionToCartesian(out var withinTolerance, tessellatedSolid.SameTolerance);
            if (withinTolerance)
                return tessellatedSolid.GetCrossSection(closestCartesianDirection, plane.DistanceToOrigin, out v2EDictionary, out numCompletedPolygons);

            var distances = tessellatedSolid.Vertices.Select(v => v.Dot(direction)).ToList();
            var positiveShift = 0.0;
            var negativeShift = 0.0;
            distances.SetPositiveAndNegativeShifts(plane.DistanceToOrigin, tessellatedSolid.SameTolerance, ref positiveShift, ref negativeShift);
            var planeDistance = plane.DistanceToOrigin + ((positiveShift < -negativeShift) ? positiveShift : negativeShift);

            var transform = direction.TransformToXYPlane(out _);
            tessellatedSolid.MakeEdgesIfNonExistent();
            var e2VDict = new Dictionary<Edge, Vector2>();
            foreach (var edge in tessellatedSolid.Edges)
            {
                var fromDistance = distances[edge.From.IndexInList];
                var toDistance = distances[edge.To.IndexInList];
                if ((fromDistance > planeDistance && toDistance < planeDistance) || (fromDistance < planeDistance && toDistance > planeDistance))
                {
                    var ip = MiscFunctions.PointOnPlaneFromIntersectingLineSegment(plane, edge.From.Coordinates, edge.To.Coordinates, out _)
                        .ConvertTo2DCoordinates(transform);
                    e2VDict.Add(edge, ip);
                }
            }
            return CreatePolygons(e2VDict, plane.Normal, plane.DistanceToOrigin, out v2EDictionary, out numCompletedPolygons);
        }

        /// <summary>
        /// Gets the loops.
        /// </summary>
        /// <param name="edgeDictionary">The edge dictionary.</param>
        /// <param name="normal">The normal.</param>
        /// <param name="distanceToOrigin">The distance to origin.</param>
        /// <param name="e2VDictionary">The e2 v dictionary.</param>
        /// <returns>List&lt;Polygon&gt;.</returns>
        private static List<Polygon> CreatePolygons(Dictionary<Edge, Vector2> edgeDictionary, Vector3 normal, double distanceToOrigin,
            out Dictionary<Vertex2D, Edge> e2VDictionary, out int numCompletedLoops)
        {
            var polygons = new List<Polygon>(); // this is the main result - create polygons
            var partialPolygons = new List<Polygon>(); // this is a list of partial polygons that are not closed loops
            numCompletedLoops = 0;  // while polygons are returned - a subset may be incomplete. These incompletes are added to the end of the list
                                    // this output variable keeps track of the number of complete, so - in many cases - it's the same length as the polygons list
            e2VDictionary = new Dictionary<Vertex2D, Edge>(); //essentially the same as the input dictionary but reversed and the key
                                                              // is the newly defined vertex (which wraps the given Vector2).
            while (edgeDictionary.Any()) // while there are still edges to process in the dictionary
            {
                var path = new List<Vector2>();
                var edgesInLoop = new List<Edge>();
                (Edge firstEdgeInLoop, TriangleFace nextFace) = GetStartingEdgeAndFace(edgeDictionary.Keys, normal);
                var currentEdge = firstEdgeInLoop;
                bool finishedLoop;
                do
                {
                    finishedLoop = false;
                    var intersectCoord = edgeDictionary[currentEdge];
                    edgeDictionary.Remove(currentEdge);
                    edgesInLoop.Add(currentEdge);
                    path.Add(intersectCoord);
                    var prevFace = nextFace;
                    nextFace = currentEdge.GetMatingFace(nextFace);
                    Edge nextEdge = null;
                    if (nextFace != null)
                    {
                        foreach (var whichEdge in nextFace.Edges) // which of the 3 edges do we go to next?
                        {
                            if (currentEdge == whichEdge) continue; // not this one - this is the one you're coming from
                            if (whichEdge == firstEdgeInLoop) // oh! you've come back to the beginning
                            {
                                finishedLoop = true;
                                if (path.Count > 2) // don't keep completed loops of two edges or less
                                    AddToPolygons(path, edgesInLoop, polygons, e2VDictionary);
                                break;
                            }
                            else if (edgeDictionary.ContainsKey(whichEdge))
                            {
                                nextEdge = whichEdge; // this is the next edge. we know this because it is also in the input dictionary
                                break;
                            }
                        }
                    }
                    // if nextEdge is null, then we have a problem. there is no where to go from here. we have a dead end. how did this happen?
                    // it often happens if you are slicing through non-water tight solids or simply an indepedent surface
                    if (!finishedLoop && nextEdge == null)
                    {
                        // see if there are other partials that can be linked to this. for example
                        AddToPolygons(path, edgesInLoop, partialPolygons, e2VDictionary);
                        finishedLoop = true;
                    }
                    else currentEdge = nextEdge;
                } while (!finishedLoop);
            }
            polygons = polygons.CreateShallowPolygonTrees(true);
            numCompletedLoops = polygons.Count;
            polygons.AddRange(partialPolygons);
            return polygons;
        }

        private static (Edge, TriangleFace) GetStartingEdgeAndFace(ICollection<Edge> edges, Vector3 normal)
        {
            foreach (var e in edges)
            {
                if (e.OwnedFace == null && e.Vector.Dot(normal) > 0) // read next comment
                    return (e, null);
                if (e.OtherFace == null && e.Vector.Dot(normal) < 0) // read next comment
                    return (e, null);
            }
            var startEdge = edges.First();

            // for a given edge, which face should it be starting with (so that the polygon is made with proper
            // right-hand rule)? We can determine this by the direction that the edge and the normal of the slice plane. If the dot product
            // of these two is positive, then the polygon is starting at the owned face. If it is negative, the start is the other face.
            // this logic is used in the previous foreach to find edges that are good starts because there is nothing behind them - 
            // meaning that they should be the first edge in a partial loop.
            var startFace = (startEdge.Vector.Dot(normal) > 0) ? startEdge.OwnedFace : startEdge.OtherFace;

            return (startEdge, startFace);
        }

        /// <summary>
        /// Adds to polygons.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="edgesInLoop">The edges in loop.</param>
        /// <param name="polygons">The polygons.</param>
        /// <param name="e2VDictionary">The e2 v dictionary.</param>
        private static void AddToPolygons(List<Vector2> path, List<Edge> edgesInLoop, List<Polygon> polygons, Dictionary<Vertex2D, Edge> e2VDictionary)
        {
            var polygon = new Polygon(path);
            polygons.Add(polygon);
            for (int i = 0; i < polygon.Vertices.Count; i++)
                e2VDictionary.Add(polygon.Vertices[i], edgesInLoop[i]);
        }

        /// <summary>
        /// Alls the slices along x.
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <param name="startDistanceAlongDirection">The start distance along direction.</param>
        /// <param name="numSlices">The number slices.</param>
        /// <param name="stepSize">Size of the step.</param>
        /// <returns>List&lt;Polygon&gt;[].</returns>
        private static List<Polygon>[] AllSlicesAlongX(TessellatedSolid ts, double startDistanceAlongDirection, int numSlices, double stepSize,
            out double[] sliceOffsets, out Dictionary<Vertex2D, Edge>[] vertex2DToEdges, out int[] numCompletedPolygonsPerLayer)
        {
            var loopsAlongX = new List<Polygon>[numSlices];
            sliceOffsets = new double[numSlices];
            vertex2DToEdges = new Dictionary<Vertex2D, Edge>[numSlices];
            numCompletedPolygonsPerLayer = new int[numSlices];
            //First, sort the vertices along the given axis. Duplicate distances are not important.
            var sortedVertices = ts.Vertices.OrderBy(v => v.X).ToArray();
            var currentEdges = new HashSet<Edge>();
            var nextDistance = sortedVertices.First().X;
            var vIndex = 0;
            for (int step = 0; step < numSlices; step++)
            {
                var x = startDistanceAlongDirection + step * stepSize;
                sliceOffsets[step] = x;
                var thisVertex = sortedVertices[vIndex];
                var needToOffset = false;
                while (thisVertex.X <= x)
                {
                    if (x.IsPracticallySame(thisVertex.X)) needToOffset = true;
                    foreach (var edge in thisVertex.Edges)
                    {
                        if (currentEdges.Contains(edge)) currentEdges.Remove(edge);
                        else currentEdges.Add(edge);
                    }
                    vIndex++;
                    if (vIndex == sortedVertices.Length) break;
                    thisVertex = sortedVertices[vIndex];
                }
                if (needToOffset)
                    x += Math.Min(stepSize, sortedVertices[vIndex].X - x) / 10.0;
                if (currentEdges.Any())
                {
                    Dictionary<Edge, Vector2> edgeDictionary = new Dictionary<Edge, Vector2>();
                    foreach (var ce in currentEdges)
                    {
                        var pointOnPlane = MiscFunctions.PointOnXPlaneFromLineSegment(x, ce.From.Coordinates,
                             ce.To.Coordinates);
                        edgeDictionary.Add(ce, new Vector2(pointOnPlane.Y, pointOnPlane.Z));
                    }
                    loopsAlongX[step] = CreatePolygons(edgeDictionary, Vector3.UnitX, x, out var v2EDictionary, out var numComplete);
                    vertex2DToEdges[step] = v2EDictionary;
                    numCompletedPolygonsPerLayer[step] = numComplete;
                }
                else loopsAlongX[step] = new List<Polygon>();
            }
            return loopsAlongX;
        }

        /// <summary>
        /// Alls the slices along y.
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <param name="startDistanceAlongDirection">The start distance along direction.</param>
        /// <param name="numSlices">The number slices.</param>
        /// <param name="stepSize">Size of the step.</param>
        /// <returns>List&lt;Polygon&gt;[].</returns>
        private static List<Polygon>[] AllSlicesAlongY(TessellatedSolid ts, double startDistanceAlongDirection, int numSlices, double stepSize,
            out double[] sliceOffsets, out Dictionary<Vertex2D, Edge>[] vertex2DToEdges, out int[] numCompletedPolygonsPerLayer)
        {
            var loopsAlongY = new List<Polygon>[numSlices];
            sliceOffsets = new double[numSlices];
            vertex2DToEdges = new Dictionary<Vertex2D, Edge>[numSlices];
            numCompletedPolygonsPerLayer = new int[numSlices];

            //First, sort the vertices along the given axis. Duplicate distances are not important.
            var sortedVertices = ts.Vertices.OrderBy(v => v.Y).ToArray();
            var currentEdges = new HashSet<Edge>();
            var nextDistance = sortedVertices.First().Y;
            var vIndex = 0;
            for (int step = 0; step < numSlices; step++)
            {
                var y = startDistanceAlongDirection + step * stepSize;
                sliceOffsets[step] = y;
                var thisVertex = sortedVertices[vIndex];
                var needToOffset = false;
                while (thisVertex.Y <= y)
                {
                    if (y.IsPracticallySame(thisVertex.Y)) needToOffset = true;
                    foreach (var edge in thisVertex.Edges)
                    {
                        if (currentEdges.Contains(edge)) currentEdges.Remove(edge);
                        else currentEdges.Add(edge);
                    }
                    vIndex++;
                    if (vIndex == sortedVertices.Length) break;
                    thisVertex = sortedVertices[vIndex];
                }
                if (needToOffset)
                    y += Math.Min(stepSize, sortedVertices[vIndex].Y - y) / 10.0;
                if (currentEdges.Any())
                {
                    var edgeDictionary = new Dictionary<Edge, Vector2>();
                    foreach (var ce in currentEdges)
                    {
                        var pointOnPlane = MiscFunctions.PointOnYPlaneFromLineSegment(y, ce.From.Coordinates,
                             ce.To.Coordinates);
                        edgeDictionary.Add(ce, new Vector2(pointOnPlane.Z, pointOnPlane.X));
                    }
                    loopsAlongY[step] = CreatePolygons(edgeDictionary, Vector3.UnitY, y, out var v2EDictionary, out var numComplete);
                    vertex2DToEdges[step] = v2EDictionary;
                    numCompletedPolygonsPerLayer[step] = numComplete;
                }
                else loopsAlongY[step] = new List<Polygon>();
            }
            return loopsAlongY;
        }

        /// <summary>
        /// Alls the slices along z.
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <param name="startDistanceAlongDirection">The start distance along direction.</param>
        /// <param name="numSlices">The number slices.</param>
        /// <param name="stepSize">Size of the step.</param>
        /// <returns>List&lt;Polygon&gt;[].</returns>
        private static List<Polygon>[] AllSlicesAlongZ(TessellatedSolid ts, double startDistanceAlongDirection, int numSlices, double stepSize,
            out double[] sliceOffsets, out Dictionary<Vertex2D, Edge>[] vertex2DToEdges, out int[] numCompletedPolygonsPerLayer)
        {
            var loopsAlongZ = new List<Polygon>[numSlices];
            sliceOffsets = new double[numSlices];
            vertex2DToEdges = new Dictionary<Vertex2D, Edge>[numSlices];
            numCompletedPolygonsPerLayer = new int[numSlices];

            //First, sort the vertices along the given axis. Duplicate distances are not important.
            var sortedVertices = ts.Vertices.OrderBy(v => v.Z).ToArray();
            var currentEdges = new HashSet<Edge>();
            var nextDistance = sortedVertices.First().Z;
            var vIndex = 0;
            for (int step = 0; step < numSlices; step++)
            {
                var z = startDistanceAlongDirection + step * stepSize;
                sliceOffsets[step] = z;
                var thisVertex = sortedVertices[vIndex];
                var needToOffset = false;
                while (thisVertex.Z <= z)
                {
                    if (z.IsPracticallySame(thisVertex.Z)) needToOffset = true;
                    foreach (var edge in thisVertex.Edges)
                    {
                        if (currentEdges.Contains(edge)) currentEdges.Remove(edge);
                        else currentEdges.Add(edge);
                    }
                    vIndex++;
                    if (vIndex == sortedVertices.Length) break;
                    thisVertex = sortedVertices[vIndex];
                }
                if (vIndex == sortedVertices.Length) break;

                if (needToOffset)
                    z += Math.Min(stepSize, sortedVertices[vIndex].Z - z) / 10.0;

                if (currentEdges.Any())
                {
                    var edgeDictionary = new Dictionary<Edge, Vector2>();
                    foreach (var ce in currentEdges)
                    {
                        var pointOnPlane = MiscFunctions.PointOnZPlaneFromLineSegment(z, ce.From.Coordinates,
                             ce.To.Coordinates);
                        edgeDictionary.Add(ce, new Vector2(pointOnPlane.X, pointOnPlane.Y));
                    }
                    loopsAlongZ[step] = CreatePolygons(edgeDictionary, Vector3.UnitZ, z, out var v2EDictionary, out var numComplete);
                    vertex2DToEdges[step] = v2EDictionary;
                    numCompletedPolygonsPerLayer[step] = numComplete;
                }
                else loopsAlongZ[step] = new List<Polygon>();
            }
            return loopsAlongZ;
        }
        #endregion


    }
}