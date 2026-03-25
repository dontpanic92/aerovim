// <copyright file="RowTextSource.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Controls.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using SharpDX;
    using static Dotnvim.NeovimClient.NeovimClient;
    using DWrite = SharpDX.DirectWrite;

    /// <summary>
    /// The text source stored a row.
    /// </summary>
    public class RowTextSource : DWrite.TextAnalysisSource
    {
        private readonly DWrite.Factory factory;
        private readonly List<string> text = new List<string>();
        private readonly List<int> codePoints = new List<int>();
        private readonly int row;
        private readonly int columnCount;
        private readonly string fullText;
        private readonly int[] charOffsets;

        /// <summary>
        /// Initializes a new instance of the <see cref="RowTextSource"/> class.
        /// </summary>
        /// <param name="factory">The directwrite factory.</param>
        /// <param name="screen">The cells.</param>
        /// <param name="row">Row index.</param>
        /// <param name="rangeStart">The start index of the range.</param>
        /// <param name="rangeEnd">the end index of the range.</param>
        public RowTextSource(DWrite.Factory factory, Cell[,] screen, int row, int rangeStart, int rangeEnd)
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
        }

        /// <summary>
        /// Gets the length.
        /// </summary>
        public int Length { get => this.text.Count; }

        /// <inheritdoc />
        public DWrite.ReadingDirection ReadingDirection => DWrite.ReadingDirection.LeftToRight;

        /// <inheritdoc />
        public IDisposable Shadow { get; set; }

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
        public int AddReference()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.Shadow?.Dispose();
        }

        /// <inheritdoc />
        public string GetLocaleName(int textPosition, out int textLength)
        {
            textLength = this.text.Count;
            return System.Threading.Thread.CurrentThread.CurrentCulture.Name;
        }

        /// <inheritdoc />
        public DWrite.NumberSubstitution GetNumberSubstitution(int textPosition, out int textLength)
        {
            textLength = this.text.Count;
            return new DWrite.NumberSubstitution(this.factory, DWrite.NumberSubstitutionMethod.None, null, true);
        }

        /// <inheritdoc />
        public string GetTextAtPosition(int textPosition)
        {
            if (textPosition >= this.charOffsets.Length)
            {
                return string.Empty;
            }

            return this.fullText.Substring(this.charOffsets[textPosition]);
        }

        /// <inheritdoc />
        public string GetTextBeforePosition(int textPosition)
        {
            int count = textPosition - 1;
            if (count <= 0)
            {
                return string.Empty;
            }

            int endOffset = count < this.charOffsets.Length
                ? this.charOffsets[count]
                : this.fullText.Length;
            return this.fullText.Substring(0, endOffset);
        }

        /// <inheritdoc />
        public Result QueryInterface(ref Guid guid, out IntPtr comObject)
        {
            comObject = IntPtr.Zero;
            return Result.False;
        }

        /// <inheritdoc />
        public int Release()
        {
            throw new NotImplementedException();
        }
    }
}
