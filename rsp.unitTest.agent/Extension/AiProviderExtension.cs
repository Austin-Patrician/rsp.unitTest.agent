using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace rsp.unitTest.agent.Extension;

public static class AiProviderExtension
{
    /// <summary>
    /// 这里去扩展DeepSeek的ChatCompletion，因为其协议适配OPENAI，直接修改URL 和key就好
    /// </summary>
    /// <param name="kernelBuilder"></param>
    /// <param name="modelId"></param>
    /// <param name="apiKey"></param>
    /// <param name="serviceId"></param>
    /// <param name="httpClient"></param>
    /// <returns></returns>
    public static IKernelBuilder AddDeepSeekChatCompletion(
        this IKernelBuilder kernelBuilder,
        string modelId,
        string apiKey,
        string? serviceId = null,
        HttpClient? httpClient = null)
    {
        // Define the endpoint for DeepSeek API
        Uri endpoint = new Uri("https://api.token-ai.cn/v1");

        kernelBuilder.Services.AddKeyedSingleton<IChatCompletionService>(serviceId, (_, _) => new OpenAIChatCompletionService(modelId: modelId, endpoint: endpoint, apiKey: apiKey));
        
        return kernelBuilder;
    }
}

