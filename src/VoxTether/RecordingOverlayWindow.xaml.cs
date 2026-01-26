using System.Windows;
using System.Windows.Media.Animation;

namespace VoxTether;

/// <summary>
/// Overlay window that provides visual feedback during recording and transcription.
/// Displays at the top center of the primary screen.
/// </summary>
public partial class RecordingOverlayWindow : Window
{
    private Storyboard? _pulseAnimation;
    private Storyboard? _spinAnimation;

    public RecordingOverlayWindow()
    {
        InitializeComponent();
        
        // Get animations from resources
        _pulseAnimation = (Storyboard?)FindResource("PulseAnimation");
        _spinAnimation = (Storyboard?)FindResource("SpinAnimation");
        
        // Position window at top center of primary screen when loaded
        Loaded += (_, _) => PositionWindow();
    }

    private void PositionWindow()
    {
        // Get the primary screen working area
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        
        // Use ActualWidth for accurate positioning after layout
        var windowWidth = ActualWidth > 0 ? ActualWidth : Width;
        if (double.IsNaN(windowWidth)) windowWidth = 160; // Fallback to default
        
        // Center horizontally, position at top with small margin
        Left = (screenWidth - windowWidth) / 2;
        Top = 10;
    }

    /// <summary>
    /// Shows the overlay in recording state with pulsing red indicator.
    /// </summary>
    public void ShowRecording()
    {
        // Stop any running animations
        StopAnimations();
        
        // Show recording panel, hide transcribing panel
        RecordingPanel.Visibility = Visibility.Visible;
        TranscribingPanel.Visibility = Visibility.Collapsed;
        
        // Start pulse animation
        _pulseAnimation?.Begin(this, true);
        
        // Show the window and reposition
        Show();
        PositionWindow();
    }

    /// <summary>
    /// Shows the overlay in transcribing state with spinning loading indicator.
    /// </summary>
    public void ShowTranscribing()
    {
        // Stop any running animations
        StopAnimations();
        
        // Show transcribing panel, hide recording panel
        RecordingPanel.Visibility = Visibility.Collapsed;
        TranscribingPanel.Visibility = Visibility.Visible;
        
        // Start spin animation
        _spinAnimation?.Begin(this, true);
        
        // Show the window and reposition
        Show();
        PositionWindow();
    }

    /// <summary>
    /// Hides the overlay and stops all animations.
    /// </summary>
    public void HideOverlay()
    {
        StopAnimations();
        Hide();
    }

    private void StopAnimations()
    {
        _pulseAnimation?.Stop(this);
        _spinAnimation?.Stop(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        StopAnimations();
        base.OnClosed(e);
    }
}
