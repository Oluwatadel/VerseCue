using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Versecue.Application.Interfaces;
using Versecue.Application.UseCases;
using Versecue.Domain.Entities;
using Versecue.Domain.Enums;
using Versecue.Domain.ValueObjects;
using Versecue.Infrastructure.Stt;
using Microsoft.Extensions.DependencyInjection;
using Versecue.Infrastructure.Persistence.Import;

namespace Versecue.Wpf.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IAudioService _audioService;
    private readonly ISttService _sttService;
    private readonly ILlmService _llmService;
    private readonly IBibleRepository _bibleRepository;
    private readonly IPresentationService _presentationService;
    private readonly BibleReferenceDetectionService _detectionService;
    private readonly IServiceProvider _serviceProvider;

    // Properties backings
    private ObservableCollection<AudioDevice> _mics = [];
    private AudioDevice? _selectedMic;
    private ObservableCollection<BibleTranslation> _translations = [];
    private BibleTranslation? _selectedTranslation;
    private ObservableCollection<Display> _displays = [];
    private Display? _selectedDisplay;
    private string _liveTranscript = "";
    private ObservableCollection<DetectedReferenceCardViewModel> _detectedCards = [];
    private string _searchQuery = "";
    private ObservableCollection<SearchResultViewModel> _searchResults = [];
    private string _statusText = "Ready";
    private bool _isSessionActive;
    private bool _isSessionPaused;
    private string _speechSimulationText = "";
    private double _volumeLevel;
    private ObservableCollection<double> _equalizerBands = new([0, 0, 0, 0, 0, 0, 0, 0]);
    private readonly Random _rand = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel(
        IAudioService audioService,
        ISttService sttService,
        ILlmService llmService,
        IBibleRepository bibleRepository,
        IPresentationService presentationService,
        BibleReferenceDetectionService detectionService,
        IServiceProvider serviceProvider)
    {
        _audioService = audioService;
        _sttService = sttService;
        _llmService = llmService;
        _bibleRepository = bibleRepository;
        _presentationService = presentationService;
        _detectionService = detectionService;
        _serviceProvider = serviceProvider;

        // Commands
        StartSessionCommand = new RelayCommand(async _ => await StartSessionAsync(), _ => !IsSessionActive);
        StopSessionCommand = new RelayCommand(async _ => await StopSessionAsync(), _ => IsSessionActive);
        PauseSessionCommand = new RelayCommand(PauseSession, _ => IsSessionActive && !IsSessionPaused);
        ResumeSessionCommand = new RelayCommand(ResumeSession, _ => IsSessionActive && IsSessionPaused);
        ClearDisplayCommand = new RelayCommand(async _ => await ClearDisplayAsync());
        InjectSimulatedSpeechCommand = new RelayCommand(_ => InjectSimulatedSpeech(), _ => IsSessionActive && !IsSessionPaused && !string.IsNullOrWhiteSpace(SpeechSimulationText));
        SearchCommand = new RelayCommand(async _ => await SearchBibleAsync());
        ImportTranslationCommand = new RelayCommand(async _ => await ImportTranslationAsync());

        // Event hooks
        _sttService.TranscriptReceived += OnTranscriptReceived;
        _audioService.AudioDataAvailable += OnAudioDataAvailable;

        // Initialization
        InitializeAsync();
    }

    // Bindable collections and values
    public ObservableCollection<AudioDevice> Mics { get => _mics; set => Set(ref _mics, value); }
    public AudioDevice? SelectedMic { get => _selectedMic; set => Set(ref _selectedMic, value); }
    public ObservableCollection<BibleTranslation> Translations { get => _translations; set => Set(ref _translations, value); }
    public BibleTranslation? SelectedTranslation { get => _selectedTranslation; set => Set(ref _selectedTranslation, value); }
    public ObservableCollection<Display> Displays { get => _displays; set => Set(ref _displays, value); }
    public Display? SelectedDisplay { get => _selectedDisplay; set => Set(ref _selectedDisplay, value); }
    public string LiveTranscript { get => _liveTranscript; set => Set(ref _liveTranscript, value); }
    public ObservableCollection<DetectedReferenceCardViewModel> DetectedCards { get => _detectedCards; set => Set(ref _detectedCards, value); }
    public string SearchQuery { get => _searchQuery; set => Set(ref _searchQuery, value); }
    public ObservableCollection<SearchResultViewModel> SearchResults { get => _searchResults; set => Set(ref _searchResults, value); }
    public string StatusText { get => _statusText; set => Set(ref _statusText, value); }
    public bool IsSessionActive { get => _isSessionActive; set { Set(ref _isSessionActive, value); CommandManager.InvalidateRequerySuggested(); } }
    public bool IsSessionPaused { get => _isSessionPaused; set { Set(ref _isSessionPaused, value); CommandManager.InvalidateRequerySuggested(); } }
    public string SpeechSimulationText { get => _speechSimulationText; set => Set(ref _speechSimulationText, value); }
    public double VolumeLevel { get => _volumeLevel; set => Set(ref _volumeLevel, value); }
    public ObservableCollection<double> EqualizerBands { get => _equalizerBands; set => Set(ref _equalizerBands, value); }

    // Commands
    public ICommand StartSessionCommand { get; }
    public ICommand StopSessionCommand { get; }
    public ICommand PauseSessionCommand { get; }
    public ICommand ResumeSessionCommand { get; }
    public ICommand ClearDisplayCommand { get; }
    public ICommand InjectSimulatedSpeechCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand ImportTranslationCommand { get; }

    private async void InitializeAsync()
    {
        try
        {
            // Query devices
            var mics = await _audioService.GetInputDevicesAsync();
            Mics = new ObservableCollection<AudioDevice>(mics);
            SelectedMic = Mics.FirstOrDefault();

            // Query translations (mock one KJV translation for MVP database lookup)
            var activeTranslations = await _bibleRepository.GetActiveTranslationsAsync();
            Translations = new ObservableCollection<BibleTranslation>(activeTranslations);
            if (Translations.Count == 0)
            {
                // Seed a dummy translation if empty, to ensure UI binds
                var kjv = new BibleTranslation("KJV", "King James Version", "en", "Public Domain");
                Translations.Add(kjv);
            }
            SelectedTranslation = Translations.FirstOrDefault();

            // Query displays
            var displays = await _presentationService.GetDisplaysAsync();
            Displays = new ObservableCollection<Display>(displays);
            var secondaryDisp = displays.FirstOrDefault(d => !d.IsPrimary);
            SelectedDisplay = !string.IsNullOrEmpty(secondaryDisp.DeviceId) ? secondaryDisp : displays.FirstOrDefault();

            // Init models
            await _sttService.InitializeAsync("dummy-stt-model-path");
            await _llmService.InitializeAsync("dummy-llm-model-path");
        }
        catch (Exception ex)
        {
            StatusText = $"Initialization Error: {ex.Message}";
        }
    }

    private async Task StartSessionAsync()
    {
        if (SelectedMic == null)
        {
            MessageBox.Show("Please select a microphone first.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            LiveTranscript = "";
            DetectedCards.Clear();
            await _audioService.StartCaptureAsync(SelectedMic.Value);
            IsSessionActive = true;
            IsSessionPaused = false;
            StatusText = "Active (Listening)";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to start session: {ex.Message}";
        }
    }

    private async Task StopSessionAsync()
    {
        try
        {
            await _audioService.StopCaptureAsync();
            await _sttService.ResetAsync();
            IsSessionActive = false;
            IsSessionPaused = false;
            StatusText = "Ready";
            VolumeLevel = 0;
            for (int i = 0; i < EqualizerBands.Count; i++) EqualizerBands[i] = 0;
        }
        catch (Exception ex)
        {
            StatusText = $"Error stopping session: {ex.Message}";
        }
    }

    private void PauseSession(object? parameter)
    {
        _audioService.Pause();
        IsSessionPaused = true;
        StatusText = "Paused";
        VolumeLevel = 0;
        for (int i = 0; i < EqualizerBands.Count; i++) EqualizerBands[i] = 0;
    }

    private void ResumeSession(object? parameter)
    {
        _audioService.Resume();
        IsSessionPaused = false;
        StatusText = "Active (Listening)";
    }

    private async Task ClearDisplayAsync()
    {
        await _presentationService.HideAsync();
        StatusText = IsSessionActive ? "Active (Listening)" : "Ready";
    }

    private void InjectSimulatedSpeech()
    {
        if (_sttService is SttService service)
        {
            service.InjectSimulatedSpeech(SpeechSimulationText);
            SpeechSimulationText = "";
        }
    }

    private async Task SearchBibleAsync()
    {
        if (SelectedTranslation == null || string.IsNullOrWhiteSpace(SearchQuery)) return;

        try
        {
            var results = await _bibleRepository.SearchVersesAsync(SelectedTranslation.Id, SearchQuery);
            SearchResults.Clear();
            foreach (var res in results)
            {
                SearchResults.Add(new SearchResultViewModel(
                    res.Verse.Id,
                    $"{res.Book.Name} {res.Chapter.ChapterNumber}:{res.Verse.VerseNumber}",
                    res.Verse.Text,
                    new RelayCommand(async _ => await DisplayPassageAsync(res.Book.Name, res.Chapter.ChapterNumber, res.Verse.VerseNumber, res.Verse.VerseNumber, res.Verse.Text))
                ));
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Search failed: {ex.Message}";
        }
    }

    private async Task ImportTranslationAsync()
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Structured Bible Files (*.json, *.xml)|*.json;*.xml|JSON Files (*.json)|*.json|XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
            Title = "Import Bible Translation File"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                var content = await File.ReadAllTextAsync(openFileDialog.FileName);
                var ext = Path.GetExtension(openFileDialog.FileName).ToLowerInvariant();

                using (var scope = _serviceProvider.CreateScope())
                {
                    var importService = scope.ServiceProvider.GetRequiredService<BibleImportService>();
                    if (ext == ".xml")
                    {
                        await importService.ImportFromXmlAsync(content);
                    }
                    else
                    {
                        await importService.ImportFromJsonAsync(content);
                    }
                }
                
                // Refresh translations list
                var activeTranslations = await _bibleRepository.GetActiveTranslationsAsync();
                Translations = new ObservableCollection<BibleTranslation>(activeTranslations);
                SelectedTranslation = Translations.FirstOrDefault(t => t.Name == SelectedTranslation?.Name) ?? Translations.FirstOrDefault();
                
                MessageBox.Show("Bible translation imported successfully!", "Import Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import translation: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void OnTranscriptReceived(object? sender, TranscriptReceivedEventArgs e)
    {
        if (SelectedTranslation == null) return;

        // Append to rolling transcript
        LiveTranscript += $" {e.Text}";

        try
        {
            // Run detection
            var sessionId = Guid.NewGuid();
            var detected = await _detectionService.DetectReferencesAsync(
                sessionId,
                Guid.NewGuid(),
                e.Text,
                SelectedTranslation.Id
            );

            // Add detected cards to the operator UI
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var det in detected)
                {
                    if (det.ResolvedReference.HasValue)
                    {
                        var reference = det.ResolvedReference.Value;
                        
                        // Load exact text asynchronously
                        Task.Run(async () =>
                        {
                            var text = await _bibleRepository.GetPassageTextAsync(reference, SelectedTranslation.Id);
                            var book = await _bibleRepository.GetBookByIdAsync(reference.BookId);

                            if (book != null && text != null)
                            {
                                var displayRef = reference.IsSingleVerse 
                                    ? $"{book.Name} {reference.ChapterNumber}:{reference.VerseStart}"
                                    : $"{book.Name} {reference.ChapterNumber}:{reference.VerseStart}-{reference.VerseEnd}";

                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                {
                                    DetectedCards.Insert(0, new DetectedReferenceCardViewModel(
                                        det.Id,
                                        displayRef,
                                        text,
                                        det.ConfidenceScore,
                                        det.DetectionSource.ToString(),
                                        new RelayCommand(async _ => await DisplayPassageAsync(book.Name, reference.ChapterNumber, reference.VerseStart ?? 1, reference.VerseEnd ?? 1, text)),
                                        new RelayCommand(card => DetectedCards.Remove((DetectedReferenceCardViewModel)card!))
                                    ));
                                });
                            }
                        });
                    }
                }
            });
        }
        catch (Exception ex)
        {
            StatusText = $"Detection failed: {ex.Message}";
        }
    }

    private async void OnAudioDataAvailable(object? sender, AudioDataAvailableEventArgs e)
    {
        // Route audio data chunks directly into the transcription engine
        await _sttService.WriteAudioChunkAsync(e.Data);

        // Equalizer & Mic Level Simulation
        if (IsSessionActive && !IsSessionPaused)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                VolumeLevel = _rand.Next(15, 80);
                for (int i = 0; i < EqualizerBands.Count; i++)
                {
                    EqualizerBands[i] = _rand.Next(5, 95);
                }
            });
        }
        else
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                VolumeLevel = 0;
                for (int i = 0; i < EqualizerBands.Count; i++)
                {
                    EqualizerBands[i] = 0;
                }
            });
        }
    }

    private async Task DisplayPassageAsync(string book, int chapter, int start, int end, string text)
    {
        if (SelectedDisplay == null) return;

        var reference = start == end ? $"{book} {chapter}:{start}" : $"{book} {chapter}:{start}-{end}";
        await _presentationService.ShowScriptureAsync(SelectedDisplay.Value, text, reference);
        StatusText = $"Displaying: {reference}";
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class DetectedReferenceCardViewModel
{
    public Guid Id { get; }
    public string Reference { get; }
    public string PassageText { get; }
    public double Confidence { get; }
    public string ConfidencePercent => $"{Math.Round(Confidence * 100)}%";
    public string Source { get; }
    public ICommand DisplayCommand { get; }
    public ICommand RejectCommand { get; }

    public DetectedReferenceCardViewModel(Guid id, string reference, string passageText, double confidence, string source, ICommand displayCommand, ICommand rejectCommand)
    {
        Id = id;
        Reference = reference;
        PassageText = passageText;
        Confidence = confidence;
        Source = source;
        DisplayCommand = displayCommand;
        RejectCommand = rejectCommand;
    }
}

public sealed class SearchResultViewModel
{
    public int VerseId { get; }
    public string Reference { get; }
    public string VerseText { get; }
    public ICommand DisplayCommand { get; }

    public SearchResultViewModel(int verseId, string reference, string verseText, ICommand displayCommand)
    {
        VerseId = verseId;
        Reference = reference;
        VerseText = verseText;
        DisplayCommand = displayCommand;
    }
}

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
    public void Execute(object? parameter) => _execute(parameter);
}
