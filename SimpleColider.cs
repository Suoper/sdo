using System;
using System.CodeDom.Compiler;
using System.Reflection;
using Microsoft.CSharp;
using UnityEngine;

public class SimpleCompiler : MonoBehaviour
{
    public static Assembly CompileCode(string code)
    {
        try
        {
            // Basic compiler setup
            var provider = new CSharpCodeProvider();
            var parameters = new CompilerParameters
            {
                GenerateInMemory = true,
                GenerateExecutable = false
            };

            // Add basic references
            parameters.ReferencedAssemblies.Add("System.dll");
            parameters.ReferencedAssemblies.Add("mscorlib.dll");

            // Try to add Unity references if available
            try
            {
                parameters.ReferencedAssemblies.Add(typeof(GameObject).Assembly.Location);
            }
            catch { }

            // Compile
            var results = provider.CompileAssemblyFromSource(parameters, code);

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

            return results.CompiledAssembly;
        }
        catch (Exception ex)
        {
            Debug.LogError("Compilation failed: " + ex.Message);
            return null;
        }
    }

    // Example usage function
    public void CompileAndRunTest()
    {
        string testCode = @"
            using UnityEngine;
            
            public class TestScript
            {
                public static void Run()
                {
                    Debug.Log(""Hello from dynamically compiled code!"");
                }
            }
        ";

        Assembly assembly = CompileCode(testCode);
        if (assembly != null)
        {
            Type type = assembly.GetType("TestScript");
            MethodInfo method = type.GetMethod("Run");
            method.Invoke(null, null);
        }
    }
}