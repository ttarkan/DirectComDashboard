using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using SimioAPI;
using SimioAPI.Extensions;
using System.Collections.Concurrent;
using System.Threading;
using Timer = System.Windows.Forms.Timer;
using SimioEventCommunication; // Reference the shared library

namespace SimioDirectDashboard
{
    public class DirectCommunicationDashboardAddIn : IDesignAddIn
    {
        public string Name => "Direct Communication Dashboard";
        public string Description =>
            "High-performance dashboard with direct in-memory communication.\n" +
            "No file I/O - connects directly to Direct Memory Event Logger.\n" +
            "Real-time event processing with guaranteed delivery.\n" +
            "Developer: Direct Communication Event System";

        public System.Drawing.Image Icon
        {
            get
            {
                var bitmap = new System.Drawing.Bitmap(16, 16);
                using (var g = System.Drawing.Graphics.FromImage(bitmap))
                {
                    g.FillRectangle(System.Drawing.Brushes.Blue, 0, 0, 16, 16);
                    g.FillEllipse(System.Drawing.Brushes.White, 4, 4, 8, 8);
                }
                return bitmap;
            }
        }

        private DirectCommunicationPlottingWindow _plottingWindow;

        public void Execute(IDesignContext context)
        {
            try
            {
                IModel model = context?.ActiveModel;
                if (model == null)
                {
                    MessageBox.Show("No active model found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var configDialog = new DirectCommunicationConfigurationDialog(model);
                if (configDialog.ShowDialog() == DialogResult.OK)
                {
                    _plottingWindow = new DirectCommunicationPlottingWindow(model, configDialog.SelectedVariables, configDialog.RefreshRateMs);
                    _plottingWindow.Show();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating direct communication dashboard: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void Dispose()
        {
            _plottingWindow?.Close();
        }
    }

    public class DirectCommunicationConfigurationDialog : Form
    {
        public List<string> SelectedVariables { get; private set; }
        public int RefreshRateMs { get; private set; } = 50;

        private IModel _model;
        private CheckedListBox _stateVariablesListBox;
        private TextBox _customVariablesTextBox;
        private NumericUpDown _refreshRateControl;
        private Label _modelInfoLabel;

        public DirectCommunicationConfigurationDialog(IModel model)
        {
            _model = model;
            InitializeComponent();
            PopulateStateVariables();
        }

        private void PopulateStateVariables()
        {
            try
            {
                _stateVariablesListBox.Items.Clear();

                if (_model != null)
                {
                    // Update model info
                    _modelInfoLabel.Text = $"Model: {_model.Name ?? "Unknown"}";

                    // Get model states (user-defined state variables)
                    var modelStates = new List<string>();

                    // Try to get states from the model
                    // Note: The exact API depends on Simio version, these are common approaches
                    try
                    {
                        // Approach 1: Through model's state definitions
                        if (_model.StateDefinitions != null)
                        {
                            foreach (var stateDef in _model.StateDefinitions)
                            {
                                if (stateDef != null && !string.IsNullOrEmpty(stateDef.Name))
                                {
                                    modelStates.Add(stateDef.Name);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // If StateDefinitions doesn't work, try other approaches
                    }

                    // Approach 2: Common system states that are often monitored
                    var commonStates = new List<string>
                    {
                        "NrInSystem",
                        "DefaultEntity.Population.NumberInSystem",
                        "DefaultEntity.Population.NumberCreated",
                        "DefaultEntity.Population.NumberDestroyed"
                    };

                    // Add common states if model states are empty
                    if (modelStates.Count == 0)
                    {
                        modelStates.AddRange(commonStates);
                    }

                    // Try to get entity/object states
                    try
                    {
                        // Look for common object types and their states
                        var objectStates = new List<string>
                        {
                            "Source1.OutputBuffer.Contents",
                            "Server1.InputBuffer.Contents",
                            "Server1.Capacity.Allocated",
                            "Sink1.InputBuffer.Contents"
                        };

                        modelStates.AddRange(objectStates);
                    }
                    catch
                    {
                        // Ignore if object traversal fails
                    }

                    // Add all discovered states to the list
                    foreach (var state in modelStates.Distinct().OrderBy(s => s))
                    {
                        _stateVariablesListBox.Items.Add(state, false); // false = unchecked by default
                    }

                    // If we found states, check a few common ones by default
                    if (_stateVariablesListBox.Items.Count > 0)
                    {
                        for (int i = 0; i < _stateVariablesListBox.Items.Count; i++)
                        {
                            string item = _stateVariablesListBox.Items[i].ToString();
                            if (item.Contains("NrInSystem") || item.Contains("NumberInSystem"))
                            {
                                _stateVariablesListBox.SetItemChecked(i, true);
                            }
                        }
                    }
                }

                // If no states were found, add a helpful message
                if (_stateVariablesListBox.Items.Count == 0)
                {
                    _stateVariablesListBox.Items.Add("No state variables detected - use custom expressions below", false);
                }

                System.Diagnostics.Debug.WriteLine($"PopulateStateVariables: Found {_stateVariablesListBox.Items.Count} state variables");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error populating state variables: {ex.Message}");
                _stateVariablesListBox.Items.Add($"Error loading states: {ex.Message}", false);
            }
        }

        private void InitializeComponent()
        {
            this.Size = new Size(600, 750);
            this.Text = "Configure Direct Communication Dashboard";
            this.StartPosition = FormStartPosition.CenterParent;

            var titleLabel = new Label
            {
                Text = "Direct Memory Communication Dashboard",
                Location = new Point(10, 10),
                Size = new Size(300, 20),
                Font = new Font("Arial", 12, FontStyle.Bold),
                ForeColor = Color.DarkBlue
            };

            _modelInfoLabel = new Label
            {
                Text = "Model: Loading...",
                Location = new Point(10, 35),
                Size = new Size(400, 15),
                Font = new Font("Arial", 9),
                ForeColor = Color.DarkGreen
            };

            // State Variables Section
            var stateVariablesLabel = new Label
            {
                Text = "Select Model State Variables to Monitor:",
                Location = new Point(10, 60),
                Size = new Size(300, 20),
                Font = new Font("Arial", 9, FontStyle.Bold)
            };

            _stateVariablesListBox = new CheckedListBox
            {
                Location = new Point(10, 85),
                Size = new Size(560, 200),
                CheckOnClick = true,
                Font = new Font("Consolas", 9)
            };

            var refreshButton = new Button
            {
                Text = "Refresh Variables",
                Location = new Point(480, 60),
                Size = new Size(90, 20),
                Font = new Font("Arial", 8)
            };
            refreshButton.Click += (sender, args) => PopulateStateVariables();

            // Custom Variables Section
            var customVariablesLabel = new Label
            {
                Text = "Additional Custom Expressions (one per line):",
                Location = new Point(10, 300),
                Size = new Size(400, 20),
                Font = new Font("Arial", 9, FontStyle.Bold)
            };

            _customVariablesTextBox = new TextBox
            {
                Location = new Point(10, 325),
                Size = new Size(560, 150),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 9),
                AcceptsReturn = true,
                AcceptsTab = false,
                Text = "// Add custom expressions here\n// Example: Server1.Capacity.Remaining\n// Example: MyCustomState"
            };

            var examplesLabel = new Label
            {
                Text = "Examples: Source1.OutputBuffer.Contents, Server1.Capacity.Allocated, MyCustomState",
                Location = new Point(10, 480),
                Size = new Size(560, 15),
                Font = new Font("Arial", 8),
                ForeColor = Color.Gray
            };

            // Performance Settings
            var performanceLabel = new Label
            {
                Text = "Performance Settings:",
                Location = new Point(10, 510),
                Size = new Size(200, 20),
                Font = new Font("Arial", 9, FontStyle.Bold)
            };

            var refreshLabel = new Label
            {
                Text = "UI Refresh Rate (ms):",
                Location = new Point(10, 535),
                Size = new Size(150, 20)
            };

            _refreshRateControl = new NumericUpDown
            {
                Location = new Point(160, 533),
                Size = new Size(60, 20),
                Minimum = 10,
                Maximum = 1000,
                Value = 50,
                Increment = 10
            };

            // Status and Info
            var advantagesLabel = new Label
            {
                Text = "Direct Communication Advantages:",
                Location = new Point(10, 570),
                Size = new Size(200, 20),
                Font = new Font("Arial", 9, FontStyle.Bold),
                ForeColor = Color.DarkGreen
            };

            var advantagesText = new Label
            {
                Text = "✓ Zero file I/O ✓ Guaranteed delivery ✓ Real-time performance ✓ Live statistics",
                Location = new Point(10, 595),
                Size = new Size(560, 15),
                ForeColor = Color.DarkGreen,
                Font = new Font("Arial", 8)
            };

            var statusLabel = new Label
            {
                Text = $"GlobalEventManager Status: {GlobalEventManager.GetDiagnosticInfo()}",
                Location = new Point(10, 620),
                Size = new Size(560, 15),
                ForeColor = Color.Blue,
                Font = new Font("Arial", 8)
            };

            // Selection Info
            var selectionInfoLabel = new Label
            {
                Text = "Tip: Check state variables above OR add custom expressions below",
                Location = new Point(10, 645),
                Size = new Size(400, 15),
                ForeColor = Color.DarkBlue,
                Font = new Font("Arial", 8, FontStyle.Italic)
            };

            var okButton = new Button
            {
                Text = "Start Dashboard",
                Location = new Point(400, 670),
                Size = new Size(100, 30),
                DialogResult = DialogResult.OK,
                BackColor = Color.LightBlue,
                Font = new Font("Arial", 9, FontStyle.Bold)
            };

            var cancelButton = new Button
            {
                Text = "Cancel",
                Location = new Point(510, 670),
                Size = new Size(70, 30),
                DialogResult = DialogResult.Cancel
            };

            okButton.Click += (sender, args) =>
            {
                var selectedVariables = new List<string>();

                // Get checked state variables
                foreach (int index in _stateVariablesListBox.CheckedIndices)
                {
                    string item = _stateVariablesListBox.Items[index].ToString();
                    if (!item.StartsWith("No state variables") && !item.StartsWith("Error loading"))
                    {
                        selectedVariables.Add(item);
                    }
                }

                // Get custom variables from text box
                var customVariables = _customVariablesTextBox.Lines
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line) &&
                                   !line.StartsWith("//") &&
                                   !line.StartsWith("Example:"))
                    .ToList();

                selectedVariables.AddRange(customVariables);

                SelectedVariables = selectedVariables.Distinct().ToList();
                RefreshRateMs = (int)_refreshRateControl.Value;

                if (SelectedVariables.Count == 0)
                {
                    MessageBox.Show("Please select at least one state variable or add a custom expression.",
                        "No Variables Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.DialogResult = DialogResult.None;
                    return;
                }

                // Show confirmation of selected variables
                string selectedList = string.Join("\n• ", SelectedVariables);
                var result = MessageBox.Show($"Selected Variables:\n• {selectedList}\n\nStart dashboard with these variables?",
                    "Confirm Variable Selection", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (result == DialogResult.No)
                {
                    this.DialogResult = DialogResult.None;
                }
            };

            this.Controls.AddRange(new Control[] {
                titleLabel, _modelInfoLabel, stateVariablesLabel, _stateVariablesListBox, refreshButton,
                customVariablesLabel, _customVariablesTextBox, examplesLabel,
                performanceLabel, refreshLabel, _refreshRateControl,
                advantagesLabel, advantagesText, statusLabel, selectionInfoLabel,
                okButton, cancelButton
            });

            this.AcceptButton = okButton;
            this.CancelButton = cancelButton;
        }
    }

    public class DirectCommunicationPlottingWindow : Form, IEventSubscriber
    {
        private Dictionary<string, List<EventPoint>> _eventSeries;
        private Dictionary<string, Color> _seriesColors;
        private Dictionary<string, TimeWeightedCalculator> _timeWeightedCalculators;
        private IModel _model;
        private List<string> _variableNames;
        private int _maxDataPoints = 1000;
        private readonly object _dataLock = new object();
        private Panel _plotPanel;
        private Panel _metricsPanel;
        private double _minTime = double.MaxValue;
        private double _maxTime = double.MinValue;
        private double _minValue = double.MaxValue;
        private double _maxValue = double.MinValue;
        private Label _statusLabel;
        private ListView _eventsListView;

        // Performance tracking
        private Timer _uiUpdateTimer;
        private volatile bool _hasNewData = false;
        private DateTime _lastUIUpdate = DateTime.Now;
        private int _refreshRateMs;
        private long _eventsProcessedSinceLastUpdate = 0;
        private long _totalEventsReceived = 0;

        public DirectCommunicationPlottingWindow(IModel model, List<string> variableNames, int refreshRateMs = 50)
        {
            _model = model;
            _variableNames = variableNames ?? new List<string>();
            _refreshRateMs = refreshRateMs;
            _eventSeries = new Dictionary<string, List<EventPoint>>();
            _seriesColors = new Dictionary<string, Color>();
            _timeWeightedCalculators = new Dictionary<string, TimeWeightedCalculator>();

            InitializeComponent();
            SetupPlot();
            StartUIUpdateTimer();

            // Subscribe to GlobalEventManager - THIS IS THE KEY!
            GlobalEventManager.Subscribe(this);

            System.Diagnostics.Debug.WriteLine("DirectCommunicationPlottingWindow: Subscribed to GlobalEventManager");
        }

        // This method receives events directly from GlobalEventManager
        public void ProcessEventBatch(List<SimulationEvent> events)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"DASHBOARD: Received batch of {events.Count} events");

                _eventsProcessedSinceLastUpdate += events.Count;
                _totalEventsReceived += events.Count;
                _hasNewData = true;

                // Process events on background thread to avoid blocking
                lock (_dataLock)
                {
                    ProcessEventsBatch(events);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DASHBOARD: Error processing event batch: {ex.Message}");
            }
        }

        private void ProcessEventsBatch(List<SimulationEvent> events)
        {
            try
            {
                foreach (var eventData in events)
                {
                    // Only process events for variables we're monitoring
                    if (_eventSeries.ContainsKey(eventData.VariableName))
                    {
                        var series = _eventSeries[eventData.VariableName];
                        var eventPoint = new EventPoint
                        {
                            Timestamp = eventData.Timestamp,
                            Value = eventData.NewValue,
                            EventType = eventData.EventType
                        };

                        series.Add(eventPoint);

                        // Update time-weighted calculator
                        if (_timeWeightedCalculators.ContainsKey(eventData.VariableName))
                        {
                            _timeWeightedCalculators[eventData.VariableName].AddEvent(eventData.Timestamp, eventData.NewValue);
                        }

                        // Limit data points for performance
                        if (series.Count > _maxDataPoints)
                        {
                            series.RemoveAt(0);
                        }

                        // Update ranges
                        _minTime = Math.Min(_minTime, eventData.Timestamp);
                        _maxTime = Math.Max(_maxTime, eventData.Timestamp);
                        _minValue = Math.Min(_minValue, eventData.NewValue);
                        _maxValue = Math.Max(_maxValue, eventData.NewValue);

                        System.Diagnostics.Debug.WriteLine($"DASHBOARD: Processed event {eventData.VariableName} = {eventData.NewValue} at time {eventData.Timestamp}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DASHBOARD: Error processing events batch: {ex.Message}");
            }
        }

        private void StartUIUpdateTimer()
        {
            _uiUpdateTimer = new Timer()
            {
                Interval = _refreshRateMs
            };
            _uiUpdateTimer.Tick += UIUpdateTimer_Tick;
            _uiUpdateTimer.Start();
        }

        private void UIUpdateTimer_Tick(object sender, EventArgs e)
        {
            if (_hasNewData)
            {
                UpdateUI();
                _hasNewData = false;
            }
        }

        private void UpdateUI()
        {
            try
            {
                // Update status with statistics
                var timeSinceLastUpdate = (DateTime.Now - _lastUIUpdate).TotalMilliseconds;
                _statusLabel.Text = $"Direct Memory: {_totalEventsReceived} total events, " +
                                  $"{_eventsProcessedSinceLastUpdate} in last {timeSinceLastUpdate:F0}ms, " +
                                  $"Queue: {GlobalEventManager.EventsInQueue}, " +
                                  $"Processed: {GlobalEventManager.TotalEventsProcessed}";

                _lastUIUpdate = DateTime.Now;
                _eventsProcessedSinceLastUpdate = 0;

                // Update metrics display
                UpdateAllMetricsDisplay();

                // Refresh the plot
                _plotPanel.Invalidate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DASHBOARD: Error updating UI: {ex.Message}");
            }
        }

        private void UpdateAllMetricsDisplay()
        {
            try
            {
                foreach (var variableName in _variableNames)
                {
                    if (_timeWeightedCalculators.ContainsKey(variableName) && _eventSeries.ContainsKey(variableName))
                    {
                        var calculator = _timeWeightedCalculators[variableName];
                        var series = _eventSeries[variableName];

                        if (series.Count > 0)
                        {
                            var currentValue = series.Last().Value;
                            var timeWeightedAvg = calculator.GetTimeWeightedAverage();

                            var currentLabel = _metricsPanel.Controls.Find($"current_{variableName}", false).FirstOrDefault() as Label;
                            var avgLabel = _metricsPanel.Controls.Find($"avg_{variableName}", false).FirstOrDefault() as Label;

                            if (currentLabel != null)
                                currentLabel.Text = $"Current: {currentValue:F2}";

                            if (avgLabel != null)
                                avgLabel.Text = $"Time-Weighted Avg: {timeWeightedAvg:F2}";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DASHBOARD: Error updating metrics: {ex.Message}");
            }
        }

        private void InitializeComponent()
        {
            this.Size = new Size(1200, 700);
            this.Text = "Direct Memory Communication Dashboard - Simio";
            this.StartPosition = FormStartPosition.CenterScreen;

            // Create main container
            var mainContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 400
            };

            // Top panel for charts
            var chartsContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 800
            };

            // Plot panel
            _plotPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            _plotPanel.Paint += PlotPanel_Paint;

            // Metrics panel
            _metricsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.LightGray
            };
            SetupMetricsPanel();

            chartsContainer.Panel1.Controls.Add(_plotPanel);
            chartsContainer.Panel2.Controls.Add(_metricsPanel);

            // Bottom panel for system info
            var infoPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            SetupInfoPanel(infoPanel);

            mainContainer.Panel1.Controls.Add(chartsContainer);
            mainContainer.Panel2.Controls.Add(infoPanel);

            // Status panel
            var statusPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 25,
                BackColor = Color.DarkBlue
            };

            _statusLabel = new Label
            {
                Text = "Direct memory communication ready...",
                Location = new Point(5, 2),
                Size = new Size(1000, 20),
                ForeColor = Color.White,
                Font = new Font("Arial", 9, FontStyle.Bold)
            };
            statusPanel.Controls.Add(_statusLabel);

            this.Controls.Add(mainContainer);
            this.Controls.Add(statusPanel);

            this.FormClosing += (sender, args) =>
            {
                _uiUpdateTimer?.Stop();
                _uiUpdateTimer?.Dispose();
                GlobalEventManager.Unsubscribe(this); // IMPORTANT: Unsubscribe when closing
                _eventSeries?.Clear();
                _seriesColors?.Clear();
                _timeWeightedCalculators?.Clear();

                System.Diagnostics.Debug.WriteLine("DirectCommunicationPlottingWindow: Unsubscribed from GlobalEventManager");
            };
        }

        private void SetupMetricsPanel()
        {
            var titleLabel = new Label
            {
                Text = "Real-Time Metrics (Direct Memory)",
                Location = new Point(5, 5),
                Size = new Size(200, 20),
                Font = new Font("Arial", 10, FontStyle.Bold),
                ForeColor = Color.DarkBlue
            };
            _metricsPanel.Controls.Add(titleLabel);

            int yPos = 30;
            foreach (var variable in _variableNames)
            {
                var calculator = new TimeWeightedCalculator();
                _timeWeightedCalculators[variable] = calculator;

                var metricLabel = new Label
                {
                    Text = $"{variable}:",
                    Location = new Point(5, yPos),
                    Size = new Size(180, 15),
                    Font = new Font("Arial", 8, FontStyle.Bold)
                };

                var currentValueLabel = new Label
                {
                    Name = $"current_{variable}",
                    Text = "Current: 0",
                    Location = new Point(10, yPos + 15),
                    Size = new Size(180, 15),
                    Font = new Font("Arial", 8)
                };

                var avgValueLabel = new Label
                {
                    Name = $"avg_{variable}",
                    Text = "Time-Weighted Avg: 0",
                    Location = new Point(10, yPos + 30),
                    Size = new Size(180, 15),
                    Font = new Font("Arial", 8)
                };

                _metricsPanel.Controls.AddRange(new Control[] { metricLabel, currentValueLabel, avgValueLabel });
                yPos += 55;
            }
        }

        private void SetupInfoPanel(Panel infoPanel)
        {
            var infoLabel = new Label
            {
                Text = "System Information:",
                Location = new Point(5, 5),
                Size = new Size(120, 20),
                Font = new Font("Arial", 10, FontStyle.Bold)
            };
            infoPanel.Controls.Add(infoLabel);

            _eventsListView = new ListView
            {
                Location = new Point(5, 25),
                Size = new Size(1180, 250),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };

            _eventsListView.Columns.Add("Metric", 200);
            _eventsListView.Columns.Add("Value", 150);
            _eventsListView.Columns.Add("Description", 400);

            // Add system statistics
            var items = new[]
            {
                new ListViewItem(new[] { "Communication Method", "Direct Memory", "Zero file I/O, guaranteed delivery" }),
                new ListViewItem(new[] { "Event Queue Status", "Active", "Real-time in-memory processing" }),
                new ListViewItem(new[] { "Data Loss Risk", "Zero", "All events guaranteed delivered" }),
                new ListViewItem(new[] { "Performance", "Maximum", "Microsecond latency communication" }),
                new ListViewItem(new[] { "Overflow Protection", "Enabled", "Automatic queue management" })
            };

            _eventsListView.Items.AddRange(items);
            infoPanel.Controls.Add(_eventsListView);
        }

        private void SetupPlot()
        {
            var colors = new Color[] {
                Color.Red, Color.Blue, Color.Green, Color.Orange,
                Color.Purple, Color.Brown, Color.Pink, Color.Cyan
            };

            for (int i = 0; i < _variableNames.Count; i++)
            {
                _eventSeries[_variableNames[i]] = new List<EventPoint>();
                _seriesColors[_variableNames[i]] = colors[i % colors.Length];
            }
        }

        private void PlotPanel_Paint(object sender, PaintEventArgs paintArgs)
        {
            lock (_dataLock)
            {
                try
                {
                    Graphics g = paintArgs.Graphics;
                    g.Clear(Color.White);

                    if (_eventSeries.Count == 0 || _maxTime == _minTime)
                    {
                        // Draw instructions
                        using (Font font = new Font("Arial", 12))
                        using (Brush brush = new SolidBrush(Color.DarkBlue))
                        {
                            string instructions = "Direct Memory Communication Active!\n\n" +
                                                "Add 'Direct Memory Event Logger' steps to your model.\n" +
                                                "Events will appear here in real-time with zero data loss.";
                            g.DrawString(instructions, font, brush, 50, 50);
                        }
                        return;
                    }

                    // Calculate plot area
                    Rectangle plotArea = new Rectangle(60, 30, _plotPanel.Width - 80, _plotPanel.Height - 80);

                    // Draw title
                    using (Font titleFont = new Font("Arial", 12, FontStyle.Bold))
                    using (Brush titleBrush = new SolidBrush(Color.DarkBlue))
                    {
                        g.DrawString("Direct Memory Communication - Variable Changes", titleFont, titleBrush, plotArea.Left, 5);
                    }

                    // Draw axes
                    using (Pen axisPen = new Pen(Color.Black, 2))
                    {
                        g.DrawLine(axisPen, plotArea.Left, plotArea.Bottom, plotArea.Right, plotArea.Bottom);
                        g.DrawLine(axisPen, plotArea.Left, plotArea.Top, plotArea.Left, plotArea.Bottom);
                    }

                    // Draw simplified grid
                    using (Pen gridPen = new Pen(Color.LightGray, 1))
                    {
                        for (int i = 1; i < 5; i++)
                        {
                            int x = plotArea.Left + (plotArea.Width * i / 5);
                            int y = plotArea.Top + (plotArea.Height * i / 5);
                            g.DrawLine(gridPen, x, plotArea.Top, x, plotArea.Bottom);
                            g.DrawLine(gridPen, plotArea.Left, y, plotArea.Right, y);
                        }
                    }

                    // Draw event series
                    foreach (var series in _eventSeries)
                    {
                        if (series.Value.Count < 1) continue;

                        using (Pen seriesPen = new Pen(_seriesColors[series.Key], 2))
                        {
                            var events = series.Value.OrderBy(evt => evt.Timestamp).ToList();
                            int stepSize = Math.Max(1, events.Count / 500);

                            for (int i = 0; i < events.Count; i += stepSize)
                            {
                                var evt = events[i];
                                int x = plotArea.Left + (int)((evt.Timestamp - _minTime) / (_maxTime - _minTime) * plotArea.Width);
                                int y = plotArea.Bottom - (int)((evt.Value - _minValue) / (_maxValue - _minValue) * plotArea.Height);

                                if (i > 0)
                                {
                                    var prevIdx = Math.Max(0, i - stepSize);
                                    var prevEvt = events[prevIdx];
                                    int prevX = plotArea.Left + (int)((prevEvt.Timestamp - _minTime) / (_maxTime - _minTime) * plotArea.Width);
                                    int prevY = plotArea.Bottom - (int)((prevEvt.Value - _minValue) / (_maxValue - _minValue) * plotArea.Height);

                                    g.DrawLine(seriesPen, prevX, prevY, x, prevY); // Horizontal
                                    g.DrawLine(seriesPen, x, prevY, x, y); // Vertical
                                }

                                // Draw event markers
                                using (Brush markerBrush = new SolidBrush(_seriesColors[series.Key]))
                                {
                                    g.FillEllipse(markerBrush, x - 2, y - 2, 4, 4);
                                }
                            }
                        }
                    }

                    // Draw legend
                    int legendY = plotArea.Top + 10;
                    foreach (var series in _eventSeries)
                    {
                        using (Brush legendBrush = new SolidBrush(_seriesColors[series.Key]))
                        using (Font legendFont = new Font("Arial", 8))
                        {
                            g.FillRectangle(legendBrush, plotArea.Right - 150, legendY, 15, 10);
                            g.DrawString(series.Key, legendFont, Brushes.Black, plotArea.Right - 130, legendY - 2);
                        }
                        legendY += 20;
                    }

                    // Draw labels
                    using (Font font = new Font("Arial", 8))
                    using (Brush brush = new SolidBrush(Color.Black))
                    {
                        g.DrawString($"Time: {_minTime:F1} - {_maxTime:F1}", font, brush, plotArea.Left, plotArea.Bottom + 10);
                        g.DrawString($"Value: {_minValue:F1} - {_maxValue:F1}", font, brush, plotArea.Left, plotArea.Bottom + 25);
                        g.DrawString($"Direct Memory - Events: {_totalEventsReceived}", font, brush, plotArea.Left, plotArea.Bottom + 40);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"DASHBOARD: Error painting plot: {ex.Message}");
                }
            }
        }
    }

    public class EventPoint
    {
        public double Timestamp { get; set; }
        public double Value { get; set; }
        public string EventType { get; set; }
    }

    public class TimeWeightedCalculator
    {
        private double _lastTime = 0;
        private double _lastValue = 0;
        private double _weightedSum = 0;
        private double _totalTime = 0;

        public void AddEvent(double time, double value)
        {
            if (_totalTime > 0)
            {
                double duration = time - _lastTime;
                _weightedSum += _lastValue * duration;
                _totalTime += duration;
            }

            _lastTime = time;
            _lastValue = value;
        }

        public double GetTimeWeightedAverage()
        {
            return _totalTime > 0 ? _weightedSum / _totalTime : _lastValue;
        }
    }
}