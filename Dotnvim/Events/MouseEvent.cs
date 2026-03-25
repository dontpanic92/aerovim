// <copyright file="MouseEvent.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Events
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the mouse event.
    /// </summary>
    public class MouseEvent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MouseEvent"/> class.
        /// </summary>
        /// <param name="type">Event type.</param>
        /// <param name="point">Mouse position.</param>
        /// <param name="button">button clicked.</param>
        public MouseEvent(Type type, Vector2 point, Buttons button)
        {
            this.EventType = type;
            this.Point = point;
            this.Button = button;
        }

        /// <summary>
        /// The event type.
        /// </summary>
        public enum Type
        {
            /// <summary>
            /// Mouse Enter
            /// </summary>
            MouseEnter,

            /// <summary>
            /// Mouse Leave
            /// </summary>
            MouseLeave,

            /// <summary>
            /// Mouse Move
            /// </summary>
            MouseMove,

            /// <summary>
            /// Mouse Click
            /// </summary>
            MouseClick,
        }

        /// <summary>
        /// The button type.
        /// </summary>
        public enum Buttons
        {
            /// <summary>
            /// None
            /// </summary>
            None,

            /// <summary>
            /// Left
            /// </summary>
            Left,

            /// <summary>
            /// Right
            /// </summary>
            Right,
        }

        /// <summary>
        /// Gets the event type.
        /// </summary>
        public Type EventType { get; }

        /// <summary>
        /// Gets the mouse position.
        /// </summary>
        public Vector2 Point { get; }

        /// <summary>
        /// Gets the button clicked.
        /// </summary>
        public Buttons Button { get; }
    }
}
