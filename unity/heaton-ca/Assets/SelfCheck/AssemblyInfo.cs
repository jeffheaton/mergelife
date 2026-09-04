using System.Runtime.CompilerServices;

// The EditMode test assembly may reach this assembly's internals: the pinned
// digest and objective constants, which SelfCheckTests recompute from the
// vendored vector files and compare against the values embedded here.
[assembly: InternalsVisibleTo("HeatonCA.EditorTests")]
