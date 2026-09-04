using System.Runtime.CompilerServices;

// The one file in Assets/Engine that is not copied from HeatonLife.Core (see
// PROVENANCE.md); tools/engine-sync-check.sh knows it by name. The EditMode
// test assembly reaches this assembly's internals for the convergence pins
// that drive the internal RunEvaluator directly.
[assembly: InternalsVisibleTo("HeatonCA.EditorTests")]
