﻿using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Services;

namespace RevitMCPCommandSet.Commands;

public class OperateElementCommand : ExternalEventCommandBase
{
    private OperateElementEventHandler _handler => (OperateElementEventHandler)Handler;

    /// <summary>
    /// 命令名称
    /// </summary>
    public override string CommandName => "operate_element";

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="uiApp">Revit UIApplication</param>
    public OperateElementCommand(UIApplication uiApp)
        : base(new OperateElementEventHandler(), uiApp)
    {
    }

    public override object Execute(JObject parameters, string requestId)
    {
        try
        {
            var data = new OperationSetting();
            // 解析参数
            data = parameters["data"].ToObject<OperationSetting>();
            if (data == null)
                throw new ArgumentNullException(nameof(data), "AI传入数据为空");

            // 设置点状构件体参数
            _handler.SetParameters(data);

            // 触发外部事件并等待完成
            if (RaiseAndWaitForCompletion(10000))
            {
                return _handler.Result;
            }
            else
            {
                throw new TimeoutException("操作元素超时");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"操作元素失败: {ex.Message}");
        }
    }
}