using System;
using HeatonCA.Engine;
using UnityEngine;
using UnityEngine.UI;

namespace HeatonCAApp
{
    /// <summary>
    /// The only place frames become pixels: <see cref="IRgbFrameSource.WriteFrame"/>
    /// into a reused RGB buffer, widened to RGBA32 with an opaque alpha, then
    /// <c>Texture2D.SetPixelData</c>. Buffers and the texture are reused across
    /// frames; the texture is point-filtered so cells stay hard-edged like the
    /// PyQt canvas.
    ///
    /// MergeLife is an RGB family, so this carries only that path — there is no
    /// colormap and no indexed or float frame source in HeatonCA.
    ///
    /// Unity textures are bottom-up while frames are top-down. The fix lives in
    /// the display, never in the pixel math: <see cref="Attach"/> gives the
    /// RawImage <c>uvRect (0, 1, 1, -1)</c>, and the PNG encoders reverse rows
    /// on their way out so a saved file is upright.
    /// </summary>
    public sealed class FrameBlitter : IDisposable
    {
        /// <summary>The flip that makes a bottom-up texture display a top-down frame.</summary>
        public static readonly Rect FlipUvRect = new Rect(0f, 1f, 1f, -1f);

        private byte[] _rgba;
        private byte[] _rgbFrame;
        private RawImage _image;

        /// <summary>The live frame texture, or null before the first blit.</summary>
        public Texture2D Texture { get; private set; }

        /// <summary>
        /// Point the given RawImage at this blitter's texture and give it the
        /// top-down flip. Safe to call before the first blit: the texture is
        /// assigned again whenever it is (re)created.
        /// </summary>
        public void Attach(RawImage image)
        {
            _image = image;
            if (_image == null)
            {
                return;
            }
            _image.uvRect = FlipUvRect;
            _image.texture = Texture;
        }

        /// <summary>
        /// Pull the current frame from <paramref name="source"/> and upload it.
        /// The texture is resized (recreated) when the lattice changed shape.
        /// </summary>
        public void Blit(IRgbFrameSource source)
        {
            if (source == null)
            {
                return;
            }
            int width = source.Width;
            int height = source.Height;
            if (width <= 0 || height <= 0)
            {
                return;
            }
            EnsureTexture(width, height);
            source.WriteFrame(_rgbFrame);
            RgbToRgba(_rgbFrame, _rgba);
            Texture.SetPixelData(_rgba, 0);
            Texture.Apply(false);
        }

        /// <summary>
        /// The last blitted frame as a PNG, top-down (what the user sees). The
        /// stored rows are already top-down, so they are reversed into a scratch
        /// texture whose bottom-up memory then encodes upright. Null before the
        /// first blit.
        /// </summary>
        public byte[] EncodePng()
        {
            if (Texture == null || _rgba == null)
            {
                return null;
            }
            return EncodeRgbaTopDown(_rgba, Texture.width, Texture.height);
        }

        /// <summary>
        /// A loose RGB frame as a PNG, magnified <paramref name="scale"/>x with
        /// nearest-neighbor sampling (hard cells, never a blur). Used for the
        /// Evolve finds page, whose thumbnails are rendered off-screen and never
        /// blitted. Scales below 1 are treated as 1.
        /// </summary>
        public static byte[] EncodeRgbAsPng(byte[] rgb, int width, int height, int scale)
        {
            if (rgb == null || width <= 0 || height <= 0)
            {
                return null;
            }
            if (rgb.Length < width * height * 3)
            {
                throw new ArgumentException(
                    $"rgb holds {rgb.Length} bytes, short of the {width * height * 3} a {width}x{height} frame needs",
                    nameof(rgb));
            }
            if (scale < 1)
            {
                scale = 1;
            }
            int outWidth = width * scale;
            int outHeight = height * scale;
            var rgba = new byte[outWidth * outHeight * 4];
            for (int y = 0; y < outHeight; y++)
            {
                int sourceRow = (y / scale) * width * 3;
                int destRow = y * outWidth * 4;
                for (int x = 0; x < outWidth; x++)
                {
                    int s = sourceRow + (x / scale) * 3;
                    int d = destRow + x * 4;
                    rgba[d] = rgb[s];
                    rgba[d + 1] = rgb[s + 1];
                    rgba[d + 2] = rgb[s + 2];
                    rgba[d + 3] = 255;
                }
            }
            return EncodeRgbaTopDown(rgba, outWidth, outHeight);
        }

        /// <summary>Release the frame texture; the blitter can be used again afterwards.</summary>
        public void Dispose()
        {
            if (_image != null)
            {
                _image.texture = null;
            }
            DestroyTexture(Texture);
            Texture = null;
            _rgba = null;
            _rgbFrame = null;
        }

        /// <summary>
        /// RGB bytes -> RGBA32 with an opaque alpha. Straight-line and
        /// allocation-free: this runs once per displayed frame.
        /// </summary>
        private static void RgbToRgba(byte[] rgb, byte[] rgba)
        {
            int cells = rgba.Length / 4;
            for (int i = 0, s = 0, d = 0; i < cells; i++, s += 3, d += 4)
            {
                rgba[d] = rgb[s];
                rgba[d + 1] = rgb[s + 1];
                rgba[d + 2] = rgb[s + 2];
                rgba[d + 3] = 255;
            }
        }

        /// <summary>
        /// Encode top-down RGBA32 rows as a PNG. Texture memory is bottom-up, so
        /// the rows are reversed on the way into the scratch texture; the file
        /// then reads upright in any viewer.
        /// </summary>
        private static byte[] EncodeRgbaTopDown(byte[] rgba, int width, int height)
        {
            var flipped = new byte[rgba.Length];
            int stride = width * 4;
            for (int y = 0; y < height; y++)
            {
                Buffer.BlockCopy(rgba, y * stride, flipped, (height - 1 - y) * stride, stride);
            }
            var temp = new Texture2D(width, height, TextureFormat.RGBA32, false);
            temp.SetPixelData(flipped, 0);
            temp.Apply(false);
            byte[] png = ImageConversion.EncodeToPNG(temp);
            DestroyTexture(temp);
            return png;
        }

        private void EnsureTexture(int width, int height)
        {
            int cells = width * height;
            if (_rgbFrame == null || _rgbFrame.Length != cells * 3)
            {
                _rgbFrame = new byte[cells * 3];
            }
            if (Texture != null && Texture.width == width && Texture.height == height)
            {
                return;
            }
            DestroyTexture(Texture);
            Texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            _rgba = new byte[cells * 4];
            if (_image != null)
            {
                _image.texture = Texture;
            }
        }

        /// <summary>
        /// Destroy a texture from either edit mode or play mode. EditMode tests
        /// and editor tooling run outside play mode, where Object.Destroy is a
        /// no-op that logs an error.
        /// </summary>
        private static void DestroyTexture(Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(texture);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
