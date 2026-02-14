using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SilksongDoorstop.Patches;
using UnityEngine;

namespace Doorstop;

public static class Entrypoint
{
    public static void Start()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string dataDir = Directory.GetDirectories(baseDir, "*_Data").FirstOrDefault();
        string managedDir = Path.Combine(baseDir, dataDir, "Managed");
        string asmPath = Path.Combine(managedDir, "Assembly-CSharp.dll");

        string entrypointPath = typeof(Entrypoint).Assembly.Location;
        string modDir = Path.GetDirectoryName(entrypointPath);

        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(managedDir);
        resolver.AddSearchDirectory(entrypointPath);
        resolver.AddSearchDirectory(typeof(object).Assembly.Location);

        ReaderParameters readerParams = new ReaderParameters
        {
            AssemblyResolver = resolver,
            ReadWrite = false
        };

        AssemblyDefinition silksongAsm = AssemblyDefinition.ReadAssembly(asmPath, readerParams);

        AssemblyDefinition doorstopAsm = AssemblyDefinition.ReadAssembly(entrypointPath, readerParams);

        ModuleDefinition silksongModule = silksongAsm.MainModule;
        TypeDefinition gamemanagerType = silksongModule.GetType("GameManager");

        ModuleDefinition doorstopModule = doorstopAsm.MainModule;
        TypeDefinition gamemanagerPatchType = doorstopModule.GetType("SilksongDoorstop.Patches.GameManagerPatch");

        MethodDefinition patchMethod = gamemanagerPatchType.Methods.First(method => method.Name == "OnGUI");

        MethodDefinition patchedMethod = new MethodDefinition("OnGUI", MethodAttributes.Private | MethodAttributes.HideBySig, silksongModule.ImportReference(typeof(void)));

        patchedMethod.Body.InitLocals = patchMethod.Body.InitLocals;

        foreach (VariableDefinition varDef in patchMethod.Body.Variables)
        {
            varDef.VariableType = silksongModule.ImportReference(varDef.VariableType);
            patchedMethod.Body.Variables.Add(varDef);
        }

        ILProcessor il = patchedMethod.Body.GetILProcessor();

        foreach (Instruction inst in patchMethod.Body.Instructions)
        {
            if (inst.OpCode == OpCodes.Call || inst.OpCode == OpCodes.Callvirt || inst.OpCode == OpCodes.Newobj)
            {
                inst.Operand = silksongModule.ImportReference((MethodReference)inst.Operand);
            }
            il.Append(inst);
        }

        gamemanagerType.Methods.Add(patchedMethod);

        silksongAsm.Write(Path.Combine(modDir, "Assembly-CSharp.dll"));

        // Cleanup resources
        silksongAsm.Dispose();
        doorstopAsm.Dispose();
    }
}
