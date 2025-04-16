﻿using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using RevitMCPCommandSet.Models.Common;

namespace RevitMCPCommandSet.Services;

public class OperateElementEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    private UIApplication uiApp;
    private UIDocument uiDoc => uiApp.ActiveUIDocument;
    private Document doc => uiDoc.Document;
    private Autodesk.Revit.ApplicationServices.Application app => uiApp.Application;

    /// <summary>
    /// 事件等待对象
    /// </summary>
    private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
    /// <summary>
    /// 创建数据（传入数据）
    /// </summary>
    public OperationSetting OperationData { get; private set; }
    /// <summary>
    /// 执行结果（传出数据）
    /// </summary>
    public AIResult<string> Result { get; private set; }

    /// <summary>
    /// 设置创建的参数
    /// </summary>
    public void SetParameters(OperationSetting data)
    {
        OperationData = data;
        _resetEvent.Reset();
    }
    public void Execute(UIApplication uiapp)
    {
        uiApp = uiapp;

        try
        {
            var result = ExecuteElementOperation(uiDoc, OperationData);

            Result = new AIResult<string>
            {
                Success = true,
                Message = $"成功执行操作",
            };
        }
        catch (Exception ex)
        {
            Result = new AIResult<string>
            {
                Success = false,
                Message = $"操作元素时出错: {ex.Message}",
            };
        }
        finally
        {
            _resetEvent.Set(); // 通知等待线程操作已完成
        }
    }

    /// <summary>
    /// 等待创建完成
    /// </summary>
    /// <param name="timeoutMilliseconds">超时时间（毫秒）</param>
    /// <returns>操作是否在超时前完成</returns>
    public bool WaitForCompletion(int timeoutMilliseconds = 10000)
    {
        return _resetEvent.WaitOne(timeoutMilliseconds);
    }

    /// <summary>
    /// IExternalEventHandler.GetName 实现
    /// </summary>
    public string GetName()
    {
        return "操作元素";
    }

    /// <summary>
    /// 根据操作设置执行相应的图元操作
    /// </summary>
    /// <param name="uidoc">当前UI文档</param>
    /// <param name="setting">操作设置</param>
    /// <returns>操作是否成功</returns>
    public static bool ExecuteElementOperation(UIDocument uidoc, OperationSetting setting)
    {
        // 检查参数有效性
        if (uidoc == null || uidoc.Document == null || setting == null || setting.ElementIds == null ||
            (setting.ElementIds.Count == 0 && setting.Action.ToLower() != "resetisolate"))
            throw new Exception("参数无效：文档为空或没有指定要操作的图元");

        var doc = uidoc.Document;

        // 将int类型的元素ID转换为ElementId类型
        ICollection<ElementId> elementIds = setting.ElementIds.Select(id => new ElementId(id)).ToList();

        // 解析操作类型
        ElementOperationType action;
        if (!Enum.TryParse(setting.Action, true, out action))
        {
            throw new Exception($"未支持的操作类型：{setting.Action}");
        }

        // 根据操作类型执行不同的操作
        switch (action)
        {
            case ElementOperationType.Select:
                // 选择元素
                uidoc.Selection.SetElementIds(elementIds);
                return true;

            case ElementOperationType.SelectionBox:
                // 在3D视图中创建剖切框

                // 检查当前视图是否为3D视图
                View3D targetView;

                if (doc.ActiveView is View3D)
                {
                    // 如果当前视图是3D视图，在当前视图中创建剖切框
                    targetView = doc.ActiveView as View3D;
                }
                else
                {
                    // 如果当前视图不是3D视图，寻找默认3D视图
                    var collector = new FilteredElementCollector(doc);
                    collector.OfClass(typeof(View3D));

                    // 尝试找到默认3D视图或任何其他可用的3D视图
                    targetView = collector
                        .Cast<View3D>()
                        .FirstOrDefault(v => !v.IsTemplate && !v.IsLocked && (v.Name.Contains("{3D}") || v.Name.Contains("Default 3D")));

                    if (targetView == null)
                    {
                        // 如果没有找到合适的3D视图，抛出异常
                        throw new Exception("无法找到合适的3D视图用于创建剖切框");
                    }

                    // 激活该3D视图
                    uidoc.ActiveView = targetView;
                }

                // 计算所选元素的包围盒
                BoundingBoxXYZ boundingBox = null;

                foreach (var id in elementIds)
                {
                    var elem = doc.GetElement(id);
                    var elemBox = elem.get_BoundingBox(null);

                    if (elemBox != null)
                    {
                        if (boundingBox == null)
                        {
                            boundingBox = new BoundingBoxXYZ
                            {
                                Min = new XYZ(elemBox.Min.X, elemBox.Min.Y, elemBox.Min.Z),
                                Max = new XYZ(elemBox.Max.X, elemBox.Max.Y, elemBox.Max.Z)
                            };
                        }
                        else
                        {
                            // 扩展边界框以包含当前元素
                            boundingBox.Min = new XYZ(
                                Math.Min(boundingBox.Min.X, elemBox.Min.X),
                                Math.Min(boundingBox.Min.Y, elemBox.Min.Y),
                                Math.Min(boundingBox.Min.Z, elemBox.Min.Z));

                            boundingBox.Max = new XYZ(
                                Math.Max(boundingBox.Max.X, elemBox.Max.X),
                                Math.Max(boundingBox.Max.Y, elemBox.Max.Y),
                                Math.Max(boundingBox.Max.Z, elemBox.Max.Z));
                        }
                    }
                }

                if (boundingBox == null)
                {
                    throw new Exception("无法为所选元素创建边界框");
                }

                // 增加边界框尺寸，使其略大于元素
                var offset = 1.0; // 1英尺的偏移
                boundingBox.Min = new XYZ(boundingBox.Min.X - offset, boundingBox.Min.Y - offset, boundingBox.Min.Z - offset);
                boundingBox.Max = new XYZ(boundingBox.Max.X + offset, boundingBox.Max.Y + offset, boundingBox.Max.Z + offset);

                // 在3D视图中启用并设置剖切框
                using (var trans = new Transaction(doc, "创建剖切框"))
                {
                    trans.Start();
                    targetView.IsSectionBoxActive = true;
                    targetView.SetSectionBox(boundingBox);
                    trans.Commit();
                }

                // 移动到视图中心
                uidoc.ShowElements(elementIds);
                return true;

            case ElementOperationType.SetColor:
                // 将元素设置为指定颜色
                using (var trans = new Transaction(doc, "设置元素颜色"))
                {
                    trans.Start();
                    SetElementsColor(doc, elementIds, setting.ColorValue);
                    trans.Commit();
                }
                // 滚动到这些元素使其可见
                uidoc.ShowElements(elementIds);
                return true;


            case ElementOperationType.SetTransparency:
                // 设置元素在当前视图中的透明度
                using (var trans = new Transaction(doc, "设置元素透明度"))
                {
                    trans.Start();

                    // 创建图形覆盖设置对象
                    var overrideSettings = new OverrideGraphicSettings();

                    // 设置透明度(确保值在0-100范围内)
                    var transparencyValue = Math.Max(0, Math.Min(100, setting.TransparencyValue));

                    // 设置表面透明度
                    overrideSettings.SetSurfaceTransparency(transparencyValue);

                    // 对每个元素应用透明度设置
                    foreach (var id in elementIds)
                    {
                        doc.ActiveView.SetElementOverrides(id, overrideSettings);
                    }

                    trans.Commit();
                }
                return true;

            case ElementOperationType.Delete:
                // 删除元素（需要事务）
                using (var trans = new Transaction(doc, "删除元素"))
                {
                    trans.Start();
                    doc.Delete(elementIds);
                    trans.Commit();
                }
                return true;

            case ElementOperationType.Hide:
                // 隐藏元素（需要活动视图和事务）
                using (var trans = new Transaction(doc, "隐藏元素"))
                {
                    trans.Start();
                    doc.ActiveView.HideElements(elementIds);
                    trans.Commit();
                }
                return true;

            case ElementOperationType.TempHide:
                // 临时隐藏元素（需要活动视图和事务）
                using (var trans = new Transaction(doc, "临时隐藏元素"))
                {
                    trans.Start();
                    doc.ActiveView.HideElementsTemporary(elementIds);
                    trans.Commit();
                }
                return true;

            case ElementOperationType.Isolate:
                // 隔离元素（需要活动视图和事务）
                using (var trans = new Transaction(doc, "隔离元素"))
                {
                    trans.Start();
                    doc.ActiveView.IsolateElementsTemporary(elementIds);
                    trans.Commit();
                }
                return true;

            case ElementOperationType.Unhide:
                // 取消隐藏元素（需要活动视图和事务）
                using (var trans = new Transaction(doc, "取消隐藏元素"))
                {
                    trans.Start();
                    doc.ActiveView.UnhideElements(elementIds);
                    trans.Commit();
                }
                return true;

            case ElementOperationType.ResetIsolate:
                // 重置隔离（需要活动视图和事务）
                using (var trans = new Transaction(doc, "重置隔离"))
                {
                    trans.Start();
                    doc.ActiveView.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                    trans.Commit();
                }
                return true;

            default:
                throw new Exception($"未支持的操作类型：{setting.Action}");
        }
    }

    /// <summary>
    /// 在视图中将指定的元素设置为指定颜色
    /// </summary>
    /// <param name="doc">文档</param>
    /// <param name="elementIds">要设置颜色的元素ID集合</param>
    /// <param name="elementColor">颜色值（RGB格式）</param>
    private static void SetElementsColor(Document doc, ICollection<ElementId> elementIds, int[] elementColor)
    {
        // 检查颜色数组是否有效
        if (elementColor == null || elementColor.Length < 3)
        {
            elementColor = new int[] { 255, 0, 0 }; // 默认红色
        }
        // 确保RGB值在0-255范围内
        var r = Math.Max(0, Math.Min(255, elementColor[0]));
        var g = Math.Max(0, Math.Min(255, elementColor[1]));
        var b = Math.Max(0, Math.Min(255, elementColor[2]));
        // 创建Revit颜色对象 - 使用byte类型转换
        var color = new Color((byte)r, (byte)g, (byte)b);
        // 创建图形覆盖设置
        var overrideSettings = new OverrideGraphicSettings();
        // 设置指定颜色
        overrideSettings.SetProjectionLineColor(color);
        overrideSettings.SetCutLineColor(color);
        overrideSettings.SetSurfaceForegroundPatternColor(color);
        overrideSettings.SetSurfaceBackgroundPatternColor(color);

        // 尝试设置填充图案
        try
        {
            // 尝试获取默认的填充图案
            var patternCollector = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement));

            // 首先尝试找到实心填充图案
            var solidPattern = patternCollector
                .Cast<FillPatternElement>()
                .FirstOrDefault(p => p.GetFillPattern().IsSolidFill);

            if (solidPattern != null)
            {
                overrideSettings.SetSurfaceForegroundPatternId(solidPattern.Id);
                overrideSettings.SetSurfaceForegroundPatternVisible(true);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"设置填充图案失败: {ex.Message}");
        }

        // 对每个元素应用覆盖设置
        foreach (var id in elementIds)
        {
            doc.ActiveView.SetElementOverrides(id, overrideSettings);
        }
    }

}