using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using HeatonCAApp;
using NUnit.Framework;

namespace HeatonCA.Tests
{
    /// <summary>
    /// AppJson, the dependency-free JSON the app uses for its own files:
    /// nested objects and arrays round-trip, numbers are culture-invariant both
    /// ways, and strings escape everything JSON requires. Non-ASCII and control
    /// characters are spelled as C# escapes so the source stays plain ASCII.
    /// </summary>
    public class AppJsonTests
    {
        [Test]
        public void RoundTripsNestedObjectsAndArrays()
        {
            var inner = new Dictionary<string, object>
            {
                ["rule"] = "e542-5f79-9341-f31e-6c6b-7f08-8773-7068",
                ["score"] = 3.75,
                ["run"] = 12,
                ["kept"] = true,
                ["note"] = null,
            };
            var root = new Dictionary<string, object>
            {
                ["version"] = 1,
                ["finds"] = new List<object> { inner, new Dictionary<string, object>() },
                ["tags"] = new List<object> { "a", 2.5, false, null, new List<object>() },
            };

            string json = AppJson.Write(root);
            Dictionary<string, object> parsed = AppJson.Obj(AppJson.Parse(json));

            Assert.AreEqual(1, AppJson.Int(parsed["version"]));
            List<object> finds = AppJson.Arr(parsed["finds"]);
            Assert.AreEqual(2, finds.Count);
            Dictionary<string, object> first = AppJson.Obj(finds[0]);
            Assert.AreEqual("e542-5f79-9341-f31e-6c6b-7f08-8773-7068", AppJson.Str(first["rule"]));
            Assert.AreEqual(3.75, AppJson.Num(first["score"]));
            Assert.AreEqual(12, AppJson.Int(first["run"]));
            Assert.AreEqual(true, first["kept"]);
            Assert.IsNull(first["note"]);
            Assert.AreEqual(0, AppJson.Obj(finds[1]).Count);
            List<object> tags = AppJson.Arr(parsed["tags"]);
            Assert.AreEqual("a", tags[0]);
            Assert.AreEqual(2.5, tags[1]);
            Assert.AreEqual(false, tags[2]);
            Assert.IsNull(tags[3]);
            Assert.AreEqual(0, AppJson.Arr(tags[4]).Count);
        }

        [Test]
        public void WritesCompactJsonInKeyOrder()
        {
            var value = new Dictionary<string, object>
            {
                ["b"] = 1,
                ["a"] = new List<object> { true, null, "x" },
            };
            Assert.AreEqual("{\"b\":1,\"a\":[true,null,\"x\"]}", AppJson.Write(value));
            Assert.AreEqual("{}", AppJson.Write(new Dictionary<string, object>()));
            Assert.AreEqual("[]", AppJson.Write(new List<object>()));
            Assert.AreEqual("null", AppJson.Write(null));
        }

        [Test]
        public void NumbersAreCultureInvariantBothWays()
        {
            CultureInfo saved = Thread.CurrentThread.CurrentCulture;
            CultureInfo savedUi = Thread.CurrentThread.CurrentUICulture;
            try
            {
                // de-DE writes 1,5 for one and a half; JSON must still say 1.5.
                var german = new CultureInfo("de-DE");
                Thread.CurrentThread.CurrentCulture = german;
                Thread.CurrentThread.CurrentUICulture = german;

                Assert.AreEqual("1.5", AppJson.Write(1.5));
                Assert.AreEqual("-0.25", AppJson.Write(-0.25f));
                Assert.AreEqual("1234567", AppJson.Write(1234567));
                Assert.AreEqual("-9007199254740993", AppJson.Write(-9007199254740993L));
                Assert.AreEqual("4294967295", AppJson.Write(4294967295u));

                Assert.AreEqual(2.25, AppJson.Num(AppJson.Parse("2.25")));
                Assert.AreEqual(-3.5e10, AppJson.Num(AppJson.Parse("-3.5e10")));
                Assert.AreEqual(1e-7, AppJson.Num(AppJson.Parse("1E-07")));
                Assert.AreEqual(7, AppJson.Int(AppJson.Parse(" 7 ")));
                Assert.AreEqual(0.5, AppJson.Num(AppJson.Parse(AppJson.Write(0.5))));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = saved;
                Thread.CurrentThread.CurrentUICulture = savedUi;
            }
        }

        [Test]
        public void DoublesRoundTripExactly()
        {
            double[] values = { 0.1, 1.0 / 3.0, 3.5, 6.02214076e23, -1e-300, 0.0, 12345.678 };
            foreach (double value in values)
            {
                Assert.AreEqual(
                    value,
                    AppJson.Num(AppJson.Parse(AppJson.Write(value))),
                    value.ToString("R", CultureInfo.InvariantCulture));
            }
        }

        [Test]
        public void EscapesQuotesBackslashesAndControlCharacters()
        {
            // Quote, backslash, tab, newline, carriage return, BEL (U+0007), and
            // a Latin-1 letter (U+00E9), which must pass through unescaped.
            const string raw = "say \"hi\"\\ tab\t newline\n cr\r bell\u0007 e\u00e9";
            string json = AppJson.Write(raw);
            Assert.AreEqual(
                "\"say \\\"hi\\\"\\\\ tab\\t newline\\n cr\\r bell\\u0007 e\u00e9\"", json);
            Assert.AreEqual(raw, AppJson.Str(AppJson.Parse(json)));
        }

        [Test]
        public void ParsesStandardEscapes()
        {
            Assert.AreEqual("a/b", AppJson.Str(AppJson.Parse("\"a\\/b\"")));
            Assert.AreEqual(
                "\b\f\n\r\t\"\\", AppJson.Str(AppJson.Parse("\"\\b\\f\\n\\r\\t\\\"\\\\\"")));
            Assert.AreEqual("\u00e9\u20ac", AppJson.Str(AppJson.Parse("\"\\u00e9\\u20AC\"")));
            Assert.AreEqual("", AppJson.Str(AppJson.Parse("\"\"")));
        }

        [Test]
        public void EscapedKeysRoundTrip()
        {
            var value = new Dictionary<string, object> { ["quote\"key\n"] = "v" };
            string json = AppJson.Write(value);
            Assert.AreEqual("{\"quote\\\"key\\n\":\"v\"}", json);
            Assert.AreEqual("v", AppJson.Str(AppJson.Obj(AppJson.Parse(json))["quote\"key\n"]));
        }

        [Test]
        public void ParsesWhitespaceAndLiterals()
        {
            object parsed = AppJson.Parse(" \n{ \"a\" : [ 1 , true , false , null ] , \"b\" : { } }\t");
            Dictionary<string, object> obj = AppJson.Obj(parsed);
            List<object> list = AppJson.Arr(obj["a"]);
            Assert.AreEqual(1.0, list[0]);
            Assert.AreEqual(true, list[1]);
            Assert.AreEqual(false, list[2]);
            Assert.IsNull(list[3]);
            Assert.AreEqual(0, AppJson.Obj(obj["b"]).Count);
        }

        [Test]
        public void RejectsMalformedInput()
        {
            Assert.Throws<FormatException>(() => AppJson.Parse("{} extra"));
            Assert.Throws<FormatException>(() => AppJson.Parse("{\"a\" 1}"));
            Assert.Throws<FormatException>(() => AppJson.Parse("[1 2]"));
            Assert.Throws<FormatException>(() => AppJson.Parse("\"bad \\q escape\""));
            Assert.Throws<FormatException>(() => AppJson.Parse("nul"));
        }

        [Test]
        public void RejectsUnsupportedValues()
        {
            Assert.Throws<ArgumentException>(() => AppJson.Write(new object()));
            Assert.Throws<ArgumentException>(() => AppJson.Write(DateTime.UtcNow));
        }
    }
}
