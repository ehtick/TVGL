﻿/******************************************************************************
 *
 * The MIT License (MIT)
 *
 * MIConvexHull, Copyright (c) 2015 David Sehnal, Matthew Campbell
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 *  
 *****************************************************************************/

using System.Runtime.CompilerServices;

namespace TVGL
{
    /// <summary>
    /// An interface for a structure with nD position.
    /// </summary>
    public interface IVector
    {
        double this[int i] { get; }
        bool IsNull();
        static IVector Null { get; }
    }

    public interface IVector2D : IVector
    {
        /// <summary>
        /// Gets the x.
        /// </summary>
        /// <value>The x.</value>
        double X { get; init; }

        /// <summary>
        /// Gets the y.
        /// </summary>
        /// <value>The y.</value>
        double Y { get; init; }
    }
    public interface IVector3D : IVector2D
    {
        /// <summary>
        /// Gets the z.
        /// </summary>
        /// <value>The z.</value>
        //double Z { get; init; }
        double Z { get; init; }
    }

    /// <summary>
    /// "Default" vertex.
    /// </summary>
    /// <seealso cref="MIConvexHull.IPoint" />
    public class DefaultPoint : IVector
    {
        public double this[int i]
        {
            get { return Coordinates[i]; }
            set { Coordinates[i] = value; }
        }
        /// <summary>
        /// Coordinates of the vertex.
        /// </summary>
        /// <value>The position.</value>
        public double[] Coordinates { get; set; }

        public bool IsNull()
        {
            return Coordinates == null;
        }

        static DefaultPoint Null => new DefaultPoint { Coordinates = null };

    }
}