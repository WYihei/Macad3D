using System.Diagnostics;
using Macad.Core.Topology;
using Macad.Common.Serialization;
using Macad.Occt;

namespace Macad.Core.Shapes;

[SerializeType]
public sealed class BooleanCut : BooleanBase
{
    public BooleanCut()
    {
        Name = "Boolean Cut";
    }

    //--------------------------------------------------------------------------------------------------

    protected override BRepAlgoAPI_BuilderAlgo CreateAlgoApi()
    {
        return new BRepAlgoAPI_Cut();
    }

    //--------------------------------------------------------------------------------------------------

    protected override void SetTools(BRepAlgoAPI_BuilderAlgo algo, TopTools_ListOfShape tools)
    {
        ((BRepAlgoAPI_BooleanOperation)algo).SetTools(tools);
    }

    //--------------------------------------------------------------------------------------------------

    public static BooleanCut Create(Body targetBody, params IShapeOperand[] operands)
    {
        Debug.Assert(targetBody != null);

        var boolean = new BooleanCut();
        targetBody.AddShape(boolean);
        foreach (var shapeOperand in operands)
        {
            boolean.AddOperand(shapeOperand);
        }

        return boolean;
    }
}