using System;
using HeatonCA.Engine;

namespace HeatonCAApp
{
    /// <summary>
    /// UI-free engine service: owns the running simulation and its transport
    /// state. Ticked by AppController; never references any view.
    ///
    /// Stepping is time-budgeted — <see cref="StepsPerSecond"/> simulated steps
    /// against real time, capped per tick so a large lattice degrades to slower
    /// playback rather than a frozen frame. The cap is a cell budget rather than
    /// a flat step count: MergeLife's cost is linear in cells, so
    /// <see cref="CellBudgetPerTick"/> cells is roughly one frame of work
    /// whatever grid the screen happens to produce.
    ///
    /// PyQt drove one generation per QTimer tick at int(1000/fps); the
    /// accumulator here decouples simulation rate from frame rate, so 60 gen/s
    /// is still 60 gen/s on a 30 Hz display.
    /// </summary>
    public sealed class SimulationHost
    {
        /// <summary>Slowest playback the Settings screen allows (gen/s).</summary>
        public const double MinStepsPerSecond = 1;

        /// <summary>Fastest playback the Settings screen allows (gen/s).</summary>
        public const double MaxStepsPerSecond = 60;

        /// <summary>Playback rate a fresh host starts at, matching the Settings default.</summary>
        public const double DefaultStepsPerSecond = 30;

        /// <summary>
        /// Cells a single <see cref="Tick"/> may advance before the backlog is
        /// dropped. 1,500,000 is about six 512x512 steps — enough that ordinary
        /// grids never clip, small enough that a huge lattice cannot stall a
        /// frame chasing the accumulator.
        /// </summary>
        public const int CellBudgetPerTick = 1500000;

        /// <summary>Hard ceiling on steps per tick, whatever the cell budget allows.</summary>
        public const int MaxStepsPerTick = 12;

        private double _stepsPerSecond = DefaultStepsPerSecond;
        private double _accumulator;

        /// <summary>The loaded world, or null when nothing is loaded.</summary>
        public ISimulation Sim { get; private set; }

        /// <summary>Whether <see cref="Tick"/> advances the world.</summary>
        public bool Playing { get; set; }

        /// <summary>
        /// Playback rate in generations per second, clamped to
        /// [<see cref="MinStepsPerSecond"/>, <see cref="MaxStepsPerSecond"/>].
        /// A NaN assignment falls back to <see cref="DefaultStepsPerSecond"/>
        /// rather than poisoning the accumulator.
        /// </summary>
        public double StepsPerSecond
        {
            get => _stepsPerSecond;
            set => _stepsPerSecond = double.IsNaN(value)
                ? DefaultStepsPerSecond
                : Math.Min(MaxStepsPerSecond, Math.Max(MinStepsPerSecond, value));
        }

        /// <summary>Set when the state changed and the frame needs a re-blit; cleared by the consumer.</summary>
        public bool Dirty { get; set; }

        /// <summary>Generation of the loaded world, or 0 when nothing is loaded.</summary>
        public int Generation => Sim != null ? Sim.Generation : 0;

        /// <summary>
        /// Steps this host will run in one <see cref="Tick"/> at most:
        /// the cell budget divided by the lattice size, clamped to
        /// [1, <see cref="MaxStepsPerTick"/>]. Always at least one step, so
        /// even a lattice larger than the whole budget still advances.
        /// </summary>
        public int StepsPerTickCap
        {
            get
            {
                if (Sim == null)
                {
                    return 1;
                }
                long cells = (long)Sim.Width * Sim.Height;
                if (cells < 1)
                {
                    cells = 1;
                }
                long cap = CellBudgetPerTick / cells;
                return (int)Math.Clamp(cap, 1L, MaxStepsPerTick);
            }
        }

        /// <summary>
        /// Take ownership of a world. Playback starts unless
        /// <paramref name="play"/> is false; the accumulator resets so the new
        /// world never inherits the old one's backlog.
        /// </summary>
        public void Load(ISimulation sim, bool play = true)
        {
            Sim = sim;
            Playing = play && sim != null;
            _accumulator = 0;
            Dirty = sim != null;
        }

        /// <summary>Drop the world and stop the transport.</summary>
        public void Unload()
        {
            Sim = null;
            Playing = false;
            _accumulator = 0;
            Dirty = false;
        }

        /// <summary>
        /// Advance real time by <paramref name="deltaSeconds"/>, running whole
        /// steps as the accumulator earns them. When more steps are owed than
        /// <see cref="StepsPerTickCap"/> allows, the backlog is dropped instead
        /// of carried: playback runs slower than requested, but the frame never
        /// freezes catching up.
        /// </summary>
        public void Tick(double deltaSeconds)
        {
            if (!Playing || Sim == null || !(deltaSeconds > 0))
            {
                return;
            }
            _accumulator += deltaSeconds * _stepsPerSecond;
            int steps = (int)_accumulator;
            if (steps <= 0)
            {
                return;
            }
            int cap = StepsPerTickCap;
            if (steps > cap)
            {
                steps = cap;
                _accumulator = 0; // drop the backlog: play slower, never freeze
            }
            else
            {
                _accumulator -= steps;
            }
            Sim.Step(steps);
            Dirty = true;
        }

        /// <summary>Advance exactly one generation, whatever the transport state (the Step button).</summary>
        public void StepOnce()
        {
            if (Sim == null)
            {
                return;
            }
            Sim.Step();
            Dirty = true;
        }
    }
}
