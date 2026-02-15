using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SilksongDoorstop.Patches;

internal abstract class CopyPatch : Patch
{
    private ModuleDefinition _targetModule;
    private ModuleDefinition _sourceModule;

    protected MethodDefinition _targetMethod;
    protected MethodDefinition _sourceMethod;

    public CopyPatch(ModuleDefinition targetModule, ModuleDefinition sourceModule, string typeName, string methodName)
    {
        _sourceModule = sourceModule;
        TypeDefinition sourceType = _sourceModule.GetType($"SilksongDoorstop.Patches.{typeName}");
        _sourceMethod = sourceType.Methods.First(method => method.Name == methodName);

        _targetModule = targetModule;
        TypeDefinition targetType = _targetModule.GetType(typeName);
        try
        {
            _targetMethod = targetType.Methods.First(method =>
                method.Name == methodName &&
                method.Parameters.Count == _sourceMethod.Parameters.Count &&
                method.Parameters.SequenceEqual(_sourceMethod.Parameters)
            );
        }
        catch (Exception)
        {
            _targetMethod = new MethodDefinition(methodName, _sourceMethod.Attributes, targetModule.ImportReference(_sourceMethod.ReturnType));
            targetType.Methods.Add(_targetMethod);
        }
    }

    virtual public void ApplyPatch()
    {
        CopyParameters();
        CopyLocals();
        CopyCode();
    }

    protected void CopyParameters()
    {
        foreach (ParameterDefinition paramDef in _sourceMethod.Parameters)
        {
            paramDef.ParameterType = _targetModule.ImportReference(paramDef.ParameterType);
            _targetMethod.Parameters.Add(paramDef);
        }
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
        ILProcessor il = _targetMethod.Body.GetILProcessor();

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
