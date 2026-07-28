using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.GenAI;
using Google.GenAI.Types;
using SpriteGen.Domain.Ports;
using Type = Google.GenAI.Types.Type;

namespace SpriteGen.Infrastructure.Llm;

public class GeminiLlmClient : ILlmClient
{
    private readonly Client _client;
    private readonly string _model;

    public GeminiLlmClient(string apiKey, string model)
    {
        _client = new Client(
            apiKey: apiKey,
            httpOptions: new HttpOptions { Timeout = 300_000 }
        );
        _model = model;
    }

    public async Task<T> CompleteAsync<T>(string systemPrompt, string userMessage)
        where T : class, new()
    {
        var config = new GenerateContentConfig
        {
            SystemInstruction = new Content
            {
                Parts = new List<Part> { new Part { Text = systemPrompt } }
            },
            Temperature = 0.7,
            ResponseMimeType = "application/json",
            ResponseSchema = BuildSchema(typeof(T))
        };

        var response = await _client.Models.GenerateContentAsync(
            model: _model,
            contents: userMessage,
            config: config
        );

        var text = response.Candidates?[0].Content?.Parts?[0].Text;
        if (string.IsNullOrEmpty(text))
            throw new InvalidOperationException("Model returned empty response.");

        var result = JsonSerializer.Deserialize<T>(text);
        if (result is null)
            throw new InvalidOperationException("Failed to deserialize model response.");

        return result;
    }

    private static Schema BuildSchema(System.Type type)
    {
        if (type == typeof(string))
            return new Schema { Type = Type.String };

        if (type == typeof(int) || type == typeof(long))
            return new Schema { Type = Type.Integer };

        if (type == typeof(double) || type == typeof(float))
            return new Schema { Type = Type.Number };

        if (type == typeof(bool))
            return new Schema { Type = Type.Boolean };

        if (type.IsArray)
        {
            return new Schema
            {
                Type = Type.Array,
                Items = BuildSchema(type.GetElementType()!)
            };
        }

        // Object: reflect public properties, honoring JsonPropertyName
        var properties = new Dictionary<string, Schema>();
        var required = new List<string>();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var jsonName = prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                           ?? ToCamelCase(prop.Name);

            properties[jsonName] = BuildSchema(prop.PropertyType);
            required.Add(jsonName);
        }

        return new Schema
        {
            Type = Type.Object,
            Properties = properties,
            Required = required
        };
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}