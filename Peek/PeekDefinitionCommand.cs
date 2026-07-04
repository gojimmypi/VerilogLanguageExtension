using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace VerilogLanguage.Peek
{
    internal sealed class PeekDefinitionCommand
    {
        public const int CommandId = 0x0103;
        public static readonly Guid CommandSet = new Guid("C7F8B2F5-7A01-4A07-8E4B-4A29F77A0B9F");

        private const string EmbeddedPeekTextViewRole = "EMBEDDED_PEEK_TEXT_VIEW";
        private const string EditPeekDefinitionCommandName = "Edit.PeekDefinition";

        private readonly AsyncPackage package;

        private PeekDefinitionCommand(AsyncPackage package, OleMenuCommandService commandService) {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var commandID = new CommandID(CommandSet, CommandId);
            var menuItem = new OleMenuCommand(this.Execute, commandID);
            menuItem.BeforeQueryStatus += this.BeforeQueryStatus;
            commandService.AddCommand(menuItem);
        }

        public static PeekDefinitionCommand Instance
        {
            get;
            private set;
        }

        public static async Task InitializeAsync(AsyncPackage package) {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new PeekDefinitionCommand(package, commandService);
        }

        private void BeforeQueryStatus(object sender, EventArgs e) {
            ThreadHelper.ThrowIfNotOnUIThread();

            OleMenuCommand command = sender as OleMenuCommand;
            if (command == null) {
                return;
            }

            IWpfTextView textView = GetActiveWpfTextView();
            bool isVerilogPeekCommandView = textView != null && IsVerilogPeekCommandView(textView);

            command.Visible = isVerilogPeekCommandView;
            command.Enabled = isVerilogPeekCommandView;
        }

        private void Execute(object sender, EventArgs e) {
            ThreadHelper.ThrowIfNotOnUIThread();

            try {
                IWpfTextView textView = GetActiveWpfTextView();
                if (textView == null || textView.TextBuffer == null || textView.TextBuffer.CurrentSnapshot == null) {
                    return;
                }

                if (!IsVerilogPeekCommandView(textView)) {
                    System.Diagnostics.Debug.WriteLine("VLE Peek Definition ignored for unsupported view. Roles=" + RoleText(textView));
                    return;
                }

                if (IsPeekHostedView(textView)) {
                    System.Diagnostics.Debug.WriteLine("VLE Peek Definition delegating Peek-hosted view to " + EditPeekDefinitionCommandName + ". Roles=" + RoleText(textView));
                    if (!TryExecuteBuiltInPeekDefinition(textView)) {
                        System.Diagnostics.Debug.WriteLine("VLE Peek Definition could not delegate Peek-hosted view to " + EditPeekDefinitionCommandName + ". Roles=" + RoleText(textView));
                    }

                    return;
                }

                TriggerPrimaryPeekSession(textView);
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine("Verilog Peek Definition command failed: " + ex);
            }
        }

        private static void TriggerPrimaryPeekSession(IWpfTextView textView) {
            ThreadHelper.ThrowIfNotOnUIThread();

            IComponentModel componentModel = Package.GetGlobalService(typeof(SComponentModel)) as IComponentModel;
            if (componentModel == null) {
                return;
            }

            IPeekBroker peekBroker = componentModel.GetService<IPeekBroker>();
            if (peekBroker == null) {
                return;
            }

            SnapshotPoint caretPoint = textView.Caret.Position.BufferPosition;
            ITrackingPoint trackingPoint = caretPoint.Snapshot.CreateTrackingPoint(
                caretPoint.Position,
                PointTrackingMode.Positive);

            System.Diagnostics.Debug.WriteLine("VLE Peek Definition starting Peek session. Roles=" + RoleText(textView));
            peekBroker.TriggerPeekSession(
                textView,
                trackingPoint,
                PredefinedPeekRelationships.Definitions.Name);
        }

        private static bool TryExecuteBuiltInPeekDefinition(IWpfTextView textView) {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (textView == null || textView.VisualElement == null) {
                return false;
            }

            try {
                textView.VisualElement.Focus();

                DTE dte = Package.GetGlobalService(typeof(DTE)) as DTE;
                if (dte == null) {
                    return false;
                }

                dte.ExecuteCommand(EditPeekDefinitionCommandName);
                return true;
            }
            catch (COMException ex) {
                System.Diagnostics.Debug.WriteLine("VLE Peek Definition built-in command failed: " + ex.Message);
                return false;
            }
            catch (InvalidOperationException ex) {
                System.Diagnostics.Debug.WriteLine("VLE Peek Definition built-in command failed: " + ex.Message);
                return false;
            }
            catch (ArgumentException ex) {
                System.Diagnostics.Debug.WriteLine("VLE Peek Definition built-in command failed: " + ex.Message);
                return false;
            }
        }

        private static bool IsVerilogTextView(IWpfTextView textView) {
            if (textView == null || textView.TextBuffer == null || textView.TextBuffer.ContentType == null) {
                return false;
            }

            return textView.TextBuffer.ContentType.IsOfType("verilog");
        }

        private static bool IsVerilogPeekCommandView(IWpfTextView textView) {
            return IsVerilogPrimaryDocumentView(textView) ||
                (IsVerilogTextView(textView) && IsPeekHostedView(textView));
        }

        private static bool IsVerilogPrimaryDocumentView(IWpfTextView textView) {
            if (!IsVerilogTextView(textView)) {
                return false;
            }

            if (textView.Roles == null) {
                return false;
            }

            return textView.Roles.Contains(PredefinedTextViewRoles.Document) &&
                textView.Roles.Contains(PredefinedTextViewRoles.PrimaryDocument) &&
                !IsPeekHostedView(textView);
        }

        private static bool IsPeekHostedView(IWpfTextView textView) {
            if (textView == null) {
                return false;
            }

            if (textView.Roles != null && textView.Roles.Contains(EmbeddedPeekTextViewRole)) {
                return true;
            }

            if (textView.VisualElement == null) {
                return false;
            }

            DependencyObject current = textView.VisualElement;
            while (current != null) {
                Type currentType = current.GetType();
                string typeName = currentType.FullName ?? currentType.Name;
                if (typeName.IndexOf("Peek", StringComparison.OrdinalIgnoreCase) >= 0) {
                    return true;
                }

                try {
                    current = VisualTreeHelper.GetParent(current);
                }
                catch (InvalidOperationException) {
                    return false;
                }
            }

            return false;
        }

        private static string RoleText(IWpfTextView textView) {
            if (textView == null || textView.Roles == null) {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            foreach (string role in textView.Roles) {
                if (builder.Length > 0) {
                    builder.Append(",");
                }

                builder.Append(role);
            }

            return builder.ToString();
        }

        private static IWpfTextView GetActiveWpfTextView() {
            ThreadHelper.ThrowIfNotOnUIThread();

            IVsTextManager textManager = Package.GetGlobalService(typeof(SVsTextManager)) as IVsTextManager;
            if (textManager == null) {
                return null;
            }

            IVsTextView vsTextView;
            int hr = textManager.GetActiveView(0, null, out vsTextView);
            if (hr != VSConstants.S_OK || vsTextView == null) {
                return null;
            }

            IComponentModel componentModel = Package.GetGlobalService(typeof(SComponentModel)) as IComponentModel;
            if (componentModel == null) {
                return null;
            }

            IVsEditorAdaptersFactoryService adapters =
                componentModel.GetService<IVsEditorAdaptersFactoryService>();
            if (adapters == null) {
                return null;
            }

            return adapters.GetWpfTextView(vsTextView);
        }
    }
}
