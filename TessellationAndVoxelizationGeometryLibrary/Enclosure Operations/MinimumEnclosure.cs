﻿// ***********************************************************************
// Assembly         : TessellationAndVoxelizationGeometryLibrary
// Author           : matth
// Created          : 04-03-2023
//
// Last Modified By : matth
// Last Modified On : 04-14-2023
// ***********************************************************************
// <copyright file="MinimumEnclosure.cs" company="Design Engineering Lab">
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
    /// The MinimumEnclosure class includes static functions for defining smallest enclosures for a
    /// tesselated solid. For example: convex hull, minimum bounding box, or minimum bounding sphere.
    /// </summary>
    public static partial class MinimumEnclosure
    {
        /// <summary>
        /// The maximum rotations for obb
        /// </summary>
        private const int MaxRotationsForOBB = 24;

        /// <summary>
        /// Finds the minimum bounding rectangle given a set of points. Either send any set of points
        /// OR the convex hull 2D.
        /// Optional booleans for what information should be set in the Bounding Rectangle.
        /// Example: If you really just need the area, you don't need the corner points or
        /// points on side.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="pointsAreConvexHull">if set to <c>true</c> [points are convex hull].</param>
        /// <param name="setCornerPoints">if set to <c>true</c> [set corner points].</param>
        /// <param name="setPointsOnSide">if set to <c>true</c> [set points on side].</param>
        /// <returns>BoundingRectangle.</returns>
        public static BoundingRectangle BoundingRectangle(this Polygon polygon, bool pointsAreConvexHull = false,
            bool setCornerPoints = true, bool setPointsOnSide = true)
        {
            return BoundingRectangle(polygon.Path, pointsAreConvexHull, setCornerPoints, setPointsOnSide);
        }
        /// <summary>
        /// Finds the minimum bounding rectangle given a set of points. Either send any set of points
        /// OR the convex hull 2D.
        /// Optional booleans for what information should be set in the Bounding Rectangle.
        /// Example: If you really just need the area, you don't need the corner points or
        /// points on side.
        /// </summary>
        /// <param name="points">The points.</param>
        /// <param name="pointsAreConvexHull">if set to <c>true</c> [points are convex hull].</param>
        /// <param name="setCornerPoints">if set to <c>true</c> [set corner points].</param>
        /// <param name="setPointsOnSide">if set to <c>true</c> [set points on side].</param>
        /// <returns>BoundingRectangle.</returns>
        public static BoundingRectangle BoundingRectangle(this IEnumerable<Vector2> points, bool pointsAreConvexHull = false,
            bool setCornerPoints = true, bool setPointsOnSide = true)
        {
            return RotatingCalipers2DMethod(points, pointsAreConvexHull, setCornerPoints, setPointsOnSide);
        }

        /// <summary>
        /// Finds the minimum bounding box.
        /// </summary>
        /// <param name="solids">The solids.</param>
        /// <returns>BoundingBox.</returns>
        public static BoundingBox<Vertex> FindMinimumBoundingBox(this List<TessellatedSolid> solids)
        {
            foreach (var solid in solids)
                if (solid.ConvexHull == null) ConvexHull3D.Create(solid, false);
            var vertices = new List<Vertex>();
            foreach (var solid in solids)
                vertices.AddRange(solid.ConvexHull.Vertices.Any() ? solid.ConvexHull.Vertices : solid.Vertices);

            return FindMinimumBoundingBox(vertices);
        }

        /// <summary>
        /// Finds the minimum bounding box.
        /// </summary>
        /// <param name="ts">The ts.</param>
        /// <returns>BoundingBox.</returns>
        public static BoundingBox<Vertex> FindMinimumBoundingBox(this TessellatedSolid ts)
        {
            if (ts.ConvexHull == null) ConvexHull3D.Create(ts, false);
            if (ts.ConvexHull == null || !ts.ConvexHull.Vertices.Any())
                return FindMinimumBoundingBox(ts.Vertices);
            else return FindMinimumBoundingBox(ts.ConvexHull.Vertices);
        }

        /// <summary>
        /// Finds the minimum bounding box.
        /// </summary>
        /// <param name="convexHull">The convex hull.</param>
        /// <returns>BoundingBox.</returns>
        public static BoundingBox<Vertex> FindMinimumBoundingBox(this ConvexHull3D convexHull)
        {
            return FindMinimumBoundingBox(convexHull.Vertices);
        }

        /// <summary>
        /// Finds the minimum bounding box.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="convexHullVertices">The convex hull vertices.</param>
        /// <returns>BoundingBox.</returns>
        public static BoundingBox<T> FindMinimumBoundingBox<T>(this IEnumerable<T> convexHullVertices) where T : IVector3D
        {
            // here we create 13 directions. Why 13? basically it is all ternary (-1,0,+1) combinations of x,y,and z.
            // skipping symmetric and 0,0,0. Another way to think of it is to make a Direction from a cube with
            // vectors emanating from every vertex, edge, and face. that would be 8+12+6 = 26. And since there
            // is no need to do mirror image directions this is 26/2 or 13.
            var directions = new List<Vector3>();
            for (var i = -1; i <= 1; i++)
                for (var j = -1; j <= 1; j++)
                    directions.Add(new Vector3(1.0, i, j).Normalize());
            directions.Add(new Vector3(0, 0, 1));
            directions.Add(new Vector3(0, 1, 0));
            directions.Add(new Vector3(0, 1, 1).Normalize());
            directions.Add(new Vector3(0, -1, 1).Normalize());
            var minVolume = double.PositiveInfinity;
            BoundingBox<T> minBox = null;
            for (var i = 0; i < 13; i++)
            {
                var box = Find_via_ChanTan_AABB_Approach(convexHullVertices, directions[i]);
                if (box == null || box.Volume >= minVolume) continue;
                minVolume = box.Volume;
                minBox = box;
            }
            if (minBox == null) return null;
            // to make a consistent result, we will put the longest dimension along X, the second along Y and the shortest
            // on Z. Also, we will flip directions so that the more positive direction is chosen, which makes for smaller
            // rotation angles to square the solid
            var largestDirection = minBox.SortedDirectionsLongToShort[0];
            var minPointsLargestDir = minBox.PointsOnFaces[2 * minBox.SortedDirectionIndicesLongToShort[0]];
            var maxPointsLargestDir = minBox.PointsOnFaces[2 * minBox.SortedDirectionIndicesLongToShort[0] + 1];
            if (largestDirection.Dot(new Vector3(1, 1, 1)) < 0)
            {
                largestDirection = -largestDirection;
                var temp = minPointsLargestDir;
                minPointsLargestDir = maxPointsLargestDir;
                maxPointsLargestDir = temp;
            }
            var midDirection = minBox.SortedDirectionsLongToShort[1];
            var minPointsMediumDir = minBox.PointsOnFaces[2 * minBox.SortedDirectionIndicesLongToShort[1]];
            var maxPointsMediumDir = minBox.PointsOnFaces[2 * minBox.SortedDirectionIndicesLongToShort[1] + 1];
            if (midDirection.Dot(new Vector3(1, 1, 1)) < 0)
            {
                midDirection = -midDirection;
                var temp = minPointsMediumDir;
                minPointsMediumDir = maxPointsMediumDir;
                maxPointsMediumDir = temp;
            }
            var smallestDirection = largestDirection.Cross(midDirection);
            var minPointsSmallDir = minBox.PointsOnFaces[2 * minBox.SortedDirectionIndicesLongToShort[2]];
            var maxPointsSmallDir = minBox.PointsOnFaces[2 * minBox.SortedDirectionIndicesLongToShort[2] + 1];
            if (smallestDirection.Dot(minBox.SortedDirectionsLongToShort[2]) < 0)
            {
                var temp = minPointsSmallDir;
                minPointsSmallDir = maxPointsSmallDir;
                maxPointsSmallDir = temp;
            }
            return new BoundingBox<T>(
                [ minBox.SortedDimensionsLongToShort[0]* largestDirection,
                    minBox.SortedDimensionsLongToShort[1]* midDirection,
                    minBox.SortedDimensionsLongToShort[2]* smallestDirection ],
                [ minPointsLargestDir, maxPointsLargestDir, minPointsMediumDir, maxPointsMediumDir,
                    minPointsSmallDir,maxPointsSmallDir ]);
        }

        #region ChanTan AABB Approach

        /// <summary>
        /// Find_via_s the chan tan_ aab b_ approach.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="convexHullVertices">The convex hull vertices.</param>
        /// <param name="minOBB">The minimum obb.</param>
        /// <returns>BoundingBox.</returns>
        private static BoundingBox<T> Find_via_ChanTan_AABB_Approach<T>(IEnumerable<T> convexHullVertices, Vector3 start) where T : IVector3D
        {
            var minOBB = FindOBBAlongDirection(convexHullVertices, start);
            var failedConsecutiveRotations = 0;
            var k = 0;
            var i = 0;
            var cvxHullVertList = convexHullVertices as IList<T> ?? convexHullVertices.ToList();
            do
            {
                if (++i == 3) i = 0;
                start = minOBB.Directions[i];
                //Find new OBB along OBB.direction2 and OBB.direction3, keeping the best OBB.
                var newObb = FindOBBAlongDirection(cvxHullVertList, start);
                if (newObb == null) return null;
                if (newObb.Volume.IsLessThanNonNegligible(minOBB.Volume))
                {
                    minOBB = newObb;
                    failedConsecutiveRotations = 0;
                }
                else failedConsecutiveRotations++;
                k++;
            } while (failedConsecutiveRotations < 3 && k < MaxRotationsForOBB);
            return minOBB;
        }

        #endregion

        #region Get Length And Extreme Vertices
        /// <summary>
        /// Given a Direction, dir, this function returns the maximum length along this Direction
        /// for the provided vertices as well as the all vertices that represent the extremes.
        /// If you only want one the length or only need one vertex at the extreme, it is more efficient
        /// and easier to use GetLengthAndExtremeVertex
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="vertices">The vertices.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="bottomVertices">The bottom vertices.</param>
        /// <param name="topVertices">The top vertices.</param>
        /// <returns>System.Double.</returns>
        public static double GetLengthAndExtremeVertices<T>(this IEnumerable<T> vertices, Vector3 direction,
            out List<T> bottomVertices,
            out List<T> topVertices) where T : IVector3D
        {
            var dir = direction.Normalize();
            var minD = double.PositiveInfinity;
            var maxD = double.NegativeInfinity;
            bottomVertices = new List<T>();
            topVertices = new List<T>();
            foreach (var v in vertices)
            {
                var distance = v.Dot(dir);
                if (distance.IsPracticallySame(minD, Constants.BaseTolerance))
                    bottomVertices.Add(v);
                else if (distance < minD)
                {
                    bottomVertices.Clear();
                    bottomVertices.Add(v);
                    minD = distance;
                }
                if (distance.IsPracticallySame(maxD, Constants.BaseTolerance))
                    topVertices.Add(v);
                else if (distance > maxD)
                {
                    topVertices.Clear();
                    topVertices.Add(v);
                    maxD = distance;
                }
            }
            return maxD - minD;
        }


        /// <summary>
        /// Given a Direction, dir, this function returns the maximum length along this Direction and one vertex
        /// that represents each extreme. Use this if you do not need all the vertices at the extremes.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="vertices">The vertices.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="bottomVertex">The bottom vertex.</param>
        /// <param name="topVertex">The top vertex.</param>
        /// <returns>System.Double.</returns>
        public static double GetLengthAndExtremeVertex<T>(this IEnumerable<T> vertices, Vector3 direction,
            out T bottomVertex,
            out T topVertex) where T : IVector3D
        {
            var (minD, maxD) = GetDistanceToExtremeVertex(vertices, direction, out bottomVertex, out topVertex);
            return maxD - minD;
        }

        /// <summary>
        /// Given a Direction, dir, this function returns the min/max distance to the furthest vertex along this
        /// Direction that represents each extreme.  Use this if you do not need all the vertices at the extremes.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="vertices">The vertices.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="bottomVertex">The bottom vertex.</param>
        /// <param name="topVertex">The top vertex.</param>
        /// <returns>System.Double.</returns>
        public static (double btmDistance, double topDistance) GetDistanceToExtremeVertex<T>(this IEnumerable<T> vertices, Vector3 direction,
            out T bottomVertex, out T topVertex) where T : IVector3D
        {
            var dir = direction.Normalize();
            var btmDistance = double.PositiveInfinity;
            bottomVertex = default; //this is an unfortunate assignment but the compiler doesn't trust
            topVertex = default;  // that is will get assigned in conditions below. Also, can't assign to
                                  //null, since Vector3 is struct
            var topDistance = double.NegativeInfinity;
            foreach (var v in vertices)
            {
                var distance = v.Dot(dir);
                if (distance < btmDistance)
                {
                    bottomVertex = v;
                    btmDistance = distance;
                }
                if (distance > topDistance)
                {
                    topVertex = v;
                    topDistance = distance;
                }
            }
            return (btmDistance, topDistance);
        }

        /// <summary>
        /// Given a Direction, dir, this function returns the maximum length along this Direction and one vertex
        /// that represents each extreme. Use this if you do not need all the vertices at the extremes.
        /// </summary>
        /// <param name="vertices">The vertices.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="bottomVertex">The bottom vertex.</param>
        /// <param name="topVertex">The top vertex.</param>
        /// <returns>System.Double.</returns>
        public static double GetLengthAndExtremeVertex(this IEnumerable<Vector3> vertices, Vector3 direction,
            out Vector3 bottomVertex,
            out Vector3 topVertex)
        {
            var (minD, maxD) = GetDistanceToExtremeVertex(vertices, direction, out bottomVertex, out topVertex);
            return maxD - minD;
        }

        /// <summary>
        /// Given a Direction, dir, this function returns the min/max distance to the furthest vertex along this
        /// Direction that represents each extreme.  Use this if you do not need all the vertices at the extremes.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="vertices">The vertices.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="bottomVertex">The bottom vertex.</param>
        /// <param name="topVertex">The top vertex.</param>
        /// <returns>System.Double.</returns>
        public static (double, double) GetDistanceToExtremeVertex<T>(this IEnumerable<Vector3> vertices, Vector3 direction,
            out Vector3 bottomVertex, out Vector3 topVertex)
        {
            var dir = direction.Normalize();
            var minD = double.PositiveInfinity;
            bottomVertex = Vector3.Null;
            topVertex = Vector3.Null;
            var maxD = double.NegativeInfinity;
            foreach (var v in vertices)
            {
                var distance = v.Dot(dir);
                if (distance < minD)
                {
                    bottomVertex = v;
                    minD = distance;
                }
                if (distance > maxD)
                {
                    topVertex = v;
                    maxD = distance;
                }
            }
            return (minD, maxD);
        }

        /// <summary>
        /// Given a Direction, dir, this function returns the maximum length along this Direction
        /// for the provided points as well as the points that represent the extremes.
        /// </summary>
        /// <param name="points">The vertices.</param>
        /// <param name="direction2D">The direction.</param>
        /// <param name="bottomPoints">The bottom vertices.</param>
        /// <param name="topPoints">The top vertices.</param>
        /// <returns>System.Double.</returns>
        public static double GetLengthAndExtremePoints(this IEnumerable<Vector2> points, Vector2 direction2D,
            out List<Vector2> bottomPoints,
            out List<Vector2> topPoints)
        {
            var minD = double.PositiveInfinity;
            bottomPoints = new List<Vector2>();
            topPoints = new List<Vector2>();
            var maxD = double.NegativeInfinity;
            foreach (var point in points)
            {
                var distance = direction2D.Dot(point);
                if (distance.IsPracticallySame(minD, Constants.BaseTolerance))
                    bottomPoints.Add(point);
                else if (distance < minD)
                {
                    bottomPoints.Clear();
                    bottomPoints.Add(point);
                    minD = distance;
                }
                if (distance.IsPracticallySame(maxD, Constants.BaseTolerance))
                    bottomPoints.Add(point);
                else if (distance > maxD)
                {
                    topPoints.Clear();
                    topPoints.Add(point);
                    maxD = distance;
                }
            }
            return maxD - minD;
        }
        #endregion

        #region 2D Rotating Calipers
        /// <summary>
        /// The caliper offset angles are constants used below in +122 lines. Not that they are simply angles
        /// of a rectangle
        /// </summary>
        private static readonly double[] CaliperOffsetAngles = new[] { Math.PI / 2, 0, -Math.PI / 2, Math.PI };

        // we want to store the extreme in a meaningful order: 0) dir1-minima, 1) dir1-maxima, 2)dir2-minima,
        // 3) dir2-maxima. However in the following method we order the sides in the rectangle from the bottom
        // (y-min) counter-clockwise. So the first is y-min (or 2 since this is dir2-minima), then 1 for dir1-max,
        // then 3 for dir2-max, then 0 since this would be xmin or dir1-minima
        /// <summary>
        /// Rotating the calipers 2D method. Convex hull must be a counter clockwise loop.
        /// Optional booleans for what information should be set in the Bounding Rectangle.
        /// Example: If you really just need the area, you don't need the corner points or
        /// points on side.
        /// </summary>
        /// <param name="initialPoints">The points.</param>
        /// <param name="pointsAreConvexHull">if set to <c>true</c> [points are convex hull].</param>
        /// <param name="setCornerPoints">if set to <c>true</c> [set corner points].</param>
        /// <param name="setPointsOnSide">if set to <c>true</c> [set points on side].</param>
        /// <returns>System.Double.</returns>
        /// <exception cref="Exception">Area should never be negligible unless data is messed up.</exception>
        public static BoundingRectangle RotatingCalipers2DMethod(IEnumerable<Vector2> initialPoints,
            bool pointsAreConvexHull, bool setCornerPoints, bool setPointsOnSide)
        {
            if (!initialPoints.Any())
                return new BoundingRectangle();
            if (initialPoints.Count() == 1)
            {
                var singlePoint = initialPoints.First();
                return new BoundingRectangle(Vector2.UnitX, Vector2.UnitY, singlePoint.X, singlePoint.X,
                    singlePoint.Y, singlePoint.Y);
            }
            /* welcome to a surprisingly complex method that is optimized for linear time.
             * the points are ordered in the CCW polygon from starting with the lowest x-value.
             * Then we rotate the shape from 0 to up to 90-degree to identify all possible 2d
             * rectangles bounding the points. the amount of angle to rotate depends on the next 
             * smallest angle for the four point on the four sides to reach the next point. This
             * is because the bounding recangle has at least one side coincident with one side
             * of the polygon.
             * In this code, we are breaking it into four sections: 1) Prune and Reorder the points, 
             * 2) Get extreme points and initial angles 3) find new rectangle properties and keep if the best
             * 4) Update Angles. After this, we simple add the side points if desired
             */
            #region 1) Prune and Reorder the points
            IList<Vector2> points = initialPoints as IList<Vector2> ?? initialPoints.ToList();
            if (!pointsAreConvexHull)
                points = ConvexHull2D.Create(points, out _);

            if (points.Count < 3)
            {
                var v = points[1] - points[0];
                v = v.Normalize();
                var w = new Vector2(-v.Y, v.X);
                var dist0 = v.Dot(points[0]);
                var dist1 = v.Dot(points[1]);
                var perpDist = w.Dot(points[0]);
                return new BoundingRectangle(v, w, Math.Min(dist0, dist1), Math.Max(dist0, dist1), perpDist, perpDist);
            }

            //Simplify the points to make sure they are the minimal convex hull
            //Only set it as the convex hull if it contains more than three points.
            var cvxPointsSimple = points.RemoveCollinearEdgesToNewList();
            if (cvxPointsSimple.Count >= 3) points = cvxPointsSimple;
            /* the cvxPoints will be arranged from a point with minimum X-value around in a CCW loop to the last point 
             * however, we want the last point that has minX, so that we can easily get the next angle  */
            var pointsCount = points.Count;
            var lastIndex = pointsCount - 1;
            //Good picture of extreme vertices in the following link
            //http://citeseerx.ist.psu.edu/viewdoc/download?doi=10.1.1.155.5671&rep=rep1&type=pdf
            //Godfried Toussaint: Solving Geometric Problems with the Rotating Calipers
            //Note that while these points are ordered counter clockwise, we are rotating the calipers in reverse (clockwise),
            //Which is why the points are directed this way.
            #endregion

            #region 2) Get Extreme Points and initial angles
            var extremeIndices = new int[4];
            // the points are arranged in a loop but not starting at the minX (minY for ties)! 
            // So the first loop must check ALL points.
            //Point0 = min X, (at the lowest Y value for ties)
            var minX = double.PositiveInfinity;
            var minY = double.PositiveInfinity;
            for (int i = 0; i < points.Count; i++)
            {
                if (minX > points[i].X || (minX == points[i].X && minY > points[i].Y))
                {
                    extremeIndices[0] = i;
                    minX = points[i].X;
                    minY = points[i].Y;
                }
            }
            extremeIndices[1] = extremeIndices[0];
            var nextIndex = (extremeIndices[1] + 1) % points.Count;
            // now going forward. we can trust that we don't need to check all the points.
            // Once, the dimension changes direction we can leave
            //Point1 = min Y (at the max X value for ties. This is done in the following while-loop)
            while (points[extremeIndices[1]].Y >= points[nextIndex].Y)
            {
                extremeIndices[1] = nextIndex;
                nextIndex++;
                if (nextIndex == pointsCount) nextIndex = 0;
            }
            //Point2 = max X (at the max Y value for ties
            extremeIndices[2] = extremeIndices[1];
            while (points[extremeIndices[2]].X <= points[nextIndex].X)
            {
                extremeIndices[2] = nextIndex;
                nextIndex++;
                if (nextIndex == pointsCount) nextIndex = 0;
            }
            //Point3 = max Y (at the min X for ties). 
            extremeIndices[3] = extremeIndices[2];
            while (points[extremeIndices[3]].Y <= points[nextIndex].Y)
            {
                extremeIndices[3] = nextIndex;
                nextIndex++;
                if (nextIndex == pointsCount) nextIndex = 0;
            }
            // now set up initial array of angles. there will always be four angles. One for each face of the rectangle
            var angles = new double[4];
            //For each of the 4 supporting points (those forming the rectangle),
            var smallestAngle = double.PositiveInfinity;
            var smallestAngleIndex = 0;
            for (var i = 0; i < 4; i++)
            {
                double angle = GetAngleWithNext(extremeIndices[i], points, i, lastIndex);
                //If the angle has rotated beyond the 90 degree bounds, it will be negative
                //And should never be chosen from then on.
                if (angle < 0) angles[i] = double.PositiveInfinity;
                else
                {
                    angles[i] = angle;
                    if (angle < smallestAngle)
                    {
                        smallestAngle = angle;
                        smallestAngleIndex = i;
                    }
                }
            }
            #endregion

            const double oneQuarterRotation = Math.PI / 2;
            var bestRectangle = new BoundingRectangle(Vector2.UnitX, Vector2.UnitY, 0, 0, double.PositiveInfinity,
                double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity);
            var bestExtremeIndices = new int[4];
            var maxIterations = points.Count();
            var iteration = 0;
            while (smallestAngle <= oneQuarterRotation && iteration < maxIterations)
            {
                iteration++;
                #region 3) find new rectangle properties and keep if the best
                //Get unit vectors for the sides of the new rectangle and find the dimensions
                var currentPoint = points[extremeIndices[smallestAngleIndex]];
                nextIndex = extremeIndices[smallestAngleIndex] == lastIndex ? 0 : extremeIndices[smallestAngleIndex] + 1;
                var nextPoint = points[nextIndex];
                // unitVectorAlongSide is found from the smallestAngle to get a side coincident with the rectangle. This is used
                // for the extremes in the OTHER direction. we need the normal for the side to find the distance across.
                var unitVectorAlongSide = (nextPoint - currentPoint).Normalize();
                // the opposite distance would start from the previous side to the current's, or +3 sides away
                var d1Min = unitVectorAlongSide.Dot(points[extremeIndices[(smallestAngleIndex + 3) % 4]]);
                // this other distance would end at the nextside to the current's, or +1 sides away
                var d1Max = unitVectorAlongSide.Dot(points[extremeIndices[(smallestAngleIndex + 1) % 4]]);
                // the vector used for distance across is 90-degree in the CCW direction. it points "into" the rectangle
                var unitVectorPointInto = new Vector2(-unitVectorAlongSide.Y, unitVectorAlongSide.X);
                // dotDistances[2] will be the min in this unitVectorPointInto dir and it corresponds to the current point
                var d2Min = unitVectorPointInto.Dot(currentPoint);
                // dotDistances[3] will be the max in this unitVectorPointInto dir which is determined for the opposite side
                // indicate by points[extremeIndices[(smallestAngleIndex + 2) % 4]]. Note the mod-4 let's us wrap-around
                var d2Max = unitVectorPointInto.Dot(points[extremeIndices[(smallestAngleIndex + 2) % 4]]);
                var length1 = d1Max - d1Min;
                var length2 = d2Max - d2Min;
                var area = length1 * length2;

                //If this is an improvement, set the parameters for the best bounding rectangle.
                if (area < bestRectangle.Area)
                {
                    if (setPointsOnSide)
                    {
                        var sidePoints = new List<Vector2>[4];
                        bestExtremeIndices[0] = extremeIndices[(smallestAngleIndex + 3) % 4];
                        bestExtremeIndices[1] = extremeIndices[(smallestAngleIndex + 1) % 4];
                        bestExtremeIndices[2] = extremeIndices[smallestAngleIndex];
                        bestExtremeIndices[3] = extremeIndices[(smallestAngleIndex + 2) % 4];
                        for (int i = 0; i < 4; i++)
                        {
                            var direction = (i < 2) ? bestRectangle.Direction1 : bestRectangle.Direction2;
                            sidePoints[i] = FindSidePoints(bestExtremeIndices[i], bestRectangle.Offsets(i), points, direction, lastIndex);
                        }
                        bestRectangle = new BoundingRectangle(unitVectorAlongSide, unitVectorPointInto, d1Min, d1Max, d2Min, d2Max,
                            length1, length2, area, sidePoints);
                    }
                    else
                        bestRectangle = new BoundingRectangle(unitVectorAlongSide, unitVectorPointInto, d1Min, d1Max, d2Min, d2Max,
                            length1, length2, area);
                }
                #endregion
                #region 4) Update angles
                extremeIndices[smallestAngleIndex] = nextIndex;
                double angle = GetAngleWithNext(extremeIndices[smallestAngleIndex], points, smallestAngleIndex, lastIndex);
                angles[smallestAngleIndex] = (angle < 0) ? double.PositiveInfinity : angle;
                smallestAngle = angles[0];
                smallestAngleIndex = 0;
                for (var i = 1; i < 4; i++)
                {
                    if (angles[i] >= smallestAngle) continue;
                    smallestAngle = angles[i];
                    smallestAngleIndex = i;
                }
                #endregion
            }
            return bestRectangle;
        }

        /// <summary>
        /// Gets the angle with next.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="points">The points.</param>
        /// <param name="sideIndex">Index of the side.</param>
        /// <param name="lastIndex">The last index.</param>
        /// <returns>System.Double.</returns>
        private static double GetAngleWithNext(int index, IList<Vector2> points, int sideIndex, int lastIndex)
        {
            var current = points[index];
            var nextPoint = index == lastIndex ? points[0] : points[index + 1];
            // for fast functions Trigonometric expressions should be avoided. And you could do so with just
            // cross and dot products but this would require vector normalization - I think square-root is
            // slower than Atan. Also Atan is more intuitive.
            return Math.Atan2(nextPoint.Y - current.Y, nextPoint.X - current.X) + CaliperOffsetAngles[sideIndex];
        }
        /// <summary>
        /// Finds the side points. We don't need to check all the points in the polygon. but we do need to check either side
        /// of the extreme points. At first I thought only backwards, but for the 3 extremes that weren't the starting reference,
        /// it is difficult to know if the rotation of the reference also brought new points to the side. The same logic can
        /// be applied to forward points as well - especially for the reference point.
        /// </summary>
        /// <param name="startingIndex">Index of the starting.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="points">The points.</param>
        /// <param name="direction">The direction.</param>
        /// <param name="lastIndex">The last index.</param>
        /// <returns>List&lt;Vector2&gt;.</returns>
        private static List<Vector2> FindSidePoints(int startingIndex, double offset, IList<Vector2> points, Vector2 direction, int lastIndex)
        {
            var sidePoints = new List<Vector2>();
            var index = startingIndex;
            do
            {
                sidePoints.Add(points[index++]);
                if (index > lastIndex) index = 0;
            } while (index != startingIndex && direction.Dot(points[index]).IsPracticallySame(offset, Constants.OBBTolerance));
            var lastAddedIndex = index == 0 ? lastIndex : index - 1; //because of the increment (3 lines up), the lastAddedIndex is one less than index
            index = startingIndex == 0 ? lastIndex : startingIndex - 1;
            while (index != lastAddedIndex && direction.Dot(points[index]).IsPracticallySame(offset, Constants.OBBTolerance))
            {
                sidePoints.Add(points[index--]);
                if (index < 0) index = lastIndex;
            }
            return sidePoints;
        }
        #endregion

        #region FindABB

        /// <summary>
        /// Finds the axis aligned bounding box.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="vertices">The vertices.</param>
        /// <returns>BoundingBox&lt;T&gt;.</returns>
        public static BoundingBox<T> FindAxisAlignedBoundingBox<T>(this IEnumerable<T> vertices) where T : IVector3D
        {
            var pointsOnBox = new List<T>[6];
            for (int i = 0; i < 6; i++)
                pointsOnBox[i] = new List<T>();
            var xMin = double.PositiveInfinity;
            var yMin = double.PositiveInfinity;
            var zMin = double.PositiveInfinity;
            var xMax = double.NegativeInfinity;
            var yMax = double.NegativeInfinity;
            var zMax = double.NegativeInfinity;
            foreach (var v in vertices)
            {
                UpdateLimitsAndBox(v, v.X, ref xMin, pointsOnBox[0], true);
                UpdateLimitsAndBox(v, v.X, ref xMax, pointsOnBox[1], false);
                UpdateLimitsAndBox(v, v.Y, ref yMin, pointsOnBox[2], true);
                UpdateLimitsAndBox(v, v.Y, ref yMax, pointsOnBox[3], false);
                UpdateLimitsAndBox(v, v.Z, ref zMin, pointsOnBox[4], true);
                UpdateLimitsAndBox(v, v.Z, ref zMax, pointsOnBox[5], false);
            }
            return new BoundingBox<T>(xMin, yMin, zMin, xMax, yMax, zMax, pointsOnBox);
        }


        /// <summary>
        /// Finds the axis aligned bounding box.
        /// </summary>
        /// <param name="solids">The solids.</param>
        /// <returns>BoundingBox.</returns>
        public static BoundingBox FindAxisAlignedBoundingBox(this IEnumerable<TessellatedSolid> solids)
        {
            var pointsOnBox = new List<Vertex>[6];
            for (int i = 0; i < 6; i++)
                pointsOnBox[i] = new List<Vertex>();
            var xMin = double.PositiveInfinity;
            var yMin = double.PositiveInfinity;
            var zMin = double.PositiveInfinity;
            var xMax = double.NegativeInfinity;
            var yMax = double.NegativeInfinity;
            var zMax = double.NegativeInfinity;
            foreach (var solid in solids)
            {
                foreach (var v in solid.ConvexHull.Vertices)
                {
                    UpdateLimitsAndBox(v, v.X, ref xMin, pointsOnBox[0], true);
                    UpdateLimitsAndBox(v, v.X, ref xMax, pointsOnBox[1], false);
                    UpdateLimitsAndBox(v, v.Y, ref yMin, pointsOnBox[2], true);
                    UpdateLimitsAndBox(v, v.Y, ref yMax, pointsOnBox[3], false);
                    UpdateLimitsAndBox(v, v.Z, ref zMin, pointsOnBox[4], true);
                    UpdateLimitsAndBox(v, v.Z, ref zMax, pointsOnBox[5], false);
                }
            }
            return new BoundingBox<Vertex>(xMin, yMin, zMin, xMax, yMax, zMax,
                pointsOnBox);
        }


        /// <summary>
        /// Updates the limits and box.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="vertex">The vertex.</param>
        /// <param name="value">The value.</param>
        /// <param name="limit">The limit.</param>
        /// <param name="pointsOnBox">The points on box.</param>
        /// <param name="isMinimum">if set to <c>true</c> [is minimum].</param>
        private static void UpdateLimitsAndBox<T>(T vertex, double value, ref double limit, List<T> pointsOnBox, bool isMinimum)
        {
            if ((isMinimum && value.IsLessThanNonNegligible(limit)) ||
            (!isMinimum && value.IsGreaterThanNonNegligible(limit)))
            {
                limit = value;
                pointsOnBox.Clear();
                pointsOnBox.Add(vertex);
            }
            else if (value.IsPracticallySame(limit))
                pointsOnBox.Add(vertex);
        }

        #endregion

        #region Find OBB Along Direction

        /// <summary>
        /// Finds the minimum oriented bounding rectangle (2D). The 3D points of a tessellated solid
        /// are projected to the plane defined by "Direction". This returns a BoundingBox structure
        /// where the first Direction is the same as the prescribed Direction and the other two are
        /// in-plane unit vectors.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="vertices">The vertices.</param>
        /// <param name="direction">The Direction.</param>
        /// <returns>BoundingBox.</returns>
        /// <exception cref="System.Exception">Volume should never be negligible, unless the input data is bad</exception>
        public static BoundingBox<T> FindOBBAlongDirection<T>(this IEnumerable<T> vertices, Vector3 direction) where T : IVector3D
        {
            var direction1 = direction.Normalize();
            var vertexList = vertices as IList<T> ?? vertices.ToList();
            var depth = GetLengthAndExtremeVertices(vertexList, direction1, out var bottomVertices, out var topVertices);

            var pointsDict = vertexList.ProjectTo2DCoordinatesReturnDictionary(direction1, out var backTransform);
            var boundingRectangle = RotatingCalipers2DMethod(pointsDict.Keys.ToList(), false, false, true);
            //Get the Direction vectors from rotating caliper and projection.

            var direction2 = new Vector3(boundingRectangle.Direction1, 0);
            direction2 = direction2.Multiply(backTransform).Normalize();
            var direction3 = direction1.Cross(direction2); // you could also get this from the bounding rectangle
                                                           // but this is quicker and more accurate to reproduce with cross-product 

            if ((depth * boundingRectangle.Length1 * boundingRectangle.Length2).IsNegligible())
            {
                //Console.WriteLine("Volume should never be negligible, unless the input data is bad");
                return null;
            }

            IEnumerable<T>[] verticesOnFaces = new IEnumerable<T>[6];
            verticesOnFaces[0] = bottomVertices;
            verticesOnFaces[1] = topVertices;
            verticesOnFaces[2] = boundingRectangle.PointsOnSides[0].SelectMany(p => pointsDict[p]);
            verticesOnFaces[3] = boundingRectangle.PointsOnSides[1].SelectMany(p => pointsDict[p]);
            //if (direction3.Dot(new Vector3(boundingRectangle.Direction2, 0).Transform(backTransform)) < 0)
            //{
            //    verticesOnFaces[4] = boundingRectangle.PointsOnSides[3].SelectMany(p => pointsDict[p]);
            //    verticesOnFaces[5] = boundingRectangle.PointsOnSides[2].SelectMany(p => pointsDict[p]);
            //}
            //else
            //{
            verticesOnFaces[4] = boundingRectangle.PointsOnSides[2].SelectMany(p => pointsDict[p]);
            verticesOnFaces[5] = boundingRectangle.PointsOnSides[3].SelectMany(p => pointsDict[p]);
            //}
            return new BoundingBox<T>([ depth*direction1, boundingRectangle.Length1*direction2,
                boundingRectangle.Length2*direction3 ], verticesOnFaces);
        }

        #endregion


    }
}
