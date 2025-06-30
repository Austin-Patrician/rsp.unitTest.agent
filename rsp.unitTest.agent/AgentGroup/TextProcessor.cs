using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel.Agents;

namespace rsp.unitTest.agent.AgentGroup;

public static class TextProcessor
{
    /// <summary>
    /// 智能分割长文本为多个可管理的块
    /// </summary>
    public static async Task<List<TextBlock>> SplitTextIntelligently(string originalText,
        ChatCompletionAgent analyzerAgent, ChatCompletionAgent dispatcherAgent)
    {
        Console.WriteLine("开始智能文本分析和分割...");

        // 1. 结构分析
        var analysisPrompt = $@"
请分析以下中文文章的结构特点：
1. 识别主要段落和逻辑结构
2. 找出适合分割的位置（段落边界、逻辑转折点）
3. 提取关键术语和概念
4. 分析写作风格

文章内容：
{originalText}

请提供详细的结构分析报告。";

        var analysisMessages = new List<string>();
        await foreach (var response in analyzerAgent.InvokeStreamingAsync(analysisPrompt))
        {
            if (response.Message.Content != null)
                analysisMessages.Add(response.Message.Content);
        }

        var analysisResult = string.Join("", analysisMessages);
        Console.WriteLine("结构分析完成");

        // 2. 智能分割
        var splitPrompt = $@"
基于以下分析结果，将文章分割成6-10个逻辑完整的块：

分析结果：
{analysisResult}

原文：
{originalText}

分割要求：
1. 每块600-1200字左右
2. 在段落或逻辑完整处分割
3. 保持每块的主题连贯性
4. 为每块生成摘要和关键词

请按以下格式输出：
[BLOCK_1]
内容：...
摘要：...
关键词：...
[/BLOCK_1]

[BLOCK_2]
内容：...
摘要：...
关键词：...
[/BLOCK_2]
...";

        var splitMessages = new List<string>();
        await foreach (var response in dispatcherAgent.InvokeStreamingAsync(splitPrompt))
        {
            if (response.Message.Content != null)
                splitMessages.Add(response.Message.Content);
        }

        var splitResult = string.Join("", splitMessages);

        // 3. 解析分割结果
        var textBlocks = ParseSplitResult(splitResult);

        Console.WriteLine($"文本已智能分割为 {textBlocks.Count} 个块");
        return textBlocks;
    }

   
    /// <summary>
    /// 合并重写后的文本块并进行质量审核
    /// </summary>
    public static async Task<string> MergeAndReview(List<RewrittenBlock> rewrittenBlocks,
        ChatCompletionAgent reviewerAgent)
    {
        Console.WriteLine("开始合并和质量审核...");

        // 1. 按顺序合并文本
        var mergedText = new StringBuilder();
        foreach (var block in rewrittenBlocks.OrderBy(b => b.Index))
        {
            mergedText.AppendLine(block.Content);
            mergedText.AppendLine(); // 段落间空行
        }

        var initialMerged = mergedText.ToString().Trim();

        // 2. 质量审核和优化
        var reviewPrompt = $@"
请审核以下重写后的文章，重点检查：
1. 段落间的衔接是否自然
2. 整体逻辑是否连贯
3. 语言风格是否统一
4. 是否有重复或矛盾的表达

如果发现问题，请直接提供修正后的完整文章。如果质量良好，请回复""质量良好，无需修改""。

文章内容：
{initialMerged}";

        var reviewMessages = new List<string>();
        await foreach (var response in reviewerAgent.InvokeStreamingAsync(reviewPrompt))
        {
            if (response.Message.Content != null)
                reviewMessages.Add(response.Message.Content);
        }

        var reviewResult = string.Join("", reviewMessages);

        // 3. 根据审核结果决定是否使用修正版本
        if (reviewResult.Contains("质量良好，无需修改"))
        {
            Console.WriteLine("质量审核通过，无需修改");
            return initialMerged;
        }
        else
        {
            Console.WriteLine("应用质量优化建议");
            return reviewResult;
        }
    }

    /// <summary>
    /// 解析分割结果
    /// </summary>
    private static List<TextBlock> ParseSplitResult(string splitResult)
    {
        var blocks = new List<TextBlock>();
        var blockPattern = @"\[BLOCK_(\d+)\](.*?)\[/BLOCK_\1\]";
        var matches = Regex.Matches(splitResult, blockPattern, RegexOptions.Singleline);

        foreach (Match match in matches)
        {
            if (match.Groups.Count >= 3)
            {
                var blockIndex = int.Parse(match.Groups[1].Value);
                var blockContent = match.Groups[2].Value.Trim();

                // 解析内容、摘要、关键词
                var contentMatch = Regex.Match(blockContent, @"内容：(.*?)(?=摘要：|$)", RegexOptions.Singleline);
                var summaryMatch = Regex.Match(blockContent, @"摘要：(.*?)(?=关键词：|$)", RegexOptions.Singleline);
                var keywordsMatch = Regex.Match(blockContent, @"关键词：(.*?)$", RegexOptions.Singleline);

                blocks.Add(new TextBlock
                {
                    Index = blockIndex - 1, // 转为0基索引
                    Content = contentMatch.Success ? contentMatch.Groups[1].Value.Trim() : blockContent,
                    Summary = summaryMatch.Success ? summaryMatch.Groups[1].Value.Trim() : "",
                    Keywords = keywordsMatch.Success ? keywordsMatch.Groups[1].Value.Trim() : ""
                });
            }
        }

        return blocks.OrderBy(b => b.Index).ToList();
    }

    /// <summary>
    /// 计算文本相似度（简单实现）
    /// </summary>
    public static double CalculateSimilarity(string text1, string text2)
    {
        var words1 = text1.Split(new[] { ' ', '，', '。', '、', '；', '：' }, StringSplitOptions.RemoveEmptyEntries);
        var words2 = text2.Split(new[] { ' ', '，', '。', '、', '；', '：' }, StringSplitOptions.RemoveEmptyEntries);

        var commonWords = words1.Intersect(words2).Count();
        var totalWords = Math.Max(words1.Length, words2.Length);

        return totalWords > 0 ? (double)commonWords / totalWords : 0;
    }
}

/// <summary>
/// 文本块结构
/// </summary>
public class TextBlock
{
    public int Index { get; set; }
    public string Content { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Keywords { get; set; } = "";
    public string PreviousContext { get; set; } = "";
    public string NextContext { get; set; } = "";
}

/// <summary>
/// 重写后的文本块
/// </summary>
public class RewrittenBlock
{
    public int Index { get; set; }
    public string Content { get; set; } = "";
    public string OriginalContent { get; set; } = "";
    public double SimilarityScore { get; set; }
    public DateTime ProcessedTime { get; set; }
    public string ProcessedBy { get; set; } = "";
}