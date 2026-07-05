// file: Intellisense/QuickInfoController.cs
//***************************************************************************
//
//  MIT License
//
//  Copyright (c) 2025-2026 gojimmypi
//
//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:
//
//  The above copyright notice and this permission notice shall be included in all
//  copies or substantial portions of the Software.
//
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//  SOFTWARE.
//
//***************************************************************************

using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using VerilogLanguage;

namespace VSLTK.Intellisense
{
    #region IIntellisenseController

    internal class TemplateQuickInfoController : IIntellisenseController
    {
        #region Private Data Members

        private ITextView _textView;
        private readonly IList<ITextBuffer> _subjectBuffers;
        private readonly TemplateQuickInfoControllerProvider _componentContext;

        #endregion

        #region Constructors
        internal TemplateQuickInfoController(
            ITextView textView,
            IList<ITextBuffer> subjectBuffers,
            TemplateQuickInfoControllerProvider componentContext) {
            _textView = textView;
            _subjectBuffers = subjectBuffers;
            _componentContext = componentContext;

            _textView.MouseHover += OnTextViewMouseHover;
        }

        #endregion

        #region IIntellisenseController Members

        public void ConnectSubjectBuffer(ITextBuffer subjectBuffer) {
        }

        public void DisconnectSubjectBuffer(ITextBuffer subjectBuffer) {
        }

        public void Detach(ITextView textView) {
            if (_textView == textView) {
                _textView.MouseHover -= OnTextViewMouseHover;
                _textView = null;
            }
        }

        #endregion

        #region Event Handlers


        /// <summary>
        /// Determine if the mouse is hovering over a token. If so, highlight the token and display QuickInfo
        /// </summary>
        private void OnTextViewMouseHover(object sender, MouseHoverEventArgs e) {
            if (_textView == null) {
                return;
            }

            string thisFile = VerilogLanguage.VerilogGlobals.GetDocumentPath(_textView.TextSnapshot);
            if (!string.IsNullOrEmpty(thisFile)) {
                try {
                    if (VerilogGlobals.ParseStatusController.NeedReparse(thisFile)) // ensure the dictionary item exists for the ParseStatus of this file and check if it is time to reparse
                    {
                        if (_subjectBuffers != null && _subjectBuffers.Count == 1) {
                            VerilogLanguage.VerilogGlobals.Reparse(_subjectBuffers[0], thisFile);
                        }
                        else {
                            // how do we end up with multiple buffers?
                            // TODO - handle this?
                        }
                    }
                }
                catch (Exception ex) {
                    System.Diagnostics.Debug.WriteLine("TemplateQuickInfoController reparse check failed: " + ex.Message);
                }
            }

            SnapshotPoint? point = GetMousePosition(new SnapshotPoint(_textView.TextSnapshot, e.Position));
            if (!point.HasValue) {
                return;
            }

            ITrackingPoint triggerPoint = point.Value.Snapshot.CreateTrackingPoint(
                point.Value.Position,
                PointTrackingMode.Positive);

            if (!_componentContext.QuickInfoBroker.IsQuickInfoActive(_textView)) {
                Task quickInfoTask = _componentContext.QuickInfoBroker.TriggerQuickInfoAsync(_textView, triggerPoint);
                _ = quickInfoTask.ContinueWith(
                    task => System.Diagnostics.Debug.WriteLine("TriggerQuickInfoAsync failed: " + task.Exception.GetBaseException().Message),
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted,
                    TaskScheduler.Default);
            }
        }

        #endregion

        #region Private Implementation

        /// <summary>
        /// get mouse location onscreen. Used to determine what word the cursor is currently hovering over
        /// </summary>
        private SnapshotPoint? GetMousePosition(SnapshotPoint topPosition) {
            // Map this point down to the appropriate subject buffer.

            return _textView.BufferGraph.MapDownToFirstMatch
                (
                topPosition,
                PointTrackingMode.Positive,
                snapshot => _subjectBuffers.Contains(snapshot.TextBuffer),
                PositionAffinity.Predecessor
                );
        }

        #endregion
    }

    #endregion
}
