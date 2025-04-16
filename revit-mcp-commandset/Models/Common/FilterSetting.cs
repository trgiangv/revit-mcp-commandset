﻿using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.Common;

/// <summary>
/// 过滤器设置 - 支持组合条件过滤
/// </summary>
public class FilterSetting
{
    /// <summary>
    /// 获取或设置要过滤的 Revit 内置类别名称（如"OST_Walls"）。
    /// 如果为 null 或空，则不进行类别过滤。
    /// </summary>
    [JsonProperty("filterCategory")]
    public string FilterCategory { get; set; } = null;
    /// <summary>
    /// 获取或设置要过滤的 Revit 元素类型名称（如"Wall"或"Autodesk.Revit.DB.Wall"）。
    /// 如果为 null 或空，则不进行类型过滤。
    /// </summary>
    [JsonProperty("filterElementType")]
    public string FilterElementType { get; set; } = null;
    /// <summary>
    /// 获取或设置要过滤的族类型的ElementId值（FamilySymbol）。
    /// 如果为0或负数，则不进行族过滤。
    /// 注意：此过滤器仅适用于元素实例，不适用于类型元素。
    /// </summary>
    [JsonProperty("filterFamilySymbolId")]
    public int FilterFamilySymbolId { get; set; } = -1;
    /// <summary>
    /// 获取或设置是否包含元素类型（如墙类型、门类型等）
    /// </summary>
    [JsonProperty("includeTypes")]
    public bool IncludeTypes { get; set; } = false;
    /// <summary>
    /// 获取或设置是否包含元素实例（如已放置的墙、门等）
    /// </summary>
    [JsonProperty("includeInstances")]
    public bool IncludeInstances { get; set; } = true;
    /// <summary>
    /// 获取或设置是否仅返回在当前视图中可见的元素。
    /// 注意：此过滤器仅适用于元素实例，不适用于类型元素。
    /// </summary>
    [JsonProperty("filterVisibleInCurrentView")]
    public bool FilterVisibleInCurrentView { get; set; }
    /// <summary>
    /// 获取或设置空间范围过滤的最小点坐标 (单位：mm)
    /// 如果设置了此值和BoundingBoxMax，将筛选出与此边界框相交的元素
    /// </summary>
    [JsonProperty("boundingBoxMin")]
    public JZPoint BoundingBoxMin { get; set; } = null;
    /// <summary>
    /// 获取或设置空间范围过滤的最大点坐标 (单位：mm)
    /// 如果设置了此值和BoundingBoxMin，将筛选出与此边界框相交的元素
    /// </summary>
    [JsonProperty("boundingBoxMax")]
    public JZPoint BoundingBoxMax { get; set; } = null;
    /// <summary>
    /// 最大元素数量限制
    /// </summary>
    [JsonProperty("maxElements")]
    public int MaxElements { get; set; } = 50; 
    /// <summary>
    /// 验证过滤器设置的有效性，检查潜在的冲突
    /// </summary>
    /// <returns>如果设置有效返回true，否则返回false</returns>
    public bool Validate(out string errorMessage)
    {
        errorMessage = null;

        // 检查是否至少选择了一种元素种类
        if (!IncludeTypes && !IncludeInstances)
        {
            errorMessage = "过滤设置无效: 必须至少包含元素类型或元素实例之一";
            return false;
        }

        // 检查是否至少指定了一个过滤条件
        if (string.IsNullOrWhiteSpace(FilterCategory) &&
            string.IsNullOrWhiteSpace(FilterElementType) &&
            FilterFamilySymbolId <= 0)
        {
            errorMessage = "过滤设置无效: 必须至少指定一个过滤条件(类别、元素类型或族类型)";
            return false;
        }

        // 检查类型元素与某些过滤器的冲突
        if (IncludeTypes && !IncludeInstances)
        {
            var invalidFilters = new List<string>();
            if (FilterFamilySymbolId > 0)
                invalidFilters.Add("族实例过滤");
            if (FilterVisibleInCurrentView)
                invalidFilters.Add("视图可见性过滤");
            if (invalidFilters.Count > 0)
            {
                errorMessage = $"当仅过滤类型元素时，以下过滤器不适用: {string.Join(", ", invalidFilters)}";
                return false;
            }
        }
        // 检查空间范围过滤器的有效性
        if (BoundingBoxMin != null && BoundingBoxMax != null)
        {
            // 确保最小点小于或等于最大点
            if (BoundingBoxMin.X > BoundingBoxMax.X ||
                BoundingBoxMin.Y > BoundingBoxMax.Y ||
                BoundingBoxMin.Z > BoundingBoxMax.Z)
            {
                errorMessage = "空间范围过滤器设置无效: 最小点坐标必须小于或等于最大点坐标";
                return false;
            }
        }
        else if (BoundingBoxMin != null || BoundingBoxMax != null)
        {
            errorMessage = "空间范围过滤器设置无效: 必须同时设置最小点和最大点坐标";
            return false;
        }
        return true;
    }
}