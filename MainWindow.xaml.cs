using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DrawingColor = System.Drawing.Color;
using DrawingBrush = System.Drawing.Brushes;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingSolidBrush = System.Drawing.SolidBrush;
using DrawingPen = System.Drawing.Pen;
using Drawing2DSmoothingMode = System.Drawing.Drawing2D.SmoothingMode;

namespace whisperMeOff;

public class ChatMessage
{
    public string Sender { get; set; } = "";
    public string Message { get; set; } = "";
    public SolidColorBrush BackgroundColor { get; set; } = Brushes.White;
    public SolidColorBrush SenderColor { get; set; } = Brushes.Black;
    public SolidColorBrush TextColor { get; set; } = Brushes.Black;
    public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;
}

public partial class MainWindow : Window
{
    public static RoutedCommand ZoomInCommand = new RoutedCommand();
    public static RoutedCommand ZoomOutCommand = new RoutedCommand();
    
    private ObservableCollection<ChatMessage> _messages = new();
    private OllamaService _aiService = new();
    private List<(string sender, string message)> _conversationHistory = new();
    private bool _isConnected = false;
    private AppSettings _settings = new();

    public MainWindow()
    {
        InitializeComponent();
        
        // Set window icon
        Icon = CreateRobotIcon();
        
        // Load saved settings
        _settings = AppSettings.Load();
        
        // Configure AI service with selected profile
        var profile = _settings.Profiles[_settings.SelectedProfileIndex];
        _aiService.Configure(profile.ServerUrl, profile.Model, profile.ApiKey);
        
        // Restore window position and size
        if (!double.IsNaN(_settings.WindowLeft) && !double.IsNaN(_settings.WindowTop))
        {
            Left = _settings.WindowLeft;
            Top = _settings.WindowTop;
        }
        Width = _settings.WindowWidth;
        Height = _settings.WindowHeight;
        if (_settings.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }
        
        // Save window position on close
        Closing += (s, e) =>
        {
            if (WindowState == WindowState.Normal)
            {
                _settings.WindowLeft = Left;
                _settings.WindowTop = Top;
                _settings.WindowWidth = Width;
                _settings.WindowHeight = Height;
            }
            _settings.WindowMaximized = WindowState == WindowState.Maximized;
            _settings.Save();
        };
        
        // Bind commands to handlers
        CommandBindings.Add(new CommandBinding(ZoomInCommand, ZoomIn_Executed));
        CommandBindings.Add(new CommandBinding(ZoomOutCommand, ZoomOut_Executed));
        
        ChatMessages.ItemsSource = _messages;
        
        // Add welcome message
        AddMessage("AI", "Hello! I'm ready to chat.\n\nYour Ollama server is running on http://localhost:11434 with models:\n• qwen3:4b\n• karanchopda333/whisper:latest\n\nGo to File > Settings to configure or start chatting!", true);
        
        // Try to connect to saved settings
        _ = TryConnectToAI();
    }

    private async Task TryConnectToAI()
    {
        try
        {
            var models = await _aiService.GetAvailableModelsAsync();
            if (models.Count > 0)
            {
                _isConnected = true;
                AddMessage("AI", $"✓ Connected to AI server! Available models: {string.Join(", ", models)}", true);
            }
            else
            {
                AddMessage("AI", "⚠ Connected to server, but no models found. Load a model in Ollama and try again.", true);
            }
        }
        catch (Exception ex)
        {
            AddMessage("AI", $"✗ Could not connect to AI server: {ex.Message}", true);
        }
    }

    private void ZoomIn_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        ZoomIn_Click(sender, e);
    }

    private void ZoomOut_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        ZoomOut_Click(sender, e);
    }

    private void AddMessage(string sender, string message, bool isAI = false)
    {
        var chatMessage = new ChatMessage
        {
            Sender = sender,
            Message = message,
            BackgroundColor = isAI 
                ? new SolidColorBrush(Color.FromRgb(255, 255, 255)) 
                : new SolidColorBrush(Color.FromRgb(220, 235, 250)), // Very faint blue
            SenderColor = new SolidColorBrush(Color.FromRgb(0, 90, 158)), // Darker blue for sender name
            TextColor = Brushes.Black, // Black font
            HorizontalAlignment = isAI ? HorizontalAlignment.Left : HorizontalAlignment.Right
        };
        _messages.Add(chatMessage);
        ChatMessages.ScrollIntoView(chatMessage);
    }

    private void Send_Click(object sender, RoutedEventArgs e)
    {
        SendMessage();
    }

    private void MessageInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            SendMessage();
            e.Handled = true;
        }
    }

    private void SendMessage()
    {
        var userMessage = MessageInput.Text.Trim();
        if (string.IsNullOrEmpty(userMessage)) return;

        // Add user message
        AddMessage("You", userMessage, false);
        MessageInput.Clear();

        // Add to conversation history
        _conversationHistory.Add(("You", userMessage));

        // Call AI service
        Task.Run(async () =>
        {
            var aiResponse = await _aiService.SendMessageAsync(userMessage, _conversationHistory);
            
            Dispatcher.Invoke(() =>
            {
                AddMessage("AI", aiResponse, true);
                _conversationHistory.Add(("AI", aiResponse));
            });
        });
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        if (WindowScale.ScaleX < 2.0)
        {
            WindowScale.ScaleX += 0.1;
            WindowScale.ScaleY += 0.1;
        }
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        if (WindowScale.ScaleX > 0.5)
        {
            WindowScale.ScaleX -= 0.1;
            WindowScale.ScaleY -= 0.1;
        }
    }

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        // Show a simple settings dialog
        var settingsWindow = new Window
        {
            Title = "AI Settings",
            Width = 480,
            Height = 540,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(Color.FromRgb(250, 250, 250))
        };

        var mainPanel = new StackPanel { Margin = new Thickness(30, 25, 30, 25) };

        // Title
        var titleLabel = new TextBlock 
        { 
            Text = "Server Configuration", 
            FontSize = 18, 
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
            Margin = new Thickness(0, 0, 0, 20)
        };
        mainPanel.Children.Add(titleLabel);

        // Profile selector
        var profileLabel = new TextBlock { Text = "Server Profile", FontSize = 13, Margin = new Thickness(0, 0, 0, 6) };
        mainPanel.Children.Add(profileLabel);

        // Wrap ComboBox in border for rounded corners
        var profileComboBoxBorder = new Border 
        { 
            CornerRadius = new CornerRadius(6),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 18)
        };
        var profileComboBox = new ComboBox 
        { 
            IsEditable = true,
            Padding = new Thickness(12, 10, 12, 10),
            FontSize = 14,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };
        profileComboBoxBorder.Child = profileComboBox;
        foreach (var profile in _settings.Profiles)
        {
            profileComboBox.Items.Add(profile.Name);
        }
        profileComboBox.SelectedIndex = _settings.SelectedProfileIndex;
        mainPanel.Children.Add(profileComboBoxBorder);

        // Server URL
        var urlLabel = new TextBlock { Text = "Server URL", FontSize = 13, Margin = new Thickness(0, 0, 0, 6) };
        mainPanel.Children.Add(urlLabel);

        // Wrap TextBox in border for rounded corners
        var urlBorder = new Border 
        { 
            CornerRadius = new CornerRadius(6),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 18)
        };
        var urlTextBox = new TextBox 
        { 
            Padding = new Thickness(12, 10, 12, 10),
            FontSize = 14,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };
        urlBorder.Child = urlTextBox;
        mainPanel.Children.Add(urlBorder);

        // Model name
        var modelLabel = new TextBlock { Text = "Model Name", FontSize = 13, Margin = new Thickness(0, 0, 0, 6) };
        mainPanel.Children.Add(modelLabel);

        // Wrap ComboBox in border for model selection
        var modelBorder = new Border 
        { 
            CornerRadius = new CornerRadius(6),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 25)
        };
        var modelComboBox = new ComboBox 
        { 
            IsEditable = true,
            Padding = new Thickness(12, 10, 12, 10),
            FontSize = 14,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };
        modelBorder.Child = modelComboBox;
        mainPanel.Children.Add(modelBorder);

        // API Key (optional)
        var apiKeyLabel = new TextBlock { Text = "API Key (optional)", FontSize = 13, Margin = new Thickness(0, 0, 0, 6) };
        mainPanel.Children.Add(apiKeyLabel);

        // Wrap TextBox in border for rounded corners
        var apiKeyBorder = new Border 
        { 
            CornerRadius = new CornerRadius(6),
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 25)
        };
        var apiKeyTextBox = new TextBox 
        { 
            Padding = new Thickness(12, 10, 12, 10),
            FontSize = 14,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };
        apiKeyBorder.Child = apiKeyTextBox;
        mainPanel.Children.Add(apiKeyBorder);

        // Update fields when profile changes
        void UpdateFields()
        {
            var idx = profileComboBox.SelectedIndex;
            if (idx >= 0 && idx < _settings.Profiles.Count)
            {
                urlTextBox.Text = _settings.Profiles[idx].ServerUrl;
                modelComboBox.Text = _settings.Profiles[idx].Model;
                apiKeyTextBox.Text = _settings.Profiles[idx].ApiKey;
            }
        }
        
        profileComboBox.SelectionChanged += (s, args) => UpdateFields();
        UpdateFields(); // Initial load

        // Buttons
        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        
        // Blue primary button style with rounded corners (matching main window Send button)
        var primaryButtonStyle = new Style(typeof(Button));
        primaryButtonStyle.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0, 120, 212))));
        primaryButtonStyle.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
        primaryButtonStyle.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
        primaryButtonStyle.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(20, 12, 20, 12)));
        primaryButtonStyle.Setters.Add(new Setter(Button.FontSizeProperty, 14.0));
        primaryButtonStyle.Setters.Add(new Setter(Button.FontWeightProperty, FontWeights.SemiBold));
        primaryButtonStyle.Setters.Add(new Setter(Button.CursorProperty, Cursors.Hand));
        
        // Add rounded corner template (CornerRadius=8 to match main window)
        var primaryTemplate = new ControlTemplate(typeof(Button));
        var primaryBorder = new FrameworkElementFactory(typeof(Border));
        primaryBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        primaryBorder.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0, 120, 212)));
        primaryBorder.SetValue(Border.BorderThicknessProperty, new Thickness(0));
        var primaryContentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
        primaryContentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        primaryContentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        primaryBorder.AppendChild(primaryContentPresenter);
        primaryTemplate.VisualTree = primaryBorder;
        primaryButtonStyle.Setters.Add(new Setter(Button.TemplateProperty, primaryTemplate));
        
        var testButton = new Button { Content = "Test Connection", Style = primaryButtonStyle, Margin = new Thickness(0, 0, 12, 0), Height = 50, MinWidth = 120 };
        var saveButton = new Button { Content = "Save", Style = primaryButtonStyle, Margin = new Thickness(0, 0, 0, 0), Height = 50, MinWidth = 80 };
        
        // Gray cancel button style with rounded corners
        var cancelButtonStyle = new Style(typeof(Button));
        cancelButtonStyle.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromRgb(240, 240, 240))));
        cancelButtonStyle.Setters.Add(new Setter(Button.ForegroundProperty, new SolidColorBrush(Color.FromRgb(60, 60, 60))));
        cancelButtonStyle.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(1)));
        cancelButtonStyle.Setters.Add(new Setter(Button.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(200, 200, 200))));
        cancelButtonStyle.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(20, 12, 20, 12)));
        cancelButtonStyle.Setters.Add(new Setter(Button.FontSizeProperty, 14.0));
        cancelButtonStyle.Setters.Add(new Setter(Button.FontWeightProperty, FontWeights.SemiBold));
        cancelButtonStyle.Setters.Add(new Setter(Button.CursorProperty, Cursors.Hand));
        
        // Add rounded corner template (CornerRadius=8 to match main window)
        var cancelTemplate = new ControlTemplate(typeof(Button));
        var cancelBorder = new FrameworkElementFactory(typeof(Border));
        cancelBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        cancelBorder.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(240, 240, 240)));
        cancelBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        cancelBorder.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(200, 200, 200)));
        var cancelContentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
        cancelContentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        cancelContentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        cancelBorder.AppendChild(cancelContentPresenter);
        cancelTemplate.VisualTree = cancelBorder;
        cancelButtonStyle.Setters.Add(new Setter(Button.TemplateProperty, cancelTemplate));
        
        var cancelButton = new Button { Content = "Cancel", Style = cancelButtonStyle, Margin = new Thickness(12, 0, 0, 0), Height = 50, MinWidth = 80 };

        testButton.Click += async (s, args) =>
        {
            var testService = new OllamaService();
            testService.Configure(urlTextBox.Text, "", apiKeyTextBox.Text);
            var models = await testService.GetAvailableModelsAsync();
            if (models.Count > 0)
            {
                // Populate the model ComboBox with available models
                modelComboBox.Items.Clear();
                foreach (var model in models)
                {
                    modelComboBox.Items.Add(model);
                }
                
                // Select the first model if none selected
                if (modelComboBox.SelectedIndex < 0 && modelComboBox.Items.Count > 0)
                {
                    modelComboBox.SelectedIndex = 0;
                }
                
                MessageBox.Show($"✓ Connected! Found {models.Count} model(s). Select from the dropdown or type a custom model name.", "Connection Test", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("✗ Could not connect. Make sure your AI server is running.", "Connection Test", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        };

        saveButton.Click += async (s, args) =>
        {
            var idx = profileComboBox.SelectedIndex;
            if (idx >= 0 && idx < _settings.Profiles.Count)
            {
                _settings.Profiles[idx].ServerUrl = urlTextBox.Text;
                _settings.Profiles[idx].Model = modelComboBox.Text;
                _settings.Profiles[idx].ApiKey = apiKeyTextBox.Text;
                _settings.SelectedProfileIndex = idx;
            }
            
            _settings.Save();
            
            _aiService.Configure(urlTextBox.Text, modelComboBox.Text, apiKeyTextBox.Text);
            _conversationHistory.Clear();
            
            try
            {
                var models = await _aiService.GetAvailableModelsAsync();
                if (models.Count > 0)
                {
                    _isConnected = true;
                    AddMessage("AI", $"✓ Connected! Models available: {string.Join(", ", models)}", true);
                }
                else
                {
                    AddMessage("AI", "⚠ Connected to server, but no models found. Make sure a model is loaded.", true);
                }
            }
            catch (Exception ex)
            {
                AddMessage("AI", $"✗ Could not connect: {ex.Message}", true);
            }
            
            settingsWindow.Close();
        };

        cancelButton.Click += (s, args) => settingsWindow.Close();

        buttonPanel.Children.Add(testButton);
        buttonPanel.Children.Add(saveButton);
        buttonPanel.Children.Add(cancelButton);
        mainPanel.Children.Add(buttonPanel);

        settingsWindow.Content = mainPanel;
        settingsWindow.ShowDialog();
    }

    private ImageSource CreateRobotIcon()
    {
        // Create a simple robot icon programmatically
        using var bitmap = new DrawingBitmap(32, 32);
        using var g = DrawingGraphics.FromImage(bitmap);
        
        // Background - rounded rectangle
        g.SmoothingMode = Drawing2DSmoothingMode.AntiAlias;
        g.Clear(DrawingColor.FromArgb(0, 120, 212)); // Blue background
        
        // Robot head - rounded rectangle
        var headBrush = new DrawingSolidBrush(DrawingColor.White);
        g.FillRectangle(headBrush, 6, 4, 20, 18);
        
        // Eyes
        g.FillEllipse(new DrawingSolidBrush(DrawingColor.FromArgb(0, 120, 212)), 10, 9, 4, 4);
        g.FillEllipse(new DrawingSolidBrush(DrawingColor.FromArgb(0, 120, 212)), 18, 9, 4, 4);
        
        // Mouth
        g.DrawLine(new DrawingPen(DrawingColor.FromArgb(0, 120, 212), 2), 11, 17, 21, 17);
        
        // Antenna
        g.DrawLine(new DrawingPen(DrawingColor.White, 2), 16, 4, 16, 1);
        g.FillEllipse(headBrush, 14, 0, 4, 3);
        
        // Convert to WPF ImageSource
        var hBitmap = bitmap.GetHbitmap();
        try
        {
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("whisperMeOff v1.0\nA Windows native application", "About");
    }
}
