using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgenticOs.Models;
using AgenticOs.Services;
using Microsoft.Extensions.Logging;

namespace AgenticOs.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ProjectConfig _config;
    private readonly DnaManagerService _dnaManager;
    private readonly TaskSchedulerService _scheduler;
    private FileSystemWatcher? _watcher;
    private string _projectRoot = string.Empty;

    [ObservableProperty]
    private string _projectRootDisplay = "（未设置项目根）";

    [ObservableProperty]
    private string _statusText = "就绪";

    [ObservableProperty]
    private ObservableCollection<TaskFrameViewModel> _callStackFrames = [];

    [ObservableProperty]
    private ObservableCollection<TopologyNodeViewModel> _topologyNodes = [];

    [ObservableProperty]
    private string _topologySummary = "";

    public MainWindowViewModel()
    {
        _config = new ProjectConfig();
        var loggerFactory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Warning));
        _dnaManager = new DnaManagerService(loggerFactory.CreateLogger<DnaManagerService>());
        _scheduler = new TaskSchedulerService(loggerFactory.CreateLogger<TaskSchedulerService>());
    }

    [RelayCommand]
    private void LoadProject()
    {
        try
        {
            _projectRoot = _config.DefaultProjectRoot;
            if (string.IsNullOrEmpty(_projectRoot))
            {
                StatusText = "未配置 AGENTIC_OS_PROJECT_ROOT 环境变量";
                ProjectRootDisplay = "（未设置）";
                return;
            }
            ProjectRootDisplay = _projectRoot;
            RefreshAll();
            StartWatching();
            StatusText = $"已加载 · 最后刷新 {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusText = $"加载失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void RefreshAll()
    {
        if (string.IsNullOrEmpty(_projectRoot))
        {
            if (!string.IsNullOrEmpty(_config.DefaultProjectRoot))
            {
                _projectRoot = _config.DefaultProjectRoot;
                ProjectRootDisplay = _projectRoot;
            }
            else
            {
                StatusText = "请先设置 AGENTIC_OS_PROJECT_ROOT";
                return;
            }
        }

        try
        {
            // 调用栈
            var stack = _scheduler.GetCallStack(_projectRoot);
            CallStackFrames.Clear();
            if (!stack.IsEmpty)
            {
                for (int i = stack.Frames.Count - 1; i >= 0; i--)
                {
                    var f = stack.Frames[i];
                    CallStackFrames.Add(new TaskFrameViewModel(f, i == stack.Frames.Count - 1));
                }
            }

            // 拓扑图
            var topology = _dnaManager.ScanTopology(_projectRoot);
            TopologyNodes.Clear();
            foreach (var a in topology.Assemblies)
            {
                TopologyNodes.Add(new TopologyNodeViewModel(a));
            }
            TopologySummary = $"共 {topology.Assemblies.Count} 个程序集，{topology.Edges.Count} 条依赖边 · {topology.ScannedAt:yyyy-MM-dd HH:mm}";

            StatusText = $"已刷新 · {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusText = $"刷新失败: {ex.Message}";
        }
    }

    private void StartWatching()
    {
        _watcher?.Dispose();
        var watchDir = Path.Combine(_projectRoot, ".agentic-os");
        if (!Directory.Exists(watchDir))
            return;

        _watcher = new FileSystemWatcher(watchDir)
        {
            Filter = "call-stack.json",
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
        };
        _watcher.Changed += (_, _) =>
        {
            try
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => RefreshAll());
            }
            catch { /* ignore */ }
        };
        _watcher.EnableRaisingEvents = true;
    }
}

public class TaskFrameViewModel
{
    public string AssemblyName { get; }
    public string TaskDescription { get; }
    public string StatusText { get; }
    public string? SuspendReason { get; }
    public string? ResumeCondition { get; }
    public string SubTasksSummary { get; }
    public bool IsTop { get; }
    public string Header => IsTop ? "▶ 栈顶（当前执行）" : "○ 已挂起";

    public TaskFrameViewModel(TaskFrame frame, bool isTop)
    {
        AssemblyName = frame.AssemblyName;
        TaskDescription = frame.TaskDescription;
        StatusText = frame.Status.ToString();
        SuspendReason = frame.SuspendReason;
        ResumeCondition = frame.ResumeCondition;
        IsTop = isTop;
        var completed = frame.SubTasks.Where(s => s.Completed).Count();
        SubTasksSummary = frame.SubTasks.Count > 0
            ? $"{completed}/{frame.SubTasks.Count} 子任务完成"
            : "";
    }
}

public class TopologyNodeViewModel
{
    public string Name { get; }
    public string Boundary { get; }
    public string Dependencies { get; }
    public string? Maintainer { get; }

    public TopologyNodeViewModel(AssemblyNode node)
    {
        Name = node.Name;
        Boundary = node.Boundary.ToString();
        Dependencies = node.Dependencies.Count > 0 ? string.Join(", ", node.Dependencies) : "—";
        Maintainer = node.Maintainer;
    }
}
