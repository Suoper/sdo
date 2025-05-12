using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.CSharp;
using UnityEngine;

namespace AiSorcery
{
    public class DynamicCodeReloader
    {
        // For tracking loaded assemblies
        private Dictionary<string, Assembly> loadedAssemblies = new Dictionary<string, Assembly>();

        // For tracking loaded plugins
        private Dictionary<string, object> loadedPlugins = new Dictionary<string, object>();

        public Assembly CompileAndLoadAssembly(string sourceCode, string assemblyName)
        {
            Debug.Log($"Attempting to compile assembly: {assemblyName}");

            try
            {
                // Create a C# code provider
                // This uses the built-in .NET compiler rather than trying to call mono.exe
                using (var provider = new CSharpCodeProvider())
                {
                    // Setup compiler parameters
                    var parameters = new CompilerParameters();

                    // Generate in-memory assembly
                    parameters.GenerateInMemory = true;
                    parameters.GenerateExecutable = false;
                    parameters.TreatWarningsAsErrors = false;

                    // Add basic references that any Unity script would need
                    parameters.ReferencedAssemblies.Add("System.dll");
                    parameters.ReferencedAssemblies.Add("System.Core.dll");

                    // Try to add Unity and ThunderRoad references
                    TryAddReference(parameters, "UnityEngine.dll");
                    TryAddReference(parameters, "UnityEngine.CoreModule.dll");
                    TryAddReference(parameters, "ThunderRoad.dll");
                    TryAddReference(parameters, "UnityEngine.PhysicsModule.dll");
                    TryAddReference(parameters, "UnityEngine.UI.dll");
                    TryAddReference(parameters, "UnityEngine.AnimationModule.dll");
                    TryAddReference(parameters, "UnityEngine.InputModule.dll");

                    // Try to add references from current AppDomain
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            string location = assembly.Location;
                            if (!string.IsNullOrEmpty(location) && File.Exists(location))
                            {
                                TryAddReference(parameters, location);
                            }
                        }
                        catch (Exception)
                        {
                            // Skip assemblies without a location
                        }
                    }

                    // Compile the source code
                    CompilerResults results = provider.CompileAssemblyFromSource(parameters, sourceCode);

                    // Check for errors
                    if (results.Errors.HasErrors)
                    {
                        // Report all errors
                        StringBuilder errorMessage = new StringBuilder();
                        errorMessage.AppendLine($"Compilation errors in {assemblyName}:");

                        foreach (CompilerError error in results.Errors)
                        {
                            string errorText = $"Line {error.Line}: {error.ErrorText}";
                            errorMessage.AppendLine(errorText);
                            Debug.LogError(errorText);
                        }

                        Debug.LogError(errorMessage.ToString());
                        return null;
                    }

                    // Successfully compiled
                    Assembly compiledAssembly = results.CompiledAssembly;

                    // Store reference to the assembly
                    if (compiledAssembly != null)
                    {
                        loadedAssemblies[assemblyName] = compiledAssembly;
                        Debug.Log($"Successfully compiled assembly: {assemblyName}");
                    }

                    return compiledAssembly;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error compiling assembly {assemblyName}: {ex.Message}\n{ex.StackTrace}");
                return null;
            }
        }

        private void TryAddReference(CompilerParameters parameters, string assemblyName)
        {
            try
            {
                // Check if it's already in the referenced assemblies
                foreach (string refAssembly in parameters.ReferencedAssemblies)
                {
                    if (Path.GetFileName(refAssembly).Equals(Path.GetFileName(assemblyName),
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return; // Already added
                    }
                }

                // If it's a full path and exists, add it directly
                if (File.Exists(assemblyName))
                {
                    parameters.ReferencedAssemblies.Add(assemblyName);
                    return;
                }

                // Try to find in the main Unity directories
                string[] searchPaths = new string[]
                {
                    // Editor paths
                    Path.Combine(Application.dataPath, "..\\Library\\ScriptAssemblies"),
                    Path.Combine(Application.dataPath, "..\\Managed"),
                    Path.Combine(Application.dataPath, "Plugins"),
                    
                    // Build paths
                    Path.Combine(Application.dataPath, "Managed"),
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                };

                foreach (string path in searchPaths)
                {
                    if (Directory.Exists(path))
                    {
                        string fullPath = Path.Combine(path, assemblyName);
                        if (File.Exists(fullPath))
                        {
                            parameters.ReferencedAssemblies.Add(fullPath);
                            Debug.Log($"Added reference: {fullPath}");
                            return;
                        }
                    }
                }

                // If no specific path was found, try adding it by name only
                // The compiler might be able to resolve it from the GAC
                parameters.ReferencedAssemblies.Add(assemblyName);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to add reference {assemblyName}: {ex.Message}");
            }
        }

        public bool LoadPlugin(string pluginName, string sourceCode)
        {
            try
            {
                Debug.Log($"Attempting to load plugin: {pluginName}");

                // Compile the plugin code
                Assembly assembly = CompileAndLoadAssembly(sourceCode, pluginName);
                if (assembly == null)
                {
                    Debug.LogError($"Failed to compile plugin: {pluginName}");
                    return false;
                }

                // Look for classes that might be plugins based on name or heritage
                bool found = false;
                foreach (Type type in assembly.GetTypes())
                {
                    // Check if the type looks like a plugin
                    bool isPluginType = false;

                    // Check by name
                    if (type.Name.Contains("Plugin") || type.Name.Contains("Mod") ||
                        type.Name.Contains("Script") || type.Name.Contains(pluginName))
                    {
                        isPluginType = true;
                    }

                    // Check by interface implementation (if possible)
                    try
                    {
                        // Look for any IPlugin or similar interface
                        foreach (Type interfaceType in type.GetInterfaces())
                        {
                            if (interfaceType.Name.Contains("IPlugin") ||
                                interfaceType.Name.Contains("IMod"))
                            {
                                isPluginType = true;
                                break;
                            }
                        }
                    }
                    catch { /* Ignore interface check errors */ }

                    // Attempt to instantiate the plugin
                    if (isPluginType)
                    {
                        try
                        {
                            // For MonoBehaviour types, we need to use a GameObject
                            if (type.IsSubclassOf(typeof(MonoBehaviour)))
                            {
                                GameObject obj = new GameObject(type.Name + "Object");
                                Component component = obj.AddComponent(type);
                                GameObject.DontDestroyOnLoad(obj);

                                // Store reference to the plugin
                                loadedPlugins[pluginName] = component;

                                Debug.Log($"Loaded plugin {pluginName} as MonoBehaviour on {obj.name}");
                                found = true;
                            }
                            // For ThunderScripts
                            else if (IsThunderScript(type))
                            {
                                GameObject obj = new GameObject(type.Name);
                                Component component = obj.AddComponent(type);
                                GameObject.DontDestroyOnLoad(obj);

                                // Try to call ScriptEnable
                                MethodInfo enableMethod = type.GetMethod("ScriptEnable");
                                if (enableMethod != null)
                                {
                                    enableMethod.Invoke(component, null);
                                }

                                // Store reference to the plugin
                                loadedPlugins[pluginName] = component;

                                Debug.Log($"Loaded plugin {pluginName} as ThunderScript on {obj.name}");
                                found = true;
                            }
                            // For regular classes
                            else
                            {
                                object instance = Activator.CreateInstance(type);

                                // Try to call Initialize method if it exists
                                MethodInfo initMethod = type.GetMethod("Initialize");
                                if (initMethod != null)
                                {
                                    initMethod.Invoke(instance, null);
                                }

                                // Store reference to the plugin
                                loadedPlugins[pluginName] = instance;

                                Debug.Log($"Loaded plugin {pluginName} as regular class");
                                found = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"Error initializing potential plugin class {type.Name}: {ex.Message}");
                        }
                    }
                }

                return found;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading plugin {pluginName}: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        // Helper method to check if a type is a ThunderScript
        private bool IsThunderScript(Type type)
        {
            try
            {
                // Walk up the inheritance chain to find if any base class is named "ThunderScript"
                Type currentType = type;
                while (currentType != null && currentType != typeof(object))
                {
                    if (currentType.Name == "ThunderScript")
                        return true;

                    currentType = currentType.BaseType;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        // Helper method to create a MonoBehaviour from a dynamically compiled assembly
        public bool InstantiateScript(Assembly assembly, string typeName, GameObject targetObject = null)
        {
            try
            {
                if (assembly == null)
                {
                    Debug.LogError("Cannot instantiate script: Assembly is null");
                    return false;
                }

                // Find the type in the assembly
                Type scriptType = null;
                foreach (Type type in assembly.GetTypes())
                {
                    if (type.Name == typeName)
                    {
                        scriptType = type;
                        break;
                    }
                }

                if (scriptType == null)
                {
                    Debug.LogError($"Type '{typeName}' not found in the assembly");
                    return false;
                }

                // Create a GameObject if none provided
                if (targetObject == null)
                {
                    targetObject = new GameObject(typeName + "Object");
                    GameObject.DontDestroyOnLoad(targetObject);
                }

                // Add the component to the GameObject
                if (scriptType.IsSubclassOf(typeof(MonoBehaviour)))
                {
                    targetObject.AddComponent(scriptType);
                    Debug.Log($"Component {typeName} added to {targetObject.name}");
                    return true;
                }
                else if (IsThunderScript(scriptType))
                {
                    var script = targetObject.AddComponent(scriptType);

                    // Try to call ScriptEnable
                    MethodInfo enableMethod = scriptType.GetMethod("ScriptEnable");
                    if (enableMethod != null)
                    {
                        enableMethod.Invoke(script, null);
                    }

                    Debug.Log($"ThunderScript {typeName} added and enabled on {targetObject.name}");
                    return true;
                }
                else
                {
                    Debug.LogWarning($"Type {typeName} is not a MonoBehaviour or ThunderScript, cannot add as component");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error instantiating script: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        // Helper method for editor script reloading (no-op in builds)
        public bool ReloadScriptInEditor(string filePath)
        {
            // This only works in the editor
            return false;
        }
    }
}