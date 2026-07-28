using Google.GenAI;
using Google.GenAI.Types;
using SpriteGen.Domain.Models;
using Type = Google.GenAI.Types.Type;

namespace SpriteGen.Infrastructure.Adapters;

public class GeminiAdapter : LlmAdapterBase
{
    private readonly Client _client;
    private const string ModelName = "gemini-3.5-flash";

    public GeminiAdapter(string apiKey, SpriteDimensions dimensions) : base(dimensions)
    {
        _client = new Client(
            apiKey: apiKey,
            httpOptions: new HttpOptions
            {
                Timeout = 300_000
            }
        );
    }

    protected override async Task<string?> CallApiAsync(string userMessage)
    {
        var config = new GenerateContentConfig
        {
            SystemInstruction = new Content
            {
                Parts = new List<Part> { new Part { Text = BuildSystemPrompt() } }
            },
            Temperature = 0.7,
            ResponseMimeType = "application/json",
            ResponseSchema = new Schema
            {
                Type = Type.Object,
                Properties = new Dictionary<string, Schema>
                {
                    {
                        "palette", new Schema
                        {
                            Type = Type.Array,
                            Items = new Schema { Type = Type.String }
                        }
                    },
                    {
                        "rows", new Schema
                        {
                            Type = Type.Array,
                            Items = new Schema
                            {
                                Type = Type.Array,
                                Items = new Schema { Type = Type.Integer }
                            }
                        }
                    }
                },
                Required = new List<string> { "palette", "rows" }
            }
        };

        var response = await _client.Models.GenerateContentAsync(
            model: ModelName,
            contents: userMessage,
            config: config
        );

        return response.Candidates?[0].Content?.Parts?[0].Text;
    }
}