using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace rsp.unitTest.agent.Analyzer;

public class UnitTestAnalyzer
{
    public async Task<AgentGroupChat> GetUnitTestGroupChat(Kernel kernel,string code)
    {
        var codeAnalyzerAsync = await CodeAnalyzerAsync(kernel,code);
        var testDesignerAsync = await TestDesignerAsync(kernel);
        var dataGeneratorAsync = await DataGeneratorAsync(kernel);
        var testWriterAsync = await TestWriterAsync(kernel);
        var testReviewerAsync = await TestReviewerAsync(kernel);
        return new AgentGroupChat(codeAnalyzerAsync, testDesignerAsync, dataGeneratorAsync, testWriterAsync,
            testReviewerAsync)
        {
            ExecutionSettings = new AgentGroupChatSettings
            {
                SelectionStrategy = new SequentialSelectionStrategy(),
                TerminationStrategy = new LastAgentTerminationStrategy(testReviewerAsync)
                {
                    MaximumIterations = 10,
                }
            }
        };
    }

    private async Task<ChatCompletionAgent> CodeAnalyzerAsync(Kernel kernel,string code)
    {
        var agentTranslator = new ChatCompletionAgent()
        {
            Instructions = $"""
                           你是一位专业的10+ 年经验的.net代码分析专家，擅长分析基于C#类文件。你需要根据以下的方法Action,入参Dto, 返回值VO进行分析：
                           {
                               code
                           }
                           
                           1. 首先识别并重点分析类中的RunSqlDataOperation或者RunDataOperateAsync方法，这是框架中的主要入口点
                              - 详细分析其参数类型和返回值类型
                              - 识别它调用的所有辅助方法和依赖的数据
                              - 分析其执行流程和主要逻辑分支
                           
                           2. 仅分析与RunSqlDataOperation直接相关的私有方法
                              - 特别关注提供SQL语句的方法
                              - 特别关注提供SQL参数的方法
                           
                           3. 简要分析类的继承结构和泛型参数
                              - 确定入参和返回值的类型
                              - 理解数据实体的结构和属性
                           
                           4. 忽略依赖注入的服务和实体（IADOConfigurable, ILoggingLogger）
                           
                           5. 明确识别以下测试所需的关键信息：
                              - RunSqlDataOperation方法的完整签名
                              - 返回数据的准确类型和结构
                              - SQL查询获取的数据字段和表
                              - 必要的参数值和常量
                           6. 忽略SQL错误或数据库连接失败的情况，以及其他不相关的异常处理逻辑
                           
                           请以结构化格式提供分析结果，重点突出如何有效测试RunSqlDataOperation方法。
                           """,
            Name = "CodeAnalyzer",
            Arguments = new KernelArguments(new PromptExecutionSettings()
            {
                ServiceId = "openai-gpt4o",
                FunctionChoiceBehavior = FunctionChoiceBehavior.Required(),
            }),
            Kernel = kernel,
        };
        return agentTranslator;
    }

    private async Task<ChatCompletionAgent> TestDesignerAsync(Kernel kernel)
    {
        var agentTranslator = new ChatCompletionAgent()
        {
            Instructions = """
                           你是一位单元测试设计专家，根据CodeAnalyzer提供的分析结果，你需要：
                           1. 为入口方法设计测试策略
                           2. 确定需要测试的正常路径场景
                           3. 确定需要测试的边界条件和异常路径(忽略SQL错误或数据库连接失败情况)
                           4. 规划需要测试的典型输入组合
                           5. 确定需要验证的预期输出和状态
                           6. 涉及到依赖注入的服务，不需要设计相应的模拟对象

                           设计全面的测试计划，确保高测试覆盖率。
                           """,
            Name = "TestDesigner",
            Arguments = new KernelArguments(new OpenAIPromptExecutionSettings()
            {
                ServiceId = "openai-gpt4o",
                FunctionChoiceBehavior = FunctionChoiceBehavior.Required(),
            }),
            Kernel = kernel,
        };
        return agentTranslator;
    }

    private async Task<ChatCompletionAgent> DataGeneratorAsync(Kernel kernel)
    {
        var agentTranslator = new ChatCompletionAgent()
        {
            Instructions = """
                           你是一位测试数据生成专家，根据TestDesigner提供的测试策略，你需要：
                           1. 为每个测试场景生成适当的JSON格式测试数据
                           2. 创建边界条件测试数据（最小值、最大值、空值等）
                           3. 生成异常路径的测试数据
                           4. 涉及到依赖注入的实体或者服务，不需要出现在JSON数据的Dto中

                           你必须始终使用以下JSON格式输出测试数据：
                           ```json
                           [
                             {
                               "TestCaseName": "方法名_条件_预期结果",
                               "Dto": {
                                 // 输入参数，与方法参数匹配
                                 "属性1": "值1",
                                 "属性2": "值2"
                                 // 更多的属性....
                               },
                               "ExpectedResult": {
                                 "Data": {
                                   // 预期返回的数据
                                   "属性1": "值1",
                                   "属性2": "值2"
                                   // 更多的属性....
                                 },
                                 "Succeeded": true/false,
                                 "StatusCode": "Success/Error/等",
                                 "Message": "成功/失败消息"
                               },
                               "PreRunData": null // 或添加测试前需要准备的数据
                             },
                             // 更多测试用例...
                           ]
                           ```

                           确保：
                           1. 每个测试用例的TestCaseName都是唯一的且描述性的
                           2. Dto包含所有必要的输入参数,没有的时候可以设置为null
                           3. ExpectedResult定义正确的预期输出结构
                           4. 数据类型与方法参数和返回值匹配
                           5. 为每种情况（正常路径、边界条件、异常情况）生成测试数据
                           
                           请确保你的响应仅包含JSON格式的测试数据，便于TestWriter直接使用。
                           在你的JSON响应后，请添加这一行标记：##ActionName:[对应的Action类名]##
                           你的响应将被自动保存为[ActionName]TestCase.json文件。
                           """,
            Name = "DataGenerator",
            Arguments = new KernelArguments(new OpenAIPromptExecutionSettings()
            {
                ServiceId = "openai-gpt4o",
                Temperature = 0.1,
                FunctionChoiceBehavior = FunctionChoiceBehavior.Required(),
            }),
            Kernel = kernel,
        };
        return agentTranslator;
    }

    private async Task<ChatCompletionAgent> TestWriterAsync(Kernel kernel)
    {
        var agentTranslator = new ChatCompletionAgent()
        {
            Instructions = """
                           你是一位单元测试代码生成专家，你的任务是创建可以立即添加到项目中的完整、可执行的测试类文件。

                           根据提供的代码分析和测试设计：
                           1. 生成单个完整的测试类文件，包含所有必要的测试方法
                           2. 实现从 DataGenerator 提供的 JSON 测试数据加载的逻辑
                           3. 确保测试代码包含完整的断言逻辑
                           4. 添加适当的注释和文档
                           5. 确保替换以下占位符：
                           - [ProjectNamespace]：被测试代码的命名空间
                           - [TestNamespace]：测试项目的命名空间
                           - [ActionName]：被测试的Action名称（不包含Action后缀）
                           - [TestMethodName]：描述性的测试方法名称（通常匹配Action名称）
                           - [ActionClassName]：完整的Action类名（包含Action后缀）
                           - [ReturnType]：API返回的数据类型
                           - [CategoryPath]：测试用例JSON文件的类别路径
                           - [TestCaseFileName]：测试用例JSON文件的名称

                           生成的代码必须符合以下要求：
                           - 文件名遵循 "[TestedClassName]Tests.cs" 的命名约定
                           - 代码必须是完整的、无错误的、可直接编译的
                           - 包含适当的异常处理和资源清理
                           - 遵循项目的代码风格和最佳实践

                           请使用以下模板编写GET请求的单元测试：
                           ```csharp
                           using Newtonsoft.Json;
                           using NUnit.Framework;
                           using RSP.Common.DataAccess;
                           using RSP.Common;
                           using System;
                           using System.Collections.Generic;
                           using System.IO;
                           using System.Threading.Tasks;
                           
                           namespace RSP.PricingTracking.UnitTest.ActionTest.Agent;
                           
                           public class [TestedClassName]Test: ActionTestBase
                           {
                               [Test]
                               [TestCaseSource(nameof(GetTestCases))]
                               public async Task [TestMethodName](TestCaseModel testCase)
                               {
                                   IADOConfigurable config = GetADOConfigurationForLocal(true);
                           
                                   try
                                   {
                                       // 根据需要测试的方法和返回值类型进行调整,args允许传递参数给对应的方法，判断入参是否需要包含Dto
                                       IEnumerable<object> args = new List<object> { config };
                                       this.TestAction<[ActionClassName], [ReturnType]>(args, testCase.ExpectedResult.SwitchType<[ReturnType]>());
                                   }
                                   catch (Exception ex)
                                   {
                                       Assert.Fail($"[TestMethodName] has error. " + ex.Message);
                                       throw ex;
                                   }
                               }
                           
                               private static List<TestCaseModel> GetTestCases()
                               {
                                   string filePath = AppContext.BaseDirectory +"\\ActionTest\\[FolderPath]\\TestCase\\[TestCaseFileName].json";
                                   string jsonContent = File.ReadAllText(filePath);
                           
                                   var settings = new JsonSerializerSettings
                                   {
                                       FloatParseHandling = FloatParseHandling.Decimal
                                   };
                                   return JsonConvert.DeserializeObject<List<TestCaseModel>>(jsonContent, settings);
                               }
                           }
                           ```

                           输出格式必须严格按照以下格式：
                           ## 文件名：[TestedClassName]Test.cs

                           ```csharp
                           // 完整的测试类代码
                           ```

                           请始终使用上述精确的格式标记文件名和代码块，以便系统能自动提取并保存文件。
                           """,
            Name = "TestWriter",
            Kernel = kernel,
            Arguments = new KernelArguments(new OpenAIPromptExecutionSettings()
            {
                ServiceId = "openai-gpt4o",
                Temperature = 0.1,
                FunctionChoiceBehavior = FunctionChoiceBehavior.Required(),
            }),
        };
        return agentTranslator;
    }

    private async Task<ChatCompletionAgent> TestReviewerAsync(Kernel kernel)
    {
        var agentTranslator = new ChatCompletionAgent()
        {
            Instructions = """
                           你是一位单元测试代码质量审查专家。你的任务是对 TestWriter 生成的测试类进行全面审查，确保：

                           1. 代码符合最佳实践和项目标准
                           2. 测试覆盖全面，包括正常路径、边界条件和异常情况
                           3. 断言逻辑正确且充分
                           4. 测试代码结构清晰，可读性强
                           5. 代码无编译错误和潜在运行时问题
                           6. 模拟对象和测试夹具设置正确
                           7. 测试数据加载和使用适当

                           如发现问题，请提供：
                           1. 具体问题描述
                           2. 修复建议，包括代码片段
                           3. 改进建议，提高测试质量和覆盖率

                           如果测试代码已经符合高质量标准，请确认并总结其优点。
                           """,
            Name = "TestReviewer",
            Kernel = kernel,
            Arguments = new KernelArguments(new OpenAIPromptExecutionSettings()
            {
                ServiceId = "openai-gpt4o",
                Temperature = 0.2,
                FunctionChoiceBehavior = FunctionChoiceBehavior.Required(),
            }),
        };
        return agentTranslator;
    }

    public async Task<string> SaveGeneratedJsonDataAsync(string content)
    {
        try
        {
            // Extract the JSON part from the response
            var jsonMatch = Regex.Match(content, @"```json\s*([\s\S]*?)\s*```");
            if (!jsonMatch.Success)
            {
                Console.WriteLine("\n无法从输出中提取JSON数据。");
                return "";
            }
        
            string jsonContent = jsonMatch.Groups[1].Value;

            return jsonContent;
            
            // Extract the action name
            var actionNameMatch = Regex.Match(content, @"##ActionName:(\w+)##");
            if (!actionNameMatch.Success)
            {
                Console.WriteLine("\n无法从输出中识别Action名称。");
                return "";
            }
        
            string actionName = actionNameMatch.Groups[1].Value;
            string fileName = $"{actionName}TestCase.json";
        
            // Get the project directory structure
            string projectDir = "F:\\code\\Austin\\rsp.unitTest.agent\\rsp.unitTest.agent\\ActionTest";
            string testCasesDir = Path.Combine(projectDir, "TestCases");
        
            // Create directory if it doesn't exist
            if (!Directory.Exists(testCasesDir))
            {
                Directory.CreateDirectory(testCasesDir);
            }
        
            // Save the file
            string filePath = Path.Combine(testCasesDir, fileName);
            await File.WriteAllTextAsync(filePath, jsonContent);
        
            Console.WriteLine($"\n测试数据已保存至：{filePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n保存测试数据时出错：{ex.Message}");
        }

        return "";
    }
    
    // 添加此方法用于保存文件
    public async Task<string> SaveGeneratedTestFileAsync(string content)
    {
        try
        {
            // 使用正则表达式提取文件名（更精确的匹配）
            var fileNameMatch = Regex.Match(content, @"##\s*文件名：\s*(\w+\.cs)");
            if (!fileNameMatch.Success)
            {
                Console.WriteLine("\n无法从输出中识别文件名。");
                return "";
            }

            string fileName = fileNameMatch.Groups[1].Value;

            // 提取代码部分
            var codeMatch = Regex.Match(content, @"```csharp\s*([\s\S]*?)\s*```");
            if (!codeMatch.Success)
            {
                Console.WriteLine("\n无法从输出中提取代码块。");
                return "";
            }

            string code = codeMatch.Groups[1].Value;

            
            return code;
            
            // 获取测试项目路径
            string testsDir = "F:\\code\\Austin\\rsp.unitTest.agent\\rsp.unitTest.agent\\ActionTest";

            // 如果测试目录不存在，则创建
            if (!Directory.Exists(testsDir))
            {
                Directory.CreateDirectory(testsDir);
            }

            // 保存文件
            string filePath = Path.Combine(testsDir, fileName);
            await File.WriteAllTextAsync(filePath, code);

            Console.WriteLine($"\n测试文件已成功保存至：{filePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n保存测试文件时出错：{ex.Message}");
        }

        return "";
    }
}
