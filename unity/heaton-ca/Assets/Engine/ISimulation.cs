// HeatonCA.Engine: copied verbatim from HeatonLife.Core (github.com/jeffheaton/heaton-life, dotnet/src/HeatonLife.Core/ISimulation.cs, commit 46a117baf33a0f916d39e2c05991604ae8a1b424).
// Copyright Jeff Heaton. Licensed under the Apache License, Version 2.0; see THIRD_PARTY_NOTICES.md.
// Modifications: namespace HeatonLife -> HeatonCA.Engine.
// Do not edit: re-run tools/import-engine.sh to sync.

namespace HeatonCA.Engine
{
    /// <summary>
    /// A time-stepped system: CA, Lenia, reaction-diffusion, boids — the C# side of
    /// core/protocols.py. Frames are always renderable Height*Width buffers in one of
    /// three shapes, each with its own sub-interface: palette-index bytes (colormapped
    /// via Colormaps.ApplyIndexed*), floats in [0, 1] (Colormaps.ApplyFloat*), or raw
    /// RGB bytes (passed through). A host like the Unity adapter drives everything
    /// through these five members plus the family's Seed*/SetState methods.
    /// </summary>
    public interface ISimulation
    {
        int Width { get; }

        int Height { get; }

        int Generation { get; }

        void Step(int n = 1);

        /// <summary>
        /// Re-run this world's initialization, returning Generation to 0. With no
        /// seed the world replays its stored seeding parameters exactly; with one,
        /// it replays the same strategy under the new seed. A world last loaded
        /// from an explicit grid replays that grid and ignores the seed — the
        /// reference's "array" init behaves the same way.
        ///
        /// The counterpart of the Python protocol's <c>reset(seed=None)</c>
        /// (python/src/heaton_life/core/protocols.py). Without it a host could not
        /// re-seed a world through the interface and had to switch on the concrete
        /// family, which is exactly what callers were doing.
        /// </summary>
        void Reset(uint? seed = null);
    }

    /// <summary>Frame = Height*Width palette indices (0..255), spec/render.md.</summary>
    public interface IIndexedFrameSource : ISimulation
    {
        /// <summary>Write the current frame into <paramref name="frame"/> (Height*Width bytes).</summary>
        void WriteFrame(byte[] frame);
    }

    /// <summary>Frame = Height*Width floats in [0, 1], spec/render.md.</summary>
    public interface IFloatFrameSource : ISimulation
    {
        /// <summary>Write the current frame into <paramref name="frame"/> (Height*Width doubles).</summary>
        void WriteFrame(double[] frame);
    }

    /// <summary>Frame = Height*Width*3 RGB bytes, passed through uncolormapped.</summary>
    public interface IRgbFrameSource : ISimulation
    {
        /// <summary>Write the current frame into <paramref name="rgb"/> (Height*Width*3 bytes).</summary>
        void WriteFrame(byte[] rgb);
    }
}
