using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.CSharp;
using UnityEngine;

public class DirectCompiler
{
    public static Assembly CompileCode(string sourceCode, string assemblyName)
    {
        Debug.Log($"Starting direct compilation of {assemblyName}...");

        try
        {
            // Setup the compiler
            var compilerParams = new CompilerParameters
            {
                GenerateInMemory = true,
                GenerateExecutable = false
            };

            // Add necessary references
            compilerParams.ReferencedAssemblies.Add("System.dll");
            compilerParams.ReferencedAssemblies.Add("System.Core.dll");

            // Try to add Unity references - use only what we know exists
            try
            {
                // Add core Unity references
                compilerParams.ReferencedAssemblies.Add(typeof(UnityEngine.Object).Assembly.Location);
                compilerParams.ReferencedAssemblies.Add(typeof(Debug).Assembly.Location);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Could not add Unity references: {ex.Message}");
            }

            // Use the simple CSharp compiler directly
            using (var provider = new CSharpCodeProvider())
            {
                var compileResult = provider.CompileAssemblyFromSource(compilerParams, sourceCode);

                // Check for errors
                if (compileResult.Errors.HasErrors)
                {
                    foreach (CompilerError error in compileResult.Errors)
                    {
                        Debug.LogError($"Compilation error: Line {error.Line}: {error.ErrorText}");
                    }
                    return null;
                }

                Debug.Log("Compilation successful!");
                return compileResult.CompiledAssembly;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Compilation failed: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }
}