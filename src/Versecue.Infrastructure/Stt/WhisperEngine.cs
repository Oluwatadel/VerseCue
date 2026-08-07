using Whisper.net;

namespace Versecue.Infrastructure.Stt
{
    public sealed class WhisperEngine : IDisposable
    {
        private WhisperFactory? _factory;

        private WhisperProcessor? _processor;

        private bool _initialized;

        private readonly SemaphoreSlim _lock = new(1, 1);

        private readonly WhisperOptions _options;

        public bool IsInitialized => _initialized;

        public WhisperEngine(
            WhisperOptions options)
        {
            _options = options;
        }

        public async Task InitializeAsync(
            CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (_initialized)
                    return;

                if (!File.Exists(_options.ModelPath))
                {
                    throw new FileNotFoundException(
                        "Whisper model not found.",
                        _options.ModelPath);
                }
                _factory = WhisperFactory.FromPath(_options.ModelPath);
                _processor = _factory
                    .CreateBuilder()
                    .WithLanguage(_options.Language)
                    .Build();
                _initialized = true;
            }
            finally
            {
                _lock.Release();
            }
        }

        public WhisperProcessor Processor
        {
            get
            {
                if(!IsInitialized)
                {
                    throw new InvalidOperationException("WhisperEngine is not initialized.");
                }
                // Return a new instance of WhisperProcessor
                return _processor!;
            }
        }

        public void Dispose()
        {
            _processor?.Dispose();
            _factory?.Dispose();

            _processor = null;
            _factory = null;

            _initialized = false;

            _lock.Dispose();
        }
    }
}
