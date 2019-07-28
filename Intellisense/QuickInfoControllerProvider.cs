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

using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using VerilogLanguage;

namespace VSLTK.Intellisense
{
    #region IIntellisenseControllerProvider

    [Export(typeof(IIntellisenseControllerProvider))]
    [Name("Template QuickInfo Controller")]
    [ContentType("verilog")]
    internal class TemplateQuickInfoControllerProvider : IIntellisenseControllerProvider
    {
        #region Asset Imports

        [Import]
        internal IQuickInfoBroker QuickInfoBroker { get; set; }

        #endregion

        #region IIntellisenseControllerFactory Members

        public IIntellisenseController TryCreateIntellisenseController(ITextView textView,
            IList<ITextBuffer> subjectBuffers)
        {
            VerilogGlobals.TheView = textView;
            return new TemplateQuickInfoController(textView, subjectBuffers, this);
        }

        #endregion

    }

    #endregion
}