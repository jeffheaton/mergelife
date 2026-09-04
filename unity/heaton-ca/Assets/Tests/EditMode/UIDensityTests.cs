using HeatonCAApp;
using NUnit.Framework;

namespace HeatonCA.Tests
{
    /// <summary>
    /// The UIBuilder density model, pure math only (the parts that never touch
    /// Screen or Application): mobile canvases target a constant physical
    /// density of 6.75 units/mm, capped at the designed 1080-unit tablet canvas,
    /// and the phone gate is "smallest physical side under 90 mm".
    /// </summary>
    public class UIDensityTests
    {
        [Test]
        public void MobileUnitsPerMmMakesTouchTargetsSevenMillimeters()
        {
            // A 48-unit touch target is ~7.1 mm on any phone, in any orientation.
            Assert.AreEqual(6.75f, UIBuilder.MobileUnitsPerMm, 1e-6f);
            Assert.AreEqual(7.1f, 48f / UIBuilder.MobileUnitsPerMm, 0.05f);
        }

        [Test]
        public void MobileReferenceWidthTargetsConstantPhysicalDensity()
        {
            // iPhone 15 Pro portrait: 1179 px @ 460 dpi = 65.1 mm wide -> ~439 units.
            Assert.AreEqual(439f, UIBuilder.MobileReferenceWidth(1179f, 460f), 3f);
            // Same phone landscape: 141.1 mm -> ~953 units; density unchanged.
            Assert.AreEqual(953f, UIBuilder.MobileReferenceWidth(2556f, 460f), 3f);
            // A 6.1" Android phone: 1080 px @ 421 dpi = 65.2 mm -> ~440 units.
            Assert.AreEqual(440f, UIBuilder.MobileReferenceWidth(1080f, 421f), 3f);
        }

        [Test]
        public void MobileReferenceWidthCapsAtTabletCanvas()
        {
            // iPad Pro 12.9" portrait: 2048 px @ 264 dpi = 197 mm computes past
            // the cap -> the designed 1080 canvas, so tablets keep their layouts.
            Assert.AreEqual(1080f, UIBuilder.MobileReferenceWidth(2048f, 264f), 0.01f);
            // iPad 11" portrait (1668 px @ 264 dpi = 160.5 mm) too.
            Assert.AreEqual(1080f, UIBuilder.MobileReferenceWidth(1668f, 264f), 0.01f);
            // Never above the cap, however wide the screen.
            Assert.LessOrEqual(UIBuilder.MobileReferenceWidth(10000f, 100f), 1080f);
        }

        [Test]
        public void MobileReferenceWidthFallsBackWhenDpiUnknown()
        {
            // Unreported dpi (0, or the 1 some emulators return) keeps the
            // historical tablet canvas rather than dividing by nothing.
            Assert.AreEqual(1080f, UIBuilder.MobileReferenceWidth(1170f, 0f), 0.01f);
            Assert.AreEqual(1080f, UIBuilder.MobileReferenceWidth(1170f, 1f), 0.01f);
        }

        [Test]
        public void PhoneSizedIsTrueForPhonesInEitherOrientation()
        {
            Assert.IsTrue(UIBuilder.PhoneSized(1179f, 2556f, 460f), "iPhone 15 Pro portrait");
            Assert.IsTrue(UIBuilder.PhoneSized(2556f, 1179f, 460f), "iPhone 15 Pro landscape");
            Assert.IsTrue(UIBuilder.PhoneSized(1080f, 2340f, 421f), "mid-range Android phone");
        }

        [Test]
        public void PhoneSizedIsFalseForTabletsAndUnknownDpi()
        {
            Assert.IsFalse(UIBuilder.PhoneSized(2048f, 2732f, 264f), "iPad Pro 12.9");
            Assert.IsFalse(UIBuilder.PhoneSized(2732f, 2048f, 264f), "iPad Pro 12.9 landscape");
            Assert.IsFalse(UIBuilder.PhoneSized(1488f, 2266f, 326f), "iPad mini");
            Assert.IsFalse(UIBuilder.PhoneSized(1179f, 2556f, 0f), "dpi unreported -> tablet");
        }

        [Test]
        public void BarTitleDropsTheGroupOnPhones()
        {
            Assert.AreEqual(
                "Cellular Automata: Life-like",
                UIBuilder.BarTitle("Cellular Automata", "Life-like", false));
            Assert.AreEqual("Life-like", UIBuilder.BarTitle("Cellular Automata", "Life-like", true));
            Assert.AreEqual("Life-like", UIBuilder.BarTitle("", "Life-like", false));
            Assert.AreEqual("Life-like", UIBuilder.BarTitle(null, "Life-like", false));
        }

        [Test]
        public void SlugIsLowercaseHyphenated()
        {
            Assert.AreEqual("burning-ship", UIBuilder.Slug("Burning Ship"));
            Assert.AreEqual("e542-5f79", UIBuilder.Slug("E542-5F79"));
            Assert.AreEqual("red-world", UIBuilder.Slug("  Red   World! "));
            Assert.AreEqual("", UIBuilder.Slug("---"));
        }
    }
}
