using System.Collections.Generic;
using HeatonCA.Engine;
using UnityEngine;

namespace HeatonCAApp
{
    /// <summary>
    /// Still thumbnails for the evolver's finds, the way HeatonCA 1.x wrote them:
    /// a 100x100 lattice seeded from soup and stepped 250 generations, which is far
    /// enough in that a rule's character — the drifting blobs, the crystal, the
    /// noise it decays to — is unmistakable at card size.
    ///
    /// Rendering is deliberately rationed. A finds page can show sixty cards, and
    /// sixty quarter-second renders on one frame is a visible freeze, so
    /// <see cref="Get"/> hands back what is cached and queues what is not, and
    /// <see cref="Tick"/> renders at most one new rule per frame; the page fills in
    /// over a second or so instead of stalling. Every thumbnail is a pure function
    /// of the rule (fixed size, fixed seed, fixed step count), so a cached texture
    /// is never stale and the PNG export matches the card exactly.
    /// </summary>
    public sealed class FindsThumbnailer
    {
        /// <summary>Thumbnail edge in cells.</summary>
        public const int ThumbSize = 100;

        /// <summary>Generations to run before the still is taken.</summary>
        public const int ThumbSteps = 250;

        /// <summary>Soup seed every thumbnail uses, so a rule always looks the same.</summary>
        public const ulong ThumbSeed = 0;

        private readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();
        private readonly Queue<string> _pending = new Queue<string>();
        private readonly HashSet<string> _queued = new HashSet<string>();

        /// <summary>
        /// This rule's thumbnail, or null while it is still queued — call again next
        /// frame. The texture belongs to this thumbnailer; do not destroy it.
        /// Textures come out upright (row 0 at the bottom, as Unity wants), so a
        /// plain <c>RawImage</c> shows them the right way up with the default uvRect.
        /// </summary>
        public Texture2D Get(string rule)
        {
            if (string.IsNullOrEmpty(rule))
                return null;
            if (_cache.TryGetValue(rule, out Texture2D texture))
                return texture;
            if (_queued.Add(rule))
                _pending.Enqueue(rule);
            return null;
        }

        /// <summary>Render at most one queued thumbnail. Call once per frame.</summary>
        public void Tick()
        {
            if (_pending.Count == 0)
                return;
            string rule = _pending.Dequeue();
            _queued.Remove(rule);
            if (_cache.ContainsKey(rule))
                return;
            _cache[rule] = CreateTexture(RenderRgb(rule), ThumbSize, 1);
        }

        /// <summary>
        /// This rule's thumbnail as PNG bytes, nearest-neighbor scaled by
        /// <paramref name="scale"/> so a saved still is legible at a glance. Rows
        /// come out top-down, matching the lattice: PNG row 0 is lattice row 0.
        /// </summary>
        public byte[] EncodePng(string rule, int scale)
        {
            if (string.IsNullOrEmpty(rule))
                return null;
            int factor = Mathf.Max(1, scale);
            Texture2D texture = CreateTexture(RenderRgb(rule), ThumbSize, factor);
            byte[] png = ImageConversion.EncodeToPNG(texture);
            DestroyTexture(texture);
            return png;
        }

        /// <summary>
        /// The thumbnail itself: a fresh lattice at this rule, soup-seeded from
        /// <see cref="ThumbSeed"/> and stepped <see cref="ThumbSteps"/> generations,
        /// as row-major RGB bytes. Pure and engine-only — no Unity objects, so a
        /// worker or a test can call it.
        /// </summary>
        public static byte[] RenderRgb(string rule)
        {
            var sim = new MergeLife(rule, ThumbSize, ThumbSize);
            sim.SeedSoup(ThumbSeed);
            sim.Step(ThumbSteps);
            return sim.Frame();
        }

        /// <summary>Release every cached texture. The thumbnailer is reusable afterwards.</summary>
        public void Dispose()
        {
            foreach (KeyValuePair<string, Texture2D> entry in _cache)
                DestroyTexture(entry.Value);
            _cache.Clear();
            _pending.Clear();
            _queued.Clear();
        }

        /// <summary>An upright RGBA32 point-filtered texture holding the scaled lattice.</summary>
        private static Texture2D CreateTexture(byte[] rgb, int size, int scale)
        {
            int edge = size * scale;
            var texture = new Texture2D(edge, edge, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.LoadRawTextureData(ScaledRgba(rgb, size, scale));
            texture.Apply(false);
            return texture;
        }

        /// <summary>
        /// Nearest-neighbor upscale of row-major RGB into the bottom-up RGBA a
        /// Texture2D stores. The row flip is what makes both the on-screen card and
        /// the encoded PNG show lattice row 0 at the top.
        /// </summary>
        private static byte[] ScaledRgba(byte[] rgb, int size, int scale)
        {
            int edge = size * scale;
            var rgba = new byte[edge * edge * 4];
            for (int y = 0; y < edge; y++)
            {
                int sourceRow = ((edge - 1 - y) / scale) * size * 3;
                int targetRow = y * edge * 4;
                for (int x = 0; x < edge; x++)
                {
                    int source = sourceRow + (x / scale) * 3;
                    int target = targetRow + x * 4;
                    rgba[target] = rgb[source];
                    rgba[target + 1] = rgb[source + 1];
                    rgba[target + 2] = rgb[source + 2];
                    rgba[target + 3] = 255;
                }
            }
            return rgba;
        }

        /// <summary>
        /// Destroy a runtime texture from either mode: the EditMode suite builds and
        /// disposes thumbnailers outside play mode, where Destroy is an error.
        /// </summary>
        private static void DestroyTexture(Texture2D texture)
        {
            if (texture == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(texture);
            else
                Object.DestroyImmediate(texture);
        }
    }
}
