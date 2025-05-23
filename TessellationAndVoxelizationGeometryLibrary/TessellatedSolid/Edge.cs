﻿// ***********************************************************************
// Assembly         : TessellationAndVoxelizationGeometryLibrary
// Author           : matth
// Created          : 04-03-2023
//
// Last Modified By : matth
// Last Modified On : 04-14-2023
// ***********************************************************************
// <copyright file="Edge.cs" company="Design Engineering Lab">
//     2014
// </copyright>
// <summary></summary>
// ***********************************************************************
using Newtonsoft.Json;
using System;
using System.Diagnostics.Eventing.Reader;
using System.Linq;


namespace TVGL
{
    /// <summary>
    /// The straight-line edge class. It connects to two nodes and lies between two faces.
    /// </summary>
    public class Edge : TessellationBaseClass
    {
        /// <summary>
        /// Prevents a default instance of the <see cref="Edge" /> class from being created.
        /// </summary>
        private Edge()
        {
        }

        /// <summary>
        /// Others the vertex.
        /// </summary>
        /// <param name="v">The v.</param>
        /// <returns>Vertex.</returns>
        /// <exception cref="System.Exception">OtherVertex: Vertex thought to connect to edge, but it doesn't.</exception>
        public Vertex OtherVertex(Vertex v)
        {
            if (v == To) return From;
            if (v == From) return To;
            throw new Exception("OtherVertex: Vertex thought to connect to edge, but it doesn't.");
        }

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="Edge" /> class.
        /// </summary>
        /// <param name="fromVertex">From vertex.</param>
        /// <param name="toVertex">To vertex.</param>
        /// <param name="ownedFace">The face.</param>
        /// <param name="otherFace">The other face.</param>
        /// <param name="doublyLinkedVertices">if set to <c>true</c> [doubly linked vertices].</param>
        /// <param name="edgeReference">The edge reference.</param>
        /// <exception cref="Exception"></exception>
        public Edge(Vertex fromVertex, Vertex toVertex, TriangleFace ownedFace, TriangleFace otherFace,
                    bool doublyLinkedVertices, long edgeReference = 0) : this(fromVertex, toVertex, doublyLinkedVertices)
        {
            if (edgeReference > 0)
                EdgeReference = edgeReference;
            else Edge.SetAndGetEdgeChecksum(this);
            _ownedFace = ownedFace;
            _otherFace = otherFace;
            ownedFace?.AddEdge(this);
            otherFace?.AddEdge(this);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Edge" /> class.
        /// </summary>
        /// <param name="fromVertex">From vertex.</param>
        /// <param name="toVertex">To vertex.</param>
        /// <param name="doublyLinkedVertices">if set to <c>true</c> [doubly linked vertices].</param>
        public Edge(Vertex fromVertex, Vertex toVertex, bool doublyLinkedVertices)
        {
            From = fromVertex;
            To = toVertex;
            if (doublyLinkedVertices) DoublyLinkVertices();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the From Vertex.
        /// </summary>
        /// <value>From.</value>
        public Vertex From { get; internal set; }

        /// <summary>
        /// Gets the To Vertex.
        /// </summary>
        /// <value>To.</value>
        public Vertex To { get; internal set; }

        /// <summary>
        /// Gets the length of the line.
        /// </summary>
        /// <value>The length.</value>
        public double Length
        {
            get
            {
                if (double.IsNaN(_length))
                    _length = Vector.Length();
                return _length;
            }
        }
        /// <summary>
        /// The length
        /// </summary>
        double _length = double.NaN;

        /// <summary>
        /// Gets the length of the line.
        /// </summary>
        /// <value>The length.</value>
        public Vector3 Vector
        {
            get
            {
                if (_vector.IsNull())
                    _vector = To.Coordinates - From.Coordinates;
                return _vector;
            }
        }
        /// <summary>
        /// The vector
        /// </summary>
        Vector3 _vector = Vector3.Null;


        /// <summary>
        /// Gets the vector.
        /// </summary>
        /// <value>The vector.</value>
        public Vector3 UnitVector => Vector.Normalize();

        /// <summary>
        /// The _other face
        /// </summary>
        private TriangleFace _otherFace;

        /// <summary>
        /// The _owned face
        /// </summary>
        private TriangleFace _ownedFace;

        /// <summary>
        /// Gets edge reference (checksum) value, which equals
        /// "From.IndexInList" + "To.IndexInList" (think strings)
        /// </summary>
        /// <value>To.</value>
        internal long EdgeReference { get; set; }

        /// <summary>
        /// Gets the owned face (the face in which the from-to direction makes sense
        /// - that is, produces the proper cross-product normal).
        /// </summary>
        /// <value>The owned face.</value>
        [JsonIgnore] //this two-way link creates an infinite loop for serialization and must be ignored.
        public TriangleFace OwnedFace
        {
            get => _ownedFace;
            internal set
            {
                if (_ownedFace != null && _ownedFace == value) return;
                _ownedFace = value;
            }
        }

        /// <summary>
        /// Gets the other face (the face in which the from-to direction doesn not
        /// make sense- that is, produces the negative cross-product normal).
        /// </summary>
        /// <value>The other face.</value>
        [JsonIgnore] //this two-way link creates an infinite loop for serialization and must be ignored.
        public TriangleFace OtherFace
        {
            get => _otherFace;
            internal set
            {
                if (_otherFace != null && _otherFace == value) return;
                _otherFace = value;
            }
        }


        /// <summary>
        /// Gets the adjacent face. You provide one face of the edge
        /// and this return the other face.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <returns>A TriangleFace.</returns>
        public TriangleFace GetMatingFace(TriangleFace face)
        {
            return face == _ownedFace ? _otherFace : _ownedFace;
        }


        /// <summary>
        /// Gets the internal angle in radians.
        /// </summary>
        /// <value>The internal angle.</value>
        public double InternalAngle
        {
            get
            {
                if (double.IsNaN(_internalAngle)) DefineInternalEdgeAngle();
                return _internalAngle;
            }
        }
        /// <summary>
        /// The internal angle
        /// </summary>
        double _internalAngle = double.NaN;

        /// <summary>
        /// Gets the curvature.
        /// </summary>
        /// <value>The curvature.</value>
        public override CurvatureType Curvature { get; internal set; }

        /// <summary>
        /// Gets the normal.
        /// </summary>
        /// <value>The normal.</value>
        [JsonIgnore] //Cannot serialize a possibly null value
        public override Vector3 Normal
        {
            get
            {
                if (_normal.IsNull()) DetermineNormal();
                return _normal;
            }
        }


        /// <summary>
        /// The normal
        /// </summary>
        Vector3 _normal = Vector3.Null;
        /// <summary>
        /// Determines the normal.
        /// </summary>
        /// <exception cref="System.NotImplementedException"></exception>
        private void DetermineNormal()
        {
            _normal = new Vector3(OwnedFace.Normal.X+OtherFace.Normal.X, 
                OwnedFace.Normal.Y + OtherFace.Normal.Y,
                OwnedFace.Normal.Z + OtherFace.Normal.Z).Normalize();
        }

        /// <summary>
        /// Updates the with new face.
        /// </summary>
        /// <param name="face">The face.</param>
        internal void UpdateWithNewFace(TriangleFace face)
        {
            var v1 = face.Vertices.FindIndex(v => v == From);
            var v2 = face.Vertices.FindIndex(v => v == To);
            var step = v2 - v1;
            if (step < 0) step += 3;
            if (step == 1) OwnedFace = face;
            else OtherFace = face;
            face.AddEdge(this);
        }



        /// <summary>
        /// Updates the edge vector and length, if a vertex has been moved.
        /// </summary>
        /// <param name="lengthAndAngleUnchanged">if set to <c>true</c> [length and angle unchanged].</param>
        /// <exception cref="Exception"></exception>
        public void Update(bool lengthAndAngleUnchanged = false)
        {
            //Reset the vector, since vertices may have been moved.
            _vector = new Vector3(To.Coordinates[0] - From.Coordinates[0],
                To.Coordinates[1] - From.Coordinates[1],
                To.Coordinates[2] - From.Coordinates[2]
            );

            if (lengthAndAngleUnchanged) return; //Done. No need to update the length or the internal edge angle
            _length = double.NaN;
            _internalAngle = double.NaN;
            //Curvature = CurvatureType.Undefined;
        }

        /// <summary>
        /// Defines the edge angle.
        /// </summary>
        /// <exception cref="System.Exception">not possible</exception>
        private void DefineInternalEdgeAngle()
        {
            /* this is a tricky function. What we need to do is take the dot-product of the normals.
             * which will give the cos(theta). Calling Inverse cosine will result in a value from 0 to
             * pi, but is the edge convex or concave? It is convex if the crossproduct of the normals is 
             * in the same direction as the edge vector (dot product is positive). But we need to know 
             * which face-normal goes first in the cross product calculation as this will change the 
             * resulting direction. The one to go first is the one that "owns" the edge. What I mean by
             * own is that the from-to of the edge makes sense in the counter-clockwise prediction of 
             * the face normal. For one face the from-to will be incorrect (normal facing inwards) - 
             * in some geometry approaches this is solved by the concept of half-edges. Here we will 
             * just re-order the two faces referenced in the edge so that the first is the one that 
             * owns the edge...the face for which the direction makes sense, and the second face will 
             * need to reverse the edge vector to make it work out in a proper counter-clockwise loop 
             * for that face. */
            if (_ownedFace == _otherFace || _ownedFace == null || _otherFace == null)
            {
                _internalAngle = double.NaN;
                return;
            }
            var dot = _ownedFace.Normal.Dot(_otherFace.Normal);
            if (!dot.IsLessThanNonNegligible(1.0, Constants.BaseTolerance))
                _internalAngle = Math.PI;
            else if (!dot.IsGreaterThanNonNegligible(-1.0, Constants.BaseTolerance))
            {
                // is it a crack or a sharp edge?
                // in order to find out we look to the other two faces connected to each
                // face to find out
                var ownedNeighborAvgNormals = new Vector3();
                var numNeighbors = 0;
                foreach (var face in _ownedFace.AdjacentFaces)
                {
                    if (face != null && face != _otherFace)
                    {
                        ownedNeighborAvgNormals += face.Normal;
                        numNeighbors++;
                    }
                }
                ownedNeighborAvgNormals = ownedNeighborAvgNormals.Divide(numNeighbors);
                var otherNeighborAvgNormals = new Vector3();
                numNeighbors = 0;
                foreach (var face in _otherFace.AdjacentFaces)
                {
                    if (face != null && face != _ownedFace)
                    {
                        otherNeighborAvgNormals += face.Normal;
                        numNeighbors++;
                    }
                }
                otherNeighborAvgNormals = otherNeighborAvgNormals.Divide(numNeighbors);
                if (ownedNeighborAvgNormals.Cross(otherNeighborAvgNormals).Dot(Vector) < 0)
                    _internalAngle = Constants.TwoPi;
                else
                    _internalAngle = 0.0;
            }
            else
            {
                var cross = _ownedFace.Normal.Cross(_otherFace.Normal).Dot(Vector);
                if (cross < 0)
                    _internalAngle = Math.PI + Math.Acos(dot);
                else //(cross > 0)
                    _internalAngle = Math.PI - Math.Acos(dot);
            }
            if (InternalAngle > Constants.TwoPi) throw new Exception("not possible");
        }


        /// <summary>
        /// Determines whether the edge two vectors is discontinuous or not. This
        /// checks both C1 and C2 discontinuity.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="chordError">The chord error.</param>
        /// <returns>A bool.</returns>
        public bool IsDiscontinuous(double chordError)
        {
            if (OtherFace == null || OwnedFace == null) return true;
            var otherV = OtherFace.Normal.Cross(Vector).Normalize();
            if ((OtherFace.AB != this && OtherFace.BC != this && OtherFace.CA != this) ||
                (OwnedFace.AB != this && OwnedFace.BC != this && OwnedFace.CA != this))
                return true;
            var otherVLength = otherV.Dot(From.Coordinates - OtherFace.OtherVertex(this).Coordinates);
            otherV = otherVLength * otherV;
            var ownedV = OwnedFace.Normal.Cross(Vector).Normalize();
            var ownedVLength = ownedV.Dot(OwnedFace.OtherVertex(this).Coordinates - From.Coordinates);
            ownedV = ownedVLength * ownedV;
            return MiscFunctions.LineSegmentsAreC1Discontinuous(ownedV.Dot(otherV), ownedV.Cross(otherV).Length(),
                ownedVLength, otherVLength, chordError);
        }


        /// <summary>
        /// Doublies the link vertices.
        /// </summary>
        internal void DoublyLinkVertices()
        {
            if (!From.Edges.Contains(this))
                From.Edges.Add(this);
            if (!To.Edges.Contains(this))
                To.Edges.Add(this);
        }

        /// <summary>
        /// Returns owned and other face in that order
        /// TODO: this function seems convoluted. Why not just return the ownedFace and otherFace directly?
        /// </summary>
        /// <param name="edgeChecksum">The edge checksum.</param>
        /// <param name="face1">The face1.</param>
        /// <param name="face2">The face2.</param>
        /// <returns>System.ValueTuple&lt;TriangleFace, TriangleFace&gt;.</returns>
        public static (TriangleFace, TriangleFace) GetOwnedAndOtherFace(long edgeChecksum, TriangleFace face1, TriangleFace face2)
        {
            var (from, to) = GetVertexIndices(edgeChecksum);
            //We are going to enforce that the edge is defined along the vertices, such that it goes from the smaller
            //vertex index to the larger. 
            var v0 = face1.Vertices.First(v => v.IndexInList == from);
            var v1 = face1.Vertices.First(v => v.IndexInList == to);
            var v2 = face1.Vertices.First(v => v.IndexInList != to && v.IndexInList != from);
            var vector1 = v1.Coordinates.Subtract(v0.Coordinates);
            var vector2 = v2.Coordinates.Subtract(v1.Coordinates);
            var dot = vector1.Cross(vector2).Dot(face1.Normal);
            //The owned face(the face in which the from-to direction makes sense
            // - that is, produces the proper cross-product normal).
            return Math.Sign(dot) > 0 ? (face1, face2) : (face2, face1);
        }

        /// <summary>
        /// Gets the vertex indices.
        /// </summary>
        /// <param name="checkSum">The check sum.</param>
        /// <returns>System.ValueTuple&lt;System.Int32, System.Int32&gt;.</returns>
        public static (int, int) GetVertexIndices(long checkSum)
        {
            //The checksum is ordered from larger to smaller
            var largeIndex = (int)(checkSum / Constants.VertexCheckSumMultiplier);
            var smallIndex = (int)(checkSum - largeIndex * Constants.VertexCheckSumMultiplier);
            return (smallIndex, largeIndex);
        }


        /// <summary>
        /// Sets the and get edge checksum.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <returns>System.Int64.</returns>
        internal static long SetAndGetEdgeChecksum(Edge edge)
        {
            var checksum = -1L;
            if (edge.To is not null && edge.From is not null)
                checksum = GetEdgeChecksum(edge.From, edge.To);
            edge.EdgeReference = checksum;
            return checksum;
        }

        /// <summary>
        /// Gets the edge checksum.
        /// </summary>
        /// <param name="vertex1">The vertex1.</param>
        /// <param name="vertex2">The vertex2.</param>
        /// <returns>System.Int64.</returns>
        public static long GetEdgeChecksum(Vertex vertex1, Vertex vertex2)
        {
            return GetEdgeChecksum(vertex1.IndexInList, vertex2.IndexInList);
        }

        /// <summary>
        /// Gets the edge checksum.
        /// </summary>
        /// <param name="vertex1Index">Index of the vertex1.</param>
        /// <param name="vertex2Index">Index of the vertex2.</param>
        /// <returns>System.Int64.</returns>
        public static long GetEdgeChecksum(int vIndex1, int vIndex2)
        {
            if (vIndex1 == -1 || vIndex2 == -1)
                return -1;
            //if (vIndex1 == vIndex2) throw new Exception("edge to same vertices.");
            return (vIndex1 < vIndex2) ? vIndex1 + Constants.VertexCheckSumMultiplier * vIndex2 :
                vIndex2 + Constants.VertexCheckSumMultiplier * vIndex1;
        }

        /// <summary>
        /// Inverts this instance.
        /// </summary>
        internal void Invert()
        {
            if (Curvature != CurvatureType.Undefined)
                Curvature = (CurvatureType)(-1 * (int)Curvature);
            _internalAngle = Constants.TwoPi - _internalAngle;
            var tempFace = OwnedFace;
            OwnedFace = OtherFace;
            OtherFace = tempFace;
        }

        //Leave this as a method, to limit excess memory.

        /// <summary>
        /// Centers this instance.
        /// </summary>
        /// <returns>Vector3.</returns>
        public Vector3 Center()
        {
            return (To.Coordinates + From.Coordinates) / 2;
        }
        #endregion
    }
}