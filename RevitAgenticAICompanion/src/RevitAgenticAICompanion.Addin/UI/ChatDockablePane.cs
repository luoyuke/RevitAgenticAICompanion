using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RevitAgenticAICompanion.Runtime;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace RevitAgenticAICompanion.UI
{
    public sealed class ChatDockablePane : Page, Autodesk.Revit.UI.IDockablePaneProvider
    {
        private readonly RuntimeCoordinator _runtimeCoordinator;
        private readonly WpfTextBox _logTextBox;
        private readonly WpfTextBox _promptTextBox;
        private readonly WpfTextBox _summaryTextBox;
        private readonly WpfTextBox _sourceTextBox;
        private readonly TextBlock _statusText;
        private readonly Button _planButton;
        private readonly Button _approveButton;
        private readonly Button _confirmButton;
        private readonly Queue<string> _pendingLogMessages;
        private AgentRuntimeStatus _currentRuntimeStatus;
        private bool _isPaneLoaded;
        private bool _hasRequestedInitialStatus;

        public ChatDockablePane(RuntimeCoordinator runtimeCoordinator)
        {
            _runtimeCoordinator = runtimeCoordinator;
            _pendingLogMessages = new Queue<string>();
            Title = "Revit Agentic AI Companion";

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(120) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(120) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(120) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            _statusText = new TextBlock
            {
                Margin = new Thickness(8),
                Text = "Checking runtime status...",
                TextWrapping = TextWrapping.Wrap,
                FontFamily = CreateUiFontFamily(),
            };
            Grid.SetRow(_statusText, 0);
            root.Children.Add(_statusText);

            _promptTextBox = new WpfTextBox
            {
                Margin = new Thickness(8),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Text = "Create a bill of quantity schedule for walls.",
                FontFamily = CreateUiFontFamily(),
            };
            Grid.SetRow(_promptTextBox, 1);
            root.Children.Add(_promptTextBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(8, 0, 8, 8),
            };
            Grid.SetRow(buttonPanel, 2);
            root.Children.Add(buttonPanel);

            _planButton = CreateButton("Plan", OnPlanClicked);
            _approveButton = CreateButton("Approve", OnApproveClicked);
            _confirmButton = CreateButton("Confirm", OnConfirmClicked);

            _approveButton.IsEnabled = false;
            _confirmButton.IsEnabled = false;

            buttonPanel.Children.Add(_planButton);
            buttonPanel.Children.Add(_approveButton);
            buttonPanel.Children.Add(_confirmButton);

            _summaryTextBox = CreateReadOnlyTextBox();
            Grid.SetRow(_summaryTextBox, 3);
            root.Children.Add(_summaryTextBox);

            _logTextBox = CreateReadOnlyTextBox();
            Grid.SetRow(_logTextBox, 4);
            root.Children.Add(_logTextBox);

            _sourceTextBox = CreateReadOnlyTextBox();
            Grid.SetRow(_sourceTextBox, 5);
            root.Children.Add(_sourceTextBox);

            Content = root;
            Loaded += OnLoaded;
            IsVisibleChanged += OnIsVisibleChanged;
        }

        public void SetupDockablePane(Autodesk.Revit.UI.DockablePaneProviderData data)
        {
            data.FrameworkElement = this;
            data.InitialState = new Autodesk.Revit.UI.DockablePaneState
            {
                DockPosition = Autodesk.Revit.UI.DockPosition.Right,
            };
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            _isPaneLoaded = true;
            FlushPendingLogs();
            if (_hasRequestedInitialStatus)
            {
                return;
            }

            _hasRequestedInitialStatus = true;
            AppendLog("Checking runtime status...");
            await RefreshRuntimeStatusAsync(logToPane: true);
        }

        private async void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(e.NewValue is bool isVisible) || !isVisible || !_isPaneLoaded)
            {
                return;
            }

            await RefreshRuntimeStatusAsync(logToPane: false);
        }

        private async void OnPlanClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                SetBusyState(true);
                AppendLog("Planning from current Revit context...");
                var session = await _runtimeCoordinator.CreateProposalAsync(_promptTextBox.Text, CancellationToken.None);
                _summaryTextBox.Text = BuildSummary(session);
                _sourceTextBox.Text = session.Proposal.RequiresCompilation
                    ? session.Proposal.GeneratedSource ?? string.Empty
                    : session.Proposal.ReplyText ?? string.Empty;

                _approveButton.IsEnabled = session.Proposal.RequiresApproval
                    && session.ValidationReport.IsValid
                    && session.CompilationResult.IsSuccess
                    && session.PreviewResult != null
                    && session.PreviewResult.IsSuccess
                    && !session.ValidationReport.IsUndoHostile;
                _confirmButton.IsEnabled = session.Proposal.RequiresApproval
                    && session.ValidationReport.IsValid
                    && session.CompilationResult.IsSuccess
                    && session.PreviewResult != null
                    && session.PreviewResult.IsSuccess
                    && session.ValidationReport.IsUndoHostile;

                AppendLog("Response ready. Planner: " + session.Proposal.Provenance.Summary);
                AppendLog("Response kind: " + session.Proposal.ResponseKind);
                if (session.Proposal.RequiresCompilation)
                {
                    AppendLog("Proposal created. Source hash: " + session.Proposal.SourceHash);
                }

                AppendLog("Compilation success: " + session.CompilationResult.IsSuccess);
                if (session.PreviewResult != null)
                {
                    AppendLog(session.PreviewResult.IsSuccess
                        ? "Preview ready. Targets: " + string.Join(", ", session.PreviewResult.TargetElementIds)
                        : "Preview failed: " + session.PreviewResult.Error);
                }

                if (session.ExecutionResult != null && session.Proposal.ExecutesReadOnly)
                {
                    AppendLog(session.ExecutionResult.IsSuccess
                        ? "Read-only query executed."
                        : "Read-only query failed: " + session.ExecutionResult.Error);
                }

                AppendLog("Artifacts written to: " + session.Proposal.ArtifactDirectory);
            }
            catch (Exception ex)
            {
                AppendLog("Planning failed: " + ex.Message);
            }
            finally
            {
                SetBusyState(false);
            }
        }

        private async void OnApproveClicked(object sender, RoutedEventArgs e)
        {
            await ApproveAsync(false);
        }

        private async void OnConfirmClicked(object sender, RoutedEventArgs e)
        {
            await ApproveAsync(true);
        }

        private async System.Threading.Tasks.Task ApproveAsync(bool explicitConfirm)
        {
            try
            {
                SetBusyState(true);
                var approved = await _runtimeCoordinator.ApproveCurrentProposalAsync(explicitConfirm);
                if (!approved)
                {
                    AppendLog("Approval failed. The proposal may be undo-hostile without confirm, invalid, or stale.");
                    return;
                }

                AppendLog("Proposal approved. Executing...");
                var execution = await _runtimeCoordinator.ExecuteCurrentProposalAsync();
                if (!execution.IsSuccess)
                {
                    AppendLog("Execution failed: " + execution.Error);
                    return;
                }

                AppendLog("Execution succeeded.");
                AppendLog("Transaction: " + execution.TransactionName);
                AppendLog("Changed element ids: " + string.Join(", ", execution.ChangedElementIds));
                _summaryTextBox.Text = BuildSummary(_runtimeCoordinator.CurrentSession);
            }
            catch (Exception ex)
            {
                AppendLog("Approval failed: " + ex.Message);
            }
            finally
            {
                SetBusyState(false);
            }
        }

        public void RefreshRuntimeStatusFromRibbon()
        {
            _ = Dispatcher.InvokeAsync(() =>
            {
                AppendLog("Refreshing runtime auth status...");
                _ = RefreshRuntimeStatusAsync(logToPane: true);
            });
        }

        public void StartLoginFromRibbon()
        {
            _ = Dispatcher.InvokeAsync(() =>
            {
                AppendLog("Starting browser sign-in...");
                _ = StartLoginFromRibbonAsync();
            });
        }

        private async Task StartLoginFromRibbonAsync()
        {
            try
            {
                SetBusyState(true);
                var login = await _runtimeCoordinator.StartLoginAsync(CancellationToken.None);
                if (!login.IsStarted)
                {
                    AppendLog("Sign in failed: " + login.Detail);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(login.AuthUrl))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = login.AuthUrl,
                        UseShellExecute = true,
                    });
                    AppendLog("Browser sign-in started. Complete it, then use Refresh Auth from the ribbon.");
                    return;
                }

                AppendLog(login.Detail);
            }
            catch (Exception ex)
            {
                AppendLog("Sign in failed: " + ex.Message);
            }
            finally
            {
                SetBusyState(false);
            }
        }

        private async Task RefreshRuntimeStatusAsync(bool logToPane)
        {
            try
            {
                SetBusyState(true);
                var status = await _runtimeCoordinator.GetRuntimeStatusAsync(CancellationToken.None);
                _currentRuntimeStatus = status;
                _statusText.Text = status.Mode + ": " + status.Detail;
                if (logToPane)
                {
                    AppendLog("Runtime status: " + status.Mode + ". " + status.Detail);
                }
            }
            catch (Exception ex)
            {
                _currentRuntimeStatus = null;
                _statusText.Text = "Runtime status unavailable: " + ex.Message;
                if (logToPane)
                {
                    AppendLog("Runtime status unavailable: " + ex.Message);
                }
            }
            finally
            {
                SetBusyState(false);
            }
        }

        private void SetBusyState(bool isBusy)
        {
            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.InvokeAsync(() => SetBusyState(isBusy));
                return;
            }

            _planButton.IsEnabled = !isBusy
                && _currentRuntimeStatus != null
                && _currentRuntimeStatus.CanPlan;
            if (_runtimeCoordinator.CurrentSession != null)
            {
                _approveButton.IsEnabled = !isBusy
                    && _runtimeCoordinator.CurrentSession.Proposal.RequiresApproval
                    && _runtimeCoordinator.CurrentSession.ValidationReport.IsValid
                    && _runtimeCoordinator.CurrentSession.CompilationResult.IsSuccess
                    && _runtimeCoordinator.CurrentSession.PreviewResult != null
                    && _runtimeCoordinator.CurrentSession.PreviewResult.IsSuccess
                    && !_runtimeCoordinator.CurrentSession.ValidationReport.IsUndoHostile;
                _confirmButton.IsEnabled = !isBusy
                    && _runtimeCoordinator.CurrentSession.Proposal.RequiresApproval
                    && _runtimeCoordinator.CurrentSession.ValidationReport.IsValid
                    && _runtimeCoordinator.CurrentSession.CompilationResult.IsSuccess
                    && _runtimeCoordinator.CurrentSession.PreviewResult != null
                    && _runtimeCoordinator.CurrentSession.PreviewResult.IsSuccess
                    && _runtimeCoordinator.CurrentSession.ValidationReport.IsUndoHostile;
            }
            else
            {
                _approveButton.IsEnabled = false;
                _confirmButton.IsEnabled = false;
            }
        }

        private static Button CreateButton(string text, RoutedEventHandler clickHandler)
        {
            var button = new Button
            {
                Content = text,
                Margin = new Thickness(0, 0, 8, 0),
                Padding = new Thickness(12, 4, 12, 4),
                MinWidth = 90,
                FontFamily = CreateUiFontFamily(),
            };
            button.Click += clickHandler;
            return button;
        }

        private static WpfTextBox CreateReadOnlyTextBox()
        {
            return new WpfTextBox
            {
                Margin = new Thickness(8, 0, 8, 8),
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = CreateUiFontFamily(),
            };
        }

        private string BuildSummary(PlanningSession session)
        {
            var validation = session.ValidationReport;
            if (!session.Proposal.RequiresCompilation)
            {
                return
                    "Reply:" + Environment.NewLine +
                    session.Proposal.ReplyText + Environment.NewLine + Environment.NewLine +
                    "Response kind: " + session.Proposal.ResponseKind + Environment.NewLine +
                    "Capability band: " + session.Proposal.CapabilityBand + Environment.NewLine +
                    "Risk level: " + session.Proposal.RiskLevel + Environment.NewLine +
                    "Scope: " + session.Proposal.ScopeSummary + Environment.NewLine +
                    "Planner: " + session.Proposal.Provenance.Summary + Environment.NewLine +
                    "Document fingerprint: " + session.ContextSnapshot.Fingerprint;
            }

            var summary =
                "Summary:" + Environment.NewLine +
                session.Proposal.ActionSummary + Environment.NewLine + Environment.NewLine +
                "Response kind: " + session.Proposal.ResponseKind + Environment.NewLine +
                "Capability band: " + session.Proposal.CapabilityBand + Environment.NewLine +
                "Risk level: " + session.Proposal.RiskLevel + Environment.NewLine +
                "Scope: " + session.Proposal.ScopeSummary + Environment.NewLine +
                "Planner: " + session.Proposal.Provenance.Summary + Environment.NewLine +
                "Validation valid: " + validation.IsValid + Environment.NewLine +
                "Compilation success: " + session.CompilationResult.IsSuccess + Environment.NewLine +
                "Undo-hostile: " + validation.IsUndoHostile + Environment.NewLine +
                "Document fingerprint: " + session.ContextSnapshot.Fingerprint + Environment.NewLine +
                "Active view: " + session.ContextSnapshot.ActiveViewName + Environment.NewLine +
                "Selected element ids: " + string.Join(", ", session.ContextSnapshot.SelectedElementIds) + Environment.NewLine +
                "Selected categories: " + string.Join(", ", session.ContextSnapshot.SelectedCategoryNames) + Environment.NewLine +
                "Available categories sampled: " + session.ContextSnapshot.AvailableModelCategories.Count;

            if (session.PreviewResult != null)
            {
                summary += Environment.NewLine + Environment.NewLine +
                    "Preview:" + Environment.NewLine +
                    "Success: " + session.PreviewResult.IsSuccess + Environment.NewLine +
                    "Summary: " + session.PreviewResult.Summary + Environment.NewLine +
                    "Target element ids: " + string.Join(", ", session.PreviewResult.TargetElementIds);
                if (!string.IsNullOrWhiteSpace(session.PreviewResult.Error))
                {
                    summary += Environment.NewLine + "Preview error: " + session.PreviewResult.Error;
                }
            }

            if (session.ExecutionResult != null)
            {
                summary += Environment.NewLine + Environment.NewLine +
                    "Execution:" + Environment.NewLine +
                    "Success: " + session.ExecutionResult.IsSuccess + Environment.NewLine +
                    "Mode/transaction: " + session.ExecutionResult.TransactionName + Environment.NewLine +
                    "Summary: " + session.ExecutionResult.Summary + Environment.NewLine +
                    "Changed/returned ids: " + string.Join(", ", session.ExecutionResult.ChangedElementIds);
                if (!string.IsNullOrWhiteSpace(session.ExecutionResult.Error))
                {
                    summary += Environment.NewLine + "Execution error: " + session.ExecutionResult.Error;
                }
            }

            return summary;
        }

        private void AppendLog(string message)
        {
            if (!_isPaneLoaded)
            {
                _pendingLogMessages.Enqueue(message);
                return;
            }

            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.InvokeAsync(() => AppendLog(message));
                return;
            }

            _logTextBox.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + message + Environment.NewLine);
            _logTextBox.ScrollToEnd();
        }

        private void FlushPendingLogs()
        {
            if (!Dispatcher.CheckAccess())
            {
                _ = Dispatcher.InvokeAsync(FlushPendingLogs);
                return;
            }

            while (_pendingLogMessages.Count > 0)
            {
                var message = _pendingLogMessages.Dequeue();
                _logTextBox.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + message + Environment.NewLine);
            }

            _logTextBox.ScrollToEnd();
        }

        private static FontFamily CreateUiFontFamily()
        {
            return new FontFamily("Segoe UI");
        }
    }
}
