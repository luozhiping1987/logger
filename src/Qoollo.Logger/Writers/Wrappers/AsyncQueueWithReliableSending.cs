﻿using Qoollo.Logger.Common;
using Qoollo.Logger.Configuration;
using Qoollo.Logger.Writers.Wrappers.Helpers.QueueAsyncProcessing;
using Qoollo.Logger.Writers.Wrappers.Helpers.TemporaryStore;
using System;
using System.Diagnostics.Contracts;
using System.Threading;

namespace Qoollo.Logger.Writers
{
    /// <summary>
    /// Обертка позволяющая в случае недоступности основного логгера
    /// вести запись логов во временное хранилище на диск, 
    /// от куда логи будут отправлены после восстановления логгером функционирования
    /// </summary>
    internal class AsyncQueueWithReliableSending: QueueAsyncProcessor<LoggingEvent>, ILoggingEventWriter
    {
        private static readonly Logger _thisClassSupportLogger = InnerSupportLogger.Instance.GetClassLogger(typeof(AsyncQueueWithReliableSending));

        private readonly ILoggingEventWriter _logger;

        private CancellationTokenSource _tokenSource;
        private readonly Thread _readerThread;
        private readonly System.IO.Stream _tempStoreLock;
        private readonly TemporaryStoreReader _reader;
        private readonly TimeSpan _sleepTime = TimeSpan.FromSeconds(10);
        private readonly TemporaryStoreWriter _writer;

        // Переполнена ли очередь
        private int _isOverflowed;

        // Граница переполнения
        private readonly int _borderOverflow;

        // Константы для замены bool на int для поодержки Interlock опереаций
        private const int IS_OVERFLOWED = 1;
        private const int IS_NOT_OVERFLOWED = 0;

        // Выбрасывать ли из очереди не влезающие сообщения
        private readonly bool _isDiscardExcess;

        private volatile bool _isDisposed = false;


        internal AsyncQueueWithReliableSending(AsyncReliableQueueWrapperConfiguration config, ILoggingEventWriter logger)
            : base(1, config.MaxQueueSize, "AsyncQueueWithReliableSending for logger", true)
        {
            Contract.Requires(config != null);
            Contract.Requires(logger != null);

            _logger = logger;

            // конфиги для обработки переполнения очереди
            _isOverflowed = IS_NOT_OVERFLOWED;
            _borderOverflow = Convert.ToInt32(config.MaxQueueSize * 0.5);
            _isDiscardExcess = config.IsDiscardExcess;

            string newFolderForTemporaryStore = TemporaryStoreLockFile.FindNotLockedDirectory(config.FolderForTemporaryStore, out _tempStoreLock);
            _reader = new TemporaryStoreReader(newFolderForTemporaryStore);
            _writer = new TemporaryStoreWriter(newFolderForTemporaryStore, config.MaxFileSize);
            _tokenSource = new CancellationTokenSource();

            _readerThread = new Thread(new ParameterizedThreadStart(TemporaryReaderLoop));
            _readerThread.IsBackground = true;
            _readerThread.Name = "AsyncQueueWithReliableSending (reader loop) for logger";

            _readerThread.Start(_tokenSource.Token);
            Start();
        }

        public AsyncQueueWithReliableSending(AsyncReliableQueueWrapperConfiguration config)
            : this(config, LoggerFactory.CreateWriter(config.InnerWriter))
        {
        }



        public void SetConverterFactory(LoggingEventConverters.ConverterFactory factory)
        {
            _logger.SetConverterFactory(factory);
        }


        private void TemporaryReaderLoop(object state)
        {
            var token = (CancellationToken)state;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var data = _reader.GetRecord();

                    if (data != null && _logger.Write(data))
                    {
                        _reader.RecordCompleted();
                    }
                    else
                    {
                        token.WaitHandle.WaitOne(_sleepTime);
                    }
                }
                catch (Exception ex)
                {
                    _thisClassSupportLogger.Error(ex, "Error while working with temporary reliable storage of logs");
                    throw;
                }
            }
        }

        protected override void Process(LoggingEvent data, object state, CancellationToken token)
        {
            if (!_logger.Write(data))
            {
                lock (_writer)
                {
                    _writer.Write(data);
                }
            }
        }

        public bool Write(LoggingEvent data)
        {
            if (_isDisposed)
            {
                _thisClassSupportLogger.Error("Attempt to write LoggingEvent in Disposed state");
                return false;
            }

            if (data.Level.Level.CompareTo(LogLevel.Error.Level) >= 0)
            {
                Process(data, null, _tokenSource.Token);
            }
            else
            {
                if (TryAdd(data))
                {
                    if (ElementCount < _borderOverflow
                        && Interlocked.CompareExchange(ref _isOverflowed, IS_NOT_OVERFLOWED, IS_OVERFLOWED) == IS_OVERFLOWED)
                    {
                        _thisClassSupportLogger.Info("Async queue now is not overflowed");
                    }
                }
                else
                {
                    if (Interlocked.CompareExchange(ref _isOverflowed, IS_OVERFLOWED, IS_NOT_OVERFLOWED) == IS_NOT_OVERFLOWED)
                    {
                        _thisClassSupportLogger.Info("Async queue overflow detected");
                    }

                    if (_isDiscardExcess)
                    {
                        return false;
                    }

                    Add(data);
                }
            }

            return true;
        }


        protected void Dispose(DisposeReason reason)
        {
            if (!_isDisposed)
            {
                _isDisposed = true;

                _tokenSource.Cancel();

                if (_readerThread != null)
                {
                    if (reason != DisposeReason.Finalize)
                        _readerThread.Join();
                }

                if (reason == DisposeReason.Dispose)
                    _logger.Dispose();
                else if (reason == DisposeReason.Close)
                    _logger.Close();

                _tempStoreLock.Dispose();
            }
        }

        public void Close()
        {
            this.Stop(true, true, false);
            Dispose(DisposeReason.Close);
        }

        /// <summary>
        /// Main clean-up code
        /// </summary>
        /// <param name="isUserCall">Is called by user. False - from finalizer</param>
        protected override void Dispose(bool isUserCall)
        {
            base.Dispose(isUserCall);
            if (isUserCall)
                this.Dispose(DisposeReason.Dispose);
            else
                this.Dispose(DisposeReason.Finalize);
        }
    }
}