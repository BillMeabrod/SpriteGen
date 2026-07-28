using Anthropic;
using Anthropic.Models.Messages;
using Anthropic.Services;
using SpriteGen.Domain.Ports;

namespace SpriteGen.Infrastructure.Llm;

public class ClaudeLlmClient : ILlmClient
{
    private readonly AnthropicClient _client;
    private readonly string _model;
    private readonly int _maxTokens;

    public ClaudeLlmClient(string apiKey, string model, int maxTokens = 16384)
    {
        _client = new AnthropicClient
        {
            ApiKey = apiKey,
            Timeout = TimeSpan.FromSeconds(300)
        };
        _model = model;
        _maxTokens = maxTokens;
    }

    public async Task<T> CompleteAsync<T>(string systemPrompt, string userMessage)
        where T : class, new()
    {
        var parameters = new MessageCreateParams
        {
            Model = _model,
            MaxTokens = _maxTokens,
            System = systemPrompt,
            Messages =
            [
                new()
                {
                    Role = Role.User,
                    Content = userMessage
                }
            ]
        };

        var message = await _client.Messages.Create<T>(parameters);

        foreach (var block in message.Content)
        {
            var parsed = block.Parsed();
            if (parsed is not null)
                return parsed;
        }

        throw new InvalidOperationException("Model returned no parseable structured content.");
    }
}