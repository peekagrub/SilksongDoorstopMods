using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SilksongDoorstop;

internal abstract class Patch
{
    private ModuleDefinition _targetModule;

    private MethodDefinition _targetMethod;
    private MethodDefinition _sourceMethod;

    private ILProcessor? _ilProcessor;

    protected ILProcessor ILProcessor
    {
        get
        {
            _ilProcessor ??= _targetMethod.Body.GetILProcessor();
            return _ilProcessor;
        }
    }

    public Patch(ModuleDefinition targetModule, ModuleDefinition sourceModule, string typeName, string methodName)
    {
        TypeDefinition sourceType = sourceModule.GetType($"SilksongDoorstop.Patches.{typeName}");
        _sourceMethod = sourceType.Methods.First(method => method.Name == methodName);

        _targetModule = targetModule;
        TypeDefinition targetType = _targetModule.GetType(typeName);
        try
        {
            _targetMethod = targetType.Methods.First(method => method.Name == methodName);
        }
        catch (Exception)
        {
            _targetMethod = new MethodDefinition(methodName, _sourceMethod.Attributes, targetModule.ImportReference(_sourceMethod.ReturnType));
            targetType.Methods.Add(_targetMethod);
        }
    }

    public abstract void ApplyPatch();

    protected void CopySourceMethod()
    {
        CopyLocals();
        CopyCode();
    }

    protected void CopyLocals()
    {
        _targetMethod.Body.InitLocals = _sourceMethod.Body.InitLocals;

        foreach (VariableDefinition varDef in _sourceMethod.Body.Variables)
        {
            varDef.VariableType = _targetModule.ImportReference(varDef.VariableType);
            _targetMethod.Body.Variables.Add(varDef);
        }
    }

    protected void CopyCode()
    {
        ILProcessor il = ILProcessor;

        foreach (Instruction inst in _sourceMethod.Body.Instructions)
        {
            if (inst.OpCode.FlowControl == FlowControl.Call)
            {
                inst.Operand = _targetModule.ImportReference((MethodReference)inst.Operand);
            }
            il.Append(inst);
        }
    }
}
