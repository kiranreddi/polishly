using Xunit;
using Polishly.App.ViewModels;

namespace Polishly.Platform.Tests;

public class OnboardingViewModelTests
{
    [Fact]
    public void OnboardingViewModel_InitialState_Step1()
    {
        var vm = new OnboardingViewModel();

        Assert.Equal(1, vm.CurrentStep);
        Assert.Equal(6, OnboardingViewModel.TotalSteps);
        Assert.False(vm.CanGoPrevious);
        Assert.True(vm.CanGoNext);
        Assert.False(vm.IsCompleted);
        Assert.Equal("Next", vm.NextButtonText);
    }

    [Fact]
    public void OnboardingViewModel_NavigationForward_CyclesThrough6Steps()
    {
        var vm = new OnboardingViewModel();

        for (int step = 1; step <= 6; step++)
        {
            Assert.Equal(step, vm.CurrentStep);
            Assert.Contains($"Step {step}", vm.StepTitle);

            if (step < 6)
            {
                vm.NextStep();
            }
        }

        Assert.True(vm.IsLastStep);
        Assert.Equal("Finish", vm.NextButtonText);

        vm.NextStep(); // Finish onboarding
        Assert.True(vm.IsCompleted);
    }

    [Fact]
    public void OnboardingViewModel_NavigationBackward_EnforcesMinimumStep()
    {
        var vm = new OnboardingViewModel();
        vm.CurrentStep = 3;

        vm.PreviousStep();
        Assert.Equal(2, vm.CurrentStep);

        vm.PreviousStep();
        Assert.Equal(1, vm.CurrentStep);

        vm.PreviousStep(); // Cannot go below 1
        Assert.Equal(1, vm.CurrentStep);
        Assert.False(vm.CanGoPrevious);
    }

    [Fact]
    public void OnboardingViewModel_BoundaryEnforcement_ClampsStepRange()
    {
        var vm = new OnboardingViewModel();
        vm.CurrentStep = 100;
        Assert.Equal(6, vm.CurrentStep);

        vm.CurrentStep = -50;
        Assert.Equal(1, vm.CurrentStep);
    }
}
