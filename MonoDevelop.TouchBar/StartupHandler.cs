using System;
using MonoDevelop.Components.Commands;
using MonoDevelop.Ide;
using MonoDevelop.Debugger;
using MonoDevelop.Projects;
using AppKit;
using System.Diagnostics;
using System.Linq;

namespace TouchbarAddin
{
    public static class MacUtil
    {
        static readonly Xwt.Toolkit macToolkit = Xwt.Toolkit.Load(Xwt.ToolkitType.XamMac);

        public static NSImage ToNSImage(this Xwt.Drawing.Image image)
        {
            return (NSImage)macToolkit.GetNativeImage(image);
        }
    }

    public class StartupHandler : CommandHandler
    {
        public NSWindow Window
        {
            get
            {
                return NSApplication.SharedApplication.KeyWindow ?? NSApplication.SharedApplication.Windows.First();
            }
        }

        private bool SupportsTouchBar() => ObjCRuntime.Class.GetHandle("NSTouchBar") != IntPtr.Zero;
        private MonoDevelopProjectTouchBarDelegate Delegate = new MonoDevelopProjectTouchBarDelegate();
        public StartupHandler()
        {

            NSApplication.SharedApplication.SetAutomaticCustomizeTouchBarMenuItemEnabled(true);
        }

        private void BuildRequested(object sender, EventArgs e)
        {
            Debug.WriteLine("BuildRequested");
        }

        protected override void Run()
        {
            if (!SupportsTouchBar())
                return;
            if (Window == null)
                return;
            MakeTouchBar();
            IdeApp.Workspace.WorkspaceItemOpened += OnSoultionOpened;
            IdeApp.Workspace.WorkspaceItemClosed += OnSoultionClosed;
            IdeApp.ProjectOperations.StartBuild += StartBuild;
            IdeApp.ProjectOperations.EndBuild += EndBuild;
            DebuggingService.DebugSessionStarted += DebuggingService_DebugSessionStarted;
            DebuggingService.PausedEvent += DebuggingService_PausedEvent;
            DebuggingService.StoppedEvent += DebuggingService_StoppedEvent;
            DebuggingService.ResumedEvent += DebuggingService_ResumedEvent;
        }

        private void DebuggingService_ResumedEvent(object sender, EventArgs e)
        {
            Debug.WriteLine("DebuggingService_ResumedEvent");
            Delegate.IsRunning = true;
            Window.SetTouchBar(MakeTouchBar());
        }

        private void DebuggingService_PausedEvent(object sender, EventArgs e)
        {
            Debug.WriteLine("DebuggingService_PausedEvent");
            Delegate.IsRunning = true;
            Window.SetTouchBar(MakeTouchBar());
        }

        private void DebuggingService_DebugSessionStarted(object sender, EventArgs e)
        {
            Debug.WriteLine("DebuggingService_DebugSessionStarted");
            Delegate.IsRunning = true;
            Window.SetTouchBar(MakeTouchBar());
        }

        private void DebuggingService_StoppedEvent(object sender, EventArgs e)
        {
            Debug.WriteLine("DebuggingService_StoppedEvent");
            Delegate.IsRunning = false;
            Window.SetTouchBar(MakeTouchBar());
        }

        private void OnSoultionClosed(object sender, WorkspaceItemEventArgs e)
        {
            Debug.WriteLine("OnSoultionClosed");
            Delegate.IsRunning = false;
            Window.SetTouchBar(null);
        }

        private void OnSoultionOpened(object sender, WorkspaceItemEventArgs e)
        {
            Debug.WriteLine("OnSoultionOpened");
            Delegate.IsRunning = false;
            Window.SetTouchBar(MakeTouchBar());
        }

        private void EndBuild(object sender, BuildEventArgs args)
        {
            Debug.WriteLine("EndBuild");
            Delegate.IsRunning = false;
            Window.SetTouchBar(MakeTouchBar());
        }

        private void StartBuild(object sender, BuildEventArgs args)
        {
            Debug.WriteLine("StartBuild");
            Delegate.IsRunning = true;
            Window.SetTouchBar(MakeTouchBar());
        }

        public NSTouchBar MakeTouchBar()
        {
            var bar = new NSTouchBar()
            {
                Delegate = Delegate,
                DefaultItemIdentifiers = MonoDevelopProjectTouchBarDelegate.DefaultIdentifiers
            };
            return bar;
        }
    }

    public class MonoDevelopTouchBarDelegate : NSTouchBarDelegate
    {
        public static string[] DefaultIdentifiers = { };
    }

    public class MonoDevelopProjectTouchBarDelegate : MonoDevelopTouchBarDelegate
    {
        internal static IBuildTarget GetRunTarget()
        {
            return IdeApp.ProjectOperations.CurrentSelectedSolution ?? IdeApp.ProjectOperations.CurrentSelectedBuildTarget;
        }

        public bool IsRunning { get; set; } = false;

        public new static string[] DefaultIdentifiers = {
            "md.build",
            "md.debug"
        };

        private NSImage[] _debugImages;

        public MonoDevelopProjectTouchBarDelegate()
        {
            _debugImages = new NSImage[]
            {
                ImageService.GetIcon("md-step-over-debug").ToNSImage(),
                ImageService.GetIcon("md-step-out-debug").ToNSImage(),
                ImageService.GetIcon("md-step-into-debug").ToNSImage()
            };
        }

        public override NSTouchBarItem MakeItem(NSTouchBar touchBar, string identifier)
        {
            NSCustomTouchBarItem item = new NSCustomTouchBarItem(identifier);
            switch (identifier)
            {
                case "md.build":
                    {
                        var buildButton = NSButton.CreateButton(BuildPlayImage(), () => throw new NotImplementedException());
                        buildButton.Activated += Build_Activated;
                        item.View = buildButton;
                        return item;
                    }
                case "md.debug":
                    {
                        if (!IsRunning && !DebuggingService.IsRunning && !DebuggingService.IsPaused)
                            return null;
                        var test = new[] { BuildDebugImage() }.Concat(_debugImages).ToArray();
                        var nsControl = NSSegmentedControl.FromImages(test, NSSegmentSwitchTracking.SelectAny, () => throw new NotImplementedException());
                        for (var i = 1; i < test.Length; i++)
                            nsControl.SetEnabled(DebuggingService.IsPaused, i);
                        nsControl.Activated += Debug_Activated;
                        item.View = nsControl;
                        return item;
                    }
            }

            return null;
        }



        private NSImage BuildDebugImage()
        {
            return DebuggingService.IsPaused ? NSImage.ImageNamed(NSImageName.TouchBarPlayTemplate) : NSImage.ImageNamed(NSImageName.TouchBarPauseTemplate);
        }

        private NSImage BuildPlayImage()
        {
            return IsRunning ? NSImage.ImageNamed(NSImageName.TouchBarRecordStopTemplate) : NSImage.ImageNamed(NSImageName.TouchBarPlayTemplate);
        }

        private void Debug_Activated(object sender, EventArgs e)
        {
            var control = sender as NSSegmentedControl;
            switch (control.SelectedSegment)
            {
                case 0:
                    if (IdeApp.ProjectOperations.CurrentSelectedBuildTarget == null)
                        return;
                    if (DebuggingService.IsPaused)
                        DebuggingService.Resume();
                    else
                        DebuggingService.Pause();
                    break;
                case 1:
                    DebuggingService.StepOver();
                    break;
                case 2:
                    DebuggingService.StepOut();
                    break;
                case 3:
                    DebuggingService.StepInto();
                    break;
            }
            control.SetSelected(false, control.SelectedSegment);
        }

        private void Build_Activated(object sender, EventArgs e)
        {
            if (IdeApp.ProjectOperations.CurrentSelectedBuildTarget == null)
                return;
            if (DebuggingService.IsRunning && !DebuggingService.IsPaused)
                DebuggingService.Stop();
            else
            {
                var target = GetRunTarget();
                if (target != null)
                    IdeApp.ProjectOperations.Debug(target);
            }
        }
    }
}