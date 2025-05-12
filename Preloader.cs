using System;
using System.IO;
using System.Reflection;
using UnityEngine;

public class ScriptLoader : MonoBehaviour
{
    public void LoadAndRunScript(string dllPath, string typeName, string methodName)
    {
        try
        {
            // Load the compiled assembly
            byte[] assemblyData = File.ReadAllBytes(dllPath);
            Assembly assembly = Assembly.Load(assemblyData);

            // Find and execute the method
            Type type = assembly.GetType(typeName);
            if (type != null)
            {
                MethodInfo method = type.GetMethod(methodName);
                if (method != null)
                {
                    if (method.IsStatic)
                    {
                        method.Invoke(null, null);
                    }
                    else
                    {
                        object instance = Activator.CreateInstance(type);
                        method.Invoke(instance, null);
                    }
                    Debug.Log($"Successfully executed {typeName}.{methodName}");
                }
                else
                {
                    Debug.LogError($"Method {methodName} not found in type {typeName}");
                }
            }
            else
            {
                Debug.LogError($"Type {typeName} not found in assembly");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error loading script: {ex.Message}");
        }
    }
}