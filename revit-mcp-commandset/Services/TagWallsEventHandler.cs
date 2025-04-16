﻿using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services;

public class TagWallsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
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
    /// 标记结果数据
    /// </summary>
    public object TaggingResults { get; private set; }

    private bool _useLeader;
    private string _tagTypeId;

    /// <summary>
    /// 设置创建的参数
    /// </summary>
    public void SetParameters(bool useLeader, string tagTypeId)
    {
        _useLeader = useLeader;
        _tagTypeId = tagTypeId;
        _resetEvent.Reset();
    }

    public void Execute(UIApplication uiapp)
    {
        uiApp = uiapp;

        try
        {
            var activeView = doc.ActiveView;

            // Get all walls in the current view
            var wallCollector = new FilteredElementCollector(doc, activeView.Id);
            ICollection<Element> walls = wallCollector.OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType()
                .ToElements();

            // Create wall tags
            var createdTags = new List<object>();
            var errors = new List<string>();

            using (var tran = new Transaction(doc, "标记墙体"))
            {
                tran.Start();

                // Find the wall tag type
                var wallTagType = FindWallTagType(doc);

                if (wallTagType == null)
                {
                    TaggingResults = new
                    {
                        success = false,
                        message = "没有找到墙标记族类型"
                    };
                    tran.RollBack();
                    return;
                }

                // Ensure tag type is active
                if (!wallTagType.IsActive)
                {
                    wallTagType.Activate();
                    doc.Regenerate();
                }

                // Create tags for each wall
                foreach (var wall in walls)
                {
#if REVIT2024_OR_GREATER
                    try
                    {
                        // Get the wall's location curve
                        LocationCurve locationCurve = wall.Location as LocationCurve;
                        if (locationCurve != null)
                        {
                            // Get the middle point of the wall
                            Curve curve = locationCurve.Curve;
                            XYZ midpoint = curve.Evaluate(0.5, true);

                            // Create tag at midpoint
                            IndependentTag tag = IndependentTag.Create(
                                doc,
                                wallTagType.Id,
                                activeView.Id,
                                new Reference(wall),
                                _useLeader, // Use leader based on parameter
                                TagOrientation.Horizontal,
                                midpoint);

                            if (tag != null)
                            {
                                createdTags.Add(new
                                {
                                    id = tag.Id.Value.ToString(),
                                    wallId = wall.Id.Value.ToString(),

                                    wallName = wall.Name,
                                    location = new
                                    {
                                        x = midpoint.X,
                                        y = midpoint.Y,
                                        z = midpoint.Z
                                    }
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"标记墙体 {wall.Id.Value} 出错: {ex.Message}");
                    }
#else
try
                        {
                            // Get the wall's location curve
                            var locationCurve = wall.Location as LocationCurve;
                            if (locationCurve != null)
                            {
                                // Get the middle point of the wall
                                var curve = locationCurve.Curve;
                                var midpoint = curve.Evaluate(0.5, true);

                                // Create tag at midpoint
                                var tag = IndependentTag.Create(
                                    doc,
                                    wallTagType.Id,
                                    activeView.Id,
                                    new Reference(wall),
                                    _useLeader, // Use leader based on parameter
                                    TagOrientation.Horizontal,
                                    midpoint);

                                if (tag != null)
                                {
                                    createdTags.Add(new
                                    {
                                        id = tag.Id.IntegerValue.ToString(),
                                        wallId = wall.Id.IntegerValue.ToString(),

                                        wallName = wall.Name,
                                        location = new
                                        {
                                            x = midpoint.X,
                                            y = midpoint.Y,
                                            z = midpoint.Z
                                        }
                                    });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"标记墙体 {wall.Id.IntegerValue} 出错: {ex.Message}");
                        }
#endif
                }

                tran.Commit();

                TaggingResults = new
                {
                    success = true,
                    totalWalls = walls.Count,
                    taggedWalls = createdTags.Count,
                    tags = createdTags,
                    errors = errors.Count > 0 ? errors : null
                };
            }
        }
        catch (Exception ex)
        {
            TaskDialog.Show("错误", $"标记墙体时出错: {ex.Message}");
            TaggingResults = new
            {
                success = false,
                message = $"发生错误: {ex.Message}"
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
        return "标记墙";
    }

    /// <summary>
    /// Find the wall tag type in the document
    /// </summary>
    private FamilySymbol FindWallTagType(Document doc)
    {
#if REVIT2024_OR_GREATER
        // If specific tag type ID was specified, try to use it
        if (!string.IsNullOrEmpty(_tagTypeId))
        {
            if (int.TryParse(_tagTypeId, out int id))
            {
                ElementId elementId = new ElementId(id);
                Element element = doc.GetElement(elementId);

                if (element != null && element is FamilySymbol symbol &&
                    (symbol.Category.Id.Value == (int)BuiltInCategory.OST_WallTags ||
                     symbol.Category.Id.Value == (int)BuiltInCategory.OST_MultiCategoryTags))
                {
                    return symbol;
                }
            }
        }

        // First try to find a tag specifically for walls
        FilteredElementCollector tagCollector = new FilteredElementCollector(doc);
        FamilySymbol wallTagType = tagCollector.OfClass(typeof(FamilySymbol))
            .WhereElementIsElementType()
            .Where(e => e.Category != null &&
                        e.Category.Id.Value == (int)BuiltInCategory.OST_WallTags)
            .Cast<FamilySymbol>()
            .FirstOrDefault();

        // If no wall tag found, try to find a multi-category tag that can tag walls
        if (wallTagType == null)
        {
            wallTagType = tagCollector.OfClass(typeof(FamilySymbol))
                .WhereElementIsElementType()
                .Where(e => e.Category != null &&
                            e.Category.Id.Value == (int)BuiltInCategory.OST_MultiCategoryTags)
                .Cast<FamilySymbol>()
                .FirstOrDefault();
        }

        return wallTagType;
#else
            // If specific tag type ID was specified, try to use it
            if (!string.IsNullOrEmpty(_tagTypeId))
            {
                if (int.TryParse(_tagTypeId, out var id))
                {
                    var elementId = new ElementId(id);
                    var element = doc.GetElement(elementId);

                    if (element != null && element is FamilySymbol symbol &&
                        (symbol.Category.Id.IntegerValue == (int)BuiltInCategory.OST_WallTags ||
                         symbol.Category.Id.IntegerValue == (int)BuiltInCategory.OST_MultiCategoryTags))
                    {
                        return symbol;
                    }
                }
            }

            // First try to find a tag specifically for walls
            var tagCollector = new FilteredElementCollector(doc);
            var wallTagType = tagCollector.OfClass(typeof(FamilySymbol))
                                                  .WhereElementIsElementType()
                                                  .Where(e => e.Category != null &&
                                                         e.Category.Id.IntegerValue == (int)BuiltInCategory.OST_WallTags)
                                                  .Cast<FamilySymbol>()
                                                  .FirstOrDefault();

            // If no wall tag found, try to find a multi-category tag that can tag walls
            if (wallTagType == null)
            {
                wallTagType = tagCollector.OfClass(typeof(FamilySymbol))
                                         .WhereElementIsElementType()
                                         .Where(e => e.Category != null &&
                                                e.Category.Id.IntegerValue == (int)BuiltInCategory.OST_MultiCategoryTags)
                                         .Cast<FamilySymbol>()
                                         .FirstOrDefault();
            }

            return wallTagType;
#endif

    }
}