﻿using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services;

public class CreateLineElementEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
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
    public List<LineElement> CreatedInfo { get; private set; }
    /// <summary>
    /// 执行结果（传出数据）
    /// </summary>
    public AIResult<List<int>> Result { get; private set; }

    public string _wallName = "常规 - ";
    public string _ductName = "矩形风管 - ";

    /// <summary>
    /// 设置创建的参数
    /// </summary>
    public void SetParameters(List<LineElement> data)
    {
        CreatedInfo = data;
        _resetEvent.Reset();
    }
    public void Execute(UIApplication uiapp)
    {
        uiApp = uiapp;

        try
        {
            var elementIds = new List<int>();
            foreach (var data in CreatedInfo)
            {
                // Step0 获取构件类型
                var builtInCategory = BuiltInCategory.INVALID;
                Enum.TryParse(data.Category.Replace(".", ""), true, out builtInCategory);

                // Step1 获取标高和偏移
                Level baseLevel = null;
                Level topLevel = null;
                double topOffset = -1;  // ft
                double baseOffset = -1; // ft
                baseLevel = doc.FindNearestLevel(data.BaseLevel / 304.8);
                baseOffset = (data.BaseOffset + data.BaseLevel) / 304.8 - baseLevel.Elevation;
                topLevel = doc.FindNearestLevel((data.BaseLevel + data.BaseOffset + data.Height) / 304.8);
                topOffset = (data.BaseLevel + data.BaseOffset + data.Height) / 304.8 - topLevel.Elevation;
                if (baseLevel == null)
                    continue;

                // Step2 获取族类型
                FamilySymbol symbol = null;
                WallType wallType = null;
                DuctType ductType = null;

                if (data.TypeId != -1 && data.TypeId != 0)
                {
                    var typeELeId = new ElementId(data.TypeId);
                    if (typeELeId != null)
                    {
                        var typeEle = doc.GetElement(typeELeId);
                        if (typeEle != null && typeEle is FamilySymbol)
                        {
                            symbol = typeEle as FamilySymbol;
                            // 获取symbol的Category对象并转换为BuiltInCategory枚举
                            builtInCategory = (BuiltInCategory)symbol.Category.Id.IntegerValue;
                        }
                        else if (typeEle != null && typeEle is WallType)
                        {
                            wallType = typeEle as WallType;
                            builtInCategory = (BuiltInCategory)wallType.Category.Id.IntegerValue;
                        }
                        else if (typeEle != null && typeEle is DuctType)
                        {
                            ductType = typeEle as DuctType;
                            builtInCategory = (BuiltInCategory)ductType.Category.Id.IntegerValue;
                        }
                    }
                }
                if (builtInCategory == BuiltInCategory.INVALID)
                    continue;
                switch (builtInCategory)
                {
                    case BuiltInCategory.OST_Walls:
                        if (wallType == null)
                        {
                            using (var transaction = new Transaction(doc, "创建墙类型"))
                            {
                                transaction.Start();
                                wallType = CreateOrGetWallType(doc, data.Thickness / 304.8);
                                transaction.Commit();
                            }
                            if (wallType == null)
                                continue;
                        }
                        break;
                    case BuiltInCategory.OST_DuctCurves:
                        if (ductType == null)
                        {
                            using (var transaction = new Transaction(doc, "创建风管类型"))
                            {
                                transaction.Start();
                                ductType = CreateOrGetDuctType(doc, data.Thickness / 304.8, data.Height / 304.8);
                                transaction.Commit();
                            }
                            if (ductType == null)
                                continue;
                        }
                        break;
                    default:
                        if (symbol == null)
                        {
                            symbol = new FilteredElementCollector(doc)
                                .OfClass(typeof(FamilySymbol))
                                .OfCategory(builtInCategory)
                                .Cast<FamilySymbol>()
                                .FirstOrDefault(fs => fs.IsActive); // 获取激活的类型作为默认类型
                            if (symbol == null)
                            {
                                symbol = new FilteredElementCollector(doc)
                                    .OfClass(typeof(FamilySymbol))
                                    .OfCategory(builtInCategory)
                                    .Cast<FamilySymbol>()
                                    .FirstOrDefault();
                            }
                        }
                        if (symbol == null)
                            continue;
                        break;
                }

                // Step3 调用通用方法创建族实例
                using (var transaction = new Transaction(doc, "创建点状构件"))
                {
                    transaction.Start();
                    switch (builtInCategory)
                    {
                        case BuiltInCategory.OST_Walls:
                            Wall wall = null;
                            wall = Wall.Create
                            (
                                doc,
                                JZLine.ToLine(data.LocationLine),
                                wallType.Id,
                                baseLevel.Id,
                                data.Height / 304.8,
                                baseOffset,
                                false,
                                false
                            );
                            if (wall != null)
                            {
                                elementIds.Add(wall.Id.IntegerValue);
                            }
                            break;
                        case BuiltInCategory.OST_DuctCurves:
                            Duct duct = null;
                            // 获取MEP系统类型（必需）
                            var mepSystemType = new FilteredElementCollector(doc)
                                .OfClass(typeof(MEPSystemType))
                                .Cast<MEPSystemType>()
                                .FirstOrDefault(m => m.SystemClassification == MEPSystemClassification.SupplyAir);

                            if (mepSystemType != null)
                            {
                                duct = Duct.Create(
                                    doc,
                                    mepSystemType.Id,
                                    ductType.Id,
                                    baseLevel.Id,
                                    JZLine.ToLine(data.LocationLine).GetEndPoint(0),
                                    JZLine.ToLine(data.LocationLine).GetEndPoint(1)
                                );

                                if (duct != null)
                                {
                                    // 设置高度偏移
                                    var offsetParam = duct.get_Parameter(BuiltInParameter.RBS_OFFSET_PARAM);
                                    if (offsetParam != null)
                                        offsetParam.Set(baseOffset);
                                    elementIds.Add(duct.Id.IntegerValue);
                                }
                            }
                            break;
                        default:
                            if (!symbol.IsActive)
                                symbol.Activate();

                            // 调用FamilyInstance通用创建方法
                            var instance = doc.CreateInstance(symbol, null, JZLine.ToLine(data.LocationLine), baseLevel, topLevel, baseOffset, topOffset);
                            if (instance != null)
                            {
                                elementIds.Add(instance.Id.IntegerValue);
                            }
                            break;
                    }
                    //doc.Refresh();
                    transaction.Commit();
                }
            }
            Result = new AIResult<List<int>>
            {
                Success = true,
                Message = $"成功创建{elementIds.Count}个族实例，其ElementId储存在Response属性中",
                Response = elementIds,
            };
        }
        catch (Exception ex)
        {
            Result = new AIResult<List<int>>
            {
                Success = false,
                Message = $"创建线状构件时出错: {ex.Message}",
            };
            TaskDialog.Show("错误", $"创建线状构件时出错: {ex.Message}");
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
        return "创建线状构件";
    }

    /// <summary>
    /// 创建或获取指定厚度的墙体类型
    /// </summary>
    /// <param name="doc">Revit文档</param>
    /// <param name="width">宽度（ft）</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private WallType CreateOrGetWallType(Document doc, double width = 200 / 304.8)
    {
        // 如果没有有效的类型
        // 先查找是否存在指定厚度的建筑墙类型
        var existingType = new FilteredElementCollector(doc)
            .OfClass(typeof(WallType))
            .Cast<WallType>()
            .FirstOrDefault(w => w.Name == $"{_wallName}{width * 304.8}mm");
        if (existingType != null)
            return existingType;

        // 不存在则创建新的墙体类型，基于基本墙
        var baseWallType = new FilteredElementCollector(doc)
            .OfClass(typeof(WallType))
            .Cast<WallType>()
            .FirstOrDefault(w => w.Name.Contains("常规")); ;
        if (baseWallType == null)
        {
            baseWallType = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(); ;
        }

        if (baseWallType == null)
            throw new InvalidOperationException("未找到可用的基础墙类型");

        // 复制墙体类型
        WallType newWallType = null;
        newWallType = baseWallType.Duplicate($"{_wallName}{width * 304.8}mm") as WallType;

        // 设置墙厚
        var cs = newWallType.GetCompoundStructure();
        if (cs != null)
        {
            // 获取原始层的材料ID
            var materialId = cs.GetLayers().First().MaterialId;

            // 创建新的单层结构
            var newLayer = new CompoundStructureLayer(
                width,  // 宽度（转换为英尺）
                MaterialFunctionAssignment.Structure,  // 功能分配
                materialId  // 材料ID
            );

            // 创建新的复合结构
            IList<CompoundStructureLayer> newLayers = new List<CompoundStructureLayer> { newLayer };
            cs.SetLayers(newLayers);

            // 应用新的复合结构
            newWallType.SetCompoundStructure(cs);
        }
        return newWallType;
    }

    /// <summary>
    /// 创建或获取指定尺寸的风管类型
    /// </summary>
    /// <param name="doc">Revit文档</param>
    /// <param name="width">宽度（ft）</param>
    /// <param name="height">高度（ft）</param>
    /// <returns>风管类型</returns>
    private DuctType CreateOrGetDuctType(Document doc, double width, double height)
    {
        var typeName = $"{_ductName}{width * 304.8}x{height * 304.8}mm";

        // 先查找是否存在指定尺寸的风管类型
        var existingType = new FilteredElementCollector(doc)
            .OfClass(typeof(DuctType))
            .Cast<DuctType>()
            .FirstOrDefault(d => d.Name == typeName && d.Shape == ConnectorProfileType.Rectangular);

        if (existingType != null)
            return existingType;

        // 不存在则创建新的风管类型，基于已有的矩形风管类型
        var baseDuctType = new FilteredElementCollector(doc)
            .OfClass(typeof(DuctType))
            .Cast<DuctType>()
            .FirstOrDefault(d => d.Shape == ConnectorProfileType.Rectangular);

        if (baseDuctType == null)
            throw new InvalidOperationException("未找到可用的基础矩形风管类型");

        // 复制风管类型
        var newDuctType = baseDuctType.Duplicate(typeName) as DuctType;

        // 设置风管尺寸参数
        var widthParam = newDuctType.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
        var heightParam = newDuctType.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);

        if (widthParam != null && heightParam != null)
        {
            widthParam.Set(width);
            heightParam.Set(height);
        }

        return newDuctType;
    }

}