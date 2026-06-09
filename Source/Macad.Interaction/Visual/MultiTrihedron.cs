using System.Collections.Generic;
using Macad.Common;
using Macad.Core;
using Macad.Occt;

namespace Macad.Interaction.Visual;

/// <summary>
/// 批量渲染多个坐标轴。每个宏一组 X=红/Y=绿/Z=蓝，各用一个 compound AIS_Shape。
/// 每宏仅 3 个 AIS 对象（原来 N×3 个），性能大幅提升。
/// </summary>
public class MultiTrihedron : VisualObject
{
    readonly List<Ax3> _CoordinateSystems = new();
    readonly double _AxisLength;
    readonly List<AIS_Shape> _AisObjects = new();

    //--------------------------------------------------------------------------------------------------

    public override AIS_InteractiveObject AisObject => _AisObjects.Count > 0 ? _AisObjects[0] : null;

    //--------------------------------------------------------------------------------------------------

    public MultiTrihedron(WorkspaceController workspaceController, IEnumerable<Ax3> coordinateSystems, double axisLength = 5.0)
        : base(workspaceController, null)
    {
        _CoordinateSystems.AddRange(coordinateSystems);
        _AxisLength = axisLength;
        _CreateVisual();
    }

    //--------------------------------------------------------------------------------------------------

    public override void Remove()
    {
        foreach (var ais in _AisObjects)
        {
            try { AisContext.Erase(ais, false); } catch { }
        }
        _AisObjects.Clear();
    }

    //--------------------------------------------------------------------------------------------------

    public override void Update()
    {
        foreach (var ais in _AisObjects)
        {
            try { AisContext.Redisplay(ais, false); } catch { }
        }
    }

    //--------------------------------------------------------------------------------------------------

    void _CreateVisual()
    {
        if (_CoordinateSystems.Count == 0)
            return;

        // X=红, Y=绿, Z=蓝
        _CreateAxisShape(cs => cs.XDirection, Colors.ActionRed);
        _CreateAxisShape(cs => cs.YDirection, Colors.ActionGreen);
        _CreateAxisShape(cs => cs.Direction, Colors.ActionBlue);
    }

    //--------------------------------------------------------------------------------------------------

    void _CreateAxisShape(System.Func<Ax3, Dir> getDir, Color color)
    {
        var compound = new TopoDS_Compound();
        var builder = new BRep_Builder();
        builder.MakeCompound(compound);

        foreach (var cs in _CoordinateSystems)
        {
            var o = cs.Location;
            var d = getDir(cs);
            var tip = new Pnt(o.X + d.X * _AxisLength, o.Y + d.Y * _AxisLength, o.Z + d.Z * _AxisLength);
            var edge = new BRepBuilderAPI_MakeEdge(o, tip).Edge();
            builder.Add(compound, edge);
        }

        var aisShape = new AIS_Shape(compound);
        aisShape.SetColor(color.ToQuantityColor());
        aisShape.SetWidth(2.0);
        aisShape.SetZLayer(-3); // TOPMOST

        AisContext.Display(aisShape, false);
        _AisObjects.Add(aisShape);
    }

    //--------------------------------------------------------------------------------------------------
}
