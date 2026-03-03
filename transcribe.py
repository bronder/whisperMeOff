import sys
import json

try:
    from faster_whisper import WhisperModel

    # Default to small model for speed, can be changed
    model_size = "small"
    if len(sys.argv) > 2:
        model_size = sys.argv[1]
    
    audio_file = sys.argv[2] if len(sys.argv) > 2 else sys.argv[1]
    
    # Load model (downloads if not cached)
    print(f"Loading model: {model_size}", file=sys.stderr)
    model = WhisperModel(model_size, device="cpu", compute_type="int8")
    
    # Transcribe
    print(f"Transcribing: {audio_file}", file=sys.stderr)
    segments, info = model.transcribe(audio_file, beam_size=5)
    
    # Collect all text
    text = " ".join([segment.text for segment in segments])
    
    # Output as JSON for easy parsing
    result = {"text": text.strip(), "language": info.language if hasattr(info, 'language') else "unknown"}
    print(json.dumps(result))
    
except Exception as e:
    print(json.dumps({"error": str(e)}), file=sys.stderr)
    sys.exit(1)
