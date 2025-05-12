using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using ChatGPTMod;

namespace AiSorcery
{
    public class ScriptableGenerator : MonoBehaviour
    {
        [Header("Generator Settings")]
        public string scriptsDirectory = "GeneratedScripts";
        public bool autoAttachScripts = true;

        // Tracking
        private List<string> generatedScripts = new List<string>();
        private Dictionary<string, Assembly> loadedAssemblies = new Dictionary<string, Assembly>();

        // References
        private ChatGPTSpellController chatGPTController;
        private DynamicCodeReloader codeReloader;

        void Awake()
        {
            chatGPTController = GetComponent<ChatGPTSpellController>();
            codeReloader = new DynamicCodeReloader();

            // Create scripts directory if it doesn't exist
            string fullScriptsPath = GetSafePath(scriptsDirectory);
            if (!Directory.Exists(fullScriptsPath))
            {
                try
                {
                    Directory.CreateDirectory(fullScriptsPath);
                    Debug.Log($"Created scripts directory: {fullScriptsPath}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error creating scripts directory: {ex.Message}");
                }
            }
        }

        private string GetSafePath(string subDirectory)
        {
            string basePath = Application.isEditor ? Application.dataPath : Application.persistentDataPath;
            return Path.Combine(basePath, subDirectory);
        }

        public async Task<string> GenerateAndAddScript(string request)
        {
            Debug.Log($"Generating new script based on request: {request}");

            try
            {
                // 1. Determine script name and purpose
                (string scriptName, string functionality) = await AnalyzeScriptRequest(request);

                // 2. Generate the script code
                string scriptCode = await GenerateScriptCode(scriptName, functionality);

                // 3. Save script to file
                string scriptPath = SaveScriptToFile(scriptName, scriptCode);

                // 4. Compile and load the script
                bool success = CompileAndLoadScript(scriptName, scriptCode);

                // 5. Try to attach the script to an appropriate game object if requested
                string attachResult = "";
                if (autoAttachScripts && success)
                {
                    attachResult = TryAttachScript(scriptName);
                }

                // Return results
                if (success)
                {
                    return $"Successfully generated and loaded '{scriptName}' script!\n" +
                           $"Saved to: {scriptPath}\n{attachResult}";
                }
                else
                {
                    return $"Script generation failed for '{scriptName}'. Check logs for details.";
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error generating script: {ex.Message}");
                return $"Error generating script: {ex.Message}";
            }
        }

        private async Task<(string scriptName, string functionality)> AnalyzeScriptRequest(string request)
        {
            // In a real implementation, use AI to determine appropriate script name and purpose
            string prompt = $"Analyze this request for a new Unity C# script:\n\n{request}\n\n" +
                           $"Return ONLY a JSON object with these properties:\n" +
                           $"1. scriptName: A suitable C# class name (PascalCase, no spaces)\n" +
                           $"2. functionality: Brief description of what the script should do\n";

            string response = await SendToDedicatedAI(prompt);

            // Extract fields from response
            string scriptName = ExtractField(response, "scriptName");
            string functionality = ExtractField(response, "functionality");

            // Default values if extraction fails
            if (string.IsNullOrEmpty(scriptName))
                scriptName = "NewGeneratedScript" + DateTime.Now.Ticks.ToString().Substring(10);

            if (string.IsNullOrEmpty(functionality))
                functionality = request;

            return (scriptName, functionality);
        }

        private async Task<string> GenerateScriptCode(string scriptName, string functionality)
        {
            // Generate a fully-formed C# script using AI
            string prompt = $"Write a complete and functional Unity C# script named {scriptName} that does the following:\n\n" +
                           $"{functionality}\n\n" +
                           $"Requirements:\n" +
                           $"- Must be a complete, compilable MonoBehaviour script\n" +
                           $"- Include necessary using statements\n" +
                           $"- Include proper Awake/Start/Update methods as needed\n" +
                           $"- Include XML comments explaining key functionality\n" +
                           $"- Should not depend on external scripts unless they are standard Unity components\n" +
                           $"- Return ONLY the code with no markdown formatting or explanations\n";

            string response = await SendToDedicatedAI(prompt);

            // Clean the response to ensure it's just the code
            return CleanCodeResponse(response);
        }

        private string SaveScriptToFile(string scriptName, string scriptCode)
        {
            // Ensure the name ends with .cs
            if (!scriptName.EndsWith(".cs"))
                scriptName += ".cs";

            string fullPath = Path.Combine(GetSafePath(scriptsDirectory), scriptName);

            // Write the script to file
            File.WriteAllText(fullPath, scriptCode);

            // Track this generated script
            if (!generatedScripts.Contains(fullPath))
                generatedScripts.Add(fullPath);

            Debug.Log($"Saved script to: {fullPath}");
            return fullPath;
        }

        private bool CompileAndLoadScript(string scriptName, string scriptCode)
        {
            try
            {
                // Remove .cs extension if present
                if (scriptName.EndsWith(".cs"))
                    scriptName = scriptName.Substring(0, scriptName.Length - 3);

                // Compile and load the script using DynamicCodeReloader
                Assembly assembly = codeReloader.CompileAndLoadAssembly(scriptCode, scriptName);

                if (assembly != null)
                {
                    // Store the loaded assembly
                    loadedAssemblies[scriptName] = assembly;
                    Debug.Log($"Successfully compiled and loaded {scriptName} assembly");
                    return true;
                }
                else
                {
                    Debug.LogError($"Failed to compile {scriptName}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error compiling script: {ex.Message}");
                return false;
            }
        }

        private string TryAttachScript(string scriptName)
        {
            try
            {
                // Remove .cs extension if present
                if (scriptName.EndsWith(".cs"))
                    scriptName = scriptName.Substring(0, scriptName.Length - 3);

                if (!loadedAssemblies.ContainsKey(scriptName))
                    return "Could not attach script: Assembly not loaded.";

                Assembly assembly = loadedAssemblies[scriptName];

                // Find the type in the loaded assembly
                Type scriptType = null;
                foreach (Type type in assembly.GetTypes())
                {
                    if (type.Name == scriptName && type.IsSubclassOf(typeof(MonoBehaviour)))
                    {
                        scriptType = type;
                        break;
                    }
                }

                if (scriptType == null)
                    return "Could not attach script: Type not found in assembly.";

                // Create a game object and attach the component
                GameObject newObject = new GameObject(scriptName + "Object");
                Component component = newObject.AddComponent(scriptType);

                if (component != null)
                {
                    GameObject.DontDestroyOnLoad(newObject);
                    return $"Script attached to new GameObject: {newObject.name}";
                }
                else
                {
                    return "Failed to attach script component.";
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error attaching script: {ex.Message}");
                return $"Error attaching script: {ex.Message}";
            }
        }

        private async Task<string> SendToDedicatedAI(string prompt)
        {
            // Use the ChatGPTSpellController's SendCommandToOpenAI method if accessible
            // Otherwise, this would be your API call to generate code

            if (chatGPTController != null)
            {
                // Use reflection to access the private method
                MethodInfo sendCommandMethod = typeof(ChatGPTSpellController).GetMethod("SendCommandToOpenAI",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (sendCommandMethod != null)
                {
                    Task<string> task = (Task<string>)sendCommandMethod.Invoke(chatGPTController, new object[] { prompt });
                    return await task;
                }
            }

            // Fallback if we can't access ChatGPTSpellController's method
            await Task.Delay(500); // Simulate API call

            // Mock response for demonstration
            if (prompt.Contains("script"))
            {
                return "{\n\"scriptName\": \"FireballSpell\",\n\"functionality\": \"Creates a fireball that damages enemies\"\n}";
            }
            else
            {
                return "using UnityEngine;\n\npublic class FireballSpell : MonoBehaviour\n{\n    public float damage = 10f;\n    public float speed = 5f;\n\n    void Update()\n    {\n        transform.Translate(Vector3.forward * speed * Time.deltaTime);\n    }\n}";
            }
        }

        private string ExtractField(string json, string fieldName)
        {
            // Simple JSON field extraction
            int startIndex = json.IndexOf($"\"{fieldName}\":");
            if (startIndex < 0) return string.Empty;

            startIndex = json.IndexOf("\"", startIndex + fieldName.Length + 3) + 1;
            int endIndex = json.IndexOf("\"", startIndex);

            if (endIndex > startIndex)
                return json.Substring(startIndex, endIndex - startIndex);

            return string.Empty;
        }
        // Add this method to your ScriptableGenerator class
        public async void ProcessCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                Debug.LogError("Empty command received");
                return;
            }

            Debug.Log($"Processing command: {command}");

            try
            {
                // Extract command type and description
                if (command.StartsWith("/generate ") || command.StartsWith("/add "))
                {
                    string description = "";

                    if (command.StartsWith("/generate "))
                        description = command.Substring("/generate ".Length).Trim();
                    else if (command.StartsWith("/add "))
                        description = command.Substring("/add ".Length).Trim();

                    if (string.IsNullOrWhiteSpace(description))
                    {
                        Debug.LogError("No description provided. Usage: /generate <description> or /add <description>");
                        return;
                    }

                    // Pass the entire description to the generation method
                    string result = await GenerateAndAddScript(description);
                    Debug.Log(result);
                }
                else if (command.StartsWith("/instantiate "))
                {
                    string scriptName = command.Substring("/instantiate ".Length).Trim();
                    string result = TryAttachScript(scriptName);
                    Debug.Log(result);
                }
                else if (command.StartsWith("/list"))
                {
                    // List all generated scripts
                    List<string> scripts = GetGeneratedScriptsList();
                    if (scripts.Count > 0)
                    {
                        Debug.Log($"Generated scripts ({scripts.Count}):");
                        foreach (var script in scripts)
                        {
                            Debug.Log($"- {Path.GetFileName(script)}");
                        }
                    }
                    else
                    {
                        Debug.Log("No scripts have been generated yet.");
                    }
                }
                else if (command.StartsWith("/help"))
                {
                    // Display help information
                    Debug.Log("Available commands:");
                    Debug.Log("/generate <description> - Generate a new script based on description");
                    Debug.Log("/add <description> - Alternative command to add a new script");
                    Debug.Log("/instantiate <scriptName> - Manually instantiate a previously generated script");
                    Debug.Log("/list - Show all generated scripts");
                    Debug.Log("/help - Show this help information");
                    Debug.Log("");
                    Debug.Log("Examples:");
                    Debug.Log("/generate make enemies fly backwards every 5 seconds");
                    Debug.Log("/add create a fireball that damages enemies on impact");
                }
                else
                {
                    Debug.LogWarning($"Unknown command: {command}");
                    Debug.Log("Type /help for a list of available commands");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing command: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private string CleanCodeResponse(string response)
        {
            // Remove markdown code blocks if present
            if (response.Contains("```"))
            {
                int start = response.IndexOf("```") + 3;
                start = response.IndexOf("\n", start) + 1;
                int end = response.LastIndexOf("```");

                if (end > start)
                    response = response.Substring(start, end - start);
            }

            return response.Trim();
        }

        public List<string> GetGeneratedScriptsList()
        {
            return new List<string>(generatedScripts);
        }
    }
}