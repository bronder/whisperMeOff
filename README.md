# whisperMeOff

A modern desktop chat application for local AI models. Connect to Ollama, LM Studio, Jan AI, or any OpenAI-compatible API server to chat with AI models running locally on your machine.

![whisperMeOff Screenshot](https://via.placeholder.com/800x450?text=whisperMeOff+Screenshot)

## Features

- 🔌 **Multiple Server Support** - Connect to Ollama, LM Studio, Jan AI, or any OpenAI-compatible server
- 💬 **Modern Chat Interface** - Clean, responsive UI with copyable messages
- ⚙️ **Easy Configuration** - Simple settings dialog to configure server URL, model, and API key
- 🔍 **Model Discovery** - Auto-detect available models from your server
- ⌨️ **Keyboard Shortcuts** - Zoom in/out with Ctrl++ / Ctrl+-
- 💾 **Persistent Settings** - Remembers your configuration between sessions

## Requirements

- Windows 10 or later
- .NET 10.0 Runtime
- A local AI server (Ollama, LM Studio, Jan AI, or similar)

## Installation

### From Source

1. Clone the repository:
   ```bash
   git clone https://github.com/bronder/whisperMeOff.git
   cd whisperMeOff
   ```

2. Build the project:
   ```bash
   dotnet build
   ```

3. Run the application:
   ```bash
   dotnet run
   ```

### Pre-built

Download the latest release from the [Releases](https://github.com/bronder/whisperMeOff/releases) page.

## Configuration

### Ollama

1. Ensure Ollama is running locally
2. Open Settings (File > Settings)
3. Select "Ollama" from the Server Profile dropdown
4. The default URL should be `http://localhost:11434`
5. Enter your model name (e.g., `llama3`, `mistral`)
6. Click "Test Connection" to verify
7. Click "Save"

### LM Studio

1. Ensure LM Studio is running
2. Open Settings
3. Select "LM Studio" from Server Profile
4. The default URL is `http://localhost:1234/v1`
5. Enter your model name
6. Test and save

### Jan AI

1. Ensure Jan is running
2. Open Settings
3. Select "Jan AI" from Server Profile (or enter `http://localhost:1337/v1`)
4. Enter your API key (if required - check Jan settings)
5. Enter your model name
6. Test and save

## Usage

1. Launch the application
2. Configure your AI server in Settings
3. Type your message in the input box
4. Press Enter or click Send
5. Copy text from chat bubbles by selecting and pressing Ctrl+C

### Keyboard Shortcuts

- `Ctrl++` or `Ctrl+=` - Zoom in
- `Ctrl+-` - Zoom out
- `Enter` - Send message
- `Shift+Enter` - New line in message

## Building for Release

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

This will create a self-contained executable in `bin/Release/net10.0-windows/win-x64/publish/`.

## Technologies

- C# / .NET 10
- WPF (Windows Presentation Foundation)
- MaterialDesign Themes

## License

MIT License - see [LICENSE](LICENSE) for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
