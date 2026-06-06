using System.Diagnostics;
using Macad.Core.Topology;
using Macad.Common.Serialization;
using Macad.Occt;

namespace Macad.Core.Shapes;

[SerializeType]
public sealed class BooleanFuse : BooleanBase
{
    [SerializeMember]
    public bool MergeFaces
    {
        get { return _MergeFaces; }
        set
        {
            if (_MergeFaces != value)
            {
                SaveUndo();
                _MergeFaces = value;
                Invalidate();
                RaisePropertyChanged();
            }
        }
    }

    //--------------------------------------------------------------------------------------------------

    protected override bool SimplifyResult => _MergeFaces;

    //--------------------------------------------------------------------------------------------------

    bool _MergeFaces;

    //--------------------------------------------------------------------------------------------------

    public BooleanFuse()
    {
        Name = "Boolean Fuse";
    }

    //--------------------------------------------------------------------------------------------------

    protected override BRepAlgoAPI_BuilderAlgo CreateAlgoApi()
    {
        return new BRepAlgoAPI_Fuse();
    }

    //--------------------------------------------------------------------------------------------------

    protected override void SetTools(BRepAlgoAPI_BuilderAlgo algo, TopTools_ListOfShape tools)
    {
        ((BRepAlgoAPI_BooleanOperation)algo).SetTools(tools);
    }

    //--------------------------------------------------------------------------------------------------

    public static BooleanFuse Create(Body targetBody, params IShapeOperand[] operands)
    {
        Debug.Assert(targetBody != null);

        var boolean = new BooleanFuse()
        {
            MergeFaces = true
        };

        targetBody.AddShape(boolean);
        foreach (var shapeOperand in operands)
        {
            boolean.AddOperand(shapeOperand);
        }

        return boolean;
    }

}