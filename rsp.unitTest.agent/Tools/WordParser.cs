using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;
using System.Text;

namespace rsp.unitTest.agent.Tools;

public class WordParser
{
    /// <summary>
    /// 读取Word文件内容并返回为字符串
    /// </summary>
    /// <param name="filePath">Word文件路径</param>
    /// <returns>文件内容字符串</returns>
    public static string ReadWordFileContent(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件不存在: {filePath}");
        }

        var content = new StringBuilder();

        try
        {
            using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(filePath, false))
            {
                Body? body = wordDoc.MainDocumentPart?.Document.Body;
                if (body != null)
                {
                    foreach (var element in body.Elements())
                    {
                        content.AppendLine(GetTextFromElement(element));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"读取Word文件时发生错误: {ex.Message}", ex);
        }

        return content.ToString().Trim();
    }

    /// <summary>
    /// 从OpenXml元素中提取文本内容
    /// </summary>
    /// <param name="element">OpenXml元素</param>
    /// <returns>文本内容</returns>
    private static string GetTextFromElement(DocumentFormat.OpenXml.OpenXmlElement element)
    {
        var text = new StringBuilder();

        if (element is Paragraph paragraph)
        {
            foreach (var run in paragraph.Elements<Run>())
            {
                foreach (var textElement in run.Elements<Text>())
                {
                    text.Append(textElement.Text);
                }
            }
        }
        else if (element is Table table)
        {
            foreach (var row in table.Elements<TableRow>())
            {
                foreach (var cell in row.Elements<TableCell>())
                {
                    foreach (var cellElement in cell.Elements())
                    {
                        text.Append(GetTextFromElement(cellElement));
                    }
                    text.Append("\t"); // 表格单元格之间用Tab分隔
                }
                text.AppendLine(); // 表格行之间换行
            }
        }
        else
        {
            // 递归处理其他元素
            foreach (var childElement in element.Elements())
            {
                text.Append(GetTextFromElement(childElement));
            }
        }

        return text.ToString();
    }

    /// <summary>
    /// 异步读取Word文件内容
    /// </summary>
    /// <param name="filePath">Word文件路径</param>
    /// <returns>文件内容字符串的Task</returns>
    public static async Task<string> ReadWordFileContentAsync(string filePath)
    {
        return await Task.Run(() => ReadWordFileContent(filePath));
    }
}