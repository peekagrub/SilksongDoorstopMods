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

	ReaderParameters readerParams = new ReaderParameters{
	    AssemblyResolver = resolver,
	    ReadWrite = false
	};

        AssemblyDefinition silksongAsm = AssemblyDefinition.ReadAssembly(asmPath, new ReaderParameters
        {
	    AssemblyResolver = resolver,
            ReadWrite = true,
	    InMemory = false
        });

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
	    string varAssembly = String.Empty;
	    string[] varTypeDef = varDef.VariableType.FullName.Split('.');
	    for (int i = 0; i < varTypeDef.Length - 1; i++)
	    {
	        varAssembly += varTypeDef[i] + ".";
	    }
	    varAssembly = varAssembly.Trim('.');
	    Type type = Type.GetType($"{varDef.VariableType.FullName}, {varAssembly}");

	    if (type == null)
	    {
	        type = Type.GetType(varDef.VariableType.FullName);
	        if (type == null)
	        {
	    	    Console.WriteLine($"Failed getting var type for: {varDef.VariableType.FullName}");
	        }
	    }

	    TypeReference typeRef = silksongAsm.MainModule.ImportReference(type);

	    patchedMethod.Body.Variables.Add(new VariableDefinition(typeRef));
	}

	ILProcessor il = patchedMethod.Body.GetILProcessor();

	foreach (Instruction inst in patchMethod.Body.Instructions)
	{
	    if (inst.OpCode == OpCodes.Newobj)
	    {
	        MethodReference operand = (MethodReference)inst.Operand;
	        string[] methodDef = operand.FullName.Split(' ', ':').Where(x => !String.IsNullOrEmpty(x)).ToArray();
	        string assembly = String.Empty;
	        string[] typeDef = methodDef[1].Split('.');
	        string[] subs = methodDef[2].TrimEnd(')').Split('(', ',');
	        subs = subs.Skip(1).ToArray().Where(x => !String.IsNullOrEmpty(x)).ToArray();
	        Type[] args = subs.Select(arg => 
	            {
	            	string argAssembly = String.Empty;
	        	string[] argTypeDef = arg.Split('.');
	        	for (int i = 0; i < argTypeDef.Length - 1; i++)
	        	{
	        	    argAssembly += argTypeDef[i] + ".";
	        	}
	        	argAssembly = argAssembly.Trim('.');
	        	Type type = Type.GetType($"{arg}, {argAssembly}");

	        	if (type == null)
	        	{
	        	    type = Type.GetType(arg);
	        	    if (type == null)
	        	    {
	        		Console.WriteLine($"Failed getting type for: {operand.FullName}");
	        		Console.WriteLine($"Failed getting arg type for: {arg}");
	        	    }
	        	}
	        	return type;
	            }).ToArray();
	        if (args == null)
	        {
	            Console.WriteLine($"Failed getting type for: {operand.FullName}");
	        }

	        for (int i = 0; i < typeDef.Length - 1; i++)
	        {
	            assembly += typeDef[i] + ".";
	        }
	        assembly = assembly.Trim('.');
	        if (String.IsNullOrEmpty(assembly))
	        {
		     TypeDefinition classType = silksongModule.GetType(methodDef[1]);
		     MethodDefinition constructor = classType.Methods.First(method => 
			method.IsConstructor &&
			method.Parameters.Count == args.Length &&
			method.Parameters.Select(param => param.ParameterType.FullName).SequenceEqual(
			    args.Select(arg => arg.FullName)
			)
		     );
		     inst.Operand = constructor;
	        } else {
	            Type classType = Type.GetType($"{methodDef[1]}, {assembly}");
	            if (classType == null)
	            {
	                classType = Type.GetType(methodDef[1]);
	                if (classType == null)
	                {
	                    Console.WriteLine($"Failed getting type for: {operand.FullName}");
	                }
	            }
	            var mi = classType.GetConstructor(args);
	            inst.Operand = silksongAsm.MainModule.ImportReference(mi);
		}

		il.Append(inst);
	    }
	    else if (inst.OpCode == OpCodes.Call || inst.OpCode == OpCodes.Callvirt)
	    {
		MethodReference operand = (MethodReference)inst.Operand;
		string[] methodDef = operand.FullName.Split(' ', ':').Where(x => !String.IsNullOrEmpty(x)).ToArray();

		string[] subs = methodDef[2].TrimEnd(')').Split('(', ',');
		string functionName = subs[0];
		subs = subs.Skip(1).ToArray().Where(x => !String.IsNullOrEmpty(x)).ToArray();
		Type[] args = subs.Select(arg => 
		    {
		    	string argAssembly = String.Empty;
			string[] argTypeDef = arg.Split('.');
			for (int i = 0; i < argTypeDef.Length - 1; i++)
			{
			    argAssembly += argTypeDef[i] + ".";
			}
			argAssembly = argAssembly.Trim('.');
			Type type = Type.GetType($"{arg}, {argAssembly}");

			if (type == null)
			{
			    type = Type.GetType(arg);
			    if (type == null)
			    {
				Console.WriteLine($"Failed getting type for: {operand.FullName}");
				Console.WriteLine($"Failed getting arg type for: {arg}");
			    }
			}
			return type;
		    }).ToArray();
		if (args == null)
		{
		    Console.WriteLine($"Failed getting type for: {operand.FullName}");
		}

		string assembly = String.Empty;
		string[] typeDef = methodDef[1].Split('.');
		for (int i = 0; i < typeDef.Length - 1; i++)
		{
		    assembly += typeDef[i] + ".";
		}
		assembly = assembly.Trim('.');
		if (String.IsNullOrEmpty(assembly))
		{
		     TypeDefinition classType = silksongModule.GetType(methodDef[1]);
		     MethodDefinition constructor = classType.Methods.First(method => 
		        method.Name == functionName &&
		        method.Parameters.Count == args.Length &&
		        method.Parameters.Select(param => param.ParameterType.FullName).SequenceEqual(
		                args.Select(arg => arg.FullName)
	                )
		     );
		     inst.Operand = constructor;
		} else 
		{
	            Type classType = Type.GetType($"{methodDef[1]}, {assembly}");
	            if (classType == null)
	            {
	                classType = Type.GetType(methodDef[1]);
	                if (classType == null)
	                {
	                    Console.WriteLine($"Failed getting type for: {operand.FullName}");
	                }
	            }
	            var mi = classType.GetMethod(functionName, args);
	            inst.Operand = silksongAsm.MainModule.ImportReference(mi);
		}

		il.Append(inst);
	    } else 
	    {
	        il.Append(inst);
	    }
	}

	gamemanagerType.Methods.Add(patchedMethod);

	silksongAsm.Write(Path.Combine(modDir, "Assembly-CSharp.dll"));
	silksongAsm.Dispose();
    }
}
