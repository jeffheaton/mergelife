// Headless compile gate. Unity only reaches an -executeMethod target after
// every script in the project compiled, so if this method runs at all the
// build is clean; tools/unity-gate.sh greps the log for the token it prints.
//
//   Unity -batchmode -quit -projectPath unity/heaton-ca \
//         -executeMethod HeatonCA.Editor.CompileGate.Run

using UnityEngine;

namespace HeatonCA.Editor
{
    /// <summary>
    /// Entry point for the compile gate: does nothing except prove that the
    /// editor assemblies compiled and log the token the gate script expects.
    /// </summary>
    public static class CompileGate
    {
        /// <summary>Logs <c>COMPILE OK</c>; reaching this line is the whole test.</summary>
        public static void Run()
        {
            Debug.Log("COMPILE OK");
        }
    }
}
