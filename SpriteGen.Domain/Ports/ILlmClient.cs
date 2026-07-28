using System.Threading.Tasks;

namespace SpriteGen.Domain.Ports;

public interface ILlmClient
{
    Task<T> CompleteAsync<T>(string systemPrompt, string userMessage)
        where T : class, new();
}