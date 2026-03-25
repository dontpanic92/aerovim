// <copyright file="EffectChain.cs">
// Copyright (c) dotnvim Developers. All rights reserved.
// Licensed under the GPLv2 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Dotnvim.Controls.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using D2D = Vortice.Direct2D1;

    /// <summary>
    /// Effect chain.
    /// </summary>
    public class EffectChain : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EffectChain"/> class.
        /// </summary>
        /// <param name="deviceContext">The device context.</param>
        public EffectChain(D2D.ID2D1DeviceContext deviceContext)
        {
            this.DeviceContext = deviceContext;
            this.CompositeEffect = new D2D.ID2D1Effect(deviceContext.CreateEffect(D2D.EffectGuids.Composite));
        }

        /// <summary>
        /// Gets the output.
        /// </summary>
        public virtual D2D.ID2D1Image Output => this.CompositeEffect.Output;

        /// <summary>
        /// Gets the effects.
        /// </summary>
        protected List<D2D.ID2D1Effect> Effects { get; } = new List<D2D.ID2D1Effect>();

        /// <summary>
        /// Gets the composite effect.
        /// </summary>
        protected D2D.ID2D1Effect CompositeEffect { get; }

        /// <summary>
        /// Gets the device context.
        /// </summary>
        protected D2D.ID2D1DeviceContext DeviceContext { get; }

        /// <summary>
        /// Push a new effect.
        /// </summary>
        /// <param name="guid">GUID of the effect.</param>
        /// <returns>EffectChain.</returns>
        public EffectChain PushEffect(Guid guid)
        {
            var effect = new D2D.ID2D1Effect(this.DeviceContext.CreateEffect(guid));
            if (this.Effects.Count != 0)
            {
                effect.SetInputEffect(0, this.Effects.Last(), false);
            }

            this.Effects.Add(effect);
            this.CompositeEffect.SetInputEffect(0, effect, false);
            return this;
        }

        /// <summary>
        /// Configure the last pushed effect.
        /// </summary>
        /// <param name="action">The config action callback.</param>
        /// <returns>EffectChain.</returns>
        public EffectChain SetupLast(Action<D2D.ID2D1Effect> action)
        {
            action?.Invoke(this.Effects.LastOrDefault());
            return this;
        }

        /// <summary>
        /// Set the composition mode.
        /// </summary>
        /// <param name="mode">The composition mode.</param>
        /// <returns>EffectChain.</returns>
        public EffectChain SetCompositionMode(D2D.CompositeMode mode)
        {
            this.CompositeEffect.SetValue((uint)D2D.CompositeProperties.Mode, mode);
            return this;
        }

        /// <summary>
        /// Returns whether there is any effect.
        /// </summary>
        /// <returns>true if there are any effects, otherwise false.</returns>
        public bool Any()
        {
            return this.Effects.Any();
        }

        /// <summary>
        /// Set the input.
        /// </summary>
        /// <param name="input">The input image.</param>
        public virtual void SetInput(D2D.ID2D1Image input)
        {
            this.Effects.FirstOrDefault()?.SetInput(0, input, false);
            this.CompositeEffect.SetInput(1, input, false);
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        /// <param name="disposing">Dispose is called manually.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var effect in this.Effects)
                {
                    effect.Dispose();
                }

                this.CompositeEffect.Dispose();
            }
        }
    }
}
