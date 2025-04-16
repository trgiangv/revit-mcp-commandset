﻿using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System.IO;

namespace RevitMCPCommandSet.Utils;

public static class ProjectUtils
{
    /// <summary>
    /// 创建族实例的通用方法
    /// </summary>
    /// <param name="doc">当前文档</param>
    /// <param name="familySymbol">族类型</param>
    /// <param name="locationPoint">位置点</param>
    /// <param name="locationLine">基准线</param>
    /// <param name="baseLevel">底部标高</param>
    /// <param name="topLevel">第二个标高(用于TwoLevelsBased)</param>
    /// <param name="baseOffset">底部偏移（ft）</param>
    /// <param name="topOffset">顶部偏移（ft）</param>
    /// <param name="faceDirection">参考方向</param>
    /// <param name="handDirection">参考方向</param>
    /// <param name="view">视图</param>
    /// <returns>创建的族实例，失败返回null</returns>
    public static FamilyInstance CreateInstance(
        this Document doc,
        FamilySymbol familySymbol,
        XYZ locationPoint = null,
        Line locationLine = null,
        Level baseLevel = null,
        Level topLevel = null,
        double baseOffset = -1,
        double topOffset = -1,
        XYZ faceDirection = null,
        XYZ handDirection = null,
        View view = null)
    {
        // 基本参数检查
        if (doc == null)
            throw new ArgumentNullException($"必要参数{typeof(Document)} {nameof(doc)}缺失！");
        if (familySymbol == null)
            throw new ArgumentNullException($"必要参数{typeof(FamilySymbol)} {nameof(familySymbol)}缺失！");

        // 激活族模型
        if (!familySymbol.IsActive)
            familySymbol.Activate();

        FamilyInstance instance = null;

        // 根据族的放置类型选择创建方法
        switch (familySymbol.Family.FamilyPlacementType)
        {
            // 基于单个标高的族（如：公制常规模型）
            case FamilyPlacementType.OneLevelBased:
                if (locationPoint == null)
                    throw new ArgumentNullException($"必要参数{typeof(XYZ)} {nameof(locationPoint)}缺失！");
                // 带标高信息
                if (baseLevel != null)
                {
                    instance = doc.Create.NewFamilyInstance(
                        locationPoint,                  // 实例将被放置的物理位置
                        familySymbol,                   // 表示要插入的实例类型的 FamilySymbol 对象
                        baseLevel,                      // 用作对象基准标高的 Level 对象
                        StructuralType.NonStructural);  // 如果是结构构件，则指定构件的类型
                }
                // 不带标高信息
                else
                {
                    instance = doc.Create.NewFamilyInstance(
                        locationPoint,                  // 实例将被放置的物理位置
                        familySymbol,                   // 表示要插入的实例类型的 FamilySymbol 对象
                        StructuralType.NonStructural);  // 如果是结构构件，则指定构件的类型
                }
                break;

            // 基于单个标高和主体的族（如：门、窗）
            case FamilyPlacementType.OneLevelBasedHosted:
                if (locationPoint == null)
                    throw new ArgumentNullException($"必要参数{typeof(XYZ)} {nameof(locationPoint)}缺失！");
                // 自动查找最近的宿主元素
                var host = doc.GetNearestHostElement(locationPoint, familySymbol);
                if (host == null)
                    throw new ArgumentNullException($"找不到合规的的宿主信息！");
                // 布置方向由主体的创建方向决定
                // 带标高信息
                if (baseLevel != null)
                {
                    instance = doc.Create.NewFamilyInstance(
                        locationPoint,                  // 实例将被放置在指定标高上的物理位置
                        familySymbol,                   // 表示要插入的实例类型的 FamilySymbol 对象
                        host,                           // 实例将嵌入其中的主体对象
                        baseLevel,                      // 用作对象基准标高的 Level 对象
                        StructuralType.NonStructural);  // 如果是结构构件，则指定构件的类型
                }
                // 不带标高信息
                else
                {
                    instance = doc.Create.NewFamilyInstance(
                        locationPoint,                  // 实例将被放置在指定标高上的物理位置
                        familySymbol,                   // 表示要插入的实例类型的 FamilySymbol 对象
                        host,                           // 实例将嵌入其中的主体对象
                        StructuralType.NonStructural);  // 如果是结构构件，则指定构件的类型
                }
                break;

            // 基于两个标高的族（如：柱子）
            case FamilyPlacementType.TwoLevelsBased:
                if (locationPoint == null)
                    throw new ArgumentNullException($"必要参数{typeof(XYZ)} {nameof(locationPoint)}缺失！");
                if (baseLevel == null)
                    throw new ArgumentNullException($"必要参数{typeof(Level)} {nameof(baseLevel)}缺失！");
                // 判断是结构柱还是建筑柱
                var structuralType = StructuralType.NonStructural;
                if (familySymbol.Category.Id.IntegerValue == (int)BuiltInCategory.OST_StructuralColumns)
                    structuralType = StructuralType.Column;
                instance = doc.Create.NewFamilyInstance(
                    locationPoint,              // 实例将被放置的物理位置
                    familySymbol,               // 表示要插入的实例类型的 FamilySymbol 对象
                    baseLevel,                  // 用作对象基准标高的 Level 对象
                    structuralType);            // 如果是结构构件，则指定构件的类型
                // 设置底部标高、顶部标高、底部偏移、顶部偏移
                if (instance != null)
                {
                    // 设置柱子的基准标高和顶部标高
                    if (baseLevel != null)
                    {
                        var baseLevelParam = instance.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM);
                        if (baseLevelParam != null)
                            baseLevelParam.Set(baseLevel.Id);
                    }
                    if (topLevel != null)
                    {
                        var topLevelParam = instance.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                        if (topLevelParam != null)
                            topLevelParam.Set(topLevel.Id);
                    }
                    // 获取底部偏移参数
                    if (baseOffset != -1)
                    {
                        var baseOffsetParam = instance.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);
                        if (baseOffsetParam != null && baseOffsetParam.StorageType == StorageType.Double)
                        {
                            // 将毫米转换为Revit内部单位
                            var baseOffsetInternal = baseOffset;
                            baseOffsetParam.Set(baseOffsetInternal);
                        }
                    }
                    // 获取顶部偏移参数
                    if (topOffset != -1)
                    {
                        var topOffsetParam = instance.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);
                        if (topOffsetParam != null && topOffsetParam.StorageType == StorageType.Double)
                        {
                            // 将毫米转换为Revit内部单位
                            var topOffsetInternal = topOffset;
                            topOffsetParam.Set(topOffsetInternal);
                        }
                    }
                }
                break;

            // 族是视图专有的（例如，详图注释）
            case FamilyPlacementType.ViewBased:
                if (locationPoint == null)
                    throw new ArgumentNullException($"必要参数{typeof(XYZ)} {nameof(locationPoint)}缺失！");
                instance = doc.Create.NewFamilyInstance(
                    locationPoint,  // 族实例的原点。如果创建在平面视图（ViewPlan）上，该原点将被投影到平面视图上
                    familySymbol,   // 表示要插入的实例类型的族符号对象
                    view);          // 放置族实例的2D视图
                break;

            // 基于工作平面的族（如：基于面的公制常规模型，包括基于面、基于墙等）
            case FamilyPlacementType.WorkPlaneBased:
                if (locationPoint == null)
                    throw new ArgumentNullException($"必要参数{typeof(XYZ)} {nameof(locationPoint)}缺失！");
                // 获取最近的宿主面
                var hostFace = doc.GetNearestFaceReference(locationPoint, 1000 / 304.8);
                if (hostFace == null)
                    throw new ArgumentNullException($"找不到合规的的宿主信息！");
                if (faceDirection == null || faceDirection == XYZ.Zero)
                {
                    var result = doc.GenerateDefaultOrientation(hostFace);
                    faceDirection = result.FacingOrientation;
                }
                // 使用点和方向在面上创建族实例
                instance = doc.Create.NewFamilyInstance(
                    hostFace,               // 对面的引用  
                    locationPoint,          // 实例将被放置的面上的点
                    faceDirection,          // 定义族实例方向的向量。请注意，此方向定义了实例在面上的旋转，因此不能与面法线平行
                    familySymbol);          // 表示要插入的实例类型的 FamilySymbol 对象。请注意，此FamilySymbol必须表示 FamilyPlacementType 为 WorkPlaneBased 的族
                break;

            // 基于线且在工作平面上的族（如：基于线的公制常规模型）
            case FamilyPlacementType.CurveBased:
                if (locationLine == null)
                    throw new ArgumentNullException($"必要参数{typeof(Line)} {nameof(locationLine)}缺失！");

                // 获取最近的宿主面（不允许有误差）
                var lineHostFace = doc.GetNearestFaceReference(locationLine.Evaluate(0.5, true), 1e-5);
                if (lineHostFace != null)
                {
                    instance = doc.Create.NewFamilyInstance(
                        lineHostFace,   // 对面的引用 
                        locationLine,   // 族实例基于的曲线
                        familySymbol);  // 一个FamilySymbol对象，表示要插入的实例的类型。请注意，此Symbol必须表示其 FamilyPlacementType 为 WorkPlaneBased 或 CurveBased 的族
                }
                else
                {
                    instance = doc.Create.NewFamilyInstance(
                        locationLine,                   // 族实例基于的曲线
                        familySymbol,                   // 一个FamilySymbol对象，表示要插入的实例的类型。请注意，此Symbol必须表示其 FamilyPlacementType 为 WorkPlaneBased 或 CurveBased 的族
                        baseLevel,                      // 一个Level对象，用作该对象的基准标高
                        StructuralType.NonStructural);  // 如果是结构构件，则指定构件的类型
                }
                if (instance != null)
                {
                    // 获取底部偏移参数
                    if (baseOffset != -1)
                    {
                        var baseOffsetParam = instance.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM);
                        if (baseOffsetParam != null && baseOffsetParam.StorageType == StorageType.Double)
                        {
                            // 将毫米转换为Revit内部单位
                            var baseOffsetInternal = baseOffset;
                            baseOffsetParam.Set(baseOffsetInternal);
                        }
                    }
                }
                break;

            // 基于线且在特定视图中的族（如：详图组件）
            case FamilyPlacementType.CurveBasedDetail:
                if (locationLine == null)
                    throw new ArgumentNullException($"必要参数{typeof(Line)} {nameof(locationLine)}缺失！");
                if (view == null)
                    throw new ArgumentNullException($"必要参数{typeof(View)} {nameof(view)}缺失！");
                instance = doc.Create.NewFamilyInstance(
                    locationLine,   // 族实例的线位置。该线必须位于视图平面内
                    familySymbol,   // 表示要插入的实例类型的族符号对象
                    view);          // 放置族实例的2D视图
                break;

            // 结构曲线驱动的族（如：梁、支撑或斜柱）
            case FamilyPlacementType.CurveDrivenStructural:
                if (locationLine == null)
                    throw new ArgumentNullException($"必要参数{typeof(Line)} {nameof(locationLine)}缺失！");
                if (baseLevel == null)
                    throw new ArgumentNullException($"必要参数{typeof(Level)} {nameof(baseLevel)}缺失！");
                instance = doc.Create.NewFamilyInstance(
                    locationLine,                   // 族实例基于的曲线
                    familySymbol,                   // 一个FamilySymbol对象，表示要插入的实例的类型。请注意，此Symbol必须表示其 FamilyPlacementType 为 WorkPlaneBased 或 CurveBased 的族
                    baseLevel,                      // 一个Level对象，用作该对象的基准标高
                    StructuralType.Beam);           // 如果是结构构件，则指定构件的类型
                break;

            // 适应性族（如：自适应公制常规模型、幕墙嵌板）
            case FamilyPlacementType.Adaptive:
                throw new NotImplementedException("未实现FamilyPlacementType.Adaptive创建方法！");

            default:
                break;
        }
        return instance;
    }

    /// <summary>
    /// 生成默认的朝向和手向（默认长边是HandOrientation，短边是FacingOrientation）
    /// </summary>
    /// <param name="hostFace"></param>
    /// <returns></returns>
    public static (XYZ FacingOrientation, XYZ HandOrientation) GenerateDefaultOrientation(this Document doc, Reference hostFace)
    {
        var facingOrientation = new XYZ();  // 朝向方向：族内Y轴正方向在载入后的朝向
        var handOrientation = new XYZ();    // 手向方向：族内X轴正方向在载入后的朝向

        // Step1 从Reference中获取面对象
        var face = doc.GetElement(hostFace.ElementId).GetGeometryObjectFromReference(hostFace) as Face;

        // Step2 获取面轮廓
        List<Curve> profile = null;
        // 轮廓线集合，每个子列表代表一个完整闭合轮廓，第一个通常为外轮廓
        var profiles = new List<List<Curve>>();
        // 获取所有轮廓循环（外轮廓和可能的内部孔洞）
        var edgeLoops = face.EdgeLoops;
        // 遍历每个轮廓循环
        foreach (EdgeArray loop in edgeLoops)
        {
            var currentLoop = new List<Curve>();
            // 获取循环中的每条边
            foreach (Edge edge in loop)
            {
                var curve = edge.AsCurve();
                currentLoop.Add(curve);
            }
            // 如果当前循环有边，则添加到结果集合
            if (currentLoop.Count > 0)
            {
                profiles.Add(currentLoop);
            }
        }
        // 第一个通常为外轮廓
        if (profiles != null && profiles.Any())
            profile = profiles.FirstOrDefault();

        // Step3 获取面法向量
        XYZ faceNormal = null;
        // 如果是平面，可以直接获取法向量属性
        if (face is PlanarFace planarFace)
            faceNormal = planarFace.FaceNormal;

        // Step4 获取面的两个合规的（符合右手螺旋定则）主方向
        var result = face.GetMainDirections();
        var primaryDirection = result.PrimaryDirection;
        var secondaryDirection = result.SecondaryDirection;

        // 默认长边方向就是HandOrientation，短边方向就是FacingOrientation
        facingOrientation = primaryDirection;
        handOrientation = secondaryDirection;

        // 判断是否符合右手定则（拇指：HandOrientation，食指：FacingOrientation，中指：FaceNormal）
        if (!facingOrientation.IsRightHandRuleCompliant(handOrientation, faceNormal))
        {
            var newHandOrientation = facingOrientation.GenerateIndexFinger(faceNormal);
            if (newHandOrientation != null)
            {
                handOrientation = newHandOrientation;
            }
        }

        return (facingOrientation, handOrientation);
    }

    /// <summary>
    /// 获取距离点最近的面Reference
    /// </summary>
    /// <param name="doc">当前文档</param>
    /// <param name="location">目标点位置</param>
    /// <param name="radius">搜索半径（内部单位）</param>
    /// <returns>最近面的Reference，未找到返回null</returns>
    public static Reference GetNearestFaceReference(this Document doc, XYZ location, double radius = 1000 / 304.8)
    {
        try
        {
            // 误差处理
            location = new XYZ(location.X, location.Y, location.Z + 0.1 / 304.8);

            // 创建或获取3D视图
            View3D view3D = null;
            var collector = new FilteredElementCollector(doc)
                .OfClass(typeof(View3D));

            foreach (View3D v in collector)
            {
                if (!v.IsTemplate)
                {
                    view3D = v;
                    break;
                }
            }

            if (view3D == null)
            {
                using (var trans = new Transaction(doc, "Create 3D View"))
                {
                    trans.Start();
                    var vft = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewFamilyType))
                        .Cast<ViewFamilyType>()
                        .FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);

                    if (vft != null)
                    {
                        view3D = View3D.CreateIsometric(doc, vft.Id);
                    }
                    trans.Commit();
                }
            }

            if (view3D == null)
            {
                TaskDialog.Show("错误", "无法创建或获取3D视图");
                return null;
            }

            // 设置6个方向的射线
            var directions = new XYZ[]
            {
                XYZ.BasisX,    // X正向
                -XYZ.BasisX,   // X负向
                XYZ.BasisY,    // Y正向
                -XYZ.BasisY,   // Y负向
                XYZ.BasisZ,    // Z正向
                -XYZ.BasisZ    // Z负向
            };

            // 创建过滤器
            var wallFilter = new ElementClassFilter(typeof(Wall));
            var floorFilter = new ElementClassFilter(typeof(Floor));
            var ceilingFilter = new ElementClassFilter(typeof(Ceiling));
            var instanceFilter = new ElementClassFilter(typeof(FamilyInstance));

            // 组合过滤器
            var categoryFilter = new LogicalOrFilter(
                new ElementFilter[] { wallFilter, floorFilter, ceilingFilter, instanceFilter });


            // 1. 最简单：所有实例化元素的过滤器
            //ElementFilter filter = new ElementIsElementTypeFilter(true);

            // 创建射线追踪器
            var refIntersector = new ReferenceIntersector(categoryFilter,
                FindReferenceTarget.Face, view3D);
            refIntersector.FindReferencesInRevitLinks = true; // 如果需要查找链接文件中的面

            var minDistance = double.MaxValue;
            Reference nearestFace = null;

            foreach (var direction in directions)
            {
                // 从当前位置发射射线
                var references = refIntersector.Find(location, direction);

                foreach (var rwc in references)
                {
                    var distance = rwc.Proximity; // 获取到面的距离

                    // 如果在搜索范围内且距离更近
                    if (distance <= radius && distance < minDistance)
                    {
                        minDistance = distance;
                        nearestFace = rwc.GetReference();
                    }
                }
            }

            return nearestFace;
        }
        catch (Exception ex)
        {
            TaskDialog.Show("错误", $"获取最近面时发生错误：{ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 获取距离点最近的可作为宿主的元素
    /// </summary>
    /// <param name="doc">当前文档</param>
    /// <param name="location">目标点位置</param>
    /// <param name="familySymbol">族类型，用于判断宿主类型</param>
    /// <param name="radius">搜索半径（内部单位）</param>
    /// <returns>最近的宿主元素，未找到返回null</returns>
    public static Element GetNearestHostElement(this Document doc, XYZ location, FamilySymbol familySymbol, double radius = 5.0)
    {
        try
        {
            // 基本参数检查
            if (doc == null || location == null || familySymbol == null)
                return null;

            // 获取族的宿主行为参数
            var hostParam = familySymbol.Family.get_Parameter(BuiltInParameter.FAMILY_HOSTING_BEHAVIOR);
            var hostingBehavior = hostParam?.AsInteger() ?? 0;

            // 创建或获取3D视图
            View3D view3D = null;
            var viewCollector = new FilteredElementCollector(doc)
                .OfClass(typeof(View3D));
            foreach (View3D v in viewCollector)
            {
                if (!v.IsTemplate)
                {
                    view3D = v;
                    break;
                }
            }

            if (view3D == null)
            {
                using (var trans = new Transaction(doc, "Create 3D View"))
                {
                    trans.Start();
                    var vft = new FilteredElementCollector(doc)
                        .OfClass(typeof(ViewFamilyType))
                        .Cast<ViewFamilyType>()
                        .FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);

                    if (vft != null)
                    {
                        view3D = View3D.CreateIsometric(doc, vft.Id);
                    }
                    trans.Commit();
                }
            }

            if (view3D == null)
            {
                TaskDialog.Show("错误", "无法创建或获取3D视图");
                return null;
            }

            // 根据宿主行为创建类型过滤器
            ElementFilter classFilter;
            switch (hostingBehavior)
            {
                case 1: // Wall based
                    classFilter = new ElementClassFilter(typeof(Wall));
                    break;
                case 2: // Floor based
                    classFilter = new ElementClassFilter(typeof(Floor));
                    break;
                case 3: // Ceiling based
                    classFilter = new ElementClassFilter(typeof(Ceiling));
                    break;
                case 4: // Roof based
                    classFilter = new ElementClassFilter(typeof(RoofBase));
                    break;
                default:
                    return null; // 不支持的宿主类型
            }

            // 设置6个方向的射线
            var directions = new XYZ[]
            {
                XYZ.BasisX,    // X正向
                -XYZ.BasisX,   // X负向
                XYZ.BasisY,    // Y正向
                -XYZ.BasisY,   // Y负向
                XYZ.BasisZ,    // Z正向
                -XYZ.BasisZ    // Z负向
            };

            // 创建射线追踪器
            var refIntersector = new ReferenceIntersector(classFilter,
                FindReferenceTarget.Element, view3D);
            refIntersector.FindReferencesInRevitLinks = true; // 如果需要查找链接文件中的元素

            var minDistance = double.MaxValue;
            Element nearestHost = null;

            foreach (var direction in directions)
            {
                // 从当前位置发射射线
                var references = refIntersector.Find(location, direction);

                foreach (var rwc in references)
                {
                    var distance = rwc.Proximity; // 获取到元素的距离

                    // 如果在搜索范围内且距离更近
                    if (distance <= radius && distance < minDistance)
                    {
                        minDistance = distance;
                        nearestHost = doc.GetElement(rwc.GetReference().ElementId);
                    }
                }
            }

            return nearestHost;
        }
        catch (Exception ex)
        {
            TaskDialog.Show("错误", $"获取最近宿主元素时发生错误：{ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 高亮显示指定的面
    /// </summary>
    /// <param name="doc">当前文档</param>
    /// <param name="faceRef">要高亮显示的面Reference</param>
    /// <param name="duration">高亮持续时间(毫秒)，默认3000毫秒</param>
    public static void HighlightFace(this Document doc, Reference faceRef)
    {
        if (faceRef == null) return;

        // 获取实心填充图案
        var solidFill = new FilteredElementCollector(doc)
            .OfClass(typeof(FillPatternElement))
            .Cast<FillPatternElement>()
            .FirstOrDefault(x => x.GetFillPattern().IsSolidFill);

        if (solidFill == null)
        {
            TaskDialog.Show("错误", "未找到实心填充图案");
            return;
        }

        // 创建高亮显示设置
        var ogs = new OverrideGraphicSettings();
        ogs.SetSurfaceForegroundPatternColor(new Color(255, 0, 0)); // 红色
        ogs.SetSurfaceForegroundPatternId(solidFill.Id);
        ogs.SetSurfaceTransparency(0); // 不透明

        // 高亮显示
        doc.ActiveView.SetElementOverrides(faceRef.ElementId, ogs);
    }

    /// <summary>
    /// 提取面的两个主要方向向量
    /// </summary>
    /// <param name="face">输入面</param>
    /// <returns>包含主方向和次方向的元组</returns>
    /// <exception cref="ArgumentNullException">当面为空时抛出</exception>
    /// <exception cref="ArgumentException">当面的轮廓不足以形成有效形状时抛出</exception>
    /// <exception cref="InvalidOperationException">当无法提取有效方向时抛出</exception>
    public static (XYZ PrimaryDirection, XYZ SecondaryDirection) GetMainDirections(this Face face)
    {
        // 1. 参数验证
        if (face == null)
            throw new ArgumentNullException(nameof(face), "面不能为空");

        // 2. 获取面的法向量，用于后续可能需要的垂直向量计算
        var faceNormal = face.ComputeNormal(new UV(0.5, 0.5));

        // 3. 获取面的外轮廓
        var edgeLoops = face.EdgeLoops;
        if (edgeLoops.Size == 0)
            throw new ArgumentException("面没有有效的边循环", nameof(face));

        // 通常第一个循环是外轮廓
        var outerLoop = edgeLoops.get_Item(0);

        // 4. 计算每条边的方向向量和长度
        var edgeDirections = new List<XYZ>();  // 存储每条边的单位向量方向
        var edgeLengths = new List<double>(); // 存储每条边的长度

        foreach (Edge edge in outerLoop)
        {
            var curve = edge.AsCurve();
            var startPoint = curve.GetEndPoint(0);
            var endPoint = curve.GetEndPoint(1);

            // 计算从起点到终点的向量
            var direction = endPoint - startPoint;
            var length = direction.GetLength();

            // 忽略太短的边（可能是由于顶点重合或数值精度问题）
            if (length > 1e-10)
            {
                edgeDirections.Add(direction.Normalize());  // 存储归一化后的方向向量
                edgeLengths.Add(length);                    // 存储边长
            }
        }

        if (edgeDirections.Count < 4) // 确保至少有4条边
        {
            throw new ArgumentException("提供的面没有足够的边来形成有效的形状", nameof(face));
        }

        // 5. 将相似方向的边分组
        var directionGroups = new List<List<int>>();  // 存储方向组，每组包含边的索引

        for (var i = 0; i < edgeDirections.Count; i++)
        {
            var foundGroup = false;
            var currentDirection = edgeDirections[i];

            // 尝试将当前边加入已有的方向组
            for (var j = 0; j < directionGroups.Count; j++)
            {
                var group = directionGroups[j];
                // 计算当前组的加权平均方向
                var groupAvgDir = CalculateWeightedAverageDirection(group, edgeDirections, edgeLengths);

                // 检查当前方向是否与组的平均方向相似（包括正反方向）
                var dotProduct = Math.Abs(groupAvgDir.DotProduct(currentDirection));
                if (dotProduct > 0.8) // 约30度内的偏差视为相似方向
                {
                    group.Add(i);  // 将当前边的索引添加到该方向组
                    foundGroup = true;
                    break;
                }
            }

            // 如果当前边与所有已有组都不相似，创建新组
            if (!foundGroup)
            {
                var newGroup = new List<int> { i };
                directionGroups.Add(newGroup);
            }
        }

        // 6. 计算每个方向组的总权重（边长和）和平均方向
        var groupWeights = new List<double>();
        var groupDirections = new List<XYZ>();

        foreach (var group in directionGroups)
        {
            // 计算该组所有边的长度总和
            double totalLength = 0;
            foreach (var edgeIndex in group)
            {
                totalLength += edgeLengths[edgeIndex];
            }
            groupWeights.Add(totalLength);

            // 计算该组的加权平均方向
            groupDirections.Add(CalculateWeightedAverageDirection(group, edgeDirections, edgeLengths));
        }

        // 7. 按照权重排序，提取主要方向
        var sortedIndices = Enumerable.Range(0, groupDirections.Count)
            .OrderByDescending(i => groupWeights[i])
            .ToArray();

        // 8. 构造结果
        if (groupDirections.Count >= 2)
        {
            // 有至少两个方向组，取权重最大的两组作为主方向和次方向
            var primaryIndex = sortedIndices[0];
            var secondaryIndex = sortedIndices[1];

            return (
                PrimaryDirection: groupDirections[primaryIndex],      // 主方向
                SecondaryDirection: groupDirections[secondaryIndex]   // 次方向
            );
        }
        else if (groupDirections.Count == 1)
        {
            // 只有一个方向组，手动创建与主方向垂直的次方向
            var primaryDirection = groupDirections[0];
            // 使用面法向量和主方向的叉积创建垂直向量
            var secondaryDirection = faceNormal.CrossProduct(primaryDirection).Normalize();

            return (
                PrimaryDirection: primaryDirection,         // 主方向 
                SecondaryDirection: secondaryDirection      // 人工构造的垂直次方向
            );
        }
        else
        {
            // 无法提取有效的方向（极少发生）
            throw new InvalidOperationException("无法从面中提取有效的方向");
        }
    }

    /// <summary>
    /// 根据边长计算一组边的加权平均方向
    /// </summary>
    /// <param name="edgeIndices">边的索引列表</param>
    /// <param name="directions">所有边的方向向量</param>
    /// <param name="lengths">所有边的长度</param>
    /// <returns>归一化的加权平均方向向量</returns>
    public static XYZ CalculateWeightedAverageDirection(List<int> edgeIndices, List<XYZ> directions, List<double> lengths)
    {
        if (edgeIndices.Count == 0)
            return null;

        double sumX = 0, sumY = 0, sumZ = 0;
        var referenceDir = directions[edgeIndices[0]];  // 使用组内第一个方向作为参考

        foreach (var i in edgeIndices)
        {
            var currentDir = directions[i];

            // 计算当前方向与参考方向的点积，判断是否需要反转
            var dot = referenceDir.DotProduct(currentDir);

            // 如果方向相反（点积为负），反转该向量再计算贡献
            // 这确保同一组内的向量指向一致，避免相互抵消
            var factor = (dot >= 0) ? lengths[i] : -lengths[i];

            // 累加向量分量（带权重）
            sumX += currentDir.X * factor;
            sumY += currentDir.Y * factor;
            sumZ += currentDir.Z * factor;
        }

        // 创建合成向量并归一化
        var avgDir = new XYZ(sumX, sumY, sumZ);
        var magnitude = avgDir.GetLength();

        // 防止零向量
        if (magnitude < 1e-10)
            return referenceDir;  // 回退至参考方向

        return avgDir.Normalize();  // 返回归一化后的方向向量
    }

    /// <summary>
    /// 判断三个向量是否同时符合右手定则且互相严格垂直
    /// </summary>
    /// <param name="thumb">拇指方向向量</param>
    /// <param name="indexFinger">食指方向向量</param>
    /// <param name="middleFinger">中指方向向量</param>
    /// <param name="tolerance">判断的容差，默认为1e-6</param>
    /// <returns>如果三个向量符合右手定则且互相垂直则返回true，否则返回false</returns>
    public static bool IsRightHandRuleCompliant(this XYZ thumb, XYZ indexFinger, XYZ middleFinger, double tolerance = 1e-6)
    {
        // 检查三个向量是否互相垂直（所有点积都接近0）
        var dotThumbIndex = Math.Abs(thumb.DotProduct(indexFinger));
        var dotThumbMiddle = Math.Abs(thumb.DotProduct(middleFinger));
        var dotIndexMiddle = Math.Abs(indexFinger.DotProduct(middleFinger));

        var areOrthogonal = (dotThumbIndex <= tolerance) &&
                            (dotThumbMiddle <= tolerance) &&
                            (dotIndexMiddle <= tolerance);

        // 只有在三个向量互相垂直的情况下才检查右手定则
        if (!areOrthogonal)
            return false;

        // 计算叉积向量与拇指的点积，判断是否符合右手定则
        var crossProduct = indexFinger.CrossProduct(middleFinger);
        var rightHandTest = crossProduct.DotProduct(thumb);

        // 点积为正值表示符合右手定则
        return rightHandTest > tolerance;
    }

    /// <summary>
    /// 根据拇指和中指方向生成符合右手定则的食指方向
    /// </summary>
    /// <param name="thumb">拇指方向向量</param>
    /// <param name="middleFinger">中指方向向量</param>
    /// <param name="tolerance">垂直判断的容差，默认为1e-6</param>
    /// <returns>生成的食指方向向量，如果输入向量不垂直则返回null</returns>
    public static XYZ GenerateIndexFinger(this XYZ thumb, XYZ middleFinger, double tolerance = 1e-6)
    {
        // 首先归一化输入向量
        var normalizedThumb = thumb.Normalize();
        var normalizedMiddleFinger = middleFinger.Normalize();

        // 检查两个向量是否垂直（点积接近于0）
        var dotProduct = normalizedThumb.DotProduct(normalizedMiddleFinger);

        // 如果点积的绝对值大于容差，则向量不垂直
        if (Math.Abs(dotProduct) > tolerance)
        {
            return null;
        }

        // 通过叉积计算食指方向并取反
        var indexFinger = normalizedMiddleFinger.CrossProduct(normalizedThumb).Negate();

        // 返回归一化后的食指方向向量
        return indexFinger.Normalize();
    }

    /// <summary>
    /// 创建或获取指定高度的标高
    /// </summary>
    /// <param name="doc">revit文档</param>
    /// <param name="elevation">标高高度（ft）</param>
    /// <param name="levelName">标高名称</param>
    /// <returns></returns>
    public static Level CreateOrGetLevel(this Document doc, double elevation, string levelName)
    {
        // 先查找是否存在指定高度的标高
        var existingLevel = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .FirstOrDefault(l => Math.Abs(l.Elevation - elevation) < 0.1 / 304.8);

        if (existingLevel != null)
            return existingLevel;

        // 创建新标高
        var newLevel = Level.Create(doc, elevation);
        // 设置标高名称
        var namesakeLevel = new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .FirstOrDefault(l => l.Name == levelName);
        if (namesakeLevel != null)
        {
            levelName = $"{levelName}_{newLevel.Id.IntegerValue.ToString()}";
        }
        newLevel.Name = levelName;

        return newLevel;
    }

    /// <summary>
    /// 查找距离给定高度最近的标高
    /// </summary>
    /// <param name="doc">当前Revit文档</param>
    /// <param name="height">目标高度（Revit内部单位）</param>
    /// <returns>距离目标高度最近的标高，若文档中没有标高则返回null</returns>
    public static Level FindNearestLevel(this Document doc, double height)
    {
        if (doc == null)
            throw new ArgumentNullException(nameof(doc), "文档不能为空");

        // 直接使用LINQ查询获取距离最近的标高
        return new FilteredElementCollector(doc)
            .OfClass(typeof(Level))
            .Cast<Level>()
            .OrderBy(level => Math.Abs(level.Elevation - height))
            .FirstOrDefault();
    }

    ///// <summary>
    ///// 刷新视图并添加延迟
    ///// </summary>
    //public static void Refresh(this Document doc, int waitingTime = 0, bool allowOperation = true)
    //{
    //    UIApplication uiApp = new UIApplication(doc.Application);
    //    UIDocument uiDoc = uiApp.ActiveUIDocument;

    //    // 检查文档是否可修改
    //    if (uiDoc.Document.IsModifiable)
    //    {
    //        // 更新模型
    //        uiDoc.Document.Regenerate();
    //    }
    //    // 更新界面
    //    uiDoc.RefreshActiveView();

    //    // 延迟等待
    //    if (waitingTime != 0)
    //    {
    //        System.Threading.Thread.Sleep(waitingTime);
    //    }

    //    // 允许用户进行非安全操作
    //    if (allowOperation)
    //    {
    //        System.Windows.Forms.Application.DoEvents();
    //    }
    //}

    /// <summary>
    /// 将指定的消息保存到桌面的指定文件中（默认覆盖文件）
    /// </summary>
    /// <param name="message">要保存的消息内容</param>
    /// <param name="fileName">目标文件名</param>
    public static void SaveToDesktop(this string message, string fileName = "temp.json", bool isAppend = false)
    {
        // 确保 logName 包含后缀
        if (!Path.HasExtension(fileName))
        {
            fileName += ".txt"; // 默认添加 .txt 后缀
        }

        // 获取桌面路径
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        // 组合完整的文件路径
        var filePath = Path.Combine(desktopPath, fileName);

        // 写入文件（覆盖模式）
        using (var sw = new StreamWriter(filePath, isAppend))
        {
            sw.WriteLine($"{message}");
        }
    }

}