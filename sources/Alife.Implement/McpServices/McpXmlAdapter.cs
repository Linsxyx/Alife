using System.Text.Json;
using Alife.Function.Interpreter;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Alife.Implement;

public static class McpXmlAdapter
{
    public static async Task<(McpClient Client, XmlHandler Handler)> CreateAsync(
        McpServerConfig config,
        Action<string, string>? resultCallback = null)
    {
        StdioClientTransport clientTransport = new(new StdioClientTransportOptions {
            Name = config.Name,
            Command = config.Command,
            Arguments = config.Arguments
        });
        McpClient client = await McpClient.CreateAsync(clientTransport);
        IList<McpClientTool> tools = await client.ListToolsAsync();

        List<XmlFunction> functions = new();
        foreach (McpClientTool tool in tools)
        {
            XmlFunction function = BuildFunction(tool, client, resultCallback);
            functions.Add(function);
        }

        XmlHandler handler = new() {
            Name = config.Name,
            Description = config.Description,
            Functions = functions,
            Instance = client,
        };

        return (client, handler);
    }

    static XmlFunction BuildFunction(McpClientTool tool, McpClient client, Action<string, string>? resultCallback)
    {
        string name = tool.Name.ToLower();
        string description = tool.Description;
        List<XmlParameter> parameters = ParseInputSchema(tool);

        async Task Invoker(XmlContext context)
        {
            Dictionary<string, object?> arguments = new();
            foreach ((string key, string value) in context.Parameters)
                arguments[key] = value;

            CallToolResult result = await client.CallToolAsync(tool.Name, arguments);

            string resultText = string.Join("\n",
                result.Content
                    .Where(block => block is TextContentBlock)
                    .Select(block => ((TextContentBlock)block).Text));

            if (result.IsError == true)
                throw new Exception(resultText);

            resultCallback?.Invoke(name, resultText);
        }

        return new XmlFunction {
            Name = name,
            Description = description,
            Parameters = parameters,
            Invoker = Invoker,
        };
    }

    static List<XmlParameter> ParseInputSchema(McpClientTool tool)
    {
        List<XmlParameter> parameters = new();

        JsonElement schema = tool.JsonSchema;
        if (schema.TryGetProperty("properties", out JsonElement properties) == false)
            return parameters;

        JsonElement? requiredArray = schema.TryGetProperty("required", out JsonElement req) ? req : null;
        HashSet<string> requiredSet = new();
        if (requiredArray != null)
        {
            foreach (JsonElement item in requiredArray.Value.EnumerateArray())
            {
                string? reqName = item.GetString();
                if (reqName != null)
                    requiredSet.Add(reqName);
            }
        }

        foreach (JsonProperty prop in properties.EnumerateObject())
        {
            string paramName = prop.Name.ToLower();
            string paramType = "String";
            string? paramDescription = null;

            if (prop.Value.TryGetProperty("type", out JsonElement typeElem))
                paramType = typeElem.GetString() ?? "String";
            if (prop.Value.TryGetProperty("description", out JsonElement descElem))
                paramDescription = descElem.GetString();

            bool isRequired = requiredSet.Contains(prop.Name);
            if (isRequired == false)
                paramType += "[可选]";

            parameters.Add(new XmlParameter {
                Name = paramName,
                Description = paramDescription,
                Type = paramType,
            });
        }

        return parameters;
    }
}
