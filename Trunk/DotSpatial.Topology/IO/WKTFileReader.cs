using System;
using System.Collections.Generic;
using System.IO;
using DotSpatial.Topology.Geometries;
using DotSpatial.Topology.Utilities.RToolsUtil;

namespace DotSpatial.Topology.IO
{
    ///<summary>
    /// Reads a sequence of <see cref="IGeometry"/>s in WKT format from a text file.
    ///</summary>
    /// <remarks>The geometries in the file may be separated by any amount of whitespace and newlines.</remarks>
    /// <author>
    /// Martin Davis
    /// </author>
    public class WktFileReader
    {
        private const int MaxLookahead = 2048;
        private readonly FileInfo _file;
        private TextReader _reader;
        private readonly WktReader _wktReader;
        private int _count;

        private WktFileReader(WktReader wktReader)
        {
            _wktReader = wktReader;
            Limit = -1;
        }

        ///<summary>
        /// Creates a new <see cref="WktFileReader" /> given the <paramref name="file" /> to read from and a <see cref="WktReader" /> to use to parse the geometries.
        ///</summary>
        /// <param name="file"> the <see cref="FileInfo" /> to read from</param>
        /// <param name="wktReader">the geometry reader to use</param>
        public WktFileReader(FileInfo file, WktReader wktReader) : this(wktReader) { _file = file; }

        ///<summary>
        /// Creates a new <see cref="WktFileReader" />, given the name of the file to read from.
        ///</summary>
        /// <param name="filename">The name of the file to read from</param>
        /// <param name="wktReader">The geometry reader to use</param>
        public WktFileReader(String filename, WktReader wktReader) : this(new FileInfo(filename), wktReader) { }

        ///<summary>
        /// Creates a new <see cref="WktFileReader" />, given a <see cref="TextReader"/> to read with.
        ///</summary>
        /// <param name="reader">The stream reader of the file to read from</param>
        /// <param name="wktReader">The geometry reader to use</param>
        public WktFileReader(TextReader reader, WktReader wktReader)
            : this(wktReader)
        {
            _reader = reader;
        }

        ///<summary>
        /// Gets/Sets the maximum number of geometries to read.
        ///</summary>
        public int Limit { get; set; }

        ///<summary>
        /// Gets/Sets the number of geometries to skip before reading.
        ///</summary>
        public int Offset { get; set; }

        ///<summary>
        /// Reads a sequence of geometries.
        ///</summary>
        /// <remarks>
        /// <para>
        /// If an offset is specified, geometries read up to the offset count are skipped.</para>
        /// <para>If a limit is specified, no more than <see cref="Limit" /> geometries are read.</para>
        /// </remarks>
        /// <returns>The list of geometries read</returns>
        public IList<IGeometry> Read()
        {
            _count = 0;

            if (_file != null)
                _reader = new StreamReader(new BufferedStream(_file.OpenRead(), MaxLookahead));
            try
            {
                return Read(_reader);
            }
            finally
            {
                _reader.Close();
            }
        }

        private IList<IGeometry> Read(TextReader bufferedReader)
        {
            IList<IGeometry> geoms = new List<IGeometry>();
            var tokens = _wktReader.Tokenizer(bufferedReader);
            tokens.MoveNext();
            while (!IsAtEndOfTokens(tokens.Current) && !IsAtLimit(geoms))
            {
                var g = _wktReader.ReadGeometryTaggedText(tokens);
                if (_count >= Offset)
                    geoms.Add(g);
                _count++;
            }
            return geoms;
        }

        private bool IsAtLimit(IList<IGeometry> geoms)
        {
            if (Limit < 0) return false;
            if (geoms.Count < Limit) return false;
            return true;
        }

        private static bool IsAtEndOfTokens(Token token)
        {
            return token is EofToken;
        }

        ///<summary>
        /// Tests if reader is at EOF.
        ///</summary>
        private bool IsAtEndOfFile(StreamReader bufferedReader)
        {
            return bufferedReader.EndOfStream;
        }
    }
}