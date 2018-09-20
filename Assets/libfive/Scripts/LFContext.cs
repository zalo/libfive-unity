using System;
using System.Collections.Generic;

namespace libfivesharp {
  ///<summary>The static container for libfive contexts.</summary>
  public static class LFContext {
    ///<summary>The current active libfive context; any LFTrees 
    ///created inside a `using` block with one of these will 
    ///automatically get disposed of upon the completion of that block.</summary>
    public static Context Active {
      get { return _activeContext; }
      set {
        if(value != null) value.priorContext = _activeContext;
        _activeContext = value;
      }
    }

    private static Context _activeContext = null;
  }

  ///<summary>A libfive context, any LFTrees created inside a `using` 
  ///block with one of these will automatically get disposed of upon 
  ///the completion of that block.</summary>
  public class Context : IDisposable {
    ///<summary>The Context that was active before this one</summary>
    public Context priorContext = null;
    ///<summary>A Context which will contain LFTrees created inside of it for later Disposal</summary>
    public Context() { }
    protected List<LFTree> trees = new List<LFTree>();
    ///<summary>Adds an LFTree to this context (to be Disposed of when this is)</summary>
    public void AddTreeToContext(LFTree tree) { trees.Add(tree); }
    ///<summary>Removes an LFTree from this context (so it is NOT Disposed when this context is)</summary>
    public void RemoveTreeFromContext(LFTree tree) { trees.Remove(tree); }
    ///<summary>Disposes all of the LFTrees created within this context</summary>
    public void Dispose() {
      foreach (LFTree tree in trees) { if(tree != null) tree.Dispose(); }
      LFContext.Active = priorContext;
    }
  }
}