﻿using AxoCover.Models;
using AxoCover.Models.Data;
using AxoCover.Models.Events;
using AxoCover.Models.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AxoCover.ViewModels
{
  public class TestExplorerViewModel : ViewModel
  {
    private readonly IEditorContext _editorContext;
    private readonly ITestProvider _testProvider;
    private readonly ITestRunner _testRunner;
    private readonly IResultProvider _resultProvider;

    private bool _isSolutionLoaded;
    public bool IsSolutionLoaded
    {
      get
      {
        return _isSolutionLoaded;
      }
      set
      {
        _isSolutionLoaded = value;
        NotifyPropertyChanged(nameof(IsSolutionLoaded));
      }
    }

    public enum RunnerStates
    {
      Ready,
      Building,
      Testing
    }

    private RunnerStates _runnerState;
    public RunnerStates RunnerState
    {
      get
      {
        return _runnerState;
      }
      set
      {
        _runnerState = value;
        NotifyPropertyChanged(nameof(RunnerState));
        NotifyPropertyChanged(nameof(IsBusy));
        NotifyPropertyChanged(nameof(IsTesting));
      }
    }

    public bool IsBusy
    {
      get
      {
        return RunnerState == RunnerStates.Building || RunnerState == RunnerStates.Testing;
      }
    }

    public bool IsTesting
    {
      get
      {
        return RunnerState == RunnerStates.Testing;
      }
    }

    private bool _isProgressIndeterminate;
    public bool IsProgressIndeterminate
    {
      get
      {
        return _isProgressIndeterminate;
      }
      set
      {
        _isProgressIndeterminate = value;
        NotifyPropertyChanged(nameof(IsProgressIndeterminate));
      }
    }

    private int _testsToExecute;
    private int _testsExecuted;

    private double _Progress;
    public double Progress
    {
      get
      {
        return _Progress;
      }
      set
      {
        _Progress = value;
        NotifyPropertyChanged(nameof(Progress));
      }
    }

    private string _statusMessage = Resources.Ready;
    public string StatusMessage
    {
      get
      {
        return _statusMessage;
      }
      set
      {
        _statusMessage = value;
        NotifyPropertyChanged(nameof(StatusMessage));
      }
    }

    private bool _isAutoCoverEnabled;
    public bool IsAutoCoverEnabled
    {
      get
      {
        return _isAutoCoverEnabled;
      }
      set
      {
        _isAutoCoverEnabled = value;
        NotifyPropertyChanged(nameof(IsAutoCoverEnabled));
      }
    }

    public bool IsHighlighting
    {
      get
      {
        return LineCoverageAdornment.IsHighlighting;
      }
      set
      {
        LineCoverageAdornment.IsHighlighting = value;
        NotifyPropertyChanged(nameof(IsHighlighting));
      }
    }

    private TestItemViewModel _TestSolution;
    public TestItemViewModel TestSolution
    {
      get
      {
        return _TestSolution;
      }
      private set
      {
        _TestSolution = value;
        NotifyPropertyChanged(nameof(TestSolution));
      }
    }

    private TestItemViewModel _SelectedItem;
    public TestItemViewModel SelectedItem
    {
      get
      {
        return _SelectedItem;
      }
      set
      {
        _SelectedItem = value;
        NotifyPropertyChanged(nameof(SelectedItem));
        NotifyPropertyChanged(nameof(IsItemSelected));
      }
    }

    public bool IsItemSelected
    {
      get
      {
        return SelectedItem != null;
      }
    }

    public ObservableCollection<TestStateGroupViewModel> StateGroups { get; set; }

    public bool IsStateGroupSelected
    {
      get
      {
        return StateGroups.Any(p => p.IsSelected);
      }
    }

    public ICommand BuildCommand
    {
      get
      {
        return new DelegateCommand(
          p => _editorContext.BuildSolution(),
          p => !IsBusy,
          p => ExecuteOnPropertyChange(p, nameof(IsBusy)));
      }
    }

    public ICommand ExpandAllCommand
    {
      get
      {
        return new DelegateCommand(p => TestSolution.ExpandAll());
      }
    }

    public ICommand CollapseAllCommand
    {
      get
      {
        return new DelegateCommand(p => TestSolution.CollapseAll());
      }
    }

    public ICommand RunTestsCommand
    {
      get
      {
        return new DelegateCommand(
          p =>
          {
            _testRunner.RunTestsAsync(SelectedItem.TestItem);
            SelectedItem.ScheduleAll();
          },
          p => !IsBusy && SelectedItem != null,
          p => ExecuteOnPropertyChange(p, nameof(IsBusy), nameof(SelectedItem)));
      }
    }

    public ICommand NavigateToTestItemCommand
    {
      get
      {
        return new DelegateCommand(
          p =>
          {
            var testItem = p as TestItem;
            switch (testItem.Kind)
            {
              case TestItemKind.Class:
                _editorContext.NavigateToClass(testItem.GetParent<TestProject>().Name, testItem.FullName);
                break;
              case TestItemKind.Method:
                _editorContext.NavigateToMethod(testItem.GetParent<TestProject>().Name, testItem.Parent.FullName, testItem.Name);
                break;
            }
          },
          p => p.CheckAs<TestItem>(q => q.Kind == TestItemKind.Class || q.Kind == TestItemKind.Method));
      }
    }

    public ICommand SelectStateGroupCommand
    {
      get
      {
        return new DelegateCommand(
          p =>
          {
            var selectedStateGroup = p as TestStateGroupViewModel;
            var previousState = selectedStateGroup.IsSelected;

            foreach (var stateGroup in StateGroups)
            {
              stateGroup.IsSelected = false;
            }

            selectedStateGroup.IsSelected = !previousState;
          });
      }
    }

    public TestExplorerViewModel(IEditorContext editorContext, ITestProvider testProvider, ITestRunner testRunner, IResultProvider resultProvider)
    {
      _editorContext = editorContext;
      _testProvider = testProvider;
      _testRunner = testRunner;
      _resultProvider = resultProvider;

      _editorContext.SolutionOpened += OnSolutionOpened;
      _editorContext.SolutionClosing += OnSolutionClosing;
      _editorContext.BuildStarted += OnBuildStarted;
      _editorContext.BuildFinished += OnBuildFinished;

      _testRunner.TestsStarted += OnTestsStarted;
      _testRunner.TestExecuted += OnTestExecuted;
      _testRunner.TestLogAdded += OnTestLogAdded;
      _testRunner.TestsFinished += OnTestsFinished;

      _resultProvider.ResultsUpdated += OnResultsUpdated;

      StateGroups = new ObservableCollection<TestStateGroupViewModel>();
      StateGroups.CollectionChanged += OnCollectionChanged; ;

    }

    private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
      NotifyPropertyChanged(nameof(IsStateGroupSelected));
      if (e.NewItems != null)
      {
        foreach (TestStateGroupViewModel item in e.NewItems)
        {
          item.IsSelectedChanged += OnIsSelectedChanged;
        }
      }

      if (e.OldItems != null)
      {
        foreach (TestStateGroupViewModel item in e.OldItems)
        {
          item.IsSelectedChanged -= OnIsSelectedChanged;
        }
      }
    }

    private void OnIsSelectedChanged(object sender, EventArgs e)
    {
      NotifyPropertyChanged(nameof(IsStateGroupSelected));
    }

    private async void OnSolutionOpened(object sender, EventArgs e)
    {
      var testSolution = await _testProvider.GetTestSolutionAsync(_editorContext.Solution);
      Update(testSolution);
      IsSolutionLoaded = true;
    }

    private void OnSolutionClosing(object sender, EventArgs e)
    {
      IsSolutionLoaded = false;
      Update(null);
      StateGroups.Clear();
    }

    private void OnBuildStarted(object sender, EventArgs e)
    {
      IsProgressIndeterminate = true;
      StatusMessage = Resources.Building;
      RunnerState = RunnerStates.Building;
    }

    private async void OnBuildFinished(object sender, EventArgs e)
    {
      IsProgressIndeterminate = false;
      StatusMessage = Resources.Done;
      RunnerState = RunnerStates.Ready;
      IsSolutionLoaded = true;
      var testSolution = await _testProvider.GetTestSolutionAsync(_editorContext.Solution);
      Update(testSolution);

      if (IsAutoCoverEnabled && RunTestsCommand.CanExecute(null))
      {
        RunTestsCommand.Execute(null);
      }
    }

    private void OnTestsStarted(object sender, EventArgs e)
    {
      _testsToExecute = SelectedItem.TestItem.TestCount;
      _testsExecuted = 0;
      IsProgressIndeterminate = true;
      StatusMessage = Resources.InitializingTestRunner;
      RunnerState = RunnerStates.Testing;
      TestSolution.ResetAll();
      StateGroups.Clear();
      _editorContext.ClearLog();
      _editorContext.ActivateLog();
    }

    private void OnTestExecuted(object sender, TestExecutedEventArgs e)
    {
      //Find test item view model in tree
      var itemPath = e.Path.Split('.');

      var itemName = string.Empty;
      var testItem = TestSolution;
      foreach (var part in itemPath)
      {
        if (itemName != string.Empty)
        {
          itemName += ".";
        }
        itemName += part;

        var childItem = testItem.Children.FirstOrDefault(p => p.TestItem.Name == itemName);

        if (childItem != null)
        {
          itemName = string.Empty;
          testItem = childItem;
        }
      }

      //Update test item view model and state groups
      if (testItem != null && itemName == string.Empty)
      {
        testItem.State = e.Outcome;
        _testsExecuted++;

        var stateGroup = StateGroups.FirstOrDefault(p => p.State == testItem.State);
        if (stateGroup == null)
        {
          stateGroup = new TestStateGroupViewModel(testItem.State);
          StateGroups.Add(stateGroup);
        }
        stateGroup.Items.Add(testItem);
      }

      //Update test execution state
      if (_testsExecuted < _testsToExecute)
      {
        IsProgressIndeterminate = false;
        Progress = (double)_testsExecuted / _testsToExecute;
        StatusMessage = string.Format(Resources.ExecutingTests, _testsExecuted, _testsToExecute);
      }
      else
      {
        IsProgressIndeterminate = true;
        StatusMessage = Resources.GeneratingCoverageReport;
      }
    }

    private void OnTestLogAdded(object sender, TestLogAddedEventArgs e)
    {
      _editorContext.WriteToLog(e.Text);
    }

    private void OnTestsFinished(object sender, TestFinishedEventArgs e)
    {
      IsProgressIndeterminate = false;
      StatusMessage = Resources.Done;
      RunnerState = RunnerStates.Ready;
    }

    private async void OnResultsUpdated(object sender, EventArgs e)
    {
      var testMethodViewModels = TestSolution
        .Children
        .Flatten(p => p.Children)
        .Where(p => p.TestItem.Kind == TestItemKind.Method)
        .ToList();

      var items = new ConcurrentDictionary<TestItemViewModel, TestResult>();

      await Task.Run(() =>
      {
        Parallel.ForEach(testMethodViewModels, p =>
        {
          var result = _resultProvider.GetTestResult(p.TestItem as TestMethod);
          if (result != null)
          {
            items[p] = result;
          }
        });
      });

      foreach (var item in items)
      {
        item.Key.Result = item.Value;
      }
    }

    private void Update(TestSolution testSolution)
    {
      if (testSolution != null)
      {
        if (TestSolution == null)
        {
          TestSolution = new TestItemViewModel(null, testSolution);
        }
        else
        {
          TestSolution.UpdateItem(testSolution);
        }
      }
      else
      {
        TestSolution = null;
      }
    }
  }
}