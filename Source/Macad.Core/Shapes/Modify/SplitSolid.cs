using System.Collections.Generic;
using System.Diagnostics;
using Macad.Core.Topology;
using Macad.Common.Serialization;
using Macad.Occt;

namespace Macad.Core.Shapes;

[SerializeType]
public sealed class SplitSolid : BooleanBase
{
    /// <summary>
    /// Additional solids produced by the split operation, beyond the first one
    /// kept in the main BRep. Transient (not serialized) - recomputed by MakeInternal on load.
    /// </summary>
    public List<TopoDS_Shape> AdditionalSolids { get; private set; } = new();

    //--------------------------------------------------------------------------------------------------

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

    protected override TopoDS_Shape GetResultBRep(TopoDS_Shape algoResult)
    {
        AdditionalSolids.Clear();

        if (algoResult == null)
            return null;

        var solids = algoResult.Solids();
        if (solids.Count <= 1)
        {
            // No split occurred or result is a single solid
            return algoResult;
        }

        // First solid becomes the main BRep of this modifier
        // Additional solids are stored for the interaction layer to create separate bodies
        for (int i = 1; i < solids.Count; i++)
        {
            AdditionalSolids.Add(solids[i]);
        }

        return solids[0];
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
