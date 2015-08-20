﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using DotSpatial.Topology.Geometries;

namespace DotSpatial.Topology.IO.KML
{
    /// <summary>
    /// Writes a formatted string containing the KML representation 
    /// of a JTS <see cref="IGeometry"/>. 
    /// The output is KML fragments which can be substituted
    /// wherever the KML <see cref="IGeometry"/> abstract 
    /// element can be used.
    /// </summary>
    /// <remarks>
    /// Output elements are indented to provide a
    /// nicely-formatted representation. 
    /// An output line prefix and maximum
    /// number of coordinates per line can be specified.
    /// </remarks>
    /// <remarks>
    /// The Z ordinate value output can be forced to be a specific value. 
    /// The <see cref="Extrude"/> and <see cref="AltitudeMode"/> modes can be set. 
    /// If set, the corresponding sub-elements will be output.
    /// </remarks>
    public class KmlWriter
    {
        #region Constant Fields

        /// <summary>
        /// The KML standard value <c>absolute</c> for use in <see cref="AltitudeMode"/>.
        /// </summary>
        public const string AltitudeModeAbsolute = "absolute";

        /// <summary>
        /// The KML standard value <c>clampToGround</c> for use in <see cref="AltitudeMode"/>.
        /// </summary>
        public const string AltitudeModeClampToGround = "clampToGround ";

        /// <summary>
        /// The KML standard value <c>relativeToGround</c> for use in <see cref="AltitudeMode"/>.
        /// </summary>
        public const string AltitudeModeRelativeToGround = "relativeToGround  ";

        private const string CoordinateSeparator = ",";
        private const int IndentSize = 2;
        private const string TupleSeparator = " ";

        #endregion

        #region Fields

        private string _format;
        private NumberFormatInfo _formatter;
        private int _maxCoordinatesPerLine = 5;

        #endregion

        #region Constructors

        public KmlWriter()
        {
            Z = Double.NaN;
            CreateDefaultFormatter();
        }

        #endregion

        #region Properties

        /// <summary>
        /// The value output in the <c>altitudeMode</c> element.
        /// </summary>
        public string AltitudeMode { get; set; }

        /// <summary>
        /// The flag to be output in the <c>extrude</c> element.
        /// </summary>
        public bool Extrude { get; set; }

        /// <summary>
        /// A tag string which is prefixed to every emitted text line.
        /// This can be used to indent the geometry text in a containing document.
        /// </summary>
        public string LinePrefix { get; set; }

        /// <summary>
        /// The maximum number of coordinates to output per line.
        /// </summary>
        public int MaxCoordinatesPerLine
        {
            get { return _maxCoordinatesPerLine; }
            set { _maxCoordinatesPerLine = Math.Max(1, value); }
        }

        /// <summary>
        /// The maximum number of decimal places to output in ordinate values.
        /// Useful for limiting output size.
        /// </summary>
        /// <remarks>
        /// negative values set the precision to <see cref="PrecisionModels.Floating"/>,
        /// like standard behavior.
        /// </remarks>
        public int Precision
        {
            get { return _formatter.NumberDecimalDigits; }
            set { CreateFormatter(value); }
        }

        /// <summary>
        /// The flag to be output in the <c>tesselate</c> element.
        /// </summary>
        public bool Tesselate { get; set; }

        /// <summary>
        /// The Z value to be output for all coordinates.
        /// This overrides any Z value present in the Geometry coordinates.
        /// </summary>
        public double Z { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Writes a <see cref="IGeometry"/> in KML format as a string.
        /// </summary>
        /// <param name="geom">the geometry to write</param>
        /// <returns>a string containing the KML geometry representation</returns>
        public string Write(IGeometry geom)
        {
            StringBuilder sb = new StringBuilder();
            Write(geom, sb);
            return sb.ToString();
        }

        /// <summary>
        /// Writes the KML representation of a <see cref="IGeometry"/> to a <see cref="TextWriter"/>.
        /// </summary>
        /// <param name="geom">the geometry to write</param>
        /// <param name="writer">the writer to write to</param>
        public void Write(IGeometry geom, TextWriter writer)
        {
            string kml = Write(geom);
            writer.Write(kml);
        }

        /// <summary>
        /// Appends the KML representation of a <see cref="IGeometry"/> to a <see cref="StringBuilder"/>.
        /// </summary>
        /// <param name="geom">the geometry to write</param>
        /// <param name="sb">the buffer to write into</param>
        public void Write(IGeometry geom, StringBuilder sb)
        {
            WriteGeometry(geom, 0, sb);
        }

        /// <summary>
        /// Writes a Geometry as KML to a string, using
        /// a specified Z value.
        /// </summary>
        /// <param name="geometry">the geometry to write</param>
        /// <param name="z">the Z value to use</param>
        /// <returns>a string containing the KML geometry representation</returns>
        public static string WriteGeometry(IGeometry geometry, double z)
        {
            KmlWriter writer = new KmlWriter { Z = z };
            return writer.Write(geometry);
        }

        /// <summary>
        /// Writes a Geometry as KML to a string, using a specified Z value, precision, extrude flag,
        /// and altitude mode code.
        /// </summary>
        /// <param name="geometry">the geometry to write</param>
        /// <param name="z">the Z value to use</param>
        /// <param name="precision">the maximum number of decimal places to write</param>
        /// <param name="extrude">the extrude flag to write</param>
        /// <param name="altitudeMode">the altitude model code to write</param>
        /// <returns>a string containing the KML geometry representation</returns>
        public static string WriteGeometry(IGeometry geometry, double z, int precision,
            bool extrude, string altitudeMode)
        {
            KmlWriter writer = new KmlWriter
            {
                Z = z,
                Precision = precision,
                Extrude = extrude,
                AltitudeMode = altitudeMode
            };
            return writer.Write(geometry);
        }

        private void CreateDefaultFormatter()
        {
            CreateFormatter(-1);
        }

        private void CreateFormatter(int precision)
        {
            IPrecisionModel precisionModel = precision < 0
                ? new PrecisionModel(PrecisionModelType.Floating)
                : new PrecisionModel(precision);
            _formatter = WktWriter.CreateFormatter(precisionModel);
            string digits = WktWriter.StringOfChar('#', _formatter.NumberDecimalDigits);
            _format = String.Format("0.{0}", digits);
        }

        private string GeometryTag(string geometryName)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<");
            sb.Append(geometryName);
            sb.Append(">");
            return sb.ToString();
        }

        private void StartLine(string text, int level, StringBuilder sb)
        {
            if (LinePrefix != null)
                sb.Append(LinePrefix);
            sb.Append(WktWriter.StringOfChar(' ', IndentSize * level));
            sb.Append(text);
        }

        /// <summary>
        /// Takes a list of coordinates and converts it to KML.
        /// </summary>
        /// <remarks>
        /// 2D and 3D aware. Terminates the coordinate output with a newline.
        /// </remarks>
        private void Write(IList<Coordinate> coords, int level, StringBuilder sb)
        {
            StartLine("<coordinates>", level, sb);

            bool isNewLine = false;
            for (int i = 0; i < coords.Count; i++)
            {
                if (i > 0)
                    sb.Append(TupleSeparator);

                if (isNewLine)
                {
                    StartLine("  ", level, sb);
                    isNewLine = false;
                }

                Write(coords[i], sb);

                // break output lines to prevent them from getting too long
                if ((i + 1) % MaxCoordinatesPerLine == 0 && i < coords.Count - 1)
                {
                    sb.Append("\n");
                    isNewLine = true;
                }
            }
            sb.Append("</coordinates>\n");
        }

        private void Write(Coordinate p, StringBuilder sb)
        {
            Write(p.X, sb);
            sb.Append(CoordinateSeparator);
            Write(p.Y, sb);

            double z = p.Z;
            // if altitude was specified directly, use it
            if (!Double.IsNaN(Z))
                z = Z;

            // only write if Z present
            // MD - is this right? Or should it always be written?
            if (!Double.IsNaN(z))
            {
                sb.Append(CoordinateSeparator);
                Write(z, sb);
            }
        }

        private void Write(double d, StringBuilder sb)
        {
            string tos = d.ToString(_format, _formatter);
            sb.Append(tos);
        }

        private void WriteGeometry(IGeometry g, int level, StringBuilder sb)
        {
            if (g is IPoint)
                WritePoint(g as IPoint, level, sb);
            else if (g is ILinearRing)
                WriteLinearRing(g as ILinearRing, level, sb, true);
            else if (g is ILineString)
                WriteLineString(g as ILineString, level, sb);
            else if (g is IPolygon)
                WritePolygon(g as IPolygon, level, sb);
            else if (g is IGeometryCollection)
                WriteGeometryCollection(g as IGeometryCollection, level, sb);
            else throw new ArgumentException(string.Format("Geometry type not supported: {0}", g.GeometryType));
        }

        private void WriteGeometryCollection(IGeometryCollection gc, int level,
            StringBuilder sb)
        {
            StartLine("<MultiGeometry>\n", level, sb);
            for (int t = 0; t < gc.NumGeometries; t++)
                WriteGeometry(gc.GetGeometryN(t), level + 1, sb);
            StartLine("</MultiGeometry>\n", level, sb);
        }

        private void WriteLinearRing(ILinearRing lr, int level,
            StringBuilder sb, bool writeModifiers)
        {
            // <LinearRing><coordinates>...</coordinates></LinearRing>
            StartLine(GeometryTag("LinearRing") + "\n", level, sb);
            if (writeModifiers)
                WriteModifiers(level, sb);
            Write(lr.Coordinates, level + 1, sb);
            StartLine("</LinearRing>\n", level, sb);
        }

        private void WriteLineString(ILineString ls, int level,
            StringBuilder sb)
        {
            // <LineString><coordinates>...</coordinates></LineString>
            StartLine(GeometryTag("LineString") + "\n", level, sb);
            WriteModifiers(level, sb);
            Write(ls.Coordinates, level + 1, sb);
            StartLine("</LineString>\n", level, sb);
        }

        private void WriteModifiers(int level, StringBuilder sb)
        {
            if (Extrude)
                StartLine("<extrude>1</extrude>\n", level, sb);
            if (Tesselate)
                StartLine("<tesselate>1</tesselate>\n", level, sb);
            if (AltitudeMode != null)
            {
                string s = String.Format("<altitudeMode>{0}</altitudeMode>\n", AltitudeMode);
                StartLine(s, level, sb);
            }
        }

        private void WritePoint(IPoint p, int level,
            StringBuilder sb)
        {
            // <Point><coordinates>...</coordinates></Point>
            StartLine(GeometryTag("Point") + "\n", level, sb);
            WriteModifiers(level, sb);
            Write(new[] { p.Coordinate }, level + 1, sb);
            StartLine("</Point>\n", level, sb);
        }

        private void WritePolygon(IPolygon p, int level,
            StringBuilder sb)
        {
            StartLine(GeometryTag("Polygon") + "\n", level, sb);
            WriteModifiers(level, sb);

            StartLine("  <outerBoundaryIs>\n", level, sb);
            WriteLinearRing((ILinearRing)p.Shell, level + 1, sb, false);
            StartLine("  </outerBoundaryIs>\n", level, sb);

            for (int t = 0; t < p.NumHoles; t++)
            {
                StartLine("  <innerBoundaryIs>\n", level, sb);
                WriteLinearRing((ILinearRing)p.GetInteriorRingN(t), level + 1, sb, false);
                StartLine("  </innerBoundaryIs>\n", level, sb);
            }

            StartLine("</Polygon>\n", level, sb);
        }

        #endregion
    }
}
