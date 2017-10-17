// Copyright Matthias Koch 2017.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nuke.Core;

static class FastlaneMetadataCreator
{
    const string KcFastlaneUrl =
        "https://raw.githubusercontent.com/fastlane/fastlane/master/{0}/lib/{0}/options.rb";


    public static void CreateMetadata(string targetDir, params string[] tools)
    {
        if (tools == null || tools.Length == 0)
            tools = new[]
            {
                "cert", "deliver", "frameit", "gym", "match", "pem", "pilot", "precheck", "produce", "scan",
                "screengrab", "sigh", "snapshot", "supply"
            };


        CreateMetadataAsync(targetDir, tools).Wait();
    }

    static async Task CreateMetadataAsync(string targetDir, string[] tools)
    {
        var metadataTasks = tools
            .Select(x => GetTaskMetadata(string.Format(KcFastlaneUrl, x.ToLowerInvariant()), x, false))
            .ToList();
        var actionsMetadata = (await GetActionFiles()).Where(x => !tools.Contains(x.Key))
            .Select(x => GetTaskMetadata(x.Value, x.Key, true));

        metadataTasks.AddRange(actionsMetadata);

        var result = await Task.WhenAll(metadataTasks);
        result = result.Where(x => x != null).ToArray();

        Logger.Log($"Successfully extracted {result.Length} Tasks");
        var tool = new JObject
        {
            ["$schema"] = "./_schema.json",
            ["License"] = new JArray("Copyright Sebastian Karasek 2017.",
                "Distributed under the MIT License.",
                "https://github.com/Arodus/nuke-tools-fastlane/blob/master/LICENSE"),
            ["References"] = new JArray(result.SelectMany(x => x.References)),
            ["CustomExecutable"] = true,
            ["Name"] = "Fastlane",
            ["Tasks"] = new JArray(result.Select(x => x.TaskMetadata))
        };


        var metadataText = JsonConvert.SerializeObject(tool, Formatting.Indented,
            new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            });
        var exists = false;
        var outputFile = targetDir + Path.DirectorySeparatorChar + "Fastlane.json";
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }
        else
        {
            if (File.Exists(outputFile))
            {
                if (File.ReadAllText(outputFile) == metadataText)
                {
                    Logger.Info("Fastlane metadata is already up to date.");
                    return;
                }
                exists = true;
            }
        }


        outputFile = exists ? outputFile + ".new" : outputFile;

        File.WriteAllText(outputFile, metadataText);
        Logger.Log($"Written Metadata to {outputFile}");
    }

    static async Task<TaskMetaData> GetTaskMetadata(string url, string toolName, bool isAction)
    {
        var metadata = new TaskMetaData(toolName);
        metadata.References.Add(url);

        try
        {
            var optionsFile = await DownloadOptionsFile(toolName, url);
            var fastlaneProperties = ParseOptions(optionsFile.Value, optionsFile.Key, isAction);
            if (fastlaneProperties == null || !fastlaneProperties.Any()) return null;
            var taskArguments = CreateTaskArguments(fastlaneProperties);
            var task = CreateTask(toolName, isAction);
            var settingsClass = new JObject
            {
                ["BaseClass"] = "FastlaneBaseSettings",
                ["Properties"] = new JArray(taskArguments)
            };
            task["SettingsClass"] = settingsClass;
            metadata.TaskMetadata = task;
            return metadata;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            throw;
        }
    }

    static JObject CreateTask(string taskName, bool isAction)
    {
        var task = new JObject
        {
            ["Postfix"] =
            ConvertPascalToCamelCase(char.ToUpperInvariant(taskName[0]) + taskName.Substring(1)),
            ["DefiniteArgument"] = isAction ? "run " + taskName.ToLowerInvariant() : taskName.ToLowerInvariant()
        };
        return task;
    }


    static async Task<IEnumerable<KeyValuePair<string, string>>> GetActionFiles()
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri("https://api.github.com"),
            DefaultRequestHeaders =
            {
                {"User-Agent", "nuke.build Tool Generator"},
                {"Accept", "application/vnd.github.v3+json"}
            }
        };

        Logger.Info("Searching for Fastlane actions.");
        var response = await client.GetAsync("repos/fastlane/fastlane/contents/fastlane/lib/fastlane/actions");
        response.EnsureSuccessStatusCode();

        var actions = JArray.Parse(await response.Content.ReadAsStringAsync());
        var actionUrls = actions.Where(x => x.Value<string>("name").EndsWith(".rb"))
            .Select(x =>
                new KeyValuePair<string, string>(
                    x.Value<string>("name").Substring(0, x.Value<string>("name").Length - 3),
                    x.Value<string>("download_url")));
        var keyValuePairs = actionUrls as KeyValuePair<string, string>[] ?? actionUrls.ToArray();
        Logger.Info($"Found {keyValuePairs.Length} actions.");
        return keyValuePairs;
    }


    static async Task<KeyValuePair<string, string>> DownloadOptionsFile(string name, string url)
    {
        Logger.Info($"Downloading options for {name}");
        var client = new HttpClient {BaseAddress = new Uri(url)};
        var response = await client.GetAsync("");
        var content = await response.Content.ReadAsStringAsync();


       
        return new KeyValuePair<string, string>(name, content);
    }

    static string ConvertPascalToCamelCase(string value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));
        return value.Split(separator: '_')
            .Aggregate("", (s, s1) => s + char.ToUpper(s1[0]) + s1.Substring(1));
    }

    static List<FastlaneProperty> ParseOptions(string optionsFile, string optionName, bool isAction)
    {
        var lines = optionsFile.Split('\n');


        var propRegion = GetPropertyRegion(lines);
        var region = propRegion as IList<string> ?? propRegion.ToList();
        if (!region.Any())
            return null;
        var propertyLines = ParsePropertiesFromRegion(region);
        var propJSons = propertyLines.Select(ParsePropertyToJson)
            .Where(s => !string.IsNullOrEmpty(s)).ToList();


        var fastlaneProperties = new List<FastlaneProperty>();
        foreach (var propJSon in propJSons)
            try
            {
                var obj = JsonConvert.DeserializeObject<FastlaneProperty>(propJSon);
                obj.IsAction = isAction;


                fastlaneProperties.Add(obj);
            }
            catch (JsonReaderException e)
            {
                var startIndex = e.LinePosition < 45 ? 0 : e.LinePosition - 45;
                var length = startIndex + 45 >= propJSon.Length ? propJSon.Length - startIndex : 45;
                Logger.Warn("Error at:" + optionName + Environment.NewLine + e.Message + "    " +
                            propJSon.Substring(startIndex, length));
            }

        var opts = fastlaneProperties
            .Where(fastlaneProperty => fastlaneProperty != null)
            .ToList();

        return opts;
    }


    static JArray CreateTaskArguments([NotNull] List<FastlaneProperty> fastlaneProperties)
    {
        if (fastlaneProperties == null) throw new ArgumentNullException(nameof(fastlaneProperties));
        var array = new JArray();
        foreach (var fastlaneProperty in fastlaneProperties)
        {
            var toolArgument = new JObject();

            var name = ConvertPascalToCamelCase(fastlaneProperty.Name);
            name = name.Equals("readonly", StringComparison.InvariantCultureIgnoreCase) ? "ReadOnlyFlag" : name;
            name = name.Equals("private", StringComparison.InvariantCultureIgnoreCase) ? "PrivateFlag" : name;
            name = name.Equals("params", StringComparison.InvariantCultureIgnoreCase) ? "ParamsValue" : name;
            name = name.Equals("Base", StringComparison.InvariantCultureIgnoreCase) ? "BaseValue" : name;


            toolArgument["Name"] = name;
            toolArgument["Format"] = fastlaneProperty.IsAction
                ? fastlaneProperty.Name + ":{value}"
                : "--" + fastlaneProperty.Name + "={value}";
            toolArgument["Secret"] = fastlaneProperty.Sensitive;
            toolArgument["Help"] = fastlaneProperty.Description;


            if (fastlaneProperty.IsStringArray)
            {
                toolArgument["Type"] = "List<string>";
                toolArgument["Separator"] = ',';
            }

            else if (!fastlaneProperty.IsString)
            {
                toolArgument["Type"] = "bool";
            }
            else if (fastlaneProperty.Type == "Array")
            {
                toolArgument["Type"] = "List<string>";
                toolArgument["Separator"] = ',';
            }
            else
            {
                toolArgument["Type"] = "string";
            }
            array.Add(toolArgument);
        }
        return array;
    }

    static string ParsePropertyToJson(IEnumerable<string> propertyLines)
    {
        var props = new List<string>();
        var propertyBlock = propertyLines.ToList();


        if (!propertyBlock.Any()) return null;
        var firstLine = Regex.Replace(propertyBlock.First(), "\\s*FastlaneCore::ConfigItem\\.new\\(", "");
        propertyBlock[0] = firstLine;

        var defaultIntend = -1;
        foreach (var t in propertyBlock)
        {
            var currentLine = t;
            if (currentLine == null) continue;

            var keyMatches = Regex.Match(currentLine, @"\s*key: :([a-z_0-9]+),");
            if (keyMatches.Success)
            {
                props.Add($"\"name\":\"{keyMatches.Groups[1].Value}\"");
                continue;
            }


            if (defaultIntend != -1 && Regex.IsMatch(currentLine, $"^ {{{defaultIntend + 1},}}"))
                continue;

            if (Regex.IsMatch(currentLine, @"\s+end")) continue;
            if (defaultIntend == -1)
                defaultIntend = Regex.Match(currentLine, @"^(\s+)").Groups[1].Length;
            var lastIndex = currentLine.LastIndexOf('#');
            if (lastIndex > currentLine.LastIndexOfAny(new[] {'\'', '"'}))
                currentLine = currentLine.Substring(0, lastIndex);
            currentLine = currentLine.TrimEnd(',', ')', '\r');
            var split = currentLine.Split(':');
            if (split.Length < 2) continue;

            var name = split.First().Trim();

            var value = split.Skip(1).Aggregate("", (s, s1) => s + s1).Trim();
            if (value.StartsWith("'"))
            {
                value = "\"" + value.Substring(1, value.Length - 2).Replace("\"", "\\\"") + "\"";
            }
            else if (value.StartsWith("\""))
            {
                //Todo multi line comment

                if (!value.EndsWith("\"") || value.EndsWith("\"\\"))
                {
                    if (value.EndsWith("\\"))
                        value = value.TrimEnd('\\', '"');

                    value += "\"";
                }

                var match = Regex.Match(value, "(.*?\")(.*)(\".*)");
                value = match.Groups[1].Value +
                        Regex.Replace(match.Groups[2].Value, "(?<!\\\\)\"", "\\\"") +
                        match.Groups[3].Value;
                value = Regex.Replace(value, @"(?<=[^\\])\\(?![""\\])", "\\\\");
            }
            else if (value == "nil")
            {
                value = "\"\"";
            }
            else
            {
                if (!Regex.IsMatch(value, @"^(true|false|\-?\d+)$")) continue;
            }

            props.Add($"\"{name}\":{value}");
        }

        if (!props.Any())
            return null;
        var propJson = props.Skip(1).Aggregate("{" + props[0], (x1, x2) => x1 + "," + x2);
        propJson += "}";
        return propJson;
    }

    static IEnumerable<string> GetPropertyRegion(IEnumerable<string> lines)
    {
        var start = false;
        var optionsStart = false;
        foreach (var line in lines)
        {
            if (!start)
            {
                if (line.Contains("def self.available_options"))
                    start = true;
                continue;
            }
            if (!optionsStart)
            {
                if (line.Trim().StartsWith("[") || line.Trim().EndsWith("["))
                    optionsStart = true;
            }
            else
            {
                if (line.Trim().StartsWith("]")) break;
                yield return line;
            }
        }
    }

    static IEnumerable<List<string>> ParsePropertiesFromRegion(IEnumerable<string> lines)
    {
        var blocks = new List<List<string>>();
        var inBlock = false;
        var currentBlock = new List<string>();
        foreach (var line in lines)
        {
            if (line == "") continue;
            if (line.StartsWith("#")) continue;

            if (line.Trim().StartsWith("FastlaneCore::ConfigItem.new"))
            {
                if (inBlock)
                {
                    blocks.Add(currentBlock);
                    currentBlock = new List<string>();
                }
                inBlock = true;
            }
            currentBlock.Add(line);
        }
        blocks.Add(currentBlock);
        return blocks;
    }

    class TaskMetaData
    {
        public TaskMetaData(string toolName)
        {
            ToolName = toolName;
            References = new List<string>();
        }

        public List<string> References { get; }
        public JToken TaskMetadata { get; set; }

        public string ToolName { get; }
    }

    public class FastlaneProperty
    {
        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("env_name")]
        public string EnvName { get; set; }

        [JsonIgnore]
        public bool IsAction { get; set; }

        [DefaultValue(true)]
        [JsonProperty("is_string", DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool IsString { get; set; }

        [JsonIgnore]
        public bool IsStringArray
        {
            get
            {
                if (Type != null && Type == "Array")
                    return false;
                if (Description == null)
                    return false;
                var help = Description.ToLowerInvariant();
                return help.Contains("comma-separated") || help.Contains("comma separated");
            }
        }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("optional")]
        public bool Optional { get; set; }

        [JsonProperty("sensitive")]
        public bool Sensitive { get; set; }

        [JsonProperty("short_option")]
        public string ShortOption { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }
}