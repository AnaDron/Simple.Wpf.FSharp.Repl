﻿namespace Simple.Wpf.FSharp.Repl.Controllers
{
    using UI.ViewModels;

    /// <summary>
    /// Controller for the REPL engine, exposes the ViewModel.
    /// </summary>
    public interface IReplEngineController
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