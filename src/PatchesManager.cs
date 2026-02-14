using Mono.Cecil;
using Mono.Cecil.Cil;
using SilksongDoorstop.Patches;

namespace SilksongDoorstop;

internal class PatchesManager
{
    private OnGUIPatch _onGui;

    public PatchesManager(ModuleDefinition _targetModule, ModuleDefinition _sourceModule)
    {
        _onGui = new(_targetModule, _sourceModule);
    }

    public void ApplyPatches()
    {
        _onGui.ApplyPatch();
    }
}
