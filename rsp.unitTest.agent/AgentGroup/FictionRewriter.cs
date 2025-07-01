using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;

namespace rsp.unitTest.agent.AgentGroup;

public class FictionRewriter
{
    private readonly Kernel _kernel;
    
    public FictionRewriter(Kernel kernel)
    {
        _kernel = kernel;
    }
    
    /// <summary>
    /// 智能小说重写的主要方法
    /// </summary>
    public async Task<string> RewriteFictionAsync(string originalFiction)
    {
        Console.WriteLine("📚 开始智能小说重写流程...");
        
        // 1. 创建小说专用Agent团队
        var fictionTeam = await CreateFictionAgentTeam();
        Console.WriteLine("✅ 小说重写Agent团队创建完成");
        
        // 2. 小说元素分析
        Console.WriteLine("🎭 开始小说结构和元素分析...");
        var fictionAnalysis = await AnalyzeFictionElements(originalFiction, fictionTeam);
        Console.WriteLine("✅ 小说元素分析完成");
        
        // 3. 章节智能分割
        Console.WriteLine("📖 开始章节和场景分割...");
        var fictionBlocks = await SplitFictionIntelligently(originalFiction, fictionTeam, fictionAnalysis);
        Console.WriteLine($"✂️ 小说已分割为 {fictionBlocks.Count} 个章节/场景块");
        
        // 4. 并行重写处理（保持情节连贯性）
        Console.WriteLine("🔄 开始多Agent并行重写...");
        var rewrittenBlocks = await ParallelRewriteFictionBlocks(fictionBlocks, fictionTeam, fictionAnalysis);
        Console.WriteLine("✨ 所有章节重写完成");
        
        // 5. 情节连贯性审核和合并
        Console.WriteLine("🔍 开始情节连贯性审核和文本合并...");
        var finalFiction = await MergeAndPlotCheck(rewrittenBlocks, fictionTeam, fictionAnalysis);
        
        // 6. 保存重写结果
        Console.WriteLine("💾 保存重写结果到文件...");
        await SaveFictionToFile(finalFiction);
        
        // 7. 生成小说质量报告
        //await GenerateFictionQualityReport(originalFiction, finalFiction, fictionTeam.QaAgent, fictionAnalysis);
        
        Console.WriteLine("🎉 小说重写流程完成!");
        return finalFiction;
    }
    
    /// <summary>
    /// 创建小说专用Agent团队
    /// </summary>
    private async Task<FictionAgentTeam> CreateFictionAgentTeam()
    {
        var team = new FictionAgentTeam
        {
            PlotAnalyzerAgent = await FictionAgentFactory.CreatePlotAnalyzerAgent(_kernel),
            CharacterAnalyzerAgent = await FictionAgentFactory.CreateCharacterAnalyzerAgent(_kernel),
            StyleAnalyzerAgent = await FictionAgentFactory.CreateStyleAnalyzerAgent(_kernel),
            SceneDispatcherAgent = await FictionAgentFactory.CreateSceneDispatcherAgent(_kernel),
            PlotReviewerAgent = await FictionAgentFactory.CreatePlotReviewerAgent(_kernel),
            QaAgent = await FictionAgentFactory.CreateFictionQualityAgent(_kernel),
            FictionRewriterAgents = new List<ChatCompletionAgent>()
        };
        
        // 创建多个小说重写Agent以支持并行处理
        for (int i = 0; i < 3; i++) // 3个重写Agent并行工作
        {
            team.FictionRewriterAgents.Add(await FictionAgentFactory.CreateFictionRewriterAgent(_kernel, i));
        }
        
        return team;
    }
    
    /// <summary>
    /// 分析小说的核心元素
    /// </summary>
    private async Task<FictionAnalysis> AnalyzeFictionElements(string originalFiction, FictionAgentTeam team)
    {
        var analysis = new FictionAnalysis();
        
        // 情节分析
        var plotPrompt = $@"
请分析以下小说的情节结构：
1. 识别主要情节线和副情节线
2. 找出关键情节点（开端、发展、高潮、结局）
3. 分析情节转折和冲突
4. 识别故事的主题和寓意

小说内容：
{originalFiction}

请提供详细的情节分析。";

        var plotMessages = new List<string>();
        await foreach (var response in team.PlotAnalyzerAgent.InvokeStreamingAsync(plotPrompt))
        {
            if (response.Message.Content != null)
                plotMessages.Add(response.Message.Content);
        }
        analysis.PlotAnalysis = string.Join("", plotMessages);
        
        // 角色分析
        var characterPrompt = $@"
请分析以下小说的角色设定：
1. 识别主要角色和次要角色
2. 分析角色性格特点和发展弧线
3. 找出角色间的关系网络
4. 识别角色的对话风格和行为特征

小说内容：
{originalFiction}

请提供详细的角色分析。";

        var characterMessages = new List<string>();
        await foreach (var response in team.CharacterAnalyzerAgent.InvokeStreamingAsync(characterPrompt))
        {
            if (response.Message.Content != null)
                characterMessages.Add(response.Message.Content);
        }
        analysis.CharacterAnalysis = string.Join("", characterMessages);
        
        // 文风分析
        var stylePrompt = $@"
请分析以下小说的写作风格：
1. 识别叙述视角（第一人称、第三人称等）
2. 分析语言风格（文艺、通俗、幽默等）
3. 找出写作技巧（描写手法、修辞方式）
4. 识别时代背景和文化特色

小说内容：
{originalFiction}

请提供详细的文风分析。";

        var styleMessages = new List<string>();
        await foreach (var response in team.StyleAnalyzerAgent.InvokeStreamingAsync(stylePrompt))
        {
            if (response.Message.Content != null)
                styleMessages.Add(response.Message.Content);
        }
        analysis.StyleAnalysis = string.Join("", styleMessages);
        
        return analysis;
    }
    
    /// <summary>
    /// 智能分割小说为章节/场景块
    /// </summary>
    private async Task<List<FictionBlock>> SplitFictionIntelligently(
        string originalFiction, FictionAgentTeam team, FictionAnalysis analysis)
    {
        var splitPrompt = $@"
基于以下分析结果，将小说分割成逻辑完整的章节或场景块：

情节分析：
{analysis.PlotAnalysis}

角色分析：
{analysis.CharacterAnalysis}

文风分析：
{analysis.StyleAnalysis}

原小说：
{originalFiction}

分割要求：
1. 每块1000-1800字左右，保持场景完整性
2. 在章节边界、场景转换、时间跳跃处分割
3. 保持情节连贯性和角色发展的完整性
4. 标记每个块的主要角色、场景、情节要点

请按以下格式输出分割结果：
[BLOCK_START:索引]
[SCENE:场景描述]
[CHARACTERS:主要角色]
[PLOT_POINTS:情节要点]
[CONTENT:文本内容]
[BLOCK_END]";

        var splitMessages = new List<string>();
        await foreach (var response in team.SceneDispatcherAgent.InvokeStreamingAsync(splitPrompt))
        {
            if (response.Message.Content != null)
                splitMessages.Add(response.Message.Content);
        }
        
        var splitResult = string.Join("", splitMessages);
        return ParseFictionBlocks(splitResult);
    }
    
    /// <summary>
    /// 解析分割结果为FictionBlock对象
    /// </summary>
    private List<FictionBlock> ParseFictionBlocks(string splitResult)
    {
        var blocks = new List<FictionBlock>();
        var blockPattern = @"\[BLOCK_START:(\d+)\]\s*\[SCENE:(.*?)\]\s*\[CHARACTERS:(.*?)\]\s*\[PLOT_POINTS:(.*?)\]\s*\[CONTENT:(.*?)\]\s*\[BLOCK_END\]";
        var matches = Regex.Matches(splitResult, blockPattern, RegexOptions.Singleline);
        
        foreach (Match match in matches)
        {
            if (match.Success)
            {
                blocks.Add(new FictionBlock
                {
                    Index = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                    Scene = match.Groups[2].Value.Trim(),
                    Characters = match.Groups[3].Value.Trim(),
                    PlotPoints = match.Groups[4].Value.Trim(),
                    Content = match.Groups[5].Value.Trim()
                });
            }
        }
        
        // 如果解析失败，使用简单分割
        if (blocks.Count == 0)
        {
            var simpleBlocks = SplitIntoSimpleBlocks(splitResult);
            blocks.AddRange(simpleBlocks);
        }
        
        return blocks;
    }
    
    /// <summary>
    /// 简单分割方法（备用）
    /// </summary>
    private List<FictionBlock> SplitIntoSimpleBlocks(string text)
    {
        var blocks = new List<FictionBlock>();
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        var blockSize = 2000; // 增加到2000字，减少分割数量
        var currentBlock = new StringBuilder();
        var blockIndex = 0;
        
        foreach (var paragraph in paragraphs)
        {
            if (currentBlock.Length + paragraph.Length > blockSize && currentBlock.Length > 0)
            {
                blocks.Add(new FictionBlock
                {
                    Index = blockIndex++,
                    Content = currentBlock.ToString().Trim(),
                    Scene = "场景分析中...",
                    Characters = "角色分析中...",
                    PlotPoints = "情节分析中..."
                });
                currentBlock.Clear();
            }
            
            currentBlock.AppendLine(paragraph);
        }
        
        if (currentBlock.Length > 0)
        {
            blocks.Add(new FictionBlock
            {
                Index = blockIndex,
                Content = currentBlock.ToString().Trim(),
                Scene = "场景分析中...",
                Characters = "角色分析中...",
                PlotPoints = "情节分析中..."
            });
        }
        
        return blocks;
    }
    
    /// <summary>
    /// 并行重写小说块
    /// </summary>
    private async Task<List<RewrittenFictionBlock>> ParallelRewriteFictionBlocks(
        List<FictionBlock> fictionBlocks, FictionAgentTeam team, FictionAnalysis analysis)
    {
        var rewrittenBlocks = new ConcurrentBag<RewrittenFictionBlock>();
        var semaphore = new SemaphoreSlim(3); // 限制并发数
        
        var tasks = fictionBlocks.Select(async (block, index) =>
        {
            await semaphore.WaitAsync();
            try
            {
                var agentIndex = index % team.FictionRewriterAgents.Count;
                var agent = team.FictionRewriterAgents[agentIndex];
                
                Console.WriteLine($"📝 Agent-{agentIndex} 正在重写 第{index + 1}章节/{fictionBlocks.Count}");
                
                // 构建小说重写提示
                var fictionPrompt = BuildFictionRewritePrompt(block, fictionBlocks, analysis);
                
                var startTime = DateTime.Now;
                var resultMessages = new List<string>();
                await foreach (var response in agent.InvokeStreamingAsync(fictionPrompt))
                {
                    if (response.Message.Content != null)
                        resultMessages.Add(response.Message.Content);
                }
                var result = string.Join("", resultMessages);
                
                // 字数验证和补充机制
                result = await ValidateAndAdjustWordCount(result, block, agent, analysis);
                
                var endTime = DateTime.Now;
                
                var rewrittenBlock = new RewrittenFictionBlock
                {
                    Index = block.Index,
                    Content = result,
                    OriginalContent = block.Content,
                    Scene = block.Scene,
                    Characters = block.Characters,
                    PlotPoints = block.PlotPoints,
                    ProcessedTime = endTime,
                    ProcessedBy = agent.Name ?? "Unknown",
                    SimilarityScore = CalculateSimilarity(block.Content, result)
                };
                
                rewrittenBlocks.Add(rewrittenBlock);
                
                var lengthRatio = (double)result.Length / block.Content.Length;
                Console.WriteLine($"✅ 第{index + 1}章节重写完成，字数比例: {lengthRatio:P1}，相似度: {rewrittenBlock.SimilarityScore:P2}，用时: {(endTime - startTime).TotalSeconds:F1}秒");
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
    /// 验证字数并在必要时进行调整
    /// </summary>
    private async Task<string> ValidateAndAdjustWordCount(string rewrittenContent, FictionBlock originalBlock, ChatCompletionAgent agent, FictionAnalysis analysis)
    {
        var originalLength = originalBlock.Content.Length;
        var rewrittenLength = rewrittenContent.Length;
        var lengthRatio = (double)rewrittenLength / originalLength;
        
        // 如果字数在90%-110%范围内，直接返回
        if (lengthRatio >= 0.9 && lengthRatio <= 1.1)
        {
            return rewrittenContent;
        }
        
        // 如果字数不足90%，需要补充
        if (lengthRatio < 0.9)
        {
            Console.WriteLine($"⚠️ 字数不足（{lengthRatio:P1}），正在自动补充内容...");
            return await EnhanceContentLength(rewrittenContent, originalBlock, agent, analysis);
        }
        
        // 如果字数超过110%，适当精简（但优先保持完整性）
        if (lengthRatio > 1.1)
        {
            Console.WriteLine($"📏 字数略超标（{lengthRatio:P1}），内容丰富，保持原样");
            return rewrittenContent; // 宁可超标也不删减重要内容
        }
        
        return rewrittenContent;
    }
    
    /// <summary>
    /// 增强内容长度
    /// </summary>
    private async Task<string> EnhanceContentLength(string content, FictionBlock originalBlock, ChatCompletionAgent agent, FictionAnalysis analysis)
    {
        var originalLength = originalBlock.Content.Length;
        var currentLength = content.Length;
        var targetLength = (int)(originalLength * 0.95); // 目标95%
        var needAddLength = targetLength - currentLength;
        
        var enhancePrompt = $@"
以下是已重写的小说章节，但字数不足，需要增强内容：

当前字数：{currentLength} 字
目标字数：{targetLength} 字
需要增加：约{needAddLength} 字

原始章节信息：
场景：{originalBlock.Scene}
角色：{originalBlock.Characters}
情节要点：{originalBlock.PlotPoints}

当前重写内容：
{content}

请在保持以下原则的基础上增强内容：
1. 【重要】保持情节发展完全不变
2. 【重要】保持角色性格和行为一致
3. 通过以下方式增加字数到目标范围：
   - 丰富心理描写和内心独白
   - 增加环境细节和氛围营造
   - 扩展动作描写的精细度
   - 补充表情、语气、肢体语言等细节
   - 增加过渡性描述和背景信息
   - 深化情感表达和感官体验

4. 确保增强后的内容自然流畅，不显突兀
5. 维持原有的文学性和可读性

请直接输出增强后的完整章节内容，确保字数达到{targetLength}字左右。";

        var enhanceMessages = new List<string>();
        await foreach (var response in agent.InvokeStreamingAsync(enhancePrompt))
        {
            if (response.Message.Content != null)
                enhanceMessages.Add(response.Message.Content);
        }
        
        var enhancedContent = string.Join("", enhanceMessages);
        var finalLength = enhancedContent.Length;
        var finalRatio = (double)finalLength / originalLength;
        
        Console.WriteLine($"✨ 内容增强完成：{currentLength} → {finalLength} 字（{finalRatio:P1}）");
        
        return enhancedContent;
    }
    
    /// <summary>
    /// 构建小说重写提示
    /// </summary>
    private string BuildFictionRewritePrompt(FictionBlock currentBlock, List<FictionBlock> allBlocks, FictionAnalysis analysis)
    {
        var prompt = new StringBuilder();
        var originalLength = currentBlock.Content.Length;
        
        prompt.AppendLine("请重写以下小说章节/场景，严格要求：");
        prompt.AppendLine("1. 完全保持原有的情节发展和角色性格");
        prompt.AppendLine("2. 大幅改变叙述方式和表达风格，避免与原文相似");
        prompt.AppendLine("3. 保持角色对话的个性化特征");
        prompt.AppendLine("4. 维持场景描写的氛围和情感基调");
        prompt.AppendLine("5. 确保与前后章节的情节连贯性");
        prompt.AppendLine($"6. 【重要】重写后的字数必须保持在原文字数的90%-110%之间（原文{originalLength}字，目标{(int)(originalLength * 0.9)}-{(int)(originalLength * 1.1)}字）");
        prompt.AppendLine("7. 【重要】不得删减情节内容，只能改变表达方式");
        prompt.AppendLine("8. 【重要】增加必要的细节描写和心理描写来丰富内容");
        prompt.AppendLine();
        
        // 添加小说整体信息
        prompt.AppendLine("【小说整体分析】");
        prompt.AppendLine($"情节特点：{GetSafeSubstring(analysis.PlotAnalysis, 200)}...");
        prompt.AppendLine($"角色特点：{GetSafeSubstring(analysis.CharacterAnalysis, 200)}...");
        prompt.AppendLine($"文风特点：{GetSafeSubstring(analysis.StyleAnalysis, 200)}...");
        prompt.AppendLine();
        
        // 添加前文上下文
        if (currentBlock.Index > 0)
        {
            var prevBlock = allBlocks.FirstOrDefault(b => b.Index == currentBlock.Index - 1);
            if (prevBlock != null)
            {
                prompt.AppendLine("【前章节结尾参考】");
                prompt.AppendLine(prevBlock.Content.Length > 200 ? 
                    prevBlock.Content.Substring(prevBlock.Content.Length - 200) : prevBlock.Content);
                prompt.AppendLine();
            }
        }
        
        prompt.AppendLine("【当前需要重写的章节】");
        prompt.AppendLine($"场景：{currentBlock.Scene}");
        prompt.AppendLine($"主要角色：{currentBlock.Characters}");
        prompt.AppendLine($"情节要点：{currentBlock.PlotPoints}");
        prompt.AppendLine($"原文字数：{originalLength} 字");
        prompt.AppendLine();
        prompt.AppendLine("原文内容：");
        prompt.AppendLine(currentBlock.Content);
        prompt.AppendLine();
        
        // 添加后文上下文
        if (currentBlock.Index < allBlocks.Count - 1)
        {
            var nextBlock = allBlocks.FirstOrDefault(b => b.Index == currentBlock.Index + 1);
            if (nextBlock != null)
            {
                prompt.AppendLine("【后章节开头参考】");
                prompt.AppendLine(nextBlock.Content.Length > 200 ? 
                    nextBlock.Content.Substring(0, 200) : nextBlock.Content);
                prompt.AppendLine();
            }
        }
        
        prompt.AppendLine("【重写要求总结】");
        prompt.AppendLine("1. 保持情节和角色完整性100%");
        prompt.AppendLine("2. 改变表达方式和叙述角度80%以上");
        prompt.AppendLine($"3. 输出字数控制在{(int)(originalLength * 0.9)}-{(int)(originalLength * 1.1)}字之间");
        prompt.AppendLine("4. 增强细节描写和情感表达");
        prompt.AppendLine("5. 保持文学性和可读性");
        prompt.AppendLine();
        prompt.AppendLine("请直接输出重写后的章节内容，确保字数达标且质量优秀。");
        
        return prompt.ToString();
    }
    
    /// <summary>
    /// 安全获取字符串子串
    /// </summary>
    private string GetSafeSubstring(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return "待分析";
        
        return text.Length > maxLength ? text.Substring(0, maxLength) : text;
    }
    
    /// <summary>
    /// 合并章节并进行情节连贯性检查
    /// </summary>
    private async Task<string> MergeAndPlotCheck(List<RewrittenFictionBlock> rewrittenBlocks, FictionAgentTeam team, FictionAnalysis analysis)
    {
        // 1. 初步合并
        var mergedFiction = string.Join("\n\n", rewrittenBlocks.Select(b => b.Content));
        
        // 2. 情节连贯性审核
        var plotReviewPrompt = $@"
请审核以下重写后的小说的情节连贯性：

原始小说分析：
情节：{analysis.PlotAnalysis}
角色：{analysis.CharacterAnalysis}
文风：{analysis.StyleAnalysis}

各章节相似度报告：
{string.Join("\n", rewrittenBlocks.Select(b => $"第{b.Index + 1}章: 相似度 {b.SimilarityScore:P2}"))}

重写后小说：
{mergedFiction}

请检查：
1. 情节发展是否连贯自然
2. 角色性格是否前后一致
3. 场景转换是否流畅
4. 对话风格是否符合角色特征
5. 整体节奏和氛围是否统一

如需修改，请提供完整的修正版本。如果质量良好，回复""情节连贯性良好""。";

        var reviewMessages = new List<string>();
        await foreach (var response in team.PlotReviewerAgent.InvokeStreamingAsync(plotReviewPrompt))
        {
            if (response.Message.Content != null)
                reviewMessages.Add(response.Message.Content);
        }
        var reviewResult = string.Join("", reviewMessages);
        
        if (reviewResult.Contains("情节连贯性良好"))
        {
            Console.WriteLine("✅ 情节连贯性审核通过");
            return mergedFiction;
        }
        else
        {
            Console.WriteLine("🔧 应用情节优化建议");
            return reviewResult;
        }
    }
    
    /// <summary>
    /// 生成小说质量报告
    /// </summary>
    private async Task GenerateFictionQualityReport(string originalFiction, string rewrittenFiction, 
        ChatCompletionAgent qaAgent, FictionAnalysis analysis)
    {
        var reportPrompt = $@"
请为以下小说重写生成专业质量报告：

原小说长度：{originalFiction.Length} 字
重写后长度：{rewrittenFiction.Length} 字

原小说片段（前300字）：
{originalFiction.Substring(0, Math.Min(300, originalFiction.Length))}...

重写片段（前300字）：
{rewrittenFiction.Substring(0, Math.Min(300, rewrittenFiction.Length))}...

原小说分析概要：
情节特点：{GetSafeSubstring(analysis.PlotAnalysis, 150)}
角色特点：{GetSafeSubstring(analysis.CharacterAnalysis, 150)}
文风特点：{GetSafeSubstring(analysis.StyleAnalysis, 150)}

请从以下维度评估：
1. 情节保真度 (0-100%)
2. 角色一致性 (0-100%)
3. 文风创新度 (0-100%)
4. 可读性和文学性 (0-100%)
5. 原创检测通过率预估
6. 整体质量评级
7. 改进建议";

        try
        {
            var reportMessages = new List<string>();
            await foreach (var response in qaAgent.InvokeStreamingAsync(reportPrompt))
            {
                if (!string.IsNullOrEmpty(response.Message?.Content))
                {
                    reportMessages.Add(response.Message.Content);
                }
            }
            
            var report = string.Join("", reportMessages);
            
            if (!string.IsNullOrEmpty(report))
            {
                Console.WriteLine("\n📋 小说质量报告：");
                Console.WriteLine(new string('=', 50));
                Console.WriteLine(report);
                Console.WriteLine(new string('=', 50));
            }
            else
            {
                await GenerateSimpleFictionReport(originalFiction, rewrittenFiction);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ 质量报告生成出错: {ex.Message}");
            await GenerateSimpleFictionReport(originalFiction, rewrittenFiction);
        }
    }
    
    /// <summary>
    /// 生成简化小说质量报告
    /// </summary>
    private async Task GenerateSimpleFictionReport(string originalFiction, string rewrittenFiction)
    {
        var similarity = CalculateSimilarity(originalFiction, rewrittenFiction);
        var lengthRatio = (double)rewrittenFiction.Length / originalFiction.Length;
        
        Console.WriteLine(new string('=', 50));
        Console.WriteLine("📚 小说重写质量报告");
        Console.WriteLine(new string('=', 50));
        Console.WriteLine($"📖 原小说长度: {originalFiction.Length} 字");
        Console.WriteLine($"📖 重写后长度: {rewrittenFiction.Length} 字");
        Console.WriteLine($"📈 长度比例: {lengthRatio:P1}");
        Console.WriteLine($"🔍 文本相似度: {similarity:P2}");
        Console.WriteLine($"💡 创新度: {(1 - similarity):P2}");
        Console.WriteLine($"🎯 预估原创检测通过率: {(similarity < 0.3 ? "高" : similarity < 0.5 ? "中" : "低")}");
        Console.WriteLine($"⭐ 整体质量评级: {GetFictionQualityGrade(similarity, lengthRatio)}");
        Console.WriteLine(new string('=', 50));
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// 获取小说质量等级
    /// </summary>
    private string GetFictionQualityGrade(double similarity, double lengthRatio)
    {
        if (similarity < 0.25 && lengthRatio >= 0.85 && lengthRatio <= 1.15)
            return "优秀 (A) - 高度原创，情节完整";
        else if (similarity < 0.4 && lengthRatio >= 0.8 && lengthRatio <= 1.2)
            return "良好 (B) - 创新度好，结构合理";
        else if (similarity < 0.6)
            return "一般 (C) - 有改进，需优化";
        else
            return "需要改进 (D) - 相似度过高";
    }
    
    /// <summary>
    /// 计算文本相似度（简化版本）
    /// </summary>
    private double CalculateSimilarity(string text1, string text2)
    {
        if (string.IsNullOrEmpty(text1) || string.IsNullOrEmpty(text2))
            return 0.0;
        
        var words1 = text1.Split(new[] { ' ', '\n', '\r', '，', '。', '？', '！' }, StringSplitOptions.RemoveEmptyEntries);
        var words2 = text2.Split(new[] { ' ', '\n', '\r', '，', '。', '？', '！' }, StringSplitOptions.RemoveEmptyEntries);
        
        var commonWords = words1.Intersect(words2).Count();
        var totalWords = Math.Max(words1.Length, words2.Length);
        
        return totalWords > 0 ? (double)commonWords / totalWords : 0.0;
    }
    
    /// <summary>
    /// 保存重写后的小说到文件
    /// </summary>
    private async Task SaveFictionToFile(string fictionContent)
    {
        try
        {
            var filePath = $"RewrittenFiction_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            await File.WriteAllTextAsync(filePath, fictionContent);
            Console.WriteLine($"📂 小说已保存到文件: {filePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ 保存小说时出错: {ex.Message}");
        }
    }
}

/// <summary>
/// 小说专用Agent团队结构
/// </summary>
public class FictionAgentTeam
{
    public ChatCompletionAgent PlotAnalyzerAgent { get; set; } = null!;
    public ChatCompletionAgent CharacterAnalyzerAgent { get; set; } = null!;
    public ChatCompletionAgent StyleAnalyzerAgent { get; set; } = null!;
    public ChatCompletionAgent SceneDispatcherAgent { get; set; } = null!;
    public ChatCompletionAgent PlotReviewerAgent { get; set; } = null!;
    public ChatCompletionAgent QaAgent { get; set; } = null!;
    public List<ChatCompletionAgent> FictionRewriterAgents { get; set; } = new();
}

/// <summary>
/// 小说分析结果
/// </summary>
public class FictionAnalysis
{
    public string PlotAnalysis { get; set; } = string.Empty;
    public string CharacterAnalysis { get; set; } = string.Empty;
    public string StyleAnalysis { get; set; } = string.Empty;
}

/// <summary>
/// 小说文本块
/// </summary>
public class FictionBlock
{
    public int Index { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Scene { get; set; } = string.Empty;
    public string Characters { get; set; } = string.Empty;
    public string PlotPoints { get; set; } = string.Empty;
}

/// <summary>
/// 重写后的小说块
/// </summary>
public class RewrittenFictionBlock
{
    public int Index { get; set; }
    public string Content { get; set; } = string.Empty;
    public string OriginalContent { get; set; } = string.Empty;
    public string Scene { get; set; } = string.Empty;
    public string Characters { get; set; } = string.Empty;
    public string PlotPoints { get; set; } = string.Empty;
    public DateTime ProcessedTime { get; set; }
    public string ProcessedBy { get; set; } = string.Empty;
    public double SimilarityScore { get; set; }
}

/// <summary>
/// 文本块基类（兼容ArticleRewriter）
/// </summary>
public class TextBlock
{
    public int Index { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// 重写块基类（兼容ArticleRewriter）
/// </summary>
public class RewrittenBlock
{
    public int Index { get; set; }
    public string Content { get; set; } = string.Empty;
    public string OriginalContent { get; set; } = string.Empty;
    public DateTime ProcessedTime { get; set; }
    public string ProcessedBy { get; set; } = string.Empty;
    public double SimilarityScore { get; set; }
}