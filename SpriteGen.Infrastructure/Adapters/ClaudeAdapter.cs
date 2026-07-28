using Anthropic;
using Anthropic.Models.Messages;
using Anthropic.Services;
using SpriteGen.Domain.Models;
using System.Text.Json;

namespace SpriteGen.Infrastructure.Adapters;

public class ClaudeAdapter : LlmAdapterBase
{
    private readonly AnthropicClient _client;
    private const string ModelName = "claude-opus-5";

    public ClaudeAdapter(string apiKey, SpriteDimensions dimensions) : base(dimensions)
    {
        _client = new AnthropicClient
        {
            ApiKey = apiKey,
            Timeout = TimeSpan.FromSeconds(300)
        };
    }

    protected override async Task<string?> CallApiAsync(string userMessage)
    {
        var parameters = new MessageCreateParams
        {
            Model = ModelName,
            MaxTokens = 16384,
            System = BuildSystemPrompt(),
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = userMessage
                }
            ]
        };

        var message = await _client.Messages.Create<SpriteResponse>(parameters);

        foreach (var block in message.Content)
        {
            var parsed = block.Parsed();
            if (parsed is not null)
                return JsonSerializer.Serialize(parsed);
        }

        return null;
    }
}