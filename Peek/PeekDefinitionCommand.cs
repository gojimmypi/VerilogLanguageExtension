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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace VerilogLanguage.Peek
{
    internal sealed class PeekDefinitionCommand
    {
        public const int CommandId = 0x0103;
        public static readonly Guid CommandSet = new Guid("C7F8B2F5-7A01-4A07-8E4B-4A29F77A0B9F");

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
            bool isVerilogPrimaryDocumentView = textView != null && IsVerilogPrimaryDocumentView(textView);

            command.Visible = isVerilogPrimaryDocumentView;
            command.Enabled = isVerilogPrimaryDocumentView;
        }

        private void Execute(object sender, EventArgs e) {
            ThreadHelper.ThrowIfNotOnUIThread();

            try {
                IWpfTextView textView = GetActiveWpfTextView();
                if (textView == null || textView.TextBuffer == null || textView.TextBuffer.CurrentSnapshot == null) {
                    return;
                }

                if (!IsVerilogPrimaryDocumentView(textView)) {
                    System.Diagnostics.Debug.WriteLine("VLE Peek Definition ignored for non-primary or Peek-hosted Verilog view.");
                    return;
                }

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

                peekBroker.TriggerPeekSession(
                    textView,
                    trackingPoint,
                    PredefinedPeekRelationships.Definitions.Name);
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine("Verilog Peek Definition command failed: " + ex);
            }
        }

        private static bool IsVerilogTextView(IWpfTextView textView) {
            if (textView == null || textView.TextBuffer == null || textView.TextBuffer.ContentType == null) {
                return false;
            }

            return textView.TextBuffer.ContentType.IsOfType("verilog");
        }

        /* We're only interested in Verilog text views that are the primary document view,
         * not Peek-hosted views or other secondary views. No nested peeks.*/
        private static bool IsVerilogPrimaryDocumentView(IWpfTextView textView) {
            if (!IsVerilogTextView(textView)) {
                return false;
            }

            if (textView.Roles == null) {
                return false;
            }

            if (!textView.Roles.Contains(PredefinedTextViewRoles.Document) ||
                !textView.Roles.Contains(PredefinedTextViewRoles.PrimaryDocument)) {
                return false;
            }

            return !IsPeekHostedView(textView);
        }

        private static bool IsPeekHostedView(IWpfTextView textView) {
            if (textView == null || textView.VisualElement == null) {
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
