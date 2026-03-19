using Autodesk.Revit.UI;
using RevitAgenticAICompanion.Infrastructure;
using RevitAgenticAICompanion.Revit;
using RevitAgenticAICompanion.Runtime;
using RevitAgenticAICompanion.UI;

namespace RevitAgenticAICompanion
{
    internal static class HostEnvironment
    {
        public static RevitRequestDispatcher Dispatcher { get; private set; }
        public static ExternalEvent ExternalEvent { get; private set; }
        public static DocumentStateTracker DocumentStateTracker { get; private set; }
        public static RuntimeCoordinator Coordinator { get; private set; }
        public static ChatDockablePane ChatPane { get; private set; }
        public static bool IsInitialized { get; private set; }

        public static void Initialize(
            RevitRequestDispatcher dispatcher,
            ExternalEvent externalEvent,
            DocumentStateTracker documentStateTracker,
            RuntimeCoordinator coordinator,
            ChatDockablePane chatPane)
        {
            Dispatcher = dispatcher;
            ExternalEvent = externalEvent;
            DocumentStateTracker = documentStateTracker;
            Coordinator = coordinator;
            ChatPane = chatPane;
            IsInitialized = true;
        }

        public static void Reset()
        {
            Dispatcher = null;
            ExternalEvent = null;
            DocumentStateTracker = null;
            Coordinator = null;
            ChatPane = null;
            IsInitialized = false;
        }

        public static void ShowPane(UIApplication application)
        {
            var pane = application.GetDockablePane(new DockablePaneId(App.DockPaneGuid));
            pane?.Show();
        }
    }
}
