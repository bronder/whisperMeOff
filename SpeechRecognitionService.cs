using System.Speech.Recognition;
using NAudio.Wave;

namespace whisperMeOff;

public class SpeechRecognitionService : IDisposable
{
    private SpeechRecognitionEngine? _recognizer;
    private bool _isListening;
    private string _recognizedText = "";
    private bool _isCompleted;
    private int _deviceIndex;
    private CancellationTokenSource? _cts;

    public event EventHandler<string>? TextRecognized;
    public event EventHandler? ListeningStarted;
    public event EventHandler? ListeningStopped;

    public bool IsListening => _isListening;

    /// <summary>
    /// Gets the list of available audio input devices.
    /// </summary>
    public static List<(int index, string name)> GetAvailableDevices()
    {
        var devices = new List<(int, string)>();
        try
        {
            var waveInDevices = WaveIn.DeviceCount;
            for (int i = 0; i < waveInDevices; i++)
            {
                var capabilities = WaveIn.GetCapabilities(i);
                devices.Add((i, capabilities.ProductName));
            }
        }
        catch
        {
            // Return empty list if we can't enumerate devices
        }
        return devices;
    }

    public async Task<string> RecognizeSpeechAsync(int deviceIndex = -1, CancellationToken cancellationToken = default)
    {
        _recognizedText = "";
        _isCompleted = false;
        _isListening = true;
        _deviceIndex = deviceIndex;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            // Create a new speech recognition engine
            // Note: System.Speech uses the default audio input device configured in Windows
            // To use a specific device, set it as default in Windows Sound settings
            _recognizer = new SpeechRecognitionEngine();
            _recognizer.LoadGrammar(new DictationGrammar());
            _recognizer.SpeechRecognized += Recognizer_SpeechRecognized;
            _recognizer.RecognizeCompleted += Recognizer_RecognizeCompleted;
            _recognizer.AudioLevelUpdated += Recognizer_AudioLevelUpdated;
            _recognizer.SpeechHypothesized += Recognizer_SpeechHypothesized;

            System.Diagnostics.Debug.WriteLine("[Speech] Starting recognition...");
            ListeningStarted?.Invoke(this, EventArgs.Empty);

            // Use default audio device (Windows handles device selection)
            _recognizer.SetInputToDefaultAudioDevice();

            // Start asynchronous recognition
            _recognizer.RecognizeAsync(RecognizeMode.Multiple);

            // Wait for recognition to complete or cancellation
            try
            {
                while (!_isCompleted && !_cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(100, _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Cancellation is expected
            }

            // Return whatever text we have (may be empty if nothing was recognized)
            var result = _recognizedText.Trim();
            System.Diagnostics.Debug.WriteLine($"[Speech] Returning: '{result}', IsCompleted={_isCompleted}, Cancelled={_cts.Token.IsCancellationRequested}");
            return result;
        }
        catch (OperationCanceledException)
        {
            return _recognizedText.Trim();
        }
        catch (Exception ex)
        {
            return $"Speech recognition error: {ex.Message}";
        }
        finally
        {
            _isListening = false;
            ListeningStopped?.Invoke(this, EventArgs.Empty);
            Cleanup();
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void Recognizer_SpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
    {
        // Lower threshold to 0.3 to capture more speech
        if (e.Result != null && e.Result.Confidence > 0.3)
        {
            _recognizedText += e.Result.Text + " ";
            System.Diagnostics.Debug.WriteLine($"[Speech] Recognized: '{e.Result.Text}' (confidence: {e.Result.Confidence})");
            TextRecognized?.Invoke(this, e.Result.Text);
        }
    }

    private void Recognizer_RecognizeCompleted(object? sender, RecognizeCompletedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[Speech] RecognizeCompleted, Error={e.Error?.Message}");
        _isCompleted = true;
    }

    private void Recognizer_AudioLevelUpdated(object? sender, AudioLevelUpdatedEventArgs e)
    {
        // AudioLevel ranges from 0 to 100, -1 means no audio
        System.Diagnostics.Debug.WriteLine($"[Speech] Audio level: {e.AudioLevel}");
    }

    private void Recognizer_SpeechHypothesized(object? sender, SpeechHypothesizedEventArgs e)
    {
        // This fires while the user is speaking - shows partial results
        // Only keep the latest hypothesis, don't accumulate
        if (e.Result != null && e.Result.Confidence > 0)
        {
            _recognizedText = e.Result.Text + " ";
            System.Diagnostics.Debug.WriteLine($"[Speech] Hypothesized: '{e.Result.Text}' (confidence: {e.Result.Confidence})");
            TextRecognized?.Invoke(this, e.Result.Text);
        }
    }

    public void StopRecognition()
    {
        try
        {
            _recognizer?.RecognizeAsyncCancel();
            _cts?.Cancel();
            _isCompleted = true;
        }
        catch
        {
            // Ignore errors during stop
        }
    }

    private void Cleanup()
    {
        if (_recognizer != null)
        {
            _recognizer.SpeechRecognized -= Recognizer_SpeechRecognized;
            _recognizer.RecognizeCompleted -= Recognizer_RecognizeCompleted;
            _recognizer.Dispose();
            _recognizer = null;
        }
    }

    public void Dispose()
    {
        Cleanup();
    }
}
