using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TVGL;
using TVGL.Boolean_Operations;
using TVGL.IOFunctions;
using TVGL.Numerics;
using TVGLUnitTestsAndBenchmarking;

namespace TVGLUnitTestsAndBenchmarking
{
    internal class Program
    {

        static readonly Stopwatch stopwatch = new Stopwatch();

        [STAThread]
        private static void Main(string[] args)
        {
            var tVGL3Dto2DTests = new TVGL3Dto2DTests();
            tVGL3Dto2DTests.BoxSilhouette();
        }
    }
}