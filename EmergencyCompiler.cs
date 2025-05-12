using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.CSharp;
using UnityEngine;

public static class EmergencyCompiler
{
    public static Assembly CompileSource(string source)
    {
        try
        {
            Debug.Log("EmergencyCompiler: Compiling source code...");

            // Create compiler
            using (var provider = new CSharpCodeProvider())
            {
                // Setup parameters
                var parameters = new CompilerParameters
                {
                    GenerateInMemory = true,
                    GenerateExecutable = false
                };

                // Add basic references
                parameters.ReferencedAssemblies.Add("System.dll");
                parameters.ReferencedAssemblies.Add("mscorlib.dll");

                try
                {
                    // Try to add Unity references
                    parameters.ReferencedAssemblies.Add(typeof(GameObject).Assembly.Location);
                    parameters.ReferencedAssemblies.Add(typeof(Debug).Assembly.Location);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("Could not add Unity references: " + ex.Message);
                }

                // Compile the source
                var results = provider.CompileAssemblyFromSource(parameters, source);

                // Check for errors
                if (results.Errors.HasErrors)
                {
                    Debug.LogError("Compilation Errors:");
                    foreach (CompilerError error in results.Errors)
                    {
                        Debug.LogError($"Line {error.Line}: {error.ErrorText}");
                    }
                    return null;
                }

                Debug.Log("EmergencyCompiler: Compilation successful!");
                return results.CompiledAssembly;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("EmergencyCompiler error: " + ex.Message + "\n" + ex.StackTrace);
            return null;
        }
    }
}