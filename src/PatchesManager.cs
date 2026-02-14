using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SilksongDoorstop.Patches;

namespace SilksongDoorstop;

internal class PatchesManager
{
    private List<Patch> _patches;

    public PatchesManager(ModuleDefinition _targetModule, ModuleDefinition _sourceModule)
    {
        _patches = new List<Patch> {
            new OnGUIPatch(_targetModule, _sourceModule),
        };
    }

    public void ApplyPatches()
    {
        foreach (Patch patch in _patches)
        {
            patch.ApplyPatch();
        }
    }
}
