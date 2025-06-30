using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using rsp.unitTest.agent.Analyzer;
using rsp.unitTest.agent.Extension;
using rsp.unitTest.agent.AgentGroup;

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
    var sampleArticle = @"
人工智能技术在现代社会中发挥着越来越重要的作用。随着计算能力的提升和算法的优化，人工智能已经在医疗、教育、金融等多个领域取得了显著的进展。

在医疗领域，人工智能辅助诊断系统能够帮助医生更准确地识别疾病，提高诊断效率。通过分析大量的医学影像数据，AI系统可以发现人眼难以察觉的细微变化，为早期诊断提供重要支持。

教育领域也受益于人工智能的发展。个性化学习平台能够根据学生的学习进度和能力特点，提供定制化的学习内容和方案。这种智能化的教学方式不仅提高了学习效率，还能够激发学生的学习兴趣。

在金融行业，人工智能被广泛应用于风险评估、欺诈检测和智能投顾等方面。通过分析用户的交易行为和信用记录，AI系统能够快速评估信贷风险，为金融机构的决策提供数据支撑。

然而，人工智能的发展也带来了一些挑战。数据隐私保护、算法公平性和就业影响等问题需要我们认真思考和解决。只有在确保技术安全可控的前提下，人工智能才能真正造福人类社会。

展望未来，人工智能将继续深度融入我们的生活和工作中。随着技术的不断完善，我们有理由相信，人工智能将为人类创造更加美好的未来。


人工智能作为当今社会变革的重要推动力，其发展历程备受关注。伴随计算资源的不断增强与算法技术的持续革新，AI已在包括医疗、教育、金融等多个行业展现出卓越成效，推动了这些领域的深刻变革。

在医疗行业，人工智能驱动的辅助诊断工具正为医生提供强有力的技术支持，使疾病识别过程更加精准高效。借助对海量医学影像数据的深入分析，这些AI系统能够捕捉到传统视检难以发现的细微异常，从而为疾病的早期发现和及时治疗提供了关键保障。

同样地，人工智能的进步也正在深刻地影响教育行业。基于AI的个性化学习系统可以依据学生的实际掌握情况和个人能力，动态调整教学内容与学习路径。这类智能教学平台不仅有效提升了学习成效，还能够增强学生的主动性和学习动力。

在金融领域，人工智能正被深度融合到风险管理、反欺诈监控及智能投资顾问等多个环节。AI系统能够对用户的交易模式与信用数据进行深入挖掘与分析，从而高效识别潜在信贷风险，并为金融机构的决策流程提供科学、及时的数据支持。

在人工智能迅速发展的同时，也伴随着诸多挑战亟待应对，例如如何保护数据隐私、保障算法的公正性以及应对人工智能对就业结构带来的冲击。只有在确保相关技术具备安全性和可控性的基础之上，人工智能才能真正为社会带来持续且积极的价值。

展望未来，人工智能势必将在我们的日常生活与各行各业中实现更为深入的融合。伴随着相关技术的持续优化，人工智能有望为人类社会带来更加积极和深远的变革，助力我们迈向更加美好的明天。


";

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
