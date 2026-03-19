using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitAgenticAICompanion.Revit
{
    public sealed class RevitRequestContext
    {
        public RevitRequestContext(UIApplication uiApplication)
        {
            UIApplication = uiApplication;
            UIDocument = uiApplication?.ActiveUIDocument;
            Document = UIDocument?.Document;
        }

        public UIApplication UIApplication { get; }
        public UIDocument UIDocument { get; }
        public Document Document { get; }
    }
}
