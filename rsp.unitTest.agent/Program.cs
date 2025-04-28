// See https://aka.ms/new-console-template for more information

using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;
using rsp.unitTest.agent.Analyzer;
using rsp.unitTest.agent.Extension;
using Console = System.Console;
using Task = System.Threading.Tasks.Task;

const string modelId = "gpt-4o";
const string openAiKey = "";

var kernel = Kernel.CreateBuilder().AddOpenAIChatCompletion(modelId, openAiKey, serviceId: "openai-gpt4o")
    .AddDeepSeekChatCompletion("deepseek-ai/DeepSeek-R1-Distill-Qwen-32B" , 
        "", serviceId: "deepseek").Build();
//var kernel = Kernel.CreateBuilder().AddOpenAIChatCompletion(modelID, openAIKey,serviceId:"openai-gpt4o").Build();

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

UnitTestAnalyzer unitTest =  new UnitTestAnalyzer();
var chat = await unitTest.GetUnitTestGroupChat(kernel);
//添加聊天消息
var chatMessage = new ChatMessageContent(AuthorRole.User,
    """
    以下是一个C#类文件的代码片段。
    public class GetDataCategoryFilterAction: SqlActionBase<ResponseVO<IEnumerable<GetDataCategoryFilterVo>>>
    {
        
        public GetDataCategoryFilterAction(IADOConfigurable adoConfigurable) : base(adoConfigurable)
        {
        }
    
        protected override ResponseVO<IEnumerable<GetDataCategoryFilterVo>> RunSqlDataOperation(ILoggingLogger loggingLogger)
        {
            var responseVo = new ResponseVO<IEnumerable<GetDataCategoryFilterVo>>();
            
            string sql = GetCategorySql();
            SqlParameter[] parameters = GetCategoryParameters();
            var categoryFilterVos = base.RunListQuery<GetDataCategoryFilterVo>(sql, parameters);
            
            responseVo.Data = categoryFilterVos;
            responseVo.SetSuccessed();
    
            return responseVo;
        }
        
        /// <summary>
        /// Get the DataDictionaries ParentDataLabel Sql
        /// </summary>
        /// <returns>SQL</returns>
        private string GetCategorySql()
        {
            return @"SELECT c.DataDictionaryCategoryId, c.DataDictionaryCategoryName 
    FROM DataDictionaryCategory c 
    WHERE c.StatusId = @ActiveStatsdId
    ORDER BY c.DataDictionaryCategoryName";
        }
    
        /// <summary>
        /// Parameters
        /// </summary>
        /// <returns>SqlParameters</returns>
        private SqlParameter[] GetCategoryParameters()
        {
            return
            [
                new SqlParameter("@ActiveStatsdId",Status.Active.ToEnumString()),
            ];
        }
    }
    """
);

chat.AddChatMessage(chatMessage);
Console.WriteLine(chatMessage);
var lastAgent = string.Empty;
Console.WriteLine();
// 这里使用了异步流来处理代理的消息。
await foreach (var response in chat.InvokeStreamingAsync())
{
    if (string.IsNullOrEmpty(response.Content))
    {
        continue;
    }

    //输入角色和代理名称
    if (!lastAgent.Equals(response.AuthorName, StringComparison.Ordinal))
    {
        Console.WriteLine($"\n# {response.Role} - {response.AuthorName ?? "*"}:");
        lastAgent = response.AuthorName ?? string.Empty;
        
    }
    
    Console.Write(response.Content);
}


// Add this after the chat streaming completes
var dataGeneratorOutput = await chat.GetChatMessagesAsync()
    .Where(m => m.AuthorName == "DataGenerator")
    .LastOrDefaultAsync();

if (dataGeneratorOutput != null)
{
    await unitTest.SaveGeneratedJsonDataAsync(dataGeneratorOutput.Content);
}

// You can also keep the test file saving code
var testWriterOutput = await chat.GetChatMessagesAsync()
    .Where(m => m.AuthorName == "TestWriter")
    .LastOrDefaultAsync();

if (testWriterOutput != null)
{
    await unitTest.SaveGeneratedTestFileAsync(testWriterOutput.Content);
}


//ChatMessageContent[] history = await chat.GetChatMessagesAsync().Reverse().ToArrayAsync();
//全部的内容
//for (int index = 0; index < history.Length; index++)
//{
//    Console.WriteLine(history[index]);
//}
Console.WriteLine($"\n[已完成: {chat.IsComplete}]");



[Experimental("SKEXP0110")]
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