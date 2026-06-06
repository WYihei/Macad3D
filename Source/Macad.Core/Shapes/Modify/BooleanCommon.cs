using System.Diagnostics;
using Macad.Core.Topology;
using Macad.Common.Serialization;
using Macad.Occt;

namespace Macad.Core.Shapes;

[SerializeType]
public sealed class BooleanCommon : BooleanBase
{
    public BooleanCommon()
    {
        Name = "Boolean Common";
    }

    //--------------------------------------------------------------------------------------------------

    protected override BRepAlgoAPI_BuilderAlgo CreateAlgoApi()
    {
        return new BRepAlgoAPI_Common();
    }

    //--------------------------------------------------------------------------------------------------

    protected override void SetTools(BRepAlgoAPI_BuilderAlgo algo, TopTools_ListOfShape tools)
    {
        ((BRepAlgoAPI_BooleanOperation)algo).SetTools(tools);
    }

    //--------------------------------------------------------------------------------------------------

    public static BooleanCommon Create(Body targetBody, params IShapeOperand[] operands)
    {
        Debug.Assert(targetBody != null);

        var boolean = new BooleanCommon();
        targetBody.AddShape(boolean);
        foreach (var shapeOperand in operands)
        {
            boolean.AddOperand(shapeOperand);
        }

        return boolean;
    }
}