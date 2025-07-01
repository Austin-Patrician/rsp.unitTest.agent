using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using rsp.unitTest.agent.Analyzer;
using rsp.unitTest.agent.Extension;
using rsp.unitTest.agent.AgentGroup;
using rsp.unitTest.agent.Tools;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

const string modelId = "gpt-4o";
const string openAiKey = "";

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:5002");

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Register UnitTestAnalyzer as a service
builder.Services.AddSingleton<UnitTestAnalyzer>();

// Register Semantic Kernel with optimized configuration for article rewriting
builder.Services.AddSingleton(sp => Kernel.CreateBuilder()
    .AddOpenAIChatCompletion(modelId, openAiKey, serviceId: "openai-gpt4o")
    .AddOpenAIChatCompletion("gpt-3.5-turbo", openAiKey, serviceId: "openai-gpt-3.5-turbo")
    .AddDeepSeekChatCompletion("deepseek-ai/DeepSeek-R1-Distill-Qwen-32B",
        "", serviceId: "deepseek")
    .Build());

// Register ArticleRewriter as a service
builder.Services.AddSingleton<ArticleRewriter>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.MapControllers();

// Add article rewriting endpoint
app.MapPost("/rewrite-article", async (HttpRequest request, ArticleRewriter rewriter) =>
{
    try
    {
        using var reader = new StreamReader(request.Body);
        var originalArticle = await reader.ReadToEndAsync();
        
        if (string.IsNullOrWhiteSpace(originalArticle))
        {
            return Results.BadRequest("文章内容不能为空");
        }
        
        Console.WriteLine($"📝 收到重写请求，文章长度: {originalArticle.Length} 字");
        
        // 使用多Agent并行重写
        var rewrittenArticle = await rewriter.RewriteArticleAsync(originalArticle);
        
        return Results.Ok(new
        {
            success = true,
            originalLength = originalArticle.Length,
            rewrittenLength = rewrittenArticle.Length,
            rewrittenArticle = rewrittenArticle,
            message = "文章重写完成"
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ 重写失败: {ex.Message}");
        return Results.Problem($"重写失败: {ex.Message}");
    }
});

// Add group chat rewriting endpoint
app.MapPost("/rewrite-article-groupchat", async (HttpRequest request, ArticleRewriter rewriter) =>
{
    try
    {
        using var reader = new StreamReader(request.Body);
        var originalArticle = await reader.ReadToEndAsync();
        
        if (string.IsNullOrWhiteSpace(originalArticle))
        {
            return Results.BadRequest("文章内容不能为空");
        }
        
        Console.WriteLine($"🎭 使用GroupChat模式重写，文章长度: {originalArticle.Length} 字");
        
        // 使用AgentGroupChat协作重写
        var rewrittenArticle = await rewriter.RewriteWithGroupChatAsync(originalArticle);
        
        return Results.Ok(new
        {
            success = true,
            originalLength = originalArticle.Length,
            rewrittenLength = rewrittenArticle.Length,
            rewrittenArticle = rewrittenArticle,
            message = "GroupChat协作重写完成"
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ GroupChat重写失败: {ex.Message}");
        return Results.Problem($"GroupChat重写失败: {ex.Message}");
    }
});

// 添加文章重写示例演示
app.MapGet("/demo-rewrite", async (ArticleRewriter rewriter) =>
{

    var path = "C:\\Users\\Austin_Zhang\\Downloads\\新建 Microsoft Word 文档.docx";
    
    if (!File.Exists(path))
    {
        return Results.NotFound("演示文章文件不存在，请检查路径");
    }
    var sampleArticle = WordParser.ReadWordFileContent(path);

    Console.WriteLine("🚀 开始演示文章重写...");
    var result = await rewriter.RewriteArticleAsync(sampleArticle);
    
    return Results.Ok(new
    {
        originalArticle = sampleArticle,
        rewrittenArticle = result,
        originalLength = sampleArticle.Length,
        rewrittenLength = result.Length,
        demo = true
    });
});

Console.WriteLine("🔥 文章重写服务已启动!");
Console.WriteLine("📍 API端点:");
Console.WriteLine("   POST /rewrite-article - 多Agent并行重写");
Console.WriteLine("   POST /rewrite-article-groupchat - AgentGroupChat协作重写");
Console.WriteLine("   GET  /demo-rewrite - 演示重写功能");
Console.WriteLine("🌐 服务地址: http://localhost:5000");

app.Run();


// Method containing the existing logic repurposed for API use
async Task<(string testFile, string jsonData, Dictionary<string, string> agentMessages)> AnalyzeCode(
    UnitTestAnalyzer unitTest, Kernel kernel, string code)
{
    var chat = await unitTest.GetUnitTestGroupChat(kernel, code);

    // Add the chat message with the code content
    //var chatMessage = new ChatMessageContent(AuthorRole.User, code);
    //chat.AddChatMessage(chatMessage);

    var agentMessages = new Dictionary<string, string>();
    var lastAgent = string.Empty;

    // Process the chat responses
    await foreach (var response in chat.InvokeStreamingAsync())
    {
        if (string.IsNullOrEmpty(response.Content))
            continue;

        // Store each agent's complete message
        if (!agentMessages.ContainsKey(response.AuthorName ?? "Unknown"))
            agentMessages[response.AuthorName ?? "Unknown"] = response.Content;
        else
            agentMessages[response.AuthorName ?? "Unknown"] += response.Content;
    }

    // Extract and save generated content
    string jsonData = string.Empty;
    string testFile = string.Empty;

    var dataGeneratorOutput = await chat.GetChatMessagesAsync()
        .Where(m => m.AuthorName == "DataGenerator")
        .LastOrDefaultAsync();

    if (dataGeneratorOutput != null)
    {
        jsonData = await unitTest.SaveGeneratedJsonDataAsync(dataGeneratorOutput.Content);
    }

    var testWriterOutput = await chat.GetChatMessagesAsync()
        .Where(m => m.AuthorName == "TestWriter")
        .LastOrDefaultAsync();

    if (testWriterOutput != null)
    {
        testFile = await unitTest.SaveGeneratedTestFileAsync(testWriterOutput.Content);
    }

    return (testFile, jsonData, agentMessages);
}

public class LastAgentTerminationStrategy : TerminationStrategy
{
    private readonly Agent _lastAgent;

    public LastAgentTerminationStrategy(Agent lastAgent)
    {
        _lastAgent = lastAgent;
    }

    protected override Task<bool> ShouldAgentTerminateAsync(
        Agent agent,
        IReadOnlyList<ChatMessageContent> history,
        CancellationToken cancellationToken)
    {
        if (history.Count < 2)
            return Task.FromResult(false);

        // 检查最后发言的是否是指定的最后一个agent
        var lastMessage = history[history.Count - 1];
        return Task.FromResult(lastMessage.AuthorName == _lastAgent.Name);
    }
}
