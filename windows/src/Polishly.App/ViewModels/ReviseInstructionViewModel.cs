using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Polishly.App.ViewModels;

public class ReviseInstructionViewModel : INotifyPropertyChanged
{
    private string _instructionText = string.Empty;
    private IntPtr _targetWindowHandle = IntPtr.Zero;
    private bool _isSubmitting = false;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<string>? InstructionSubmitted;
    public event EventHandler? Cancelled;

    public string InstructionText
    {
        get => _instructionText;
        set
        {
            if (_instructionText != value)
            {
                _instructionText = value ?? string.Empty;
                OnPropertyChanged();
                ((RelayCommand)SubmitCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public IntPtr TargetWindowHandle
    {
        get => _targetWindowHandle;
        set
        {
            if (_targetWindowHandle != value)
            {
                _targetWindowHandle = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsSubmitting
    {
        get => _isSubmitting;
        set
        {
            if (_isSubmitting != value)
            {
                _isSubmitting = value;
                OnPropertyChanged();
                ((RelayCommand)SubmitCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand SubmitCommand { get; }
    public ICommand CancelCommand { get; }

    public ReviseInstructionViewModel()
    {
        SubmitCommand = new RelayCommand(Submit, CanSubmit);
        CancelCommand = new RelayCommand(Cancel);
    }

    public bool CanSubmit()
    {
        return !string.IsNullOrWhiteSpace(InstructionText) && !IsSubmitting;
    }

    public void Submit()
    {
        if (!CanSubmit()) return;

        IsSubmitting = true;
        InstructionSubmitted?.Invoke(this, InstructionText);
        IsSubmitting = false;
    }

    public void Cancel()
    {
        InstructionText = string.Empty;
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
