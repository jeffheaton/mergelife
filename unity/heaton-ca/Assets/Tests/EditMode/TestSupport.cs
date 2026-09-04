using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace HeatonCA.Tests
{
    /// <summary>
    /// Locates the data the EditMode suite replays. Two sources: this repository's own
    /// cross-engine contract, conformance/vectors.txt (three directories above Assets/,
    /// because the Unity project lives at unity/heaton-ca inside the mergelife checkout;
    /// HEATONCA_CONFORMANCE_VECTORS names the file explicitly for other layouts), and the
    /// heaton-life vectors vendored under Assets/Tests/Vectors~ (the trailing tilde keeps
    /// Unity from importing them, so they ship with the tests and never with a player).
    /// </summary>
    internal static class VectorPaths
    {
        /// <summary>Environment variable that names the conformance vectors file explicitly.</summary>
        internal const string ConformanceEnvVar = "HEATONCA_CONFORMANCE_VECTORS";

        /// <summary>
        /// Path of conformance/vectors.txt: HEATONCA_CONFORMANCE_VECTORS when set, otherwise
        /// the repository copy relative to this project. Throws, naming the override, when
        /// the resolved file does not exist.
        /// </summary>
        internal static string RepoConformanceVectors()
        {
            string fallback = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", "..", "..", "conformance", "vectors.txt"));
            return ResolveConformanceVectors(Environment.GetEnvironmentVariable(ConformanceEnvVar), fallback);
        }

        /// <summary>
        /// The resolution rule behind <see cref="RepoConformanceVectors"/>, split out so a test
        /// can drive it with paths of its choosing: a non-empty <paramref name="configured"/>
        /// wins, otherwise <paramref name="fallback"/>; either way a missing file fails loudly
        /// with a message that names the environment override.
        /// </summary>
        internal static string ResolveConformanceVectors(string configured, string fallback)
        {
            if (!string.IsNullOrEmpty(configured))
            {
                if (!File.Exists(configured))
                {
                    throw new FileNotFoundException(
                        $"{ConformanceEnvVar} points at a missing file: {configured}", configured);
                }
                return configured;
            }
            if (!File.Exists(fallback))
            {
                throw new FileNotFoundException(
                    $"could not find the conformance vectors at {fallback}: run the tests from the " +
                    $"mergelife checkout (unity/heaton-ca inside the repo) or set {ConformanceEnvVar} " +
                    "to the vectors.txt file", fallback);
            }
            return fallback;
        }

        /// <summary>Assets/Tests/Vectors~, the vendored heaton-life vectors (see its README.md).</summary>
        internal static string VendoredRoot() => Path.Combine(Application.dataPath, "Tests", "Vectors~");

        /// <summary>A path under <see cref="VendoredRoot"/>, e.g. Vendored("evolve", "mini-run-24", "params.json").</summary>
        internal static string Vendored(params string[] parts)
        {
            string path = VendoredRoot();
            foreach (string part in parts)
                path = Path.Combine(path, part);
            return path;
        }

        /// <summary>
        /// Reads a .f64 vector file: consecutive little-endian IEEE-754 doubles, as the
        /// heaton-life generators write them (numpy float64 tofile). Byte-swapped on a
        /// big-endian host so the values, not the bytes, are what the tests compare.
        /// </summary>
        internal static double[] ReadF64(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length % 8 != 0)
                throw new InvalidDataException($"{path}: {bytes.Length} bytes is not a whole number of doubles");
            if (!BitConverter.IsLittleEndian)
            {
                for (int i = 0; i < bytes.Length; i += 8)
                    Array.Reverse(bytes, i, 8);
            }
            var values = new double[bytes.Length / 8];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
    }

    /// <summary>
    /// Minimal JSON reader for the vectors' params.json files — objects become
    /// Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;, numbers double.
    /// Deliberately dependency-free: reading the vectors should not hinge on a
    /// JSON library (Unity has no System.Text.Json).
    /// </summary>
    internal static class MiniJson
    {
        public static object Parse(string text)
        {
            int pos = 0;
            object value = ParseValue(text, ref pos);
            SkipWhitespace(text, ref pos);
            if (pos != text.Length)
                throw new InvalidDataException($"trailing JSON content at {pos}");
            return value;
        }

        private static object ParseValue(string text, ref int pos)
        {
            SkipWhitespace(text, ref pos);
            char c = text[pos];
            switch (c)
            {
                case '{': return ParseObject(text, ref pos);
                case '[': return ParseArray(text, ref pos);
                case '"': return ParseString(text, ref pos);
                case 't': Expect(text, ref pos, "true"); return true;
                case 'f': Expect(text, ref pos, "false"); return false;
                case 'n': Expect(text, ref pos, "null"); return null;
                default: return ParseNumber(text, ref pos);
            }
        }

        private static Dictionary<string, object> ParseObject(string text, ref int pos)
        {
            var result = new Dictionary<string, object>();
            pos++; // {
            SkipWhitespace(text, ref pos);
            if (text[pos] == '}')
            {
                pos++;
                return result;
            }
            while (true)
            {
                SkipWhitespace(text, ref pos);
                string key = ParseString(text, ref pos);
                SkipWhitespace(text, ref pos);
                if (text[pos++] != ':')
                    throw new InvalidDataException($"expected ':' at {pos - 1}");
                result[key] = ParseValue(text, ref pos);
                SkipWhitespace(text, ref pos);
                char next = text[pos++];
                if (next == '}')
                    return result;
                if (next != ',')
                    throw new InvalidDataException($"expected ',' or '}}' at {pos - 1}");
            }
        }

        private static List<object> ParseArray(string text, ref int pos)
        {
            var result = new List<object>();
            pos++; // [
            SkipWhitespace(text, ref pos);
            if (text[pos] == ']')
            {
                pos++;
                return result;
            }
            while (true)
            {
                result.Add(ParseValue(text, ref pos));
                SkipWhitespace(text, ref pos);
                char next = text[pos++];
                if (next == ']')
                    return result;
                if (next != ',')
                    throw new InvalidDataException($"expected ',' or ']' at {pos - 1}");
            }
        }

        private static string ParseString(string text, ref int pos)
        {
            if (text[pos++] != '"')
                throw new InvalidDataException($"expected '\"' at {pos - 1}");
            var sb = new StringBuilder();
            while (true)
            {
                char c = text[pos++];
                if (c == '"')
                    return sb.ToString();
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                char esc = text[pos++];
                switch (esc)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        sb.Append((char)int.Parse(
                            text.Substring(pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        pos += 4;
                        break;
                    default:
                        throw new InvalidDataException($"unknown escape '\\{esc}'");
                }
            }
        }

        private static double ParseNumber(string text, ref int pos)
        {
            int start = pos;
            while (pos < text.Length && (char.IsDigit(text[pos]) || "+-.eE".IndexOf(text[pos]) >= 0))
                pos++;
            return double.Parse(text.Substring(start, pos - start), CultureInfo.InvariantCulture);
        }

        private static void Expect(string text, ref int pos, string literal)
        {
            if (string.CompareOrdinal(text, pos, literal, 0, literal.Length) != 0)
                throw new InvalidDataException($"expected '{literal}' at {pos}");
            pos += literal.Length;
        }

        private static void SkipWhitespace(string text, ref int pos)
        {
            while (pos < text.Length && char.IsWhiteSpace(text[pos]))
                pos++;
        }
    }

    /// <summary>Typed navigation over MiniJson's object graph.</summary>
    internal static class J
    {
        public static Dictionary<string, object> Obj(object o) => (Dictionary<string, object>)o;

        public static List<object> Arr(object o) => (List<object>)o;

        public static string Str(object o) => (string)o;

        public static double Num(object o) => (double)o;

        public static int Int(object o) => (int)(double)o;

        public static Dictionary<string, object> LoadCase(string caseDir) =>
            Obj(MiniJson.Parse(File.ReadAllText(Path.Combine(caseDir, "params.json"))));
    }

    /// <summary>
    /// Minimal reader for the conformance vectors' PNGs: 8-bit grayscale or RGB,
    /// non-interlaced. Unity's BCL has no ZLibStream, so the zlib wrapper around
    /// the IDAT deflate stream is peeled by hand (2-byte header; the trailing
    /// adler32 is simply left unread).
    /// </summary>
    internal static class PngReader
    {
        public static (int Width, int Height, int Channels, byte[] Pixels) Read(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            if (data.Length < 8 || data[0] != 0x89 || data[1] != (byte)'P')
                throw new InvalidDataException("not a PNG");

            int width = 0, height = 0, bitDepth = 0, colorType = -1;
            using var idat = new MemoryStream();
            int pos = 8;
            while (pos + 8 <= data.Length)
            {
                int length = ReadBigEndian(data, pos);
                string type = Encoding.ASCII.GetString(data, pos + 4, 4);
                int body = pos + 8;
                if (type == "IHDR")
                {
                    width = ReadBigEndian(data, body);
                    height = ReadBigEndian(data, body + 4);
                    bitDepth = data[body + 8];
                    colorType = data[body + 9];
                    if (data[body + 12] != 0)
                        throw new InvalidDataException("interlaced PNGs unsupported");
                }
                else if (type == "IDAT")
                {
                    idat.Write(data, body, length);
                }
                else if (type == "IEND")
                {
                    break;
                }
                pos = body + length + 4; // skip CRC
            }
            int channels = colorType switch
            {
                0 => 1,
                2 => 3,
                _ => throw new InvalidDataException($"vector PNGs are 8-bit gray/RGB; got color type {colorType}"),
            };
            if (bitDepth != 8)
                throw new InvalidDataException($"vector PNGs are 8-bit; got depth={bitDepth}");

            byte[] compressed = idat.ToArray();
            using var raw = new MemoryStream();
            using (var deflate = new DeflateStream(
                new MemoryStream(compressed, 2, compressed.Length - 2), CompressionMode.Decompress))
            {
                deflate.CopyTo(raw);
            }
            byte[] scanlines = raw.ToArray();

            int stride = width * channels;
            var pixels = new byte[stride * height];
            var previous = new byte[stride];
            for (int y = 0; y < height; y++)
            {
                int offset = y * (stride + 1);
                byte filter = scanlines[offset];
                var row = new byte[stride];
                for (int x = 0; x < stride; x++)
                {
                    byte value = scanlines[offset + 1 + x];
                    byte left = x >= channels ? row[x - channels] : (byte)0;
                    byte up = previous[x];
                    byte upLeft = x >= channels ? previous[x - channels] : (byte)0;
                    row[x] = filter switch
                    {
                        0 => value,
                        1 => (byte)(value + left),
                        2 => (byte)(value + up),
                        3 => (byte)(value + (left + up) / 2),
                        4 => (byte)(value + Paeth(left, up, upLeft)),
                        _ => throw new InvalidDataException($"unknown PNG filter {filter}"),
                    };
                }
                Array.Copy(row, 0, pixels, y * stride, stride);
                previous = row;
            }
            return (width, height, channels, pixels);
        }

        private static byte Paeth(byte a, byte b, byte c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            if (pa <= pb && pa <= pc) return a;
            return pb <= pc ? b : c;
        }

        private static int ReadBigEndian(byte[] data, int offset) =>
            (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
    }
}
