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
        private readonly ComboBox _runtimeProviderComboBox;
        private readonly ComboBox _runtimeProfileComboBox;
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

            var runtimeToolbar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(8),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetRow(runtimeToolbar, 0);
            root.Children.Add(runtimeToolbar);

            runtimeToolbar.Children.Add(new TextBlock
            {
                Text = "Provider:",
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = CreateUiFontFamily(),
            });

            _runtimeProviderComboBox = new ComboBox
            {
                Width = 100,
                Margin = new Thickness(0, 0, 18, 0),
                FontFamily = CreateUiFontFamily(),
            };
            AddRuntimeProviderItem("Codex", AgentRuntimeProvider.Codex);
            AddRuntimeProviderItem("Claude", AgentRuntimeProvider.Claude);
            _runtimeProviderComboBox.SelectedIndex = _runtimeCoordinator.RuntimeProvider == AgentRuntimeProvider.Claude ? 1 : 0;
            _runtimeProviderComboBox.SelectionChanged += OnRuntimeProviderChanged;
            runtimeToolbar.Children.Add(_runtimeProviderComboBox);

            runtimeToolbar.Children.Add(new TextBlock
            {
                Text = "Runtime:",
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = CreateUiFontFamily(),
            });

            _runtimeProfileComboBox = new ComboBox
            {
                Width = 130,
                Margin = new Thickness(0, 0, 18, 0),
                FontFamily = CreateUiFontFamily(),
            };
            AddRuntimeProfileItem("Provider default", RuntimeProfile.ProviderDefault, null);
            AddRuntimeProfileItem("Fast", RuntimeProfile.Fast, null);
            AddRuntimeProfileItem("Balanced", RuntimeProfile.Balanced, null);
            AddRuntimeProfileItem("Deep", RuntimeProfile.Deep, "May be slower and use more quota.");
            _runtimeProfileComboBox.SelectedIndex = 2;
            runtimeToolbar.Children.Add(_runtimeProfileComboBox);

            runtimeToolbar.Children.Add(new TextBlock
            {
                Text = "Runtime status:",
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = CreateUiFontFamily(),
            });

            _statusText = new TextBlock
            {
                Text = "Checking...",
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = CreateUiFontFamily(),
            };
            runtimeToolbar.Children.Add(_statusText);

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

            _planButton.IsEnabled = false;
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
                var runtimeOptions = GetSelectedRuntimeOptions();
                AppendLog("Runtime provider: " + _runtimeCoordinator.RuntimeProvider);
                AppendLog("Runtime profile: " + runtimeOptions.DisplayName);
                var session = await _runtimeCoordinator.CreateProposalAsync(_promptTextBox.Text, runtimeOptions, CancellationToken.None);
                ApplySessionToUi(session);

                if (session.FailurePacket != null)
                {
                    AppendLog("Failure analyzed. Stage: " + session.FailurePacket.FailureStage);
                }

                AppendLog("Response ready. Planner: " + session.Proposal.Provenance.Summary);
                AppendLog("Response kind: " + session.Proposal.ResponseKind);
                AppendLog("Confidence: " + session.Proposal.ConfidenceLevel);
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

                if (session.RetrievedEvidence.Count > 0)
                {
                    AppendLog("Retrieved evidence count: " + session.RetrievedEvidence.Count);
                }

                if (session.ExecutionResult != null && session.Proposal.ExecutesReadOnly)
                {
                    AppendLog(session.ExecutionResult.IsSuccess
                        ? "Read-only query executed."
                        : "Read-only query failed: " + session.ExecutionResult.Error);
                }

                AppendLog("Artifacts written to: " + session.Proposal.ArtifactDirectory);
            }
            catch (AgentRuntimeException ex)
            {
                AppendLog("Planning failed: " + ex.Message);
                AppendRuntimeFailure(ex.FailureRecord);
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
                var previousProposalId = _runtimeCoordinator.CurrentSession?.Proposal?.ProposalId ?? string.Empty;
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
                    var latestSession = _runtimeCoordinator.CurrentSession;
                    if (latestSession != null && !string.Equals(latestSession.Proposal?.ProposalId, previousProposalId, StringComparison.Ordinal))
                    {
                        ApplySessionToUi(latestSession);
                        AppendLog("Failure analyzed. Planner: " + latestSession.Proposal.Provenance.Summary);
                        AppendLog("Failure analysis response kind: " + latestSession.Proposal.ResponseKind);
                        if (latestSession.Proposal.RequiresApproval)
                        {
                            AppendLog("A corrected proposal is ready for review.");
                        }
                    }

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
                AppendLog("Refreshing runtime status...");
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
                _statusText.Text = BuildRuntimeStatusLabel(status);
                _statusText.ToolTip = status.Mode + ": " + status.Detail;
                if (logToPane)
                {
                    AppendLog("Runtime status (" + _runtimeCoordinator.RuntimeProvider + "): " + status.Mode + ". " + status.Detail);
                }
            }
            catch (Exception ex)
            {
                _currentRuntimeStatus = null;
                _statusText.Text = "Error";
                _statusText.ToolTip = "Runtime status unavailable: " + ex.Message;
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

        private void AddRuntimeProviderItem(string label, AgentRuntimeProvider provider)
        {
            var item = new ComboBoxItem
            {
                Content = label,
                Tag = provider,
                FontFamily = CreateUiFontFamily(),
            };
            _runtimeProviderComboBox.Items.Add(item);
        }

        private async void OnRuntimeProviderChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = _runtimeProviderComboBox.SelectedItem as ComboBoxItem;
            if (!(selectedItem?.Tag is AgentRuntimeProvider provider))
            {
                return;
            }

            _runtimeCoordinator.SetRuntimeProvider(provider);
            AppendLog("Runtime provider selected: " + provider);
            await RefreshRuntimeStatusAsync(logToPane: true);
        }

        private void AddRuntimeProfileItem(string label, RuntimeProfile profile, string tooltip)
        {
            var item = new ComboBoxItem
            {
                Content = label,
                Tag = profile,
                ToolTip = tooltip,
                FontFamily = CreateUiFontFamily(),
            };
            _runtimeProfileComboBox.Items.Add(item);
        }

        private RuntimeInvocationOptions GetSelectedRuntimeOptions()
        {
            var selectedItem = _runtimeProfileComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem?.Tag is RuntimeProfile profile)
            {
                return new RuntimeInvocationOptions(profile);
            }

            return RuntimeInvocationOptions.Default;
        }

        private static string BuildRuntimeStatusLabel(AgentRuntimeStatus status)
        {
            if (status == null || !status.IsAvailable || !status.IsAuthenticated)
            {
                return "Error";
            }

            if (!status.CanPlan || (status.Detail ?? string.Empty).IndexOf("validation is unavailable", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Warning";
            }

            return "OK";
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
                    "Confidence: " + session.Proposal.ConfidenceLevel + Environment.NewLine +
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
                "Confidence: " + session.Proposal.ConfidenceLevel + Environment.NewLine +
                "Evidence summary: " + session.Proposal.EvidenceSummary + Environment.NewLine +
                "Planner: " + session.Proposal.Provenance.Summary + Environment.NewLine +
                "Validation valid: " + validation.IsValid + Environment.NewLine +
                "Compilation success: " + session.CompilationResult.IsSuccess + Environment.NewLine +
                "Undo-hostile: " + validation.IsUndoHostile + Environment.NewLine +
                "Document fingerprint: " + session.ContextSnapshot.Fingerprint + Environment.NewLine +
                "Active view: " + session.ContextSnapshot.ActiveViewName + Environment.NewLine +
                "Selected element ids: " + string.Join(", ", session.ContextSnapshot.SelectedElementIds) + Environment.NewLine +
                "Selected categories: " + string.Join(", ", session.ContextSnapshot.SelectedCategoryNames) + Environment.NewLine +
                "Available categories sampled: " + session.ContextSnapshot.AvailableModelCategories.Count;

            if (session.Proposal.Assumptions.Count > 0)
            {
                summary += Environment.NewLine + Environment.NewLine +
                    "Assumptions:" + Environment.NewLine +
                    string.Join(Environment.NewLine, session.Proposal.Assumptions);
            }

            if (session.RetrievedEvidence.Count > 0)
            {
                summary += Environment.NewLine + Environment.NewLine + "Retrieved Evidence:";
                foreach (var evidence in session.RetrievedEvidence)
                {
                    summary += Environment.NewLine +
                        "Probe " + evidence.ProbeOrdinal + ": " + evidence.Purpose + Environment.NewLine +
                        "Question: " + evidence.Question + Environment.NewLine +
                        "Summary: " + evidence.Summary + Environment.NewLine +
                        "Element ids: " + string.Join(", ", evidence.ElementIds);
                }
            }

            if (session.UserPreferences.Count > 0)
            {
                summary += Environment.NewLine + Environment.NewLine + "User Preferences:";
                foreach (var preference in session.UserPreferences)
                {
                    summary += Environment.NewLine +
                        "[" + preference.ConfidenceLevel + "] " + preference.Key + " = " + preference.Value;
                }
            }

            if (session.FailurePacket != null)
            {
                summary += Environment.NewLine + Environment.NewLine +
                    "Failure Context:" + Environment.NewLine +
                    "Stage: " + session.FailurePacket.FailureStage + Environment.NewLine +
                    "Exception type: " + session.FailurePacket.ExceptionType + Environment.NewLine +
                    "Exception message: " + session.FailurePacket.ExceptionMessage + Environment.NewLine +
                    "Original run id: " + session.FailurePacket.OriginalRunId;
            }

            if (session.Proposal.ContinuesPlanning)
            {
                summary += Environment.NewLine + Environment.NewLine +
                    "Probe Purpose: " + session.Proposal.ProbePurpose + Environment.NewLine +
                    "Probe Question: " + session.Proposal.ProbeQuestion;
            }

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

        private void ApplySessionToUi(PlanningSession session)
        {
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
        }

        private void AppendRuntimeFailure(AgentRuntimeFailureRecord failureRecord)
        {
            if (failureRecord == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(failureRecord.CliVersion))
            {
                AppendLog("Runtime CLI version: " + failureRecord.CliVersion);
            }

            if (!string.IsNullOrWhiteSpace(failureRecord.ExecutablePath))
            {
                AppendLog("Codex executable: " + failureRecord.ExecutablePath);
            }

            if (!string.IsNullOrWhiteSpace(failureRecord.ConfigModel) || !string.IsNullOrWhiteSpace(failureRecord.ConfigReasoningEffort))
            {
                AppendLog("Codex config: model=" +
                    (string.IsNullOrWhiteSpace(failureRecord.ConfigModel) ? "(default)" : failureRecord.ConfigModel) +
                    ", reasoning=" +
                    (string.IsNullOrWhiteSpace(failureRecord.ConfigReasoningEffort) ? "(default)" : failureRecord.ConfigReasoningEffort));
            }

            if (failureRecord.ExitCode.HasValue)
            {
                AppendLog("Codex exit code: " + failureRecord.ExitCode.Value);
            }

            if (!string.IsNullOrWhiteSpace(failureRecord.StderrSummary))
            {
                AppendLog("Codex stderr: " + failureRecord.StderrSummary);
            }
            else if (!string.IsNullOrWhiteSpace(failureRecord.StdoutSummary))
            {
                AppendLog("Codex stdout: " + failureRecord.StdoutSummary);
            }
        }
    }
}
