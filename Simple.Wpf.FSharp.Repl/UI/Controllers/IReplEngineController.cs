﻿namespace Simple.Wpf.FSharp.Repl.UI.Controllers
{
    using System;
    using ViewModels;

    /// <summary>
    /// Controller for the REPL engine, exposes the ViewModel.
    /// </summary>
    public interface IReplEngineController : IDisposable
    {
        /// <summary>
        /// The ViewModel for the REPL engine.
        /// </summary>
        IReplEngineViewModel ViewModel { get; }

        /// <summary>
        /// Execute the script
        /// </summary>
        /// <param name="script">The script to execute.</param>
        void Execute(string script);
    }
}