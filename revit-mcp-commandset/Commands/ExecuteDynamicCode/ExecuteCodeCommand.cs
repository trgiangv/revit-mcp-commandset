﻿using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.ExecuteDynamicCode;

/// <summary>
/// 处理代码执行的命令类
/// </summary>
public class ExecuteCodeCommand : ExternalEventCommandBase
{
    private ExecuteCodeEventHandler _handler => (ExecuteCodeEventHandler)Handler;

    public override string CommandName => "send_code_to_revit";

    public ExecuteCodeCommand(UIApplication uiApp)
        : base(new ExecuteCodeEventHandler(), uiApp)
    {
    }

    public override object Execute(JObject parameters, string requestId)
    {
        try
        {
            // 参数验证
            if (!parameters.ContainsKey("code"))
            {
                throw new ArgumentException("Missing required parameter: 'code'");
            }

            // 解析代码和参数
            var code = parameters["code"].Value<string>();
            var parametersArray = parameters["parameters"] as JArray;
            var executionParameters = parametersArray?.ToObject<object[]>() ?? Array.Empty<object>();

            // 设置执行参数
            _handler.SetExecutionParameters(code, executionParameters);

            // 触发外部事件并等待完成
            if (RaiseAndWaitForCompletion(60000)) // 1分钟超时
            {
                return _handler.ResultInfo;
            }
            else
            {
                throw new TimeoutException("代码执行超时");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"执行代码失败: {ex.Message}", ex);
        }
    }
}