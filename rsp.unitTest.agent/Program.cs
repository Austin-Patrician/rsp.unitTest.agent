using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using rsp.unitTest.agent.Analyzer;
using rsp.unitTest.agent.Extension;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

const string modelId = "gpt-4o";
const string openAiKey = "";

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:5000");
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

// Register Semantic Kernel
builder.Services.AddSingleton(sp => Kernel.CreateBuilder()
    .AddOpenAIChatCompletion(modelId, openAiKey, serviceId: "openai-gpt4o")
    .AddDeepSeekChatCompletion("deepseek-ai/DeepSeek-R1-Distill-Qwen-32B",
        "", serviceId: "deepseek")
    .Build());

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.UseHttpsRedirection();
app.UseCors("AllowAll");

// Define file upload endpoint
app.MapPost("/api/analyze-code", async (HttpRequest request, UnitTestAnalyzer unitTest, Kernel kernel) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest("Form content type expected.");

    var form = await request.ReadFormAsync();
    var files = form.Files;

    if (files == null || files.Count == 0)
        return Results.BadRequest("No files uploaded.");

    // Concatenate all uploaded file contents
    var dtoFiles = files.FirstOrDefault(_ => _.Name == "dto")!;
    var voFiles = files.FirstOrDefault(_ => _.Name == "vo")!;
    var actionFiles = files.FirstOrDefault(_ => _.Name == "action")!;

    var codeContent = new StringBuilder("以下是具体的Action,DTO,VO的内容：");
    codeContent.AppendLine("以下是DTO的内容：");
    using var dtoReader = new StreamReader(dtoFiles.OpenReadStream());
    codeContent.AppendLine(await dtoReader.ReadToEndAsync());

    using var voReader = new StreamReader(voFiles.OpenReadStream());
    codeContent.AppendLine();
    codeContent.AppendLine("以下是VO的内容：");
    codeContent.AppendLine(await voReader.ReadToEndAsync());
    codeContent.AppendLine();

    using var actionReader = new StreamReader(actionFiles.OpenReadStream());
    codeContent.AppendLine();
    codeContent.AppendLine("以下是Action的内容：");
    codeContent.AppendLine(await actionReader.ReadToEndAsync());

    string code = codeContent.ToString();
    // Process the code using the existing functionality
    var result = await AnalyzeCode(unitTest, kernel, code);

    return Results.Ok(new
    {
        message = "Analysis completed successfully",
        testFileGenerated = result.testFile,
        jsonDataGenerated = result.jsonData,
        agents = result.agentMessages
    });
});

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