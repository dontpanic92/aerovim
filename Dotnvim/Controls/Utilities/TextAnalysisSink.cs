// <copyright file="TextAnalysisSink.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Controls.Utilities
{
    using System;
    using System.Collections.Generic;
    using DWrite = Vortice.DirectWrite;

    /// <summary>
    /// Text analysis sink.
    /// </summary>
    public class TextAnalysisSink : SharpGen.Runtime.CallbackBase, DWrite.IDWriteTextAnalysisSink
    {
        /// <summary>
        /// Gets the script analyses.
        /// </summary>
        public List<(int CodePointStart, int CodePointLength, DWrite.ScriptAnalysis ScriptAnalysis)> ScriptAnalyses { get; }
            = new List<(int, int, DWrite.ScriptAnalysis)>();

        /// <inheritdoc />
        public void SetBidiLevel(uint textPosition, uint textLength, byte explicitLevel, byte resolvedLevel)
        {
        }

        /// <inheritdoc />
        public void SetLineBreakpoints(uint textPosition, DWrite.LineBreakpoint[] lineBreakpoints)
        {
        }

        /// <inheritdoc />
        public void SetNumberSubstitution(uint textPosition, uint textLength, DWrite.IDWriteNumberSubstitution numberSubstitution)
        {
        }

        /// <inheritdoc />
        public void SetScriptAnalysis(uint textPosition, uint textLength, DWrite.ScriptAnalysis scriptAnalysis)
        {
            this.ScriptAnalyses.Add(((int)textPosition, (int)textLength, scriptAnalysis));
        }
    }
}
