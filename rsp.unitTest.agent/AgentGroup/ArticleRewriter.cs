using System.Collections.Concurrent;
using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;

namespace rsp.unitTest.agent.AgentGroup;

public class ArticleRewriter
{
    private readonly Kernel _kernel;
    
    public ArticleRewriter(Kernel kernel)
    {
        _kernel = kernel;
    }
    
    /// <summary>
    /// 高效重写8000字文章的主要方法
    /// </summary>
    public async Task<string> RewriteArticleAsync(string originalArticle)
    {
        Console.WriteLine("🚀 开始智能文章重写流程...");
        
        // 1. 创建Agent团队
        var agentTeam = await CreateAgentTeam();
        Console.WriteLine("✅ Agent团队创建完成");
        
        // 2. 智能分析和分割
        Console.WriteLine("📊 开始文章结构分析...");
        var textBlocks = await TextProcessor.SplitTextIntelligently(
            originalArticle, agentTeam.AnalyzerAgent, agentTeam.DispatcherAgent);
        Console.WriteLine($"✂️ 文章已智能分割为 {textBlocks.Count} 个语义完整的块");
        
        // 3. 并行重写处理
        Console.WriteLine("🔄 开始多Agent并行重写...");
        var rewrittenBlocks = await ParallelRewriteBlocks(textBlocks, agentTeam.RewriterAgents);
        Console.WriteLine("✨ 所有文本块重写完成");
        
        // 4. 质量审核和合并
        Console.WriteLine("🔍 开始质量审核和文本合并...");
        var finalArticle = await MergeAndQualityCheck(rewrittenBlocks, agentTeam);
        
        // 5. 最终质量报告
        //await GenerateQualityReport(originalArticle, finalArticle, agentTeam.QaAgent);
        
        Console.WriteLine("🎉 文章重写流程完成!");
        return finalArticle;
    }
    
    /// <summary>
    /// 创建完整的Agent团队
    /// </summary>
    private async Task<AgentTeam> CreateAgentTeam()
    {
        var team = new AgentTeam
        {
            ControllerAgent = await AgentFactory.CreateControllerAgent(_kernel),
            AnalyzerAgent = await AgentFactory.CreateAnalyzerAgent(_kernel),
            DispatcherAgent = await AgentFactory.CreateDispatcherAgent(_kernel),
            ReviewerAgent = await AgentFactory.CreateReviewerAgent(_kernel),
            QaAgent = await AgentFactory.CreateQualityAssuranceAgent(_kernel),
            RewriterAgents = new List<ChatCompletionAgent>()
        };
        
        // 创建多个重写Agent以支持并行处理
        for (int i = 0; i < 4; i++) // 4个重写Agent并行工作
        {
            team.RewriterAgents.Add(await AgentFactory.CreateRewriterAgent(_kernel, i));
        }
        
        return team;
    }
    
    /// <summary>
    /// 并行重写所有文本块
    /// </summary>
    private async Task<List<RewrittenBlock>> ParallelRewriteBlocks(
        List<TextBlock> textBlocks, List<ChatCompletionAgent> rewriterAgents)
    {
        var rewrittenBlocks = new ConcurrentBag<RewrittenBlock>();
        var semaphore = new SemaphoreSlim(4); // 限制并发数
        
        var tasks = textBlocks.Select(async (block, index) =>
        {
            await semaphore.WaitAsync();
            try
            {
                var agentIndex = index % rewriterAgents.Count;
                var agent = rewriterAgents[agentIndex];
                
                Console.WriteLine($"🔄 Agent-{agentIndex} 正在处理块 {index + 1}/{textBlocks.Count}");
                
                // 构建上下文感知的重写提示
                var contextPrompt = BuildContextAwarePrompt(block, textBlocks);
                
                var startTime = DateTime.Now;
                var resultMessages = new List<string>();
                await foreach (var response in agent.InvokeStreamingAsync(contextPrompt))
                {
                    if (response.Message.Content != null)
                        resultMessages.Add(response.Message.Content);
                }
                var result = string.Join("", resultMessages);
                var endTime = DateTime.Now;
                
                var rewrittenBlock = new RewrittenBlock
                {
                    Index = block.Index,
                    Content = result,
                    OriginalContent = block.Content,
                    ProcessedTime = endTime,
                    ProcessedBy = agent.Name ?? "Unknown",
                    SimilarityScore = TextProcessor.CalculateSimilarity(block.Content, result)
                };
                
                rewrittenBlocks.Add(rewrittenBlock);
                
                Console.WriteLine($"✅ 块 {index + 1} 重写完成，相似度: {rewrittenBlock.SimilarityScore:P2}，用时: {(endTime - startTime).TotalSeconds:F1}秒");
            }
            finally
            {
                semaphore.Release();
            }
        });
        
        await Task.WhenAll(tasks);
        
        return rewrittenBlocks.OrderBy(b => b.Index).ToList();
    }
    
    /// <summary>
    /// 构建上下文感知的重写提示
    /// </summary>
    private string BuildContextAwarePrompt(TextBlock currentBlock, List<TextBlock> allBlocks)
    {
        var prompt = new StringBuilder();
        
        prompt.AppendLine("请重写以下文本块，要求：");
        prompt.AppendLine("1. 完全保持原文的核心意思和逻辑");
        prompt.AppendLine("2. 大幅改变表达方式，避免与原文相似");
        prompt.AppendLine("3. 保持与上下文的自然衔接");
        prompt.AppendLine("4. 维持专业性和准确性");
        prompt.AppendLine();
        
        // 添加前文上下文
        if (currentBlock.Index > 0)
        {
            var prevBlock = allBlocks.FirstOrDefault(b => b.Index == currentBlock.Index - 1);
            if (prevBlock != null)
            {
                prompt.AppendLine("【前文结尾参考】");
                prompt.AppendLine(prevBlock.Content.Length > 150 ? 
                    prevBlock.Content.Substring(prevBlock.Content.Length - 150) : prevBlock.Content);
                prompt.AppendLine();
            }
        }
        
        prompt.AppendLine("【当前需要重写的文本】");
        prompt.AppendLine(currentBlock.Content);
        prompt.AppendLine();
        
        // 添加后文上下文
        if (currentBlock.Index < allBlocks.Count - 1)
        {
            var nextBlock = allBlocks.FirstOrDefault(b => b.Index == currentBlock.Index + 1);
            if (nextBlock != null)
            {
                prompt.AppendLine("【后文开头参考】");
                prompt.AppendLine(nextBlock.Content.Length > 150 ? 
                    nextBlock.Content.Substring(0, 150) : nextBlock.Content);
                prompt.AppendLine();
            }
        }
        
        // 添加关键词和摘要信息
        if (!string.IsNullOrEmpty(currentBlock.Keywords))
        {
            prompt.AppendLine($"【关键词】{currentBlock.Keywords}");
        }
        
        if (!string.IsNullOrEmpty(currentBlock.Summary))
        {
            prompt.AppendLine($"【核心要点】{currentBlock.Summary}");
        }
        
        prompt.AppendLine();
        prompt.AppendLine("请直接输出重写后的文本，不要包含任何额外的说明。");
        
        return prompt.ToString();
    }
    
    /// <summary>
    /// 合并文本并进行质量检查
    /// </summary>
    private async Task<string> MergeAndQualityCheck(List<RewrittenBlock> rewrittenBlocks, AgentTeam agentTeam)
    {
        // 1. 初步合并
        var mergedText = string.Join("\n\n", rewrittenBlocks.Select(b => b.Content));
        
        // 2. 审核员检查
        var reviewPrompt = $@"
请审核以下重写后的文章：

原始相似度报告：
{string.Join("\n", rewrittenBlocks.Select(b => $"块{b.Index + 1}: 相似度 {b.SimilarityScore:P2}"))}

合并后文章：
{mergedText}

请检查：
1. 段落间衔接是否自然
2. 整体逻辑是否连贯  
3. 语言风格是否统一
4. 专业术语使用是否得当

如需修改，请提供完整的修正版本。如果质量良好，回复""质量符合要求""。";

        var reviewMessages = new List<string>();
        await foreach (var response in agentTeam.ReviewerAgent.InvokeStreamingAsync(reviewPrompt))
        {
            if (response.Message.Content != null)
                reviewMessages.Add(response.Message.Content);
        }
        var reviewResult = string.Join("", reviewMessages);
        
        if (reviewResult.Contains("质量符合要求"))
        {
            Console.WriteLine("✅ 质量审核通过");
            return mergedText;
        }
        else
        {
            Console.WriteLine("🔧 应用审核建议");
            return reviewResult;
        }
    }
    
    /// <summary>
    /// 生成质量报告
    /// </summary>
    private async Task GenerateQualityReport(string originalText, string rewrittenText, ChatCompletionAgent qaAgent)
    {
        var reportPrompt = $@"
请为以下文章重写生成质量报告：

原文长度：{originalText.Length} 字
重写后长度：{rewrittenText.Length} 字

原文片段（前200字）：
{originalText.Substring(0, Math.Min(200, originalText.Length))}...

重写片段（前200字）：
{rewrittenText.Substring(0, Math.Min(200, rewrittenText.Length))}...

请评估：
1. 语义保真度 (0-100%)
2. 表达差异度 (0-100%) 
3. 原创检测通过率预估
4. 整体质量评级
5. 改进建议";

        try
        {
            var reportMessages = new List<string>();
            
            // 使用 ConfigureAwait(false) 和更稳定的异步处理
            await foreach (var response in qaAgent.InvokeStreamingAsync(reportPrompt).ConfigureAwait(false))
            {
                if (!string.IsNullOrEmpty(response.Message?.Content))
                {
                    reportMessages.Add(response.Message.Content);
                }
            }
            
            var report = string.Join("", reportMessages);
            
            if (!string.IsNullOrEmpty(report))
            {
                Console.WriteLine("\n📋 质量报告：");
                Console.WriteLine(new string('=', 50));
                Console.WriteLine(report);
                Console.WriteLine(new string('=', 50));
            }
            else
            {
                Console.WriteLine("⚠️ 质量报告生成失败，使用备用方案...");
                await GenerateSimpleQualityReport(originalText, rewrittenText);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ 质量报告生成出错: {ex.Message}");
            Console.WriteLine("📋 使用简化质量报告：");
            await GenerateSimpleQualityReport(originalText, rewrittenText);
        }
    }
    
    /// <summary>
    /// 生成简化质量报告（备用方案）
    /// </summary>
    private async Task GenerateSimpleQualityReport(string originalText, string rewrittenText)
    {
        var similarity = TextProcessor.CalculateSimilarity(originalText, rewrittenText);
        var lengthRatio = (double)rewrittenText.Length / originalText.Length;
        
        Console.WriteLine(new string('=', 50));
        Console.WriteLine("📊 文章重写质量报告");
        Console.WriteLine(new string('=', 50));
        Console.WriteLine($"📝 原文长度: {originalText.Length} 字");
        Console.WriteLine($"📝 重写后长度: {rewrittenText.Length} 字");
        Console.WriteLine($"📈 长度比例: {lengthRatio:P1}");
        Console.WriteLine($"🔍 文本相似度: {similarity:P2}");
        Console.WriteLine($"💡 表达差异度: {(1 - similarity):P2}");
        Console.WriteLine($"🎯 预估原创检测通过率: {(similarity < 0.3 ? "高" : similarity < 0.5 ? "中" : "低")}");
        Console.WriteLine($"⭐ 整体质量评级: {GetQualityGrade(similarity, lengthRatio)}");
        Console.WriteLine(new string('=', 50));
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// 获取质量等级
    /// </summary>
    private string GetQualityGrade(double similarity, double lengthRatio)
    {
        if (similarity < 0.3 && lengthRatio >= 0.8 && lengthRatio <= 1.2)
            return "优秀 (A)";
        else if (similarity < 0.5 && lengthRatio >= 0.7 && lengthRatio <= 1.3)
            return "良好 (B)";
        else if (similarity < 0.7)
            return "一般 (C)";
        else
            return "需要改进 (D)";
    }
    
    /// <summary>
    /// 使用AgentGroupChat进行协作重写（备选方案）
    /// </summary>
    public async Task<string> RewriteWithGroupChatAsync(string originalArticle)
    {
        Console.WriteLine("🎭 启动AgentGroupChat协作模式...");
        
        var groupChat = await GetArticleRewriterGroupChat(originalArticle);
        
        var initialMessage = $@"
我们需要重写这篇8000字的中文文章，确保内容质量不变但能通过原创检测。

文章内容：
{originalArticle}

请按以下流程协作：
1. ControllerAgent：制定整体策略
2. AnalyzerAgent：分析文章结构  
3. DispatcherAgent：智能分割文本
4. RewriterAgent：执行重写
5. ReviewerAgent：质量审核

开始协作！";

        groupChat.AddChatMessage(new ChatMessageContent(AuthorRole.User, initialMessage));
        
        var messages = new List<ChatMessageContent>();
        await foreach (var message in groupChat.InvokeAsync())
        {
            messages.Add(message);
            Console.WriteLine($"[{message.AuthorName}]: {message.Content}");
            
            // 当ReviewerAgent完成审核时结束
            if (message.AuthorName == "ReviewerAgent" && 
                (message.Content?.Contains("最终文章") == true || 
                 message.Content?.Contains("重写完成") == true))
            {
                break;
            }
        }
        
        return messages.LastOrDefault()?.Content ?? "重写失败";
    }

    private async Task<AgentGroupChat> GetArticleRewriterGroupChat(string originalArticle)
    {
        // 创建所有需要的Agent
        var controllerAgent = await AgentFactory.CreateControllerAgent(_kernel);
        var analyzerAgent = await AgentFactory.CreateAnalyzerAgent(_kernel);
        var dispatcherAgent = await AgentFactory.CreateDispatcherAgent(_kernel);
        var rewriterAgent = await AgentFactory.CreateRewriterAgent(_kernel, 0);
        var reviewerAgent = await AgentFactory.CreateReviewerAgent(_kernel);
        
        // 创建Agent组
        return new AgentGroupChat(controllerAgent, analyzerAgent, dispatcherAgent, rewriterAgent, reviewerAgent)
        {
            ExecutionSettings = new AgentGroupChatSettings
            {
                SelectionStrategy = new SequentialSelectionStrategy(),
                TerminationStrategy = new LastAgentTerminationStrategy(reviewerAgent)
                {
                    MaximumIterations = 15,
                }
            }
        };
    }
}

/// <summary>
/// Agent团队结构
/// </summary>
public class AgentTeam
{
    public ChatCompletionAgent ControllerAgent { get; set; } = null!;
    public ChatCompletionAgent AnalyzerAgent { get; set; } = null!;
    public ChatCompletionAgent DispatcherAgent { get; set; } = null!;
    public ChatCompletionAgent ReviewerAgent { get; set; } = null!;
    public ChatCompletionAgent QaAgent { get; set; } = null!;
    public List<ChatCompletionAgent> RewriterAgents { get; set; } = new();
}

