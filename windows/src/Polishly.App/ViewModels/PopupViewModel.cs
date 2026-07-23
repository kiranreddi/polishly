using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Polishly.App.Services;
using Polishly.Core.Diff;
using Polishly.Core.Models;
using Polishly.Core.StateMachine;

namespace Polishly.App.ViewModels;

public class PopupViewModel : INotifyPropertyChanged
{
    private readonly IRewriteStateMachine _stateMachine;
    private readonly WordDiffEngine _diffEngine;

    private string _originalText = string.Empty;
    private string _rewrittenText = string.Empty;
    private IReadOnlyList<DiffSegment> _diffSegments = Array.Empty<DiffSegment>();
    private string _errorMessage = string.Empty;
    private bool _isVisible = false;
    private IntPtr _targetWindowHandle = IntPtr.Zero;
    private ScreenRect _targetSelectionRect;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? RequestClose;
    public event EventHandler<string>? RequestPaste;
    public event EventHandler<string>? RequestCopy;
    public event EventHandler? RequestRevise;

    public string OriginalText
    {
        get => _originalText;
        set
        {
            if (_originalText != value)
            {
                _originalText = value ?? string.Empty;
                OnPropertyChanged();
                UpdateDiff();
            }
        }
    }

    public string RewrittenText
    {
        get => _rewrittenText;
        set
        {
            if (_rewrittenText != value)
            {
                _rewrittenText = value ?? string.Empty;
                OnPropertyChanged();
                UpdateDiff();
            }
        }
    }

    public IReadOnlyList<DiffSegment> DiffSegments
    {
        get => _diffSegments;
        private set
        {
            _diffSegments = value;
            OnPropertyChanged();
        }
    }

    public RewriteState CurrentState => _stateMachine.CurrentState;

    public string StateDescription => CurrentState switch
    {
        RewriteState.Idle => "Ready",
        RewriteState.Capturing => "Capturing context...",
        RewriteState.StreamReady => "Connecting to AI...",
        RewriteState.Streaming => "Generating suggestions...",
        RewriteState.StreamComplete => "Review changes",
        RewriteState.Replacing => "Applying changes...",
        RewriteState.Accepted => "Accepted",
        RewriteState.Copied => "Copied to clipboard",
        RewriteState.Failed => "Failed",
        RewriteState.Cancelled => "Cancelled",
        _ => CurrentState.ToString()
    };

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            _errorMessage = value ?? string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage) || CurrentState == RewriteState.Failed;

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                OnPropertyChanged();
            }
        }
    }

    public IntPtr TargetWindowHandle
    {
        get => _targetWindowHandle;
        set
        {
            _targetWindowHandle = value;
            OnPropertyChanged();
        }
    }

    public ScreenRect TargetSelectionRect
    {
        get => _targetSelectionRect;
        set
        {
            _targetSelectionRect = value;
            OnPropertyChanged();
        }
    }

    public ICommand AcceptCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand RetryCommand { get; }
    public ICommand RegenerateCommand { get; }
    public ICommand OpenReviseCommand { get; }
    public ICommand DismissCommand { get; }

    public PopupViewModel(IRewriteStateMachine stateMachine, WordDiffEngine diffEngine)
    {
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _diffEngine = diffEngine ?? throw new ArgumentNullException(nameof(diffEngine));

        _stateMachine.StateChanged += OnStateMachineStateChanged;

        AcceptCommand = new RelayCommand(Accept, CanAccept);
        CopyCommand = new RelayCommand(Copy, CanCopy);
        RetryCommand = new RelayCommand(Retry);
        RegenerateCommand = new RelayCommand(Retry);
        OpenReviseCommand = new RelayCommand(OpenRevise);
        DismissCommand = new RelayCommand(Dismiss);
    }

    public void Reset(string originalText)
    {
        _originalText = originalText ?? string.Empty;
        _rewrittenText = string.Empty;
        _errorMessage = string.Empty;
        _diffSegments = Array.Empty<DiffSegment>();

        OnPropertyChanged(nameof(OriginalText));
        OnPropertyChanged(nameof(RewrittenText));
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(DiffSegments));
        OnPropertyChanged(nameof(CurrentState));
        OnPropertyChanged(nameof(StateDescription));
    }

    public void AppendStreamingToken(string token)
    {
        if (string.IsNullOrEmpty(token)) return;

        _rewrittenText += token;
        OnPropertyChanged(nameof(RewrittenText));
        UpdateDiff();

        if (CurrentState == RewriteState.StreamReady)
        {
            _stateMachine.Transition(RewriteEvent.StartStreaming);
        }
        else if (CurrentState == RewriteState.Streaming)
        {
            _stateMachine.Transition(RewriteEvent.ReceiveToken, token);
        }
    }

    public void CompleteStream()
    {
        _stateMachine.Transition(RewriteEvent.StreamFinished);
    }

    public void UpdateDiff()
    {
        DiffSegments = _diffEngine.ComputeDiff(_originalText, _rewrittenText);
    }

    public bool CanAccept()
    {
        return CurrentState == RewriteState.StreamComplete && !string.IsNullOrEmpty(RewrittenText);
    }

    public void Accept()
    {
        if (!CanAccept()) return;

        _stateMachine.Transition(RewriteEvent.Accept);
        RequestPaste?.Invoke(this, RewrittenText);
        IsVisible = false;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    public bool CanCopy()
    {
        return (CurrentState == RewriteState.StreamComplete || CurrentState == RewriteState.Failed) && !string.IsNullOrEmpty(RewrittenText);
    }

    public void Copy()
    {
        if (string.IsNullOrEmpty(RewrittenText)) return;

        _stateMachine.Transition(RewriteEvent.Copy);
        RequestCopy?.Invoke(this, RewrittenText);
        IsVisible = false;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    public void Retry()
    {
        _rewrittenText = string.Empty;
        _errorMessage = string.Empty;
        UpdateDiff();
        OnPropertyChanged(nameof(RewrittenText));
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(HasError));

        _stateMachine.Transition(RewriteEvent.TriggerHotkey);
    }

    public void OpenRevise()
    {
        RequestRevise?.Invoke(this, EventArgs.Empty);
    }

    public void Dismiss()
    {
        _stateMachine.Transition(RewriteEvent.Reject);
        IsVisible = false;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    public void HandleEscape()
    {
        Dismiss();
    }

    public void HandleClickOutside()
    {
        Dismiss();
    }

    private void OnStateMachineStateChanged(object? sender, RewriteStateChangedEventArgs e)
    {
        if (e.NewState == RewriteState.Failed && !string.IsNullOrEmpty(e.Payload))
        {
            ErrorMessage = e.Payload;
        }

        OnPropertyChanged(nameof(CurrentState));
        OnPropertyChanged(nameof(StateDescription));
        OnPropertyChanged(nameof(HasError));
        ((RelayCommand)AcceptCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CopyCommand).RaiseCanExecuteChanged();
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
