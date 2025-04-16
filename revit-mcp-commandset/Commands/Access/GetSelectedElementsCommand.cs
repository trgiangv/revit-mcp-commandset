﻿using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Access;

public class GetSelectedElementsCommand : ExternalEventCommandBase
{
    private GetSelectedElementsEventHandler _handler => (GetSelectedElementsEventHandler)Handler;

    public override string CommandName => "get_selected_elements";

    public GetSelectedElementsCommand(UIApplication uiApp)
        : base(new GetSelectedElementsEventHandler(), uiApp)
    {
    }

    public override object Execute(JObject parameters, string requestId)
    {
        try
        {
            // 解析参数
            var limit = parameters?["limit"]?.Value<int>();

            // 设置数量限制
            _handler.Limit = limit;

            // 触发外部事件并等待完成
            if (RaiseAndWaitForCompletion(15000))
            {
                return _handler.ResultElements;
            }
            else
            {
                throw new TimeoutException("获取选中元素超时");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"获取选中元素失败: {ex.Message}");
        }
    }
}