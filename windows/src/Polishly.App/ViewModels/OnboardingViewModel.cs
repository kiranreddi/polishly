using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Polishly.Core.Models;
using Polishly.Providers;

namespace Polishly.App.ViewModels;

public class OnboardingViewModel : INotifyPropertyChanged
{
    public const int TotalSteps = 6;
    private int _currentStep = 1;
    private bool _isCompleted = false;
    private string _practiceInput = "The quick brown fox jumps over the lazy dog";
    private string _practiceOutput = "The swift auburn fox leaps over the sleepy dog";
    private string _practiceStatus = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? OnboardingCompleted;
    public SettingsViewModel Settings { get; }

    public int CurrentStep
    {
        get => _currentStep;
        set
        {
            int clamped = Math.Clamp(value, 1, TotalSteps);
            if (_currentStep != clamped)
            {
                _currentStep = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StepTitle));
                OnPropertyChanged(nameof(StepDescription));
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(IsLastStep));
                OnPropertyChanged(nameof(NextButtonText));
                OnPropertyChanged(nameof(IsStep1));
                OnPropertyChanged(nameof(IsStep2));
                OnPropertyChanged(nameof(IsStep3));
                OnPropertyChanged(nameof(IsStep4));
                OnPropertyChanged(nameof(IsStep5));
                OnPropertyChanged(nameof(IsStep6));
                ((RelayCommand)PreviousStepCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsCompleted
    {
        get => _isCompleted;
        set
        {
            if (_isCompleted != value)
            {
                _isCompleted = value;
                OnPropertyChanged();
            }
        }
    }

    public string StepTitle => CurrentStep switch
    {
        1 => "Step 1: Welcome to Polishly",
        2 => "Step 2: Privacy & Safety",
        3 => "Step 3: AI Provider Configuration",
        4 => "Step 4: Hotkey Customization",
        5 => "Step 5: Interactive Practice",
        6 => "Step 6: Setup Complete!",
        _ => "Step 1: Welcome"
    };

    public string StepDescription => CurrentStep switch
    {
        1 => "Polishly helps you polish text in any Windows app instantly with AI.",
        2 => "Windows does not require a special accessibility permission. Polishly uses UI Automation and a guarded clipboard transaction only after you press the hotkey.",
        3 => "Choose your preferred AI provider (Demo, OpenAI, Anthropic, Groq, Cerebras) and enter an API key.",
        4 => "Set your global shortcut (default: Ctrl+Shift+P) to trigger Polishly anywhere.",
        5 => "Try triggering Polishly on the practice text below to see inline diff suggestions in action.",
        6 => "You're ready! Polishly will run in the system tray and respond to your hotkey.",
        _ => string.Empty
    };

    public string PracticeInput
    {
        get => _practiceInput;
        set { _practiceInput = value; OnPropertyChanged(); }
    }

    public string PracticeOutput
    {
        get => _practiceOutput;
        set { _practiceOutput = value; OnPropertyChanged(); }
    }

    public string PracticeStatus
    {
        get => _practiceStatus;
        private set { _practiceStatus = value; OnPropertyChanged(); }
    }

    public bool CanGoPrevious => CurrentStep > 1;
    public bool CanGoNext => CurrentStep < TotalSteps;
    public bool IsLastStep => CurrentStep == TotalSteps;
    public string NextButtonText => IsLastStep ? "Finish" : "Next";
    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;
    public bool IsStep4 => CurrentStep == 4;
    public bool IsStep5 => CurrentStep == 5;
    public bool IsStep6 => CurrentStep == 6;

    public ICommand NextStepCommand { get; }
    public ICommand PreviousStepCommand { get; }
    public ICommand CompleteOnboardingCommand { get; }
    public ICommand RewritePracticeCommand { get; }

    public OnboardingViewModel() : this(new SettingsViewModel())
    {
    }

    public OnboardingViewModel(SettingsViewModel settings)
    {
        Settings = settings;
        NextStepCommand = new RelayCommand(NextStep);
        PreviousStepCommand = new RelayCommand(PreviousStep, () => CanGoPrevious);
        CompleteOnboardingCommand = new RelayCommand(CompleteOnboarding);
        RewritePracticeCommand = new RelayCommand(RewritePractice);
    }

    public void NextStep()
    {
        if (CurrentStep < TotalSteps)
        {
            CurrentStep++;
        }
        else
        {
            CompleteOnboarding();
        }
    }

    public void PreviousStep()
    {
        if (CurrentStep > 1)
        {
            CurrentStep--;
        }
    }

    public void CompleteOnboarding()
    {
        IsCompleted = true;
        OnboardingCompleted?.Invoke(this, EventArgs.Empty);
        Settings.Save();
    }

    private async void RewritePractice()
    {
        if (!Settings.ValidateApiKey(Settings.ActiveProviderId, Settings.ApiKey))
        {
            PracticeStatus = "Add a valid provider key or choose Demo mode first.";
            return;
        }

        PracticeStatus = "Rewriting…";
        try
        {
            var provider = ProviderFactory.Create(
                Settings.ActiveProviderId, Settings.ApiKey, Settings.ActiveModel);
            var request = new RewriteRequest(
                PracticeInput,
                RewriteMode.Improve,
                CustomInstruction: null);
            var output = new System.Text.StringBuilder();
            await foreach (var token in provider.StreamRewriteAsync(request))
            {
                output.Append(token.Text);
            }
            PracticeOutput = output.ToString();
            PracticeStatus = "Rewrite complete. Your original practice text was not changed.";
        }
        catch (Exception ex)
        {
            PracticeStatus = $"Rewrite failed: {ex.Message}";
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
