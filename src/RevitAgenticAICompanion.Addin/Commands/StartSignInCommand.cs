using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitAgenticAICompanion.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class StartSignInCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            if (!HostEnvironment.IsInitialized)
            {
                message = "The AI Companion host is not initialized.";
                return Result.Failed;
            }

            HostEnvironment.ShowPane(commandData.Application);
            HostEnvironment.ChatPane?.StartLoginFromRibbon();
            return Result.Succeeded;
        }
    }
}
