// <copyright file="RowTextSource.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Controls.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using static Dotnvim.NeovimClient.NeovimClient;
    using DWrite = Vortice.DirectWrite;

    /// <summary>
    /// The text source stored a row.
    /// </summary>
    public class RowTextSource : SharpGen.Runtime.CallbackBase, DWrite.IDWriteTextAnalysisSource
    {
        private readonly DWrite.IDWriteFactory factory;
        private readonly List<string> text = new List<string>();
        private readonly List<int> codePoints = new List<int>();
        private readonly int row;
        private readonly int columnCount;
        private readonly string fullText;
        private readonly int[] charOffsets;
        private readonly string localeName;
        private GCHandle textHandle;
        private nint textPtr;
        private GCHandle localeHandle;
        private nint localePtr;

        /// <summary>
        /// Initializes a new instance of the <see cref="RowTextSource"/> class.
        /// </summary>
        /// <param name="factory">The directwrite factory.</param>
        /// <param name="screen">The cells.</param>
        /// <param name="row">Row index.</param>
        /// <param name="rangeStart">The start index of the range.</param>
        /// <param name="rangeEnd">the end index of the range.</param>
        public RowTextSource(DWrite.IDWriteFactory factory, Cell[,] screen, int row, int rangeStart, int rangeEnd)
        {
            this.factory = factory;

            for (int j = rangeStart; j < rangeEnd; j++)
            {
                if (screen[row, j].Character != null)
                {
                    int codePoint = screen[row, j].Character.Value;
                    this.text.Add(char.ConvertFromUtf32(codePoint));
                    this.codePoints.Add(codePoint);
                }
            }

            this.charOffsets = new int[this.text.Count];
            int charOffset = 0;
            for (int k = 0; k < this.text.Count; k++)
            {
                this.charOffsets[k] = charOffset;
                charOffset += this.text[k].Length;
            }

            this.fullText = string.Join(string.Empty, this.text);

            this.row = row;
            this.columnCount = rangeEnd - rangeStart;

            this.localeName = System.Threading.Thread.CurrentThread.CurrentCulture.Name;
            this.textHandle = GCHandle.Alloc(this.fullText, GCHandleType.Pinned);
            this.textPtr = this.textHandle.AddrOfPinnedObject();
            this.localeHandle = GCHandle.Alloc(this.localeName, GCHandleType.Pinned);
            this.localePtr = this.localeHandle.AddrOfPinnedObject();
        }

        /// <summary>
        /// Gets the length.
        /// </summary>
        public int Length { get => this.text.Count; }

        /// <summary>
        /// Gets the full text string.
        /// </summary>
        public string FullText => this.fullText;

        /// <summary>
        /// Gets the code point in the specific index.
        /// </summary>
        /// <param name="index">Index.</param>
        /// <returns>The code point.</returns>
        public int GetCodePoint(int index)
        {
            return this.codePoints[index];
        }

        /// <summary>
        /// Gets a substring.
        /// </summary>
        /// <param name="codePointStart">Start index.</param>
        /// <param name="codePointLength">End index.</param>
        /// <returns>The substring.</returns>
        public string GetSubString(int codePointStart, int codePointLength)
        {
            int startOffset = this.charOffsets[codePointStart];
            int endOffset = codePointStart + codePointLength < this.charOffsets.Length
                ? this.charOffsets[codePointStart + codePointLength]
                : this.fullText.Length;
            return this.fullText.Substring(startOffset, endOffset - startOffset);
        }

        /// <inheritdoc />
        public DWrite.ReadingDirection GetParagraphReadingDirection()
        {
            return DWrite.ReadingDirection.LeftToRight;
        }

        /// <inheritdoc />
        public unsafe uint GetTextAtPosition(uint textPosition, nint textString)
        {
            if (textPosition >= this.charOffsets.Length)
            {
                *(nint*)textString = IntPtr.Zero;
                return 0;
            }

            int charOff = this.charOffsets[(int)textPosition];
            *(nint*)textString = this.textPtr + (charOff * sizeof(char));
            return (uint)(this.fullText.Length - charOff);
        }

        /// <inheritdoc />
        public unsafe uint GetTextBeforePosition(uint textPosition, nint textString)
        {
            int count = (int)textPosition - 1;
            if (count <= 0)
            {
                *(nint*)textString = IntPtr.Zero;
                return 0;
            }

            int endOffset = count < this.charOffsets.Length
                ? this.charOffsets[count]
                : this.fullText.Length;
            *(nint*)textString = this.textPtr;
            return (uint)endOffset;
        }

        /// <inheritdoc />
        public unsafe uint GetLocaleName(uint textPosition, nint localeName)
        {
            *(nint*)localeName = this.localePtr;
            return (uint)this.text.Count;
        }

        /// <inheritdoc />
        public void GetNumberSubstitution(uint textPosition, out uint textLength, out DWrite.IDWriteNumberSubstitution numberSubstitution)
        {
            textLength = (uint)this.text.Count;
            numberSubstitution = this.factory.CreateNumberSubstitution(DWrite.NumberSubstitutionMethod.None, null, true);
        }

        /// <inheritdoc />
        protected override void DisposeCore(bool disposing)
        {
            if (this.textHandle.IsAllocated)
            {
                this.textHandle.Free();
            }

            if (this.localeHandle.IsAllocated)
            {
                this.localeHandle.Free();
            }
        }
    }
}
