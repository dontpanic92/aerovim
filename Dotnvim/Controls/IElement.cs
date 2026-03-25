// <copyright file="IElement.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Controls
{
    using System;
    using System.Drawing;
    using System.Numerics;
    using Dotnvim.Events;
    using D2D = Vortice.Direct2D1;
    using D3D11 = Vortice.Direct3D11;

    /// <summary>
    /// The base interface for all visual element.
    /// </summary>
    public interface IElement : IDisposable
    {
        /// <summary>
        /// Gets or sets the size of this element.
        /// </summary>
        SizeF Size { get; set; }

        /// <summary>
        /// Gets or sets the position of this element.
        /// </summary>
        Vector2 Position { get; set; }

        /// <summary>
        /// Gets the Direct2D factory.
        /// </summary>
        D2D.ID2D1Factory1 Factory { get; }

        /// <summary>
        /// Gets the Direct2D device.
        /// </summary>
        D2D.ID2D1Device Device2D { get; }

        /// <summary>
        /// Gets the Direct2D device.
        /// </summary>
        D3D11.ID3D11Device Device { get; }

        /// <summary>
        /// Draw the element.
        /// </summary>
        /// <param name="deviceContext">The context.</param>
        void Draw(D2D.ID2D1DeviceContext deviceContext);

        /// <summary>
        /// Request to redraw the control.
        /// </summary>
        /// <param name="control">The control to be invalidated.</param>
        void Invalidate(IElement control);

        /// <summary>
        /// Calculate the layout.
        /// </summary>
        void Layout();

        /// <summary>
        /// Test whether the point is inside the element.
        /// </summary>
        /// <param name="point">The coord.</param>
        /// <returns>true if the point is inside the element, otherwise false.</returns>
        bool HitTest(Vector2 point);

        /// <summary>
        /// Mouse is moving.
        /// </summary>
        /// <param name="e">event args.</param>
        void OnMouseMove(MouseEvent e);

        /// <summary>
        /// Mouse entered the element boundary.
        /// </summary>
        /// <param name="e">event args.</param>
        void OnMouseEnter(MouseEvent e);

        /// <summary>
        /// Mouse left the element boundary.
        /// </summary>
        /// <param name="e">event args.</param>
        void OnMouseLeave(MouseEvent e);

        /// <summary>
        /// Mouse clicked the element.
        /// </summary>
        /// <param name="e">event args.</param>
        void OnMouseClick(MouseEvent e);
    }
}
