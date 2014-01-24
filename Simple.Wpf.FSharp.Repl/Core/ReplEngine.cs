﻿namespace Simple.Wpf.FSharp.Repl.Core
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reactive;
    using System.Reactive.Concurrency;
    using System.Reactive.Disposables;
    using System.Reactive.Linq;
    using System.Reactive.Subjects;
    using System.Threading;
    using ICSharpCode.SharpZipLib.Zip;
    using Properties;
    using Services;

    /// <summary>
    /// Wrapper around the F# Interactive process.
    /// </summary>
    public sealed class ReplEngine : IReplEngine, IDisposable
    {
        /// <summary>
        /// REPL engine quit line for the F# Interactive process.
        /// </summary>
        public const string QuitLine = "#quit;;";

        /// <summary>
        /// REPL engine line termination characters.
        /// </summary>
        public const string LineTermination = ";;";

        private const string Executable32Bit = @"fsi.exe";
        private const string ExecutableAnyCpu = @"fsiAnyCpu.exe";
        private const string AwaitingInput = "> ";

        private const string BaseDirectory = @".simple.wpf.fsharp.repl";
        private const string FSharpDirectory = @"fsharp";
        private const string ZipFilename = @"fsharp.zip";

        private const string WorkingDirectoryOutput = "Working folder = \"{0}\"";

        private readonly string _workingDirectory;

        private readonly IProcessService _processService;
        private readonly IScheduler _scheduler;
        private readonly bool _anyCpu;
        private readonly Subject<ReplProcessOutput> _outputStream;
        private readonly BehaviorSubject<State> _stateStream;
        private readonly CompositeDisposable _disposable;

        private string _startupScript;
        private ReplProcess _replProcess;

        internal sealed class ReplProcess : IDisposable
        {
            private readonly IProcess _process;
            private readonly IDisposable _disposable;

            public ReplProcess(IProcess process, IDisposable disposable)
            {
                _process = process;
                _disposable = disposable;
            }

            public void Dispose()
            {
                _process.WriteStandardInput(QuitLine);
                _process.WaitForExit();
                
                _disposable.Dispose();
                _process.Dispose();
            }

            public void WriteLine(string script)
            {
                _process.WriteStandardInput(script);
            }
        }

        internal sealed class ReplProcessOutput
        {
            public string Output { get; private set; }

            public bool IsError { get; private set; }

            public ReplProcessOutput(string output, bool isError = false)
            {
                Output = output;
                IsError = isError;
            }
        }

        /// <summary>
        /// Creates an instance of the REPL engine with the specified parameters.
        /// </summary>
        /// <param name="workingDirectory">The working directory for the F# Interactive process.</param>
        /// <param name="processService">Handles creating windows processes.</param>
        /// <param name="scheduler">The Reactive scheduler for the REPL engine, defaults to the task pool scheduler.</param>
        /// <param name="anyCpu">Flag indicating whether to run as 32bit (false) or to determine at runtime (true).</param>
        public ReplEngine(string workingDirectory = null, IProcessService processService = null, IScheduler scheduler = null, bool anyCpu = true)
        {
            _scheduler = scheduler;
            _anyCpu = anyCpu;
            _processService = processService ?? new ProcessService();
            _scheduler = scheduler ?? TaskPoolScheduler.Default;

            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                _workingDirectory = workingDirectory.Trim();
                var directoryInfo = new DirectoryInfo(_workingDirectory);

                if (!directoryInfo.Exists)
                {
                    directoryInfo.Create();
                }
            }
            else
            {
                _workingDirectory = Path.GetTempPath();
            }

            _stateStream = new BehaviorSubject<State>(Core.State.Unknown);
            _outputStream = new Subject<ReplProcessOutput>();

            _disposable = new CompositeDisposable
            {
                _stateStream,
                _outputStream,
            };
        }

        /// <summary>
        /// REPL engine output as a Reactive extensions stream.
        /// </summary>
        public IObservable<string> Output { get { return _outputStream.Where(x => !x.IsError).Select(x => x.Output); } }

        /// <summary>
        /// REPL engine errors as a Reactive extensions stream.
        /// </summary>
        public IObservable<string> Error { get { return _outputStream.Where(x => x.IsError).Select(x => x.Output); } }

        /// <summary>
        /// REPL engine state changes as a Reactive extensions stream.
        /// </summary>
        public IObservable<State> State { get { return _stateStream.DistinctUntilChanged(); } }

        /// <summary>
        /// REPL engine working directory as a Reactive extensions stream.
        /// </summary>
        public string WorkingDirectory { get { return _workingDirectory; } }

        /// <summary>
        /// Starts the REPL engine.
        /// </summary>
        /// <param name="script">The script to run at startup.</param>
        /// <returns>Returns the REPL engine.</returns>
        public IReplEngine Start(string script = null)
        {
            var state = _stateStream.First();
            if (state != Core.State.Stopped && state != Core.State.Unknown && state != Core.State.Faulted)
            {
                return this;
            }

            _stateStream.OnNext(Core.State.Starting);

            _startupScript = script;
            _replProcess = StartProcess();

            return this;
        }

        /// <summary>
        /// Stops the REPL engine.
        /// </summary>
        /// <returns>Returns the REPL engine.</returns>
        public IReplEngine Stop()
        {
            var state = _stateStream.First();
            if (state == Core.State.Stopping || state == Core.State.Stopped)
            {
                return this;
            }

            _stateStream.OnNext(Core.State.Stopping);

            _replProcess.Dispose();

            _replProcess = null;
            _startupScript = null;

            _stateStream.OnNext(Core.State.Stopped);
            
            return this;
        }

        /// <summary>
        /// Reset the REPL engine, if it has already been started.
        /// </summary>
        /// <returns>Returns the REPL engine.</returns>
        public IReplEngine Reset()
        {
            var state = _stateStream.First();
            if (state == Core.State.Stopping || state == Core.State.Stopped)
            {
                return this;
            }

            _stateStream.OnNext(Core.State.Stopping);

            _replProcess.Dispose();

            _stateStream.OnNext(Core.State.Stopped);
            _stateStream.OnNext(Core.State.Starting);

            _replProcess = StartProcess();

            return this;
        }

        /// <summary>
        /// Executes a scripts, if the REPL engine has been started.
        /// </summary>
        /// <param name="script">The script to be executed.</param>
        /// <returns>Returns the REPL engine.</returns>
        public IReplEngine Execute(string script)
        {
            var state = _stateStream.First();
            if (state != Core.State.Running && state != Core.State.Executing)
            {
                return this;
            }

            if (script.EndsWith(LineTermination))
            {
                _stateStream.OnNext(Core.State.Executing);
            }

            _replProcess.WriteLine(script);

            return this;
        }

        /// <summary>
        /// Disposes the REPL engine, if it's been started then it will be stopped.
        /// </summary>
        public void Dispose()
        {
            Stop();

            _disposable.Dispose();
        }

        private ReplProcess StartProcess()
        {
            var process = CreateProcess(); 
            var tokenSource = new CancellationTokenSource();

            var disposable = Observable.Create<Unit>(o =>
            {
                process.Start();

                o.OnNext(Unit.Default);
                return Disposable.Empty;
            })
            .Select(_ => ObserveStandardErrors(process, tokenSource.Token))
            .Select(_ => ObserveStandardOutput(process, tokenSource.Token))
            .Subscribe(_ => { }, e => _stateStream.OnNext(Core.State.Faulted));

            return new ReplProcess(process, Disposable.Create(() =>
            {
                tokenSource.Cancel();
                tokenSource.Dispose();

                disposable.Dispose();
            }));
        }

        private IObservable<Unit> ObserveStandardOutput(IProcess process, CancellationToken cancellationToken)
        {
            return Observable.Start(() =>
            {
                _outputStream.OnNext(new ReplProcessOutput(string.Format(WorkingDirectoryOutput, _workingDirectory)));
                _outputStream.OnNext(new ReplProcessOutput(Environment.NewLine));

                while (!cancellationToken.IsCancellationRequested)
                {
                    var output = string.Empty;

                    try
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            var readTask = process.StandardOutputReadAsync(cancellationToken);
                            readTask.Wait(cancellationToken);

                            output += (char) readTask.Result;

                            if (output == AwaitingInput)
                            {
                                _outputStream.OnNext(new ReplProcessOutput(output));

                                if (_stateStream.First() == Core.State.Starting && !string.IsNullOrEmpty(_startupScript))
                                {
                                    _outputStream.OnNext(new ReplProcessOutput(_startupScript));
                                    _outputStream.OnNext(new ReplProcessOutput(Environment.NewLine));

                                    _stateStream.OnNext(Core.State.Executing);
                                    _replProcess.WriteLine(_startupScript);
                                }
                                else
                                {
                                    _stateStream.OnNext(Core.State.Running);
                                }

                                break;
                            }

                            if (output.EndsWith(Environment.NewLine))
                            {
                                _outputStream.OnNext(new ReplProcessOutput(output));
                                break;
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
            }, _scheduler);
        }

        private IObservable<Unit> ObserveStandardErrors(IProcess process, CancellationToken cancellationToken)
        {
            return Observable.Start(() =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var error = string.Empty;

                    try
                    {
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            var readTask = process.StandardErrorReadAsync(cancellationToken);
                            readTask.Wait(cancellationToken);

                            error += (char) readTask.Result;

                            if (error.EndsWith(Environment.NewLine))
                            {
                                _outputStream.OnNext(new ReplProcessOutput(error, true));
                                break;
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
            }, _scheduler);
        }

        private static void ExtractFSharpBinaries()
        {
            var tempDirectory = Path.GetTempPath();
            var baseDirectory = Path.Combine(tempDirectory, BaseDirectory);
            var binaryDirectory = Path.Combine(baseDirectory, FSharpDirectory);

            if (Directory.Exists(binaryDirectory))
            {
                return;
            }

            var di = new DirectoryInfo(baseDirectory);
            if (!di.Exists)
            {
                di.Create();
            }

            di = new DirectoryInfo(binaryDirectory);
            if (!di.Exists)
            {
                di.Create();
            }

            var zipFilePath = Path.Combine(binaryDirectory, ZipFilename);
            using (var stream = File.Create(zipFilePath))
            {
                stream.Write(Resources.FSharp, 0, Resources.FSharp.Length);
            }

            var zipper = new FastZip();
            zipper.ExtractZip(zipFilePath, binaryDirectory, FastZip.Overwrite.Always, null, null, null, true);

            File.Delete(zipFilePath);
        }

        private string GetExecutablePath()
        {
            ExtractFSharpBinaries();

            var tempDirectory = Path.GetTempPath();
            var baseDirectory = Path.Combine(tempDirectory, BaseDirectory);
            var binaryDirectory = Path.Combine(baseDirectory, FSharpDirectory);

            var execute = _anyCpu ? ExecutableAnyCpu : Executable32Bit;
            return Path.Combine(binaryDirectory, execute);
        }

        private IProcess CreateProcess()
        {
            var executablePath = GetExecutablePath();
            var process = _processService.StartReplExecutable(_workingDirectory, executablePath);

            Debug.WriteLine("Working directory - " + _workingDirectory);
            Debug.WriteLine("File name - " + executablePath);

            return process;
        }
    }
}