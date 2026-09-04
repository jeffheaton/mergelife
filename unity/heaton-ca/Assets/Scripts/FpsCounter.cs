namespace HeatonCAApp
{
    /// <summary>
    /// Frames rendered in the last wall-clock second — the number the
    /// Simulator's "Steps: n, FPS: n" overlay shows.
    ///
    /// A literal count over a sliding one-second window, not a smoothed
    /// reciprocal of the frame time: PyQt counted frames against a one-second
    /// timer, and a plain count is the reading people expect to see settle on 60
    /// and stay there. Timestamps live in a fixed ring, so the counter never
    /// allocates while the app runs.
    /// </summary>
    public sealed class FpsCounter
    {
        /// <summary>Width of the sliding window, in seconds.</summary>
        public const double WindowSeconds = 1.0;

        /// <summary>
        /// Ring capacity. Frames beyond this many inside one window are still
        /// counted correctly up to the cap; 1024 covers any real display, and
        /// anything faster reads as 1024 rather than growing a buffer.
        /// </summary>
        public const int Capacity = 1024;

        private readonly double[] _times = new double[Capacity];
        private int _head; // index of the oldest kept timestamp
        private int _count;

        /// <summary>Frames counted in the last <see cref="WindowSeconds"/>.</summary>
        public int Fps { get; private set; }

        /// <summary>
        /// Record a rendered frame at <paramref name="nowSeconds"/> (any
        /// monotonic clock; the app passes <c>Time.realtimeSinceStartupAsDouble</c>)
        /// and refresh <see cref="Fps"/>.
        /// </summary>
        public void Frame(double nowSeconds)
        {
            if (_count == Capacity)
            {
                // Full: drop the oldest to make room, so the newest frame is
                // never the one lost.
                _head = (_head + 1) % Capacity;
                _count--;
            }
            _times[(_head + _count) % Capacity] = nowSeconds;
            _count++;
            Expire(nowSeconds);
            Fps = _count;
        }

        /// <summary>
        /// Refresh <see cref="Fps"/> at <paramref name="nowSeconds"/> without
        /// recording a frame, so a stalled renderer decays to 0 instead of
        /// holding its last reading.
        /// </summary>
        public void Sample(double nowSeconds)
        {
            Expire(nowSeconds);
            Fps = _count;
        }

        /// <summary>Forget every recorded frame (a screen the user just came back to).</summary>
        public void Reset()
        {
            _head = 0;
            _count = 0;
            Fps = 0;
        }

        /// <summary>Drop timestamps that have fallen out of the window.</summary>
        private void Expire(double nowSeconds)
        {
            double cutoff = nowSeconds - WindowSeconds;
            while (_count > 0 && _times[_head] <= cutoff)
            {
                _head = (_head + 1) % Capacity;
                _count--;
            }
        }
    }
}
