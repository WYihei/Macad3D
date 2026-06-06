using System.Diagnostics;
using Macad.Core.Topology;
using Macad.Common.Serialization;
using Macad.Occt;

namespace Macad.Core.Shapes;

[SerializeType]
public sealed class SplitSolid : BooleanBase
{
    public SplitSolid()
    {
        Name = "Split Solid";
    }

    //--------------------------------------------------------------------------------------------------

    protected override BRepAlgoAPI_BuilderAlgo CreateAlgoApi()
    {
        return new BRepAlgoAPI_Splitter();
    }

    //--------------------------------------------------------------------------------------------------

    protected override void SetTools(BRepAlgoAPI_BuilderAlgo algo, TopTools_ListOfShape tools)
    {
        ((BRepAlgoAPI_Splitter)algo).SetTools(tools);
    }

    //--------------------------------------------------------------------------------------------------

    public static SplitSolid Create(Body targetBody, params IShapeOperand[] operands)
    {
        Debug.Assert(targetBody != null);

        var split = new SplitSolid();
        targetBody.AddShape(split);
        foreach (var shapeOperand in operands)
        {
            split.AddOperand(shapeOperand);
        }

        return split;
    }
}
