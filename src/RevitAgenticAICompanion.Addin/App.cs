using System;
using System.Reflection;
using Autodesk.Revit.UI;
using RevitAgenticAICompanion.Commands;
using RevitAgenticAICompanion.Infrastructure;
using RevitAgenticAICompanion.Revit;
using RevitAgenticAICompanion.Runtime;
using RevitAgenticAICompanion.Storage;
using RevitAgenticAICompanion.UI;

namespace RevitAgenticAICompanion
{
    public class App : IExternalApplication
    {
        internal static readonly Guid DockPaneGuid = new Guid("2F5CE3C1-8BAA-4A8A-9EAE-95C9E8B6E401");
        private DocumentStateTracker _documentStateTracker;
        private System.IDisposable _runtimeDisposable;

        public Result OnStartup(UIControlledApplication application)
        {
            var storagePaths = new LocalStoragePaths("RevitAgenticAICompanion");
            storagePaths.EnsureCreated();

            var dispatcher = new RevitRequestDispatcher();
            var externalEvent = ExternalEvent.Create(dispatcher);
            dispatcher.BindExternalEvent(externalEvent);

            _documentStateTracker = new DocumentStateTracker();
            _documentStateTracker.Attach(application);

            var artifactStore = new ArtifactStore(storagePaths);
            var auditStore = new AuditStore(storagePaths);
            var userMemoryStore = new UserMemoryStore(storagePaths);
            var threadStore = new ProjectThreadStore(storagePaths);
            var codexClient = new CodexAgentRuntimeClient(storagePaths, threadStore);
            _runtimeDisposable = codexClient;
            var runtimeClient = new FallbackAgentRuntimeClient(codexClient, new LocalReviewAgentRuntimeClient());
            var validator = new GeneratedCodeValidator();
            var compiler = new GeneratedActionCompiler();
            var executor = new GeneratedActionExecutor();
            var coordinator = new RuntimeCoordinator(dispatcher, _documentStateTracker, runtimeClient, validator, compiler, executor, artifactStore, auditStore, userMemoryStore);
            var chatPane = new ChatDockablePane(coordinator);

            HostEnvironment.Initialize(dispatcher, externalEvent, _documentStateTracker, coordinator, chatPane);

            application.RegisterDockablePane(
                new DockablePaneId(DockPaneGuid),
                "Revit Agentic AI Companion",
                chatPane);

            CreateRibbon(application);
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            _documentStateTracker?.Detach(application);
            _runtimeDisposable?.Dispose();
            HostEnvironment.Reset();
            return Result.Succeeded;
        }

        private static void CreateRibbon(UIControlledApplication application)
        {
            const string tabName = "Codex";
            const string panelName = "Revit AI";

            try
            {
                application.CreateRibbonTab(tabName);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
            }

            RibbonPanel panel = null;
            foreach (var existingPanel in application.GetRibbonPanels(tabName))
            {
                if (string.Equals(existingPanel.Name, panelName, StringComparison.Ordinal))
                {
                    panel = existingPanel;
                    break;
                }
            }

            if (panel == null)
            {
                panel = application.CreateRibbonPanel(tabName, panelName);
            }

            var showChatButton = new PushButtonData(
                "RevitAgenticAiCompanion.ShowChat",
                "AI Companion",
                Assembly.GetExecutingAssembly().Location,
                typeof(ShowChatCommand).FullName);

            var refreshAuthButton = new PushButtonData(
                "RevitAgenticAiCompanion.RefreshAuth",
                "Refresh Auth",
                Assembly.GetExecutingAssembly().Location,
                typeof(RefreshAuthCommand).FullName);

            var signInButton = new PushButtonData(
                "RevitAgenticAiCompanion.SignIn",
                "Sign In",
                Assembly.GetExecutingAssembly().Location,
                typeof(StartSignInCommand).FullName);

            if (panel.GetItems().Count == 0)
            {
                panel.AddItem(showChatButton);
                panel.AddItem(refreshAuthButton);
                panel.AddItem(signInButton);
            }
        }
    }
}
