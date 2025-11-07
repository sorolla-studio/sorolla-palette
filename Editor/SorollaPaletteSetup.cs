using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace SorollaPalette.Editor
{
    [InitializeOnLoad]
    public static class SorollaPaletteSetup
    {
        private const string SETUP_COMPLETE_KEY = "SorollaPalette_SetupComplete";
        private const string OPENUPM_REGISTRY_URL = "https://package.openupm.com";
        private const string GOOGLE_REGISTRY_URL = "https://unityregistry-pa.googleapis.com/";
        
        static SorollaPaletteSetup()
        {
            // Run setup once when package is first imported
            if (!SessionState.GetBool(SETUP_COMPLETE_KEY, false))
            {
                EditorApplication.delayCall += RunSetup;
            }
        }
        
        [MenuItem("Tools/Sorolla Palette/Run Setup (Force)")]
        public static void ForceRunSetup()
        {
            SessionState.SetBool(SETUP_COMPLETE_KEY, false);
            RunSetup();
        }
        
        private static void RunSetup()
        {
            SessionState.SetBool(SETUP_COMPLETE_KEY, true);
            
            Debug.Log("[Sorolla Palette] Running initial setup...");
            
            bool registriesAdded = AddScopedRegistriesToManifest();
            bool dependenciesAdded = AddGameAnalyticsDependency();
            
            if (registriesAdded || dependenciesAdded)
            {
                Debug.Log("[Sorolla Palette] Dependencies added to manifest. Refreshing Asset Database to trigger Package Manager resolve...");
                
                // Force Unity to reload the manifest by refreshing the asset database
                AssetDatabase.Refresh();
                
                Debug.Log("[Sorolla Palette] Setup complete. Package Manager will resolve dependencies.");
            }
            else
            {
                Debug.Log("[Sorolla Palette] All dependencies already configured.");
            }
        }
        
        private static bool AddScopedRegistriesToManifest()
        {
            string manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            
            if (!File.Exists(manifestPath))
            {
                Debug.LogError("[Sorolla Palette] manifest.json not found!");
                return false;
            }
            
            try
            {
                string jsonContent = File.ReadAllText(manifestPath);
                var manifest = MiniJson.Deserialize(jsonContent) as Dictionary<string, object>;
                
                if (manifest == null)
                {
                    Debug.LogError("[Sorolla Palette] Failed to parse manifest.json");
                    return false;
                }
                
                // Get or create scopedRegistries array
                List<object> scopedRegistries;
                if (manifest.ContainsKey("scopedRegistries"))
                {
                    scopedRegistries = manifest["scopedRegistries"] as List<object>;
                }
                else
                {
                    scopedRegistries = new List<object>();
                    manifest["scopedRegistries"] = scopedRegistries;
                }
                
                // Check if Google registry already exists
                bool hasGoogle = false;
                Dictionary<string, object> googleRegistry = null;
                
                // Check if OpenUPM registry already exists
                bool hasOpenUPM = false;
                Dictionary<string, object> openUpmRegistry = null;
                
                foreach (var reg in scopedRegistries)
                {
                    var registry = reg as Dictionary<string, object>;
                    if (registry != null && registry.ContainsKey("url"))
                    {
                        string url = registry["url"].ToString();
                        if (url == GOOGLE_REGISTRY_URL)
                        {
                            hasGoogle = true;
                            googleRegistry = registry;
                        }
                        else if (url == OPENUPM_REGISTRY_URL)
                        {
                            hasOpenUPM = true;
                            openUpmRegistry = registry;
                        }
                    }
                }
                
                // Add or update Google registry
                if (!hasGoogle)
                {
                    googleRegistry = new Dictionary<string, object>
                    {
                        { "name", "Game Package Registry by Google" },
                        { "url", GOOGLE_REGISTRY_URL },
                        { "scopes", new List<object> { "com.google" } }
                    };
                    scopedRegistries.Add(googleRegistry);
                    Debug.Log("[Sorolla Palette] Added Google registry to manifest.json");
                }
                else
                {
                    // Update existing Google registry scopes
                    if (googleRegistry.ContainsKey("scopes"))
                    {
                        var scopes = googleRegistry["scopes"] as List<object>;
                        if (scopes != null && !scopes.Contains("com.google"))
                        {
                            scopes.Add("com.google");
                            Debug.Log("[Sorolla Palette] Updated Google registry scopes");
                        }
                    }
                }
                
                // Add or update OpenUPM registry
                if (!hasOpenUPM)
                {
                    // Create new OpenUPM registry
                    openUpmRegistry = new Dictionary<string, object>
                    {
                        { "name", "package.openupm.com" },
                        { "url", OPENUPM_REGISTRY_URL },
                        { "scopes", new List<object> { "com.gameanalytics", "com.google.external-dependency-manager" } }
                    };
                    scopedRegistries.Add(openUpmRegistry);
                    Debug.Log("[Sorolla Palette] Added OpenUPM registry to manifest.json");
                }
                else
                {
                    // Update existing OpenUPM registry scopes
                    if (openUpmRegistry.ContainsKey("scopes"))
                    {
                        var scopes = openUpmRegistry["scopes"] as List<object>;
                        if (scopes != null)
                        {
                            bool modified = false;
                            
                            if (!scopes.Contains("com.gameanalytics"))
                            {
                                scopes.Add("com.gameanalytics");
                                modified = true;
                            }
                            
                            if (!scopes.Contains("com.google.external-dependency-manager"))
                            {
                                scopes.Add("com.google.external-dependency-manager");
                                modified = true;
                            }
                            
                            if (modified)
                            {
                                Debug.Log("[Sorolla Palette] Updated OpenUPM registry scopes in manifest.json");
                            }
                            else
                            {
                                Debug.Log("[Sorolla Palette] OpenUPM registry already configured correctly");
                            }
                        }
                    }
                }
                
                // Write back to file with pretty formatting
                string updatedJson = MiniJson.Serialize(manifest, prettyPrint: true);
                File.WriteAllText(manifestPath, updatedJson);
                
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Sorolla Palette] Error modifying manifest.json: {e.Message}");
                return false;
            }
        }
        
        private static bool AddGameAnalyticsDependency()
        {
            string manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            
            try
            {
                string jsonContent = File.ReadAllText(manifestPath);
                var manifest = MiniJson.Deserialize(jsonContent) as Dictionary<string, object>;
                
                if (manifest == null || !manifest.ContainsKey("dependencies"))
                {
                    return false;
                }
                
                var dependencies = manifest["dependencies"] as Dictionary<string, object>;
                
                // Add GameAnalytics and EDM if not already present
                bool modified = false;
                
                if (!dependencies.ContainsKey("com.gameanalytics.sdk"))
                {
                    dependencies["com.gameanalytics.sdk"] = "7.10.6";
                    modified = true;
                    Debug.Log("[Sorolla Palette] Added GameAnalytics SDK dependency");
                }
                
                if (!dependencies.ContainsKey("com.google.external-dependency-manager"))
                {
                    dependencies["com.google.external-dependency-manager"] = "https://github.com/googlesamples/unity-jar-resolver.git?path=upm";
                    modified = true;
                    Debug.Log("[Sorolla Palette] Added External Dependency Manager dependency");
                }
                
                if (modified)
                {
                    string updatedJson = MiniJson.Serialize(manifest, prettyPrint: true);
                    File.WriteAllText(manifestPath, updatedJson);
                    return true;
                }
                
                return false;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Sorolla Palette] Error adding GameAnalytics dependency: {e.Message}");
                return false;
            }
        }
    }
    
    /// <summary>
    /// Minimal JSON serializer/deserializer for manifest.json manipulation
    /// </summary>
    public static class MiniJson
    {
        public static object Deserialize(string json)
        {
            return json == null ? null : Parser.Parse(json);
        }
        
        public static string Serialize(object obj, bool prettyPrint = false)
        {
            return Serializer.Serialize(obj, prettyPrint);
        }
        
        private sealed class Parser
        {
            private const string WHITE_SPACE = " \t\n\r";
            private const string WORD_BREAK = " \t\n\r{}[],:\"";
            
            private StringReader json;
            
            private Parser(string jsonString)
            {
                json = new StringReader(jsonString);
            }
            
            public static object Parse(string jsonString)
            {
                var instance = new Parser(jsonString);
                return instance.ParseValue();
            }
            
            private void Dispose()
            {
                json.Dispose();
                json = null;
            }
            
            private object ParseValue()
            {
                SkipWhitespace();
                char nextChar = PeekChar();
                
                switch (nextChar)
                {
                    case '{':
                        return ParseObject();
                    case '[':
                        return ParseArray();
                    case '"':
                        return ParseString();
                    case '-':
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        return ParseNumber();
                    default:
                        string word = ParseWord();
                        if (word == "true") return true;
                        if (word == "false") return false;
                        if (word == "null") return null;
                        return word;
                }
            }
            
            private Dictionary<string, object> ParseObject()
            {
                var table = new Dictionary<string, object>();
                json.Read(); // {
                
                while (true)
                {
                    SkipWhitespace();
                    char nextChar = PeekChar();
                    
                    if (nextChar == '}')
                    {
                        json.Read();
                        return table;
                    }
                    
                    if (nextChar == ',')
                    {
                        json.Read();
                        continue;
                    }
                    
                    string key = ParseString();
                    SkipWhitespace();
                    json.Read(); // :
                    table[key] = ParseValue();
                }
            }
            
            private List<object> ParseArray()
            {
                var array = new List<object>();
                json.Read(); // [
                
                while (true)
                {
                    SkipWhitespace();
                    char nextChar = PeekChar();
                    
                    if (nextChar == ']')
                    {
                        json.Read();
                        return array;
                    }
                    
                    if (nextChar == ',')
                    {
                        json.Read();
                        continue;
                    }
                    
                    array.Add(ParseValue());
                }
            }
            
            private string ParseString()
            {
                json.Read(); // "
                var sb = new System.Text.StringBuilder();
                
                while (true)
                {
                    if (json.Peek() == -1) break;
                    
                    char c = (char)json.Read();
                    
                    if (c == '"') break;
                    
                    if (c == '\\')
                    {
                        if (json.Peek() == -1) break;
                        c = (char)json.Read();
                        
                        switch (c)
                        {
                            case '"':
                            case '\\':
                            case '/':
                                sb.Append(c);
                                break;
                            case 'b':
                                sb.Append('\b');
                                break;
                            case 'f':
                                sb.Append('\f');
                                break;
                            case 'n':
                                sb.Append('\n');
                                break;
                            case 'r':
                                sb.Append('\r');
                                break;
                            case 't':
                                sb.Append('\t');
                                break;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                
                return sb.ToString();
            }
            
            private object ParseNumber()
            {
                string number = ParseWord();
                
                if (number.Contains("."))
                {
                    double parsedDouble;
                    double.TryParse(number, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsedDouble);
                    return parsedDouble;
                }
                
                long parsedLong;
                long.TryParse(number, out parsedLong);
                return parsedLong;
            }
            
            private string ParseWord()
            {
                var sb = new System.Text.StringBuilder();
                
                while (!IsWordBreak(PeekChar()))
                {
                    sb.Append((char)json.Read());
                    if (json.Peek() == -1) break;
                }
                
                return sb.ToString();
            }
            
            private void SkipWhitespace()
            {
                while (WHITE_SPACE.IndexOf(PeekChar()) != -1)
                {
                    json.Read();
                    if (json.Peek() == -1) break;
                }
            }
            
            private char PeekChar()
            {
                return (char)json.Peek();
            }
            
            private bool IsWordBreak(char c)
            {
                return WHITE_SPACE.IndexOf(c) != -1 || WORD_BREAK.IndexOf(c) != -1;
            }
        }
        
        private sealed class Serializer
        {
            private System.Text.StringBuilder builder;
            private bool prettyPrint;
            private int indentLevel;
            
            private Serializer(bool prettyPrint)
            {
                builder = new System.Text.StringBuilder();
                this.prettyPrint = prettyPrint;
                indentLevel = 0;
            }
            
            public static string Serialize(object obj, bool prettyPrint)
            {
                var instance = new Serializer(prettyPrint);
                instance.SerializeValue(obj);
                return instance.builder.ToString();
            }
            
            private void SerializeValue(object value)
            {
                if (value == null)
                {
                    builder.Append("null");
                }
                else if (value is string)
                {
                    SerializeString((string)value);
                }
                else if (value is bool)
                {
                    builder.Append(((bool)value) ? "true" : "false");
                }
                else if (value is Dictionary<string, object>)
                {
                    SerializeObject((Dictionary<string, object>)value);
                }
                else if (value is List<object>)
                {
                    SerializeArray((List<object>)value);
                }
                else if (value is long || value is int)
                {
                    builder.Append(value);
                }
                else if (value is double || value is float)
                {
                    builder.Append(((double)value).ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                else
                {
                    SerializeString(value.ToString());
                }
            }
            
            private void SerializeObject(Dictionary<string, object> obj)
            {
                builder.Append('{');
                
                if (prettyPrint)
                {
                    builder.Append('\n');
                    indentLevel++;
                }
                
                bool first = true;
                foreach (var kvp in obj)
                {
                    if (!first)
                    {
                        builder.Append(',');
                        if (prettyPrint) builder.Append('\n');
                    }
                    
                    if (prettyPrint) Indent();
                    
                    SerializeString(kvp.Key);
                    builder.Append(':');
                    if (prettyPrint) builder.Append(' ');
                    SerializeValue(kvp.Value);
                    
                    first = false;
                }
                
                if (prettyPrint)
                {
                    builder.Append('\n');
                    indentLevel--;
                    Indent();
                }
                
                builder.Append('}');
            }
            
            private void SerializeArray(List<object> array)
            {
                builder.Append('[');
                
                bool first = true;
                foreach (var item in array)
                {
                    if (!first) builder.Append(',');
                    if (prettyPrint) builder.Append(' ');
                    
                    SerializeValue(item);
                    first = false;
                }
                
                if (prettyPrint) builder.Append(' ');
                builder.Append(']');
            }
            
            private void SerializeString(string str)
            {
                builder.Append('"');
                
                foreach (char c in str)
                {
                    switch (c)
                    {
                        case '"':
                            builder.Append("\\\"");
                            break;
                        case '\\':
                            builder.Append("\\\\");
                            break;
                        case '\b':
                            builder.Append("\\b");
                            break;
                        case '\f':
                            builder.Append("\\f");
                            break;
                        case '\n':
                            builder.Append("\\n");
                            break;
                        case '\r':
                            builder.Append("\\r");
                            break;
                        case '\t':
                            builder.Append("\\t");
                            break;
                        default:
                            builder.Append(c);
                            break;
                    }
                }
                
                builder.Append('"');
            }
            
            private void Indent()
            {
                for (int i = 0; i < indentLevel; i++)
                {
                    builder.Append("  ");
                }
            }
        }
    }
}
