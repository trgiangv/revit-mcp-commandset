﻿using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Access;

public class GetAvailableFamilyTypesCommand : ExternalEventCommandBase
{
    private GetAvailableFamilyTypesEventHandler _handler => (GetAvailableFamilyTypesEventHandler)Handler;

    public override string CommandName => "get_available_family_types";

    public GetAvailableFamilyTypesCommand(UIApplication uiApp)
        : base(new GetAvailableFamilyTypesEventHandler(), uiApp)
    {
    }

    public override object Execute(JObject parameters, string requestId)
    {
        try
        {
            // 解析参数
            var categoryList = parameters?["categoryList"]?.ToObject<List<string>>() ?? new List<string>();
            var familyNameFilter = parameters?["familyNameFilter"]?.Value<string>();
            var limit = parameters?["limit"]?.Value<int>();

            // 设置查询参数
            _handler.CategoryList = categoryList;
            _handler.FamilyNameFilter = familyNameFilter;
            _handler.Limit = limit;

            // 触发外部事件并等待完成，最多等待15秒
            if (RaiseAndWaitForCompletion(15000))
            {
                return _handler.ResultFamilyTypes;
            }
            else
            {
                throw new TimeoutException("获取可用族类型超时");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"获取可用族类型失败: {ex.Message}");
        }
    }
}