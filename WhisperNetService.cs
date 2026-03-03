using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.LibraryLoader;

namespace whisperMeOff;

/// <summary>
/// Helper service that wraps Whisper.net model loading and provides
/// transcription and built-in translation support.
/// </summary>
public class WhisperNetService : IDisposable
{
    private readonly string _modelFile;
    private WhisperFactory? _factory;

    public WhisperNetService(string modelFile = "ggml-small.bin")
    {
        _modelFile = modelFile;
    }

    public void EnsureFactoryLoaded()
    {
        if (_factory != null) return;

        if (!File.Exists(_modelFile))
            throw new FileNotFoundException("Whisper model not found.", _modelFile);

        // Set runtime order: try Vulkan first (GPU), then CUDA, then CPU
        RuntimeOptions.RuntimeLibraryOrder = [RuntimeLibrary.Vulkan, RuntimeLibrary.Cuda, RuntimeLibrary.Cuda12, RuntimeLibrary.Cpu];
        System.Diagnostics.Debug.WriteLine("[Whisper] ========== MODEL LOADING ==========");
        System.Diagnostics.Debug.WriteLine($"[Whisper] Model file: {Path.GetFullPath(_modelFile)}");
        System.Diagnostics.Debug.WriteLine($"[Whisper] Model size: {new FileInfo(_modelFile).Length / 1024 / 1024} MB");
        System.Diagnostics.Debug.WriteLine("[Whisper] Runtime order: Vulkan -> CUDA -> CPU");
        
        // Add logger to capture Whisper.net library loading messages
        using var whisperLogger = Whisper.net.Logger.LogProvider.AddLogger((level, message) =>
        {
            System.Diagnostics.Debug.WriteLine($"[Whisper Lib] {level}: {message}");
        });

        _factory = WhisperFactory.FromPath(_modelFile);
        System.Diagnostics.Debug.WriteLine("[Whisper] Factory loaded successfully");
    }

    /// <summary>
    /// Transcribe or translate the provided WAV stream using Whisper.net.
    /// Uses the library's built-in translation when <paramref name="translate"/> is true.
    /// </summary>
    public async Task<string> ProcessAudioAsync(Stream wavStream, bool translate = false, string? targetLanguage = null, CancellationToken cancellationToken = default)
    {
        EnsureFactoryLoaded();

        // Build processor with options
        var builder = _factory!.CreateBuilder()
            .WithLanguage("auto");

        if (translate)
        {
            // Use the built-in translate option. Whisper.net exposes a parameterless
            // WithTranslate() option in some versions which enables translation.
            builder = builder.WithTranslate();
            // Note: not all Whisper.net versions support specifying a target language
            // via the builder API; if available in the runtime used, this can be
            // extended to pass the target language.
        }

        using var processor = builder.Build();

        var results = new List<string>();
        await foreach (var r in processor.ProcessAsync(wavStream, cancellationToken))
        {
            if (!string.IsNullOrWhiteSpace(r.Text))
            {
                results.Add(r.Text);
            }
        }

        return string.Join(" ", results).Trim();
    }

    public void Dispose()
    {
        _factory?.Dispose();
        _factory = null;
    }
}
