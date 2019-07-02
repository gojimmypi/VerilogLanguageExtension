//***************************************************************************
// 
//    Copyright (c) Microsoft Corporation. All rights reserved.
//    This code is licensed under the Visual Studio SDK license terms.
//    THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
//    ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
//    IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
//    PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//***************************************************************************

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio;
using System.Windows;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Text.Formatting;

namespace VerilogLanguage
{
    #region Command Filter

    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType("verilog")]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    internal sealed class VsTextViewCreationListener : IVsTextViewCreationListener
    {
        [Import]
        IVsEditorAdaptersFactoryService AdaptersFactory = null;

        [Import]
        ICompletionBroker CompletionBroker = null;

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            IWpfTextView view = AdaptersFactory.GetWpfTextView(textViewAdapter);
            Debug.Assert(view != null);

            CommandFilter filter = new CommandFilter(view, CompletionBroker);

            IOleCommandTarget next;
            textViewAdapter.AddCommandFilter(filter, out next);
            filter.Next = next;
        }
    }

    internal sealed class CommandFilter : IOleCommandTarget
    {
        ICompletionSession _currentSession;

        public CommandFilter(IWpfTextView textView, ICompletionBroker broker)
        {
            _currentSession = null;

            TextView = textView;
            Broker = broker;
        }

        public IWpfTextView TextView { get; private set; }
        public ICompletionBroker Broker { get; private set; }
        public IOleCommandTarget Next { get; set; }

        private char GetTypeChar(IntPtr pvaIn)
        {
            return (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            bool handled = false;
            int hresult = VSConstants.S_OK;

            // 1. Pre-process
            if (pguidCmdGroup == VSConstants.VSStd2K)
            {
                switch ((VSConstants.VSStd2KCmdID)nCmdID)
                {
                    case VSConstants.VSStd2KCmdID.AUTOCOMPLETE:
                    case VSConstants.VSStd2KCmdID.COMPLETEWORD:
                        handled = StartSession();
                        break;
                    case VSConstants.VSStd2KCmdID.RETURN:
                        handled = Complete(false);
                        break;
                    case VSConstants.VSStd2KCmdID.TAB:
                        handled = Complete(true);
                        break;
                    case VSConstants.VSStd2KCmdID.CANCEL:
                        handled = Cancel();
                        break;
                }
            }

            // this next line was suggested by the IDE, as relating to the next hresult = Next.Exec() call
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

            if (!handled)
            {

                hresult = Next.Exec(pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            }

            if (ErrorHandler.Succeeded(hresult))
            {
                if (pguidCmdGroup == VSConstants.VSStd2K)
                {
                    switch ((VSConstants.VSStd2KCmdID)nCmdID)
                    {
                        case VSConstants.VSStd2KCmdID.TYPECHAR:
                            char ch = GetTypeChar(pvaIn);
                            if (ch == ' ')
                                StartSession();
                            else if (_currentSession != null)
                                Filter();
                            break;
                        case VSConstants.VSStd2KCmdID.BACKSPACE:
                            Filter();
                            break;
                    }
                }
            }

            // As all other attempts to force a re-scan of the entire document for reclassification have failed,
            // when special characters wioth wide-ranging highlighting influence (e.g. "/*" are encountered - 
            // the entire document is replaced. Even attempts to first replace the text before the cursor, and then 
            // after the cursor... results in the cursor being repositioned at the end of the document. :/
            //
            // when this undesired cursor positioning occurs, we'll need to put it back just before completion.
            //
            // See changes detected in event handler VerilogTokenTagger.BufferChanged
            // 
            if (VerilogGlobals.NeedsCursorReposition)
            {
                //SnapshotPoint bp = VerilogGlobals.TheView.Caret.Position.BufferPosition;
                //int ThisLineIndex = VerilogGlobals.TheBuffer.CurrentSnapshot.GetLineNumberFromPosition(bp);
                //
                //ITextViewLine thisLine = VerilogGlobals.TheView.TextViewLines.GetTextViewLineContainingBufferPosition(bp);
                //
                // SnapshotPoint sp = new SnapshotPoint(VerilogGlobals.TheBuffer.CurrentSnapshot,5);
                //
                // See https://docs.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.text.formatting.itextviewline?view=visualstudiosdk-2017
                // Remarks
                //
                // Most properties and parameters that are doubles correspond to coordinates or distances in the 
                // text rendering coordinate system.In this coordinate system, x = 0.0 corresponds to the left 
                // edge of the drawing surface onto which text is rendered(x = view.ViewportLeft corresponds to 
                // the left edge of the viewport), and y = view.ViewportTop corresponds to the top edge of the 
                // viewport. The x-coordinate increases from left to right, and the y - coordinate increases 
                // from top to bottom.
                // 
                // The horizontal and vertical axes of the view behave differently. When the text in the view 
                // is formatted, only the visible lines are formatted. As a result, a viewport cannot be scrolled
                // horizontally and vertically in the same way.
                // 
                // A viewport is scrolled horizontally by changing the left coordinate of the viewport so that it
                // moves with respect to the drawing surface.
                // 
                // A view can be scrolled vertically only by performing a new layout.
                // 
                // Doing a layout in the view may cause the ViewportTop property of the view to change. For 
                // example, scrolling down one line will not translate any of the visible lines.Instead it will 
                // simply change the view's ViewportTop property (causing the lines to move on the screen even 
                // though their y-coordinates have not changed).
                // 
                // Distances in the text rendering coordinate system correspond to logical pixels. If the text 
                // rendering surface is displayed without any scaling transform, then 1 unit in the text rendering
                // coordinate system corresponds to one pixel on the display.
                //
                // ITextViewLine thisLine = VerilogGlobals.TheView.TextViewLines.GetTextViewLineContainingBufferPosition(bp);
                //

                // first get the total current length in chars (not including CR/LF) to 
                // ensure [TheNewPosition] is not gearter than this.
                int CurrentLength = VerilogGlobals.TheBuffer.CurrentSnapshot.GetText().Length;
                if (VerilogGlobals.TheNewPosition > CurrentLength)
                {
                    VerilogGlobals.TheNewPosition = CurrentLength;
                }

                if (VerilogGlobals.TheNewPosition >= 0)
                {
                    // Note that if we try to do this when actually making the changes, an error is encountered:
                    // System.ArgumentException: 'The supplied SnapshotPoint is on an incorrect snapshot.'
                    //
                    SnapshotPoint bp  = new SnapshotPoint(VerilogGlobals.TheBuffer.CurrentSnapshot, VerilogGlobals.TheNewPosition);

                    //
                    // see https://docs.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.text.editor.itextview.displaytextlinecontainingbufferposition?view=visualstudiosdk-2017
                    //
                    // verticalDistance Double
                    //
                    // The distance(in pixels) between the ITextViewLine and the edge of the view. If 
                    // relativeTo is equal to ViewRelativePosition.Top, then the distance is from the top 
                    // of the view to the top of the ITextViewLine. Otherwise, it is the distance from the 
                    // bottom of the ITextViewLine to the bottom on the view.Negative values are 
                    // allowed, which may cause the line to be displayed outside the viewport.
                    // This method can become quite expensive if verticalDistance is large. You should avoid 
                    // making verticalDistance greater than the height of the view.

                    VerilogGlobals.TheView.DisplayTextLineContainingBufferPosition(bp,
                           VerilogGlobals.PriorVerticalDistance, // delta ViewportTop
                           ViewRelativePosition.Top);

                    // mensure the cursor stays in the same place.
                    // see https://stackoverflow.com/questions/42712164/replacing-text-in-document-while-preserving-the-caret
                    VerilogGlobals.TheView.Caret.MoveTo(bp);
                }
                VerilogGlobals.NeedsCursorReposition = false;

                //thisLine = VerilogGlobals.TheView.TextViewLines[5];
                //VerilogGlobals.TheView.Caret.MoveTo(thisLine);
            }
            return hresult;
        }

        /// <summary>
        /// Narrow down the list of options as the user types input
        /// </summary>
        private void Filter()
        {
            if (_currentSession == null)
                return;

            _currentSession.SelectedCompletionSet.SelectBestMatch();
            _currentSession.SelectedCompletionSet.Recalculate();
        }

        /// <summary>
        /// Cancel the auto-complete session, and leave the text unmodified
        /// </summary>
        bool Cancel()
        {
            if (_currentSession == null)
                return false;

            _currentSession.Dismiss();

            return true;
        }

        /// <summary>
        /// Auto-complete text using the specified token
        /// </summary>
        bool Complete(bool force)
        {
            if (_currentSession == null)
                return false;

            if (!_currentSession.SelectedCompletionSet.SelectionStatus.IsSelected && !force)
            {
                _currentSession.Dismiss();
                return false;
            }
            else
            {
                _currentSession.Commit();
                return true;
            }
        }

        /// <summary>
        /// Display list of potential tokens
        /// </summary>
        bool StartSession()
        {
            if (_currentSession != null)
                return false;

            SnapshotPoint caret = TextView.Caret.Position.BufferPosition;
            ITextSnapshot snapshot = caret.Snapshot;

            if (!Broker.IsCompletionActive(TextView))
            {
                _currentSession = Broker.CreateCompletionSession(TextView, snapshot.CreateTrackingPoint(caret, PointTrackingMode.Positive), true);
            }
            else
            {
                _currentSession = Broker.GetSessions(TextView)[0];
            }
            _currentSession.Dismissed += (sender, args) => _currentSession = null;

            _currentSession.Start();

            return true;
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            // this next line was suggested by the IDE, as relating to the next hresult = Next.Exec() call
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
            VerilogGlobals.PerfMon.CommandFilter_QueryStatus_Count++;
            if (pguidCmdGroup == VSConstants.VSStd2K)
            {
                switch ((VSConstants.VSStd2KCmdID)prgCmds[0].cmdID)
                {
                    case VSConstants.VSStd2KCmdID.AUTOCOMPLETE:
                    case VSConstants.VSStd2KCmdID.COMPLETEWORD:
                        prgCmds[0].cmdf = (uint)OLECMDF.OLECMDF_ENABLED | (uint)OLECMDF.OLECMDF_SUPPORTED;
                        return VSConstants.S_OK;
                }
            }
            return Next.QueryStatus(pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }
    }

    #endregion
}