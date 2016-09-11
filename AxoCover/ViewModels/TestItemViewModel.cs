﻿using AxoCover.Models.Data;
using AxoCover.Models.Extensions;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace AxoCover.ViewModels
{
  public class TestItemViewModel : ViewModel
  {
    public TestItem TestItem { get; private set; }

    public TestItemViewModel Parent { get; private set; }

    public ObservableCollection<TestItemViewModel> Children { get; private set; }

    private TestState _state;
    public TestState State
    {
      get
      {
        return _state;
      }
      set
      {
        _state = value;
        IsStateUpToDate = true;
        NotifyPropertyChanged(nameof(State));
        NotifyPropertyChanged(nameof(IconPath));
        NotifyPropertyChanged(nameof(OverlayIconPath));

        foreach (var parent in this.Crawl(p => p.Parent))
        {
          if (!parent.IsStateUpToDate || parent.State < _state)
          {
            parent.State = _state;
          }
        }
      }
    }

    private bool _isStateUpToDate;
    public bool IsStateUpToDate
    {
      get
      {
        return _isStateUpToDate;
      }
      set
      {
        _isStateUpToDate = value;
        NotifyPropertyChanged(nameof(IsStateUpToDate));
      }
    }

    private bool _isExpanded;
    public bool IsExpanded
    {
      get
      {
        return _isExpanded;
      }
      set
      {
        _isExpanded = value;
        NotifyPropertyChanged(nameof(IsExpanded));
        if (Children.Count == 1)
        {
          Children.First().IsExpanded = value;
        }
      }
    }

    public string IconPath
    {
      get
      {
        if (TestItem.Kind == TestItemKind.Method)
        {
          if (State != TestState.Unknown)
          {
            return AxoCoverPackage.ResourcesPath + State + ".png";
          }
          else
          {
            return AxoCoverPackage.ResourcesPath + "test.png";
          }
        }
        else
        {
          return AxoCoverPackage.ResourcesPath + TestItem.Kind + ".png";
        }
      }
    }

    public string OverlayIconPath
    {
      get
      {
        if (TestItem.Kind != TestItemKind.Method)
        {
          if (State != TestState.Unknown)
          {
            return AxoCoverPackage.ResourcesPath + State + ".png";
          }
          else
          {
            return AxoCoverPackage.ResourcesPath + "test.png";
          }
        }
        else
        {
          return null;
        }
      }
    }

    private TestResult _result;
    public TestResult Result
    {
      get
      {
        return _result;
      }
      set
      {
        _result = value;
        NotifyPropertyChanged(nameof(Result));
      }
    }

    public TestItemViewModel(TestItemViewModel parent, TestItem testItem)
    {
      if (testItem == null)
        throw new ArgumentNullException(nameof(testItem));

      TestItem = testItem;
      Parent = parent;
      Children = new ObservableCollection<TestItemViewModel>();
      foreach (var childItem in testItem.Children)
      {
        AddChild(childItem);
      }
    }

    public void UpdateItem(TestItem testItem)
    {
      TestItem = testItem;
      NotifyPropertyChanged(nameof(TestItem));

      var childrenToUpdate = Children.ToList();
      foreach (var childItem in testItem.Children)
      {
        var childToUpdate = childrenToUpdate.FirstOrDefault(p => p.TestItem == childItem);
        if (childToUpdate != null)
        {
          childToUpdate.UpdateItem(childItem);
          childrenToUpdate.Remove(childToUpdate);
        }
        else
        {
          AddChild(childItem);
        }
      }

      foreach (var childToDelete in childrenToUpdate)
      {
        Children.Remove(childToDelete);
      }
    }

    public void ResetAll()
    {
      IsStateUpToDate = false;

      foreach (var child in Children)
      {
        child.ResetAll();
      }
    }

    public void ScheduleAll()
    {
      State = TestState.Scheduled;

      foreach (var child in Children)
      {
        child.ScheduleAll();
      }
    }

    public void CollapseAll()
    {
      IsExpanded = false;
      foreach (var child in Children)
      {
        child.CollapseAll();
      }
    }

    public void ExpandAll()
    {
      IsExpanded = true;
      foreach (var child in Children)
      {
        child.ExpandAll();
      }
    }

    private void AddChild(TestItem testItem)
    {
      var child = new TestItemViewModel(this, testItem);

      int i;
      for (i = 0; i < Children.Count; i++)
      {
        if (StringComparer.OrdinalIgnoreCase.Compare(Children[i].TestItem.Name, TestItem.Name) > 0)
        {
          break;
        }
      }

      Children.Insert(i, child);
    }
  }
}