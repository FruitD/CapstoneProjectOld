using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections;
using CapstoneProject.Models;
using CapstoneProject.Controls;
using CapstoneProject.DAL;
using System.Collections.ObjectModel;

namespace CapstoneProject.Pages
{
    /// <summary>
    /// Interaction logic for Chart.xaml
    /// </summary>

    //By Levi Delezene
    public partial class Chart : Page
    {

        List<Task> taskList;

        private Point savedMousePosition;
        private TranslateTransform move;
        private ScaleTransform zoom;
        private Project _project;

        private double dayWidth = Properties.Settings.Default.dayWidth;
        int buttonSpacing = 50;
        int buttonHeight = 25;

        private TaskControl adjustingTask;
        private Point previous;
        private int leftDateChange = -1;
        private int rightDateChange = -1;
        private int dayThreshold = 50;

        private Brush taskBrush;

        private Dictionary<string, int> dayMonths = new Dictionary<string, int>(); //Dictionary to add months and their respective days
        private string duration = "";
        WindowState prevWindowState = new WindowState();

        public Chart(Project project) {
            InitializeComponent();

            Project = project;
            this.PreviewMouseWheel += ZoomCanvas;
            this.MouseMove += DragCanvas;
            this.MouseUp += ReleaseMouseDrag;

            addItemsHashTable();
            // addItemsCombo();
        }
        public Chart(Project project,string _duration)
        {
            InitializeComponent();

            Project = project;
            duration=_duration;
            this.PreviewMouseWheel += ZoomCanvas;

            addItemsHashTable();
           // addItemsCombo();
        }

        /// <summary>
        /// Written By: Chris Neeser
        /// Date: 10/22/2019
        /// Populates all Tasks and Dependencies for Project from database
        /// </summary>
        /// <returns>List of Root Tasks</returns>
        public List<Task> GetTasksAndDependanciesFromDatabase()
        {
            //Retreive Task List
            OTask oTask = new OTask();
            ObservableCollection<Task> tasks = oTask.Select(_project.Id);


            //Loop through task list and add dependencies
            ODependency oDependency = new ODependency();
            foreach (Task task in tasks)
            {
                List<Dependency> dependencyList = oDependency.Select(task.Id);
                foreach (Dependency dep in dependencyList)
                {
                    Task dependentTask = tasks.Single(s => s.Id == dep.TaskId);
                    task.AddDependentTask(dependentTask);
                }

            }
            return (tasks.Where(s => s.RootNode == true)).ToList();
        }

        // Created by Sandro Pawlidis (10/15/2019)
        public int DrawGraph(List<Task> mainLevel) {
            mainCanvas.Children.Clear();
            int top = 50;
            //Only try to draw the tasks if there are some to draw;
            if (mainLevel.Count > 0)
            {
                for (int i = 0; i < mainLevel.Count; i++)
                {
                    int spaceUsed = DrawSubTasks(mainLevel[i], top);
                    top += (spaceUsed + 1) * (buttonHeight + buttonSpacing) + 50;
                }


                int screenHeight = top;
                mainCanvas.Height = (screenHeight > System.Windows.SystemParameters.PrimaryScreenHeight) ? screenHeight : System.Windows.SystemParameters.PrimaryScreenHeight;
                DrawCalendar(365);
            }
            else
            {
                DrawCalendar(365);
            }
            return top;
        }

        public void RefreshGraph()
        {
            DrawGraph(GetTasksAndDependanciesFromDatabase());
        }

        // Created by Sandro Pawlidis (10/15/2019)
        private int DrawSubTasks(Task parent, double topMargin) {
            //min/max/mostlikely duration for subtasks
            double tempDuration = 0;
            if (duration == "" || duration == "minDuration")
            {
                taskBrush = new SolidColorBrush(Colors.LightGray);
                tempDuration = parent.MinDuration;
            }
            else if (duration == "maxDuration")
            {
                taskBrush = new SolidColorBrush(Colors.Red);
                tempDuration = parent.MaxDuration;

            }
            else if (duration == "mostLikelyDuration")
            {
                taskBrush = new SolidColorBrush(Colors.Yellow);
                tempDuration = parent.MostLikelyDuration;
            }
            double newTopMargin = topMargin;
            double minWidth = int.MaxValue;

            int subtaskCount = 0;
            for (int i = 0; i < parent.DependentTasks.Count; i++) {
                subtaskCount += DrawSubTasks(parent.DependentTasks[i], newTopMargin);
                Point start = new Point(((DateTime)parent.StartedDate - _project.StartDate).TotalDays * dayWidth + tempDuration * dayWidth - dayWidth / 4, topMargin + buttonHeight / 2);
                Point end = new Point(((DateTime)parent.DependentTasks[i].StartedDate - _project.StartDate).TotalDays * dayWidth, newTopMargin + buttonHeight / 2);

                double width = start.X + (end.X - start.X) / 2;
                minWidth = (width < minWidth) ? width : minWidth;

                Point midTop = new Point(minWidth, start.Y);
                Point midBot = new Point(minWidth, end.Y);

                List<Point> points = new List<Point>();
                points.Add(start);
                points.Add(midTop);
                points.Add(midBot);
                points.Add(end);

                Path p = getPath(points);

                KeyValuePair<Task, Task> pair = new KeyValuePair<Task, Task>(parent, parent.DependentTasks[i]);               
                p.DataContext = pair;

                Button button = new Button();

                Canvas.SetZIndex(p, 99);
                mainCanvas.Children.Add(p);

                newTopMargin = topMargin + (i + 1) * (buttonHeight + buttonSpacing);
                newTopMargin += subtaskCount * (buttonHeight + buttonSpacing);
            }

            //taskcontrol
            TaskControl t = new TaskControl(parent, this);
            t.taskBorder.Background = taskBrush;
            t.ToolTip = createToolTip(parent);
            t.MouseDown += resizeTask;
            Canvas.SetLeft(t, ((DateTime)parent.StartedDate - _project.StartDate).TotalDays * dayWidth);
            Canvas.SetTop(t, topMargin);
            
            //Min/Max/mostlikely view----By Alankar Pokhrel
            t.Width = tempDuration * dayWidth - dayWidth / 4;
            
            Canvas.SetZIndex(t, 100);
            mainCanvas.Children.Add(t);

            subtaskCount += (parent.DependentTasks.Count > 1) ? parent.DependentTasks.Count - 1 : 0;
            //MessageBox.Show(subtaskCount.ToString());
            return subtaskCount;
        }

        // Created by Sandro Pawlidis (11/3/2019)
        private void progressResizeTask() {
            Task task = (Task)adjustingTask.DataContext;
            // TODO: Check if date is decreased past parents start dated/after childs start date
            // TODO: ?? Maybe increase started date of all child tasks if parent is increased past child start date.
            // TODO: Update Tasks in database.
            double mouseX = Mouse.GetPosition(mainCanvas).X;
            double taskX = Canvas.GetLeft(adjustingTask);

            if (rightDateChange != -1) {
                if (Math.Abs(mouseX - previous.X) > dayThreshold) {
                    int direction = Math.Sign(mouseX - previous.X);
                    previous = Mouse.GetPosition(mainCanvas);

                    task.MinDuration += direction;
                    DrawGraph(taskList);
                }
            } else if(leftDateChange != -1) {
                if (Math.Abs(mouseX - previous.X) > dayThreshold) {
                    int direction = Math.Sign(mouseX - previous.X);
                    previous = Mouse.GetPosition(mainCanvas);

                    task.StartedDate = ((DateTime)task.StartedDate).AddDays(direction);
                    task.MinDuration += -direction;
                    DrawGraph(taskList);
                }
            }
        }

        // Created by Sandro Pawlidis (11/3/2019)
        private void resizeTask(object sender, RoutedEventArgs e) {
            TaskControl t = (TaskControl)sender;
            Task task = (Task)t.DataContext;

            double mouseX = Mouse.GetPosition(mainCanvas).X;
            double taskX = Canvas.GetLeft(t);

            double mouseOnTask = mouseX - taskX;
            previous = Mouse.GetPosition(mainCanvas);

            if (mouseOnTask < t.Width * 0.05) {
                leftDateChange = 0;
                adjustingTask = t;
            }

            else if (mouseOnTask > t.Width * 0.95) {
                rightDateChange = 0;
                adjustingTask = t;
            }

        }

        /// <summary>
        /// Written By: Chris Neeser
        /// Date: 10/22/2019
        /// Create the tool tip for a task
        /// </summary>
        /// <param name="task">The task to create the tooltip for</param>
        /// <returns>The tooltip to assign to your controls ToolTip property</returns>
        private TaskToolTip createToolTip(Task task)
        {
            TaskToolTip ttp = new TaskToolTip();
            ttp.Style = (Style)FindResource("AppToolTip");
            ttp.Task = task;
            return ttp;
        }

        // Created by Sandro Pawlidis (10/15/2019)
        private Path getPath(List<Point> points) {
            Path path = new Path();
            path.Stroke = Brushes.Black;
            path.StrokeThickness = 2;

            PathSegmentCollection segments = new PathSegmentCollection();
            for (int i = 1; i < points.Count; i++)
                segments.Add(new LineSegment(points[i], true));

            path.Data = new PathGeometry() {
                Figures = new PathFigureCollection() {
                    new PathFigure() {
                        StartPoint = points[0],
                        Segments = segments
                    }
                }
            };

            MenuItem menuItem = new MenuItem();
            menuItem.Header = "Remove dependency";
            menuItem.Click += mi_deleteDependency_Click;
            menuItem.DataContext = path;

            ContextMenu menu = new ContextMenu();
            menu.Items.Add(menuItem);        
            path.ContextMenu = menu;

            return path;
        }


        public Project Project { get => _project; set => _project = value; }

        /// <summary>
        /// Deletes a dependency
        /// Written by Levi Delezene
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void mi_deleteDependency_Click(object sender, RoutedEventArgs e) {
            Path path = (Path)((MenuItem)sender).DataContext;
            KeyValuePair<Task, Task> pair = (KeyValuePair<Task,Task>)path.DataContext;
            //Look at all tasks and remove this task^ from the one it's dependent on???
            Task parent = pair.Key;
            Task child = pair.Value;
            parent.DependentTasks.Remove(child);
            parent.save();

            mainCanvas.Children.Remove(path);
            DrawGraph(GetTasksAndDependanciesFromDatabase());
        }

        private void mi_addTask_Click(object sender, RoutedEventArgs e)
        {
            new frmTask(this).ShowDialog();
            DrawGraph(GetTasksAndDependanciesFromDatabase());
        }

        // Created by Sandro Pawlidis (9/25/2019)
        private void DrawCalendar(int days)
        {
            int lblCurrentDay = 1;
            int dicIndex = 0;
            for (int i = 0; i < days; i++)
            {
                Line line = new Line();
                line.Stroke = System.Windows.Media.Brushes.Cyan;
                line.StrokeThickness = 2;

                line.X1 = i * dayWidth;
                line.X2 = line.X1;

                line.Y1 = 0;
                line.Y2 = mainCanvas.Height; //Changed .Height to .ActualHeight to make use of auto width and height - Chase

                mainCanvas.Children.Add(line);

                Label lbDay = new Label();
                if (lblCurrentDay > dayMonths.Values.ElementAt(dicIndex))
                {
                    lblCurrentDay = 1;
                    dicIndex++;
                }
                if (lblCurrentDay == 1)
                {
                    
                    lbDay.Content = dayMonths.Keys.ElementAt(dicIndex)+"\r\n"+lblCurrentDay;
                }
                else
                {

                    lbDay.Content = "\r\n"+lblCurrentDay;
                }
                lblCurrentDay++;

                Canvas.SetLeft(lbDay, line.X1);
                mainCanvas.Children.Add(lbDay);
            }

        }

        public void AddTask(Task task)
        {
            if (task == null) return;
            CalculationService cs = new CalculationService();
            Rect rectVal = cs.dateToChartCoordinate(Project.StartDate,task.StartedDate, task.MaxDuration);
            Grid taskGrid = new Grid();
            Rectangle taskRect = new Rectangle();
            TextBlock taskTextBlock = new TextBlock();
            taskTextBlock.Text = task.Name;

            taskRect.Width = rectVal.Width;
            taskRect.Height = 50;
            taskRect.StrokeThickness = 2;
            taskRect.Stroke = new SolidColorBrush(Colors.Red);

            taskGrid.Children.Add(taskRect);
            taskGrid.Children.Add(taskTextBlock);

            TaskControl taskControl = new TaskControl(task, this);
            taskControl.Width = rectVal.Width;
            taskControl.Height = 50;

            Canvas.SetTop(taskControl, 30);
            Canvas.SetLeft(taskControl, rectVal.X);

            mainCanvas.Children.Add(taskControl);

        }

        // Created by Sandro Pawlidis (9/25/2019)
        private void SetupCanvas()
        {
            //Set the height so that we have a vertical scrollbar
            mainCanvas.Height = System.Windows.SystemParameters.PrimaryScreenHeight;
            mainCanvas.Width = 365 * dayWidth; //TO-DO: Necessary for MouseEvents to fire. Discuss. 

            move = new TranslateTransform();
            zoom = new ScaleTransform();

            TransformGroup group = new TransformGroup();
            group.Children.Add(move);
            group.Children.Add(zoom);

            mainCanvas.LayoutTransform = group;
            mainCanvas.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        // Created by Sandro Pawlidis (9/25/2019)
        private void ZoomCanvas(object sender, MouseWheelEventArgs e)
        {
            double zoomSpeed = 0.05;

            if (e.Delta > 0)
            {
                zoom.ScaleX += zoomSpeed;
                zoom.ScaleY += zoomSpeed;
            }
            else if (e.Delta < 0)
            {
                zoom.ScaleX -= zoomSpeed;
                zoom.ScaleY -= zoomSpeed;
            }
        }

        // Created by Sandro Pawlidis (9/25/2019)
        private void DragCanvas(object sender, RoutedEventArgs e)
        {
            if (leftDateChange != -1 || rightDateChange != -1) {            
                progressResizeTask();
                return;
            }
        }

        // Created by Sandro Pawlidis (9/25/2019)
        private void SetMouseDrag(object sender, RoutedEventArgs e)
        {
            savedMousePosition = Mouse.GetPosition(mainCanvas);
        }

        // Created by Sandro Pawlidis (9/25/2019)
        private void ReleaseMouseDrag(object sender, RoutedEventArgs e)
        {
            //translating = false;

            if (leftDateChange != -1 || rightDateChange != -1)
                leftDateChange = rightDateChange = -1;
        }

        // Created by Chris Neeser (10/1/2019)
        Point scrollMousePoint = new Point();
        double hOff = 1;

        private void scrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            scrollMousePoint = e.GetPosition(scrollViewer);
            hOff = scrollViewer.HorizontalOffset;
            scrollViewer.CaptureMouse();
        }

        private void scrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (scrollViewer.IsMouseCaptured)
            {
                scrollViewer.ScrollToHorizontalOffset(hOff + (scrollMousePoint.X - e.GetPosition(scrollViewer).X));
            }
        }

        private void scrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            scrollViewer.ReleaseMouseCapture();
        }

        private void scrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
        }


        // Adds the months and days to the dictionary - Chase Torres (9/26/2019)
        private void addItemsHashTable()
        {
            dayMonths.Add("January", 31);
            dayMonths.Add("February", 28);
            dayMonths.Add("March", 31);
            dayMonths.Add("April", 30);
            dayMonths.Add("May", 31);
            dayMonths.Add("June", 30);
            dayMonths.Add("July", 31);
            dayMonths.Add("August", 31);
            dayMonths.Add("September", 30);
            dayMonths.Add("October", 31);
            dayMonths.Add("November", 30);
            dayMonths.Add("December", 31);
        }

        //Going to try to use a groupbox as the container to add tasks to the canvas
        private void addGroupBoxCanvas()
        {
            GroupBox taskGroup = new GroupBox();
            
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            prevWindowState = Window.GetWindow(this).WindowState;
            Window.GetWindow(this).WindowState = WindowState.Maximized;

            SetupCanvas();


            taskList = GetTasksAndDependanciesFromDatabase();

            int screenHeight = 0;

            screenHeight = DrawGraph(taskList);

            Window.GetWindow(this).KeyDown += Page_KeyDown;
        }

        private void Page_KeyDown(object sender, KeyEventArgs e)
        {
           
            if (e.Key == Key.Escape)
            {
                if (NavigationService.CanGoBack)
                {
                    NavigationService.GoBack();
                    Window.GetWindow(this).WindowState = prevWindowState;
                    Window.GetWindow(this).KeyDown -= Page_KeyDown;
                }
            }
        }
    }
}
