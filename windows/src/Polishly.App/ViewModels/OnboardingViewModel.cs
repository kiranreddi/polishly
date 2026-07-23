using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Polishly.App.ViewModels;

public class OnboardingViewModel : INotifyPropertyChanged
{
    public const int TotalSteps = 6;
    private int _currentStep = 1;
    private bool _isCompleted = false;
    private string _practiceInput = "The quick brown fox jumps over the lazy dog";
    private string _practiceOutput = "The swift auburn fox leaps over the sleepy dog";

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? OnboardingCompleted;

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
        2 => "Step 2: Permissions & Accessibility",
        3 => "Step 3: AI Provider Configuration",
        4 => "Step 4: Hotkey Customization",
        5 => "Step 5: Interactive Practice",
        6 => "Step 6: Setup Complete!",
        _ => "Step 1: Welcome"
    };

    public string StepDescription => CurrentStep switch
    {
        1 => "Polishly helps you polish text in any Windows app instantly with AI.",
        2 => "Polishly requires UI Automation and Clipboard permissions to read and replace text.",
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

    public bool CanGoPrevious => CurrentStep > 1;
    public bool CanGoNext => CurrentStep < TotalSteps;
    public bool IsLastStep => CurrentStep == TotalSteps;
    public string NextButtonText => IsLastStep ? "Finish" : "Next";

    public ICommand NextStepCommand { get; }
    public ICommand PreviousStepCommand { get; }
    public ICommand CompleteOnboardingCommand { get; }

    public OnboardingViewModel()
    {
        NextStepCommand = new RelayCommand(NextStep);
        PreviousStepCommand = new RelayCommand(PreviousStep, () => CanGoPrevious);
        CompleteOnboardingCommand = new RelayCommand(CompleteOnboarding);
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
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
