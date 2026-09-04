using System.Collections.Generic;
using System.IO;
using HeatonCA.Engine;
using NUnit.Framework;

namespace HeatonCA.Tests
{
    /// <summary>
    /// mergelife-decode vectors: the rule-lab table rows (spec/mergelife.md
    /// "Decoded rule table"), bit-exact including the float64 percents.
    /// Ported from heaton-life-unity's EditMode suite; replays the vendored copies
    /// under Assets/Tests/Vectors~/mergelife-decode (red-world,
    /// promoted-and-negative, tied-limits).
    /// </summary>
    public class MergeLifeDecodeTests
    {
        public static IEnumerable<TestCaseData> Cases()
        {
            foreach (string dir in Directory.GetDirectories(VectorPaths.Vendored("mergelife-decode")))
                yield return new TestCaseData(Path.GetFileName(dir))
                    .SetName($"Decode({Path.GetFileName(dir)})");
        }

        [TestCaseSource(nameof(Cases))]
        public void Vector(string caseName)
        {
            string caseDir = VectorPaths.Vendored("mergelife-decode", caseName);
            var root = J.LoadCase(caseDir);
            Assert.AreEqual("bit-exact", J.Str(root["tier"]));

            var rows = MergeLife.DecodeRule(J.Str(root["rule"]));
            var expected = J.Arr(root["expected_rows"]);
            Assert.AreEqual(expected.Count, rows.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                var e = J.Obj(expected[i]);
                var row = rows[i];
                Assert.AreEqual(J.Int(e["limit"]), row.Limit, $"row {i} limit");
                Assert.AreEqual(J.Int(e["range_low"]), row.RangeLow, $"row {i} range_low");
                Assert.AreEqual(J.Int(e["range_high"]), row.RangeHigh, $"row {i} range_high");
                Assert.AreEqual(J.Num(e["percent"]), row.Percent, $"row {i} percent");
                Assert.AreEqual(J.Int(e["color_index"]), row.ColorIndex, $"row {i} color_index");
                Assert.AreEqual(J.Str(e["color_name"]), row.ColorName, $"row {i} color_name");
                Assert.AreEqual(J.Int(e["target_index"]), row.TargetIndex, $"row {i} target_index");
                Assert.AreEqual(J.Str(e["target_name"]), row.TargetName, $"row {i} target_name");
                var rgb = J.Arr(e["target_rgb"]);
                Assert.AreEqual(J.Int(rgb[0]), row.TargetR, $"row {i} target r");
                Assert.AreEqual(J.Int(rgb[1]), row.TargetG, $"row {i} target g");
                Assert.AreEqual(J.Int(rgb[2]), row.TargetB, $"row {i} target b");
                Assert.AreEqual(J.Int(e["range_byte"]), row.RangeByte, $"row {i} range_byte");
                Assert.AreEqual(J.Int(e["percent_byte"]), row.PercentByte, $"row {i} percent_byte");
            }
        }
    }
}
