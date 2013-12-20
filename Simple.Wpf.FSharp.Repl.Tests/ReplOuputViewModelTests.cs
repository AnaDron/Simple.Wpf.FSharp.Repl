﻿namespace Simple.Wpf.FSharp.Repl.Tests
{
    using NUnit.Framework;
    using ViewModels;

    [TestFixture]
    public class ReplOuputViewModelTests
    {
        [Test]
        public void is_errored()
        {
            // ARRANGE
            // ACT
            var viewModel = new ReplOuputViewModel("stdin(2,1): error FS0039: The value or constructor 'sssss' is not defined");

            // ASSERT
            Assert.That(viewModel.IsError, Is.True);
        }

        [Test]
        public void is_not_errored()
        {
            // ARRANGE
            // ACT
            var viewModel = new ReplOuputViewModel("val x : float = 23.0");

            // ASSERT
            Assert.That(viewModel.IsError, Is.False);
        }
    }
}
