﻿// ***********************************************************************
// Assembly         : TessellationAndVoxelizationGeometryLibrary
// Author           : matth
// Created          : 04-03-2023
//
// Last Modified By : matth
// Last Modified On : 04-13-2023
// ***********************************************************************
// <copyright file="3MFFileData.cs" company="Design Engineering Lab">
//     2014
// </copyright>
// <summary></summary>
// ***********************************************************************
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using TVGL.threemfclasses;

using Object = TVGL.threemfclasses.Object;

namespace TVGL
{
    /// <summary>
    /// Class ThreeMFFileData.
    /// </summary>
    [XmlRoot("model")]
#if help
    internal class ThreeMFFileData : IO
#else
    public class ThreeMFFileData : IO
#endif
    {
        /// <summary>
        /// The definition XML name space model
        /// </summary>
        private const string defXMLNameSpaceModel = "http://schemas.microsoft.com/3dmanufacturing/core/2015/02";

        /// <summary>
        /// The definition XML name space content types
        /// </summary>
        private const string defXMLNameSpaceContentTypes =
            "http://schemas.openxmlformats.org/package/2006/content-types";

        /// <summary>
        /// The definition XML name space relationships
        /// </summary>
        private const string defXMLNameSpaceRelationships =
            "http://schemas.openxmlformats.org/package/2006/relationships";


        /// <summary>
        /// Initializes a new instance of the <see cref="ThreeMFFileData" /> class.
        /// </summary>
        public ThreeMFFileData()
        {
            metadata = new List<Metadata>();
        }

        /// <summary>
        /// Gets or sets the metadata.
        /// </summary>
        /// <value>The metadata.</value>
        [XmlElement]
        public List<Metadata> metadata { get; set; }

        /// <summary>
        /// Gets or sets the resources.
        /// </summary>
        /// <value>The resources.</value>
        [XmlElement]
        public Resources resources { get; set; }

        /// <summary>
        /// Gets or sets the build.
        /// </summary>
        /// <value>The build.</value>
        [XmlElement]
        public Build build { get; set; }

        /// <summary>
        /// Gets the comments.
        /// </summary>
        /// <value>The comments.</value>
        internal new List<string> Comments
        {
            get
            {
                var result = metadata.Select(m => m.type + " ==> " + m.Value).ToList();
                result.AddRange(_comments);
                return result;
            }
        }

        /// <summary>
        /// Gets or sets the requiredextensions.
        /// </summary>
        /// <value>The requiredextensions.</value>
        public string requiredextensions { get; set; }


        /// <summary>
        /// Opens the solids.
        /// </summary>
        /// <param name="s">The s.</param>
        /// <param name="filename">The filename.</param>
        /// <returns>TessellatedSolid[].</returns>
        internal static TessellatedSolid[] OpenSolids(Stream s, string filename, TessellatedSolidBuildOptions tsBuildOptions)
        {
            var result = new List<TessellatedSolid>();
            var archive = new ZipArchive(s);
            foreach (var modelFile in archive.Entries.Where(f => f.FullName.EndsWith(".model")))
            {
                var modelStream = modelFile.Open();
                result.AddRange(OpenModelFile(modelStream, filename,tsBuildOptions));
            }
            return result.ToArray();
        }

        /// <summary>
        /// Opens the model file.
        /// </summary>
        /// <param name="s">The s.</param>
        /// <param name="filename">The filename.</param>
        /// <returns>TessellatedSolid[].</returns>
        internal static TessellatedSolid[] OpenModelFile(Stream s, string filename, TessellatedSolidBuildOptions tsBuildOptions)
        {
            var now = DateTime.Now;
            ThreeMFFileData threeMFData = null;
            //try
            //{
            var settings = new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                IgnoreWhitespace = true
            };
            using var reader = XmlReader.Create(s, settings);
            if (reader.IsStartElement("model"))
            {
                var defaultNamespace = reader["xmlns"];
                var serializer = new XmlSerializer(typeof(ThreeMFFileData), defaultNamespace);
                threeMFData = (ThreeMFFileData)serializer.Deserialize(reader);
            }
            threeMFData.FileName = filename;
            var results = new List<TessellatedSolid>();
            threeMFData.Name = Path.GetFileNameWithoutExtension(filename);
            var nameIndex =
                threeMFData.metadata.FindIndex(
                    md => md != null && (md.type.Equals("name", StringComparison.CurrentCultureIgnoreCase) ||
                                         md.type.Equals("title", StringComparison.CurrentCultureIgnoreCase)));
            if (nameIndex != -1)
            {
                threeMFData.Name = threeMFData.metadata[nameIndex].Value;
                threeMFData.metadata.RemoveAt(nameIndex);
            }
            foreach (var item in threeMFData.build.Items)
            {
                results.AddRange(threeMFData.TessellatedSolidsFromIDAndTransform(item.objectid,
                    item.transformMatrix, threeMFData.Name + "_", tsBuildOptions));
            }

            Message.output("Successfully read in 3Dmodel file (" + (DateTime.Now - now) + ").", 3);
            return results.ToArray();
            //}
            //catch (Exception exception)
            //{
            //    Message.output("Unable to read in 3Dmodel file.", 1);
            //    Message.output("Exception: " + exception.Message, 3);
            //    return null;
            //}
        }

        /// <summary>
        /// Tessellateds the solids from identifier and transform.
        /// </summary>
        /// <param name="objectid">The objectid.</param>
        /// <param name="transformMatrix">The transform matrix.</param>
        /// <param name="name">The name.</param>
        /// <returns>IEnumerable&lt;TessellatedSolid&gt;.</returns>
        private IEnumerable<TessellatedSolid> TessellatedSolidsFromIDAndTransform(int objectid,
            Matrix4x4 transformMatrix, string name, TessellatedSolidBuildOptions tsBuildOptions)
        {
            var solid = resources.objects.First(obj => obj.id == objectid);
            var result = TessellatedSolidsFromObject(solid, name, tsBuildOptions);
            if (!transformMatrix.IsNull())
                foreach (var ts in result)
                    ts.Transform(transformMatrix);
            return result;
        }

        /// <summary>
        /// Tessellateds the solids from object.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="name">The name.</param>
        /// <returns>List&lt;TessellatedSolid&gt;.</returns>
        private List<TessellatedSolid> TessellatedSolidsFromObject(Object obj, string name, TessellatedSolidBuildOptions tsBuildOptions)
        {
            name += obj.name + "_" + obj.id;
            var result = new List<TessellatedSolid>();
            if (obj.mesh != null) result.Add(TessellatedSolidFromMesh(obj.mesh, obj.MaterialID, name, tsBuildOptions));
            foreach (var comp in obj.components)
            {
                result.AddRange(TessellatedSolidsFromComponent(comp, name, tsBuildOptions));
            }
            return result;
        }

        /// <summary>
        /// Tessellateds the solids from component.
        /// </summary>
        /// <param name="comp">The comp.</param>
        /// <param name="name">The name.</param>
        /// <returns>IEnumerable&lt;TessellatedSolid&gt;.</returns>
        private IEnumerable<TessellatedSolid> TessellatedSolidsFromComponent(Component comp, string name, TessellatedSolidBuildOptions tsBuildOptions)
        {
            return TessellatedSolidsFromIDAndTransform(comp.objectid, comp.transformMatrix, name, tsBuildOptions);
        }

        /// <summary>
        /// Tessellateds the solid from mesh.
        /// </summary>
        /// <param name="mesh">The mesh.</param>
        /// <param name="materialID">The material identifier.</param>
        /// <param name="name">The name.</param>
        /// <returns>TessellatedSolid.</returns>
        private TessellatedSolid TessellatedSolidFromMesh(Mesh mesh, int materialID, string name, TessellatedSolidBuildOptions tsBuildOptions)
        {
            var defaultColor = new Color(Constants.DefaultColor);
            if (materialID >= 0)
            {
                var material = resources.materials.FirstOrDefault(mat => mat.id == materialID);
                if (material != null)
                {
                    var defaultColorXml =
                        resources.colors.FirstOrDefault(col => col.id == material.colorid);
                    if (defaultColorXml != null) defaultColor = defaultColorXml.color;
                }
            }
            var verts = mesh.vertices.Select(v => new Vector3(v.x, v.y, v.z)).ToList();

            Color[] colors = null;
            var uniformColor = true;
            var numTriangles = mesh.triangles.Count;
            for (var j = 0; j < numTriangles; j++)
            {
                var triangle = mesh.triangles[j];
                if (triangle.pid == -1) continue;
                if (triangle.p1 == -1) continue;
                var baseMaterial =
                    resources.basematerials.FirstOrDefault(bm => bm.id == triangle.pid);
                if (baseMaterial == null) continue;
                var baseColor = baseMaterial.bases[triangle.p1];
                if (j == 0)
                {
                    defaultColor = baseColor.color;
                    continue;
                }
                if (uniformColor && baseColor.color.Equals(defaultColor)) continue;
                uniformColor = false;
                colors ??= new Color[mesh.triangles.Count];
                colors[j] = baseColor.color;
            }
            if (uniformColor) colors = new[] { defaultColor };
            else
                for (var j = 0; j < numTriangles; j++)
                    colors[j] ??= defaultColor;
            return new TessellatedSolid(verts, mesh.triangles.Select(t => (t.v1, t.v2, t.v3 )).ToList(),
                 colors, tsBuildOptions, Units, name, FileName, Comments, Language);
        }

        /// <summary>
        /// Saves the specified stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="solids">The solids.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        internal static bool Save(Stream stream, IList<TessellatedSolid> solids)
        {
            Stream entryStream;
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
            var entry = archive.CreateEntry("3D/3dmodel.model");
            using (entryStream = entry.Open())
                SaveModel(entryStream, solids);
            archive.CreateEntry("Metadata/thumbnail.png");
            entry = archive.CreateEntry("_rels/.rels");
            using (entryStream = entry.Open())
                SaveRelationships(entryStream);
            entry = archive.CreateEntry("[Content_Types].xml");
            using (entryStream = entry.Open())
                SaveContentTypes(entryStream);
            return true;
        }

        /// <summary>
        /// Saves the specified stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="solids">The solids.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        internal static bool SaveModel(Stream stream, IList<TessellatedSolid> solids)
        {
            var objects = new List<Object>();
            var baseMats = new BaseMaterials { id = 1 };
            for (var i = 0; i < solids.Count; i++)
            {
                var solid = solids[i];
                var thisObject = new Object { name = solid.Name, id = i + 2 };
                // this is "+ 2" since the id's start with 1 instead of 0 plus BaseMaterials is typically 1, so start at 2.
                var triangles = new List<Triangle>();

                foreach (var face in solid.Faces)
                {
                    var colString = (face.Color ?? solid.SolidColor ?? new Color(Constants.DefaultColor)).ToString();
                    var colorIndex = baseMats.bases.FindIndex(col => col.colorString.Equals(colString));
                    if (colorIndex == -1)
                    {
                        colorIndex = baseMats.bases.Count;
                        baseMats.bases.Add(new Base { colorString = colString });
                    }
                    triangles.Add(new Triangle
                    {
                        v1 = face.A.IndexInList,
                        v2 = face.B.IndexInList,
                        v3 = face.C.IndexInList,
                        pid = 1,
                        p1 = colorIndex
                    });
                }
                thisObject.mesh = new Mesh
                {
                    vertices = solid.Vertices.Select(v => new threemfclasses.Vertex
                    { x = v.X, y = v.Y, z = v.Z }).ToList(),
                    triangles = triangles
                };
                objects.Add(thisObject);
            }

            var metaData = new List<Metadata>();
            var allRawComments = solids.SelectMany(s => s.Comments);
            var comments = new List<string>();
            foreach (var comment in allRawComments.Where(string.IsNullOrWhiteSpace))
            {
                var arrowIndex = comment.IndexOf("==>");
                if (arrowIndex == -1) comments.Add(comment);
                else
                {
                    var endOfType = arrowIndex - 1;
                    var beginOfValue = arrowIndex + 3; //todo: check this -1 and +3
                    metaData.Add(new Metadata
                    {
                        type = comment.Substring(0, endOfType),
                        Value = comment.Substring(beginOfValue)
                    });
                }
            }
            var threeMFData = new ThreeMFFileData
            {
                Units = solids[0].Units,
                Name = solids[0].Name.Split('_')[0],
                Language = solids[0].Language,
                metadata = metaData,
                build = new Build { Items = objects.Select(o => new Item { objectid = o.id }).ToList() },
                resources =
                    new Resources
                    {
                        basematerials = new[] { baseMats }.ToList(), //colors = colors, materials = materials,
                        objects = objects
                    }
            };
            threeMFData.Comments.AddRange(comments);
            try
            {
                using (var writer = XmlWriter.Create(stream))
                {
                    writer.WriteComment(TvglDateMarkText);
                    if (!string.IsNullOrWhiteSpace(solids[0].FileName))
                        writer.WriteComment("Originally loaded from " + solids[0].FileName);
                    var serializer = new XmlSerializer(typeof(ThreeMFFileData), defXMLNameSpaceModel);
                    serializer.Serialize(writer, threeMFData);
                }
                Message.output("Successfully wrote 3MF file to stream.", 3);
                return true;
            }
            catch (Exception exception)
            {
                Message.output("Unable to write in model file.", 1);
                Message.output("Exception: " + exception.Message, 3);
                return false;
            }
        }

        /// <summary>
        /// Saves the relationships.
        /// </summary>
        /// <param name="stream">The stream.</param>
        private static void SaveRelationships(Stream stream)
        {
            //[XmlArrayItem("vertex", IsNullable = false)]
            var rels = new[]
                {
                    new Relationship
                    {
                        Target = "/3D/3dmodel.model",
                        Id = "rel-1",
                        Type = "http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel"
                    },
                    new Relationship
                    {
                        Target = "/Metadata/thumbnail.png",
                        Id = "rel0",
                        Type = "http://schemas.openxmlformats.org/package/2006/relationships/metadata/thumbnail"
                    }
                };

            using var writer = XmlWriter.Create(stream);
            var serializer = new XmlSerializer(typeof(Relationship), defXMLNameSpaceRelationships);
            serializer.Serialize(writer, rels[0]);
        }

        /// <summary>
        /// Saves the content types.
        /// </summary>
        /// <param name="stream">The stream.</param>
        private static void SaveContentTypes(Stream stream)
        {
            var defaults = new List<Default>
            {
                new Default
                {
                    Extension = "rels",
                    ContentType = "application/vnd.openxmlformats-package.relationships+xml"
                },
                new Default
                {
                    Extension = "model",
                    ContentType = "application/vnd.ms-package.3dmanufacturing-3dmodel+xml"
                },
                new Default {Extension = "png", ContentType = "image/png"}
            };
            var types = new Types { Defaults = defaults };

            using var writer = XmlWriter.Create(stream);
            var serializer = new XmlSerializer(typeof(Types), defXMLNameSpaceContentTypes);
            serializer.Serialize(writer, types);
        }
    }
}