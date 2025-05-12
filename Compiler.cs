using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class NewScriptableGenerator : MonoBehaviour
{
    private Dictionary<string, Assembly> loadedAssemblies = new Dictionary<string, Assembly>();

    public bool GenerateScript(string scriptName, string scriptCode)
    {
        try
        {
            Debug.Log($"Generating script: {scriptName}");

            // Remove .cs extension if present
            if (scriptName.EndsWith(".cs"))
                scriptName = scriptName.Substring(0, scriptName.Length - 3);

            // Add namespace and using declarations if they're not already present
            if (!scriptCode.Contains("namespace") && !scriptCode.Contains("using UnityEngine;"))
            {
                scriptCode = "using UnityEngine;\nusing System;\nusing System.Collections;\nusing System.Collections.Generic;\n\n" + scriptCode;
            }

            // Try to compile the script
            Assembly assembly = EmergencyCompiler.CompileSource(scriptCode);

            if (assembly != null)
            {
                loadedAssemblies[scriptName] = assembly;
                Debug.Log($"Successfully generated script: {scriptName}");
                return true;
            }
            else
            {
                Debug.LogError($"Failed to generate script: {scriptName}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error generating script {scriptName}: {ex.Message}");
            return false;
        }
    }

    public bool InstantiateScript(string scriptName)
    {
        try
        {
            Debug.Log($"Instantiating script: {scriptName}");

            // Remove .cs extension if present
            if (scriptName.EndsWith(".cs"))
                scriptName = scriptName.Substring(0, scriptName.Length - 3);

            if (!loadedAssemblies.ContainsKey(scriptName))
            {
                Debug.LogError($"Script not found: {scriptName}");
                return false;
            }

            Assembly assembly = loadedAssemblies[scriptName];

            // Find the type that matches the script name
            Type scriptType = null;
            foreach (Type type in assembly.GetTypes())
            {
                if (type.Name.Equals(scriptName, StringComparison.OrdinalIgnoreCase))
                {
                    scriptType = type;
                    break;
                }
            }

            if (scriptType == null)
            {
                Debug.LogError($"Type {scriptName} not found in assembly");
                return false;
            }

            // Create a GameObject and add the component
            GameObject newObject = new GameObject(scriptName + "Object");

            if (scriptType.IsSubclassOf(typeof(MonoBehaviour)))
            {
                newObject.AddComponent(scriptType);
                Debug.Log($"Added {scriptName} component to {newObject.name}");
                return true;
            }
            else
            {
                Debug.LogWarning($"Type {scriptName} is not a MonoBehaviour");

                // For non-MonoBehaviour classes, we can still try to create an instance
                try
                {
                    object instance = Activator.CreateInstance(scriptType);

                    // Try to invoke Run or Start method if available
                    MethodInfo method = scriptType.GetMethod("Run") ?? scriptType.GetMethod("Start");
                    if (method != null)
                    {
                        method.Invoke(instance, null);
                        Debug.Log($"Created instance of {scriptName} and invoked Run/Start method");
                    }
                    else
                    {
                        Debug.Log($"Created instance of {scriptName} (no Run/Start method found)");
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to create instance: {ex.Message}");
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error instantiating script {scriptName}: {ex.Message}");
            return false;
        }
    }

    // Command handler for testing
    public void ProcessCommand(string command)
    {
        try
        {
            if (command.StartsWith("/generate "))
            {
                string[] parts = command.Substring(10).Split(new[] { ' ' }, 2);
                if (parts.Length < 2)
                {
                    Debug.LogError("Invalid /generate command format. Use: /generate ScriptName ScriptCode");
                    return;
                }

                string scriptName = parts[0];
                string scriptCode = parts[1];

                GenerateScript(scriptName, scriptCode);
            }
            else if (command.StartsWith("/instantiate "))
            {
                string scriptName = command.Substring(13).Trim();
                InstantiateScript(scriptName);
            }
            else
            {
                Debug.LogWarning("Unknown command. Available commands: /generate, /instantiate");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error processing command: {ex.Message}");
        }
    }
}