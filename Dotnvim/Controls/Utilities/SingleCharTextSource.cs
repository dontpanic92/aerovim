// <copyright file="SingleCharTextSource.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Controls.Utilities
{
    using System;
    using System.Runtime.InteropServices;
    using DWrite = Vortice.DirectWrite;

    /// <summary>
    /// The text source that holds a single char.
    /// </summary>
    public class SingleCharTextSource : SharpGen.Runtime.CallbackBase, DWrite.IDWriteTextAnalysisSource
    {
        private readonly DWrite.IDWriteFactory factory;
        private readonly int codePoint;
        private readonly string charString;
        private readonly string localeName;
        private GCHandle textHandle;
        private nint textPtr;
        private GCHandle localeHandle;
        private nint localePtr;

        /// <summary>
        /// Initializes a new instance of the <see cref="SingleCharTextSource"/> class.
        /// </summary>
        /// <param name="factory">The directwrite factory.</param>
        /// <param name="codePoint">The codepoint.</param>
        public SingleCharTextSource(DWrite.IDWriteFactory factory, int codePoint)
        {
            this.factory = factory;
            this.codePoint = codePoint;
            this.charString = char.ConvertFromUtf32(codePoint);
            this.localeName = System.Threading.Thread.CurrentThread.CurrentCulture.Name;
            this.textHandle = GCHandle.Alloc(this.charString, GCHandleType.Pinned);
            this.textPtr = this.textHandle.AddrOfPinnedObject();
            this.localeHandle = GCHandle.Alloc(this.localeName, GCHandleType.Pinned);
            this.localePtr = this.localeHandle.AddrOfPinnedObject();
        }

        /// <inheritdoc />
        public DWrite.ReadingDirection GetParagraphReadingDirection()
        {
            return DWrite.ReadingDirection.LeftToRight;
        }

        /// <inheritdoc />
        public unsafe uint GetTextAtPosition(uint textPosition, nint textString)
        {
            if (textPosition != 0)
            {
                *(nint*)textString = IntPtr.Zero;
                return 0;
            }

            *(nint*)textString = this.textPtr;
            return (uint)this.charString.Length;
        }

        /// <inheritdoc />
        public unsafe uint GetTextBeforePosition(uint textPosition, nint textString)
        {
            if (textPosition != 0)
            {
                *(nint*)textString = IntPtr.Zero;
                return 0;
            }

            *(nint*)textString = this.textPtr;
            return (uint)this.charString.Length;
        }

        /// <inheritdoc />
        public unsafe uint GetLocaleName(uint textPosition, nint localeName)
        {
            *(nint*)localeName = this.localePtr;
            return 1;
        }

        /// <inheritdoc />
        public void GetNumberSubstitution(uint textPosition, out uint textLength, out DWrite.IDWriteNumberSubstitution numberSubstitution)
        {
            textLength = 1;
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
