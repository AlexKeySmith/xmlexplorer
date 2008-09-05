namespace XmlExplorer.Controls
{
    using System;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Drawing;
    using System.Windows.Forms;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
    using System.Xml;
    using System.Xml.XPath;
    using System.IO;
    using System.Security.Permissions;
    using System.Security;

    public partial class TabbedXmlExplorerWindow : Form
    {
        #region Variables

        /// <summary>
        /// Dialog used to change the font and forecolor for the window tree views.
        /// </summary>
        private FontDialog _fontDialog;

        /// <summary>
        /// A list of the XmlExplorerWindows that are currently loading an XML file.
        /// Used to display progress in the status bar.
        /// </summary>
        private List<object> _windowsCurrentlyLoading = new List<object>();

        private DateTime _startedLoading;

        private Font _treeFont;
        private Color _treeForeColor;

        /// <summary>
        /// A flag to bypass the custom drawing of syntax highlights,
        /// optionally used to improve performance on large documents.
        /// </summary>
        private bool _useSyntaxHighlighting = true;

        private ExpressionsWindow _expressionsWindow;
        private ValidationWindow _validationWindow;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the TabbedXmlExplorerWindow.
        /// </summary>
        public TabbedXmlExplorerWindow()
        {
            this.InitializeComponent();

            // handle file drag-drop operations
            try
            {
                new UIPermission(UIPermissionWindow.AllWindows).Demand();
                this.AllowDrop = true;
            }
            catch (SecurityException)
            {
            }

            this.DragOver += this.OnDragOver;
            this.DragDrop += this.OnDragDrop;

            // wire up all of the toolbar and menu events
            this.toolStripMenuItemFile.DropDownOpening += this.OnToolStripMenuItemFileDropDownOpening;
            this.toolStripMenuItemEdit.DropDownOpening += this.OnToolStripMenuItemEditDropDownOpening;
            this.toolStripMenuItemView.DropDownOpening += this.OnToolStripMenuItemViewDropDownOpening;

            this.toolStripTextBoxXpath.KeyDown += this.OnToolStripTextBoxXpathKeyDown;
            this.toolStripTextBoxXpath.TextChanged += this.OnToolStripTextBoxXpathTextChanged;

            // where applicable, I wire the click events of multiple tools to the same event handler
            // to reduce redundant code (for example, for the Open menu item and the Open toolbar button).
            this.contextMenuStripMenuItemClose.Click += this.OnToolStripMenuItemCloseClick;
            this.toolStripMenuItemClose.Click += this.OnToolStripMenuItemCloseClick;

            this.toolStripMenuItemExit.Click += this.OnToolStripMenuItemExitClick;
            this.toolStripMenuItemFont.Click += this.OnToolStripMenuItemFontClick;

            this.toolStripMenuItemUseHighlighting.Click += this.OnToolStripMenuItemUseHighlightingClick;

            this.toolStripMenuItemOpen.Click += this.OnToolStripButtonOpenClick;
            this.toolStripButtonOpen.Click += this.OnToolStripButtonOpenClick;

            this.toolStripMenuItemOpenInEditor.Click += this.OnToolStripButtonOpenInEditorClick;

            this.toolStripButtonRefresh.Click += this.OnToolStripButtonRefreshClick;
            this.toolStripMenuItemRefresh.Click += this.OnToolStripButtonRefreshClick;

            this.toolStripButtonCopyFormattedOuterXml.Click += this.OnToolStripButtonCopyFormattedOuterXmlClick;
            this.toolStripMenuItemCopyFormattedOuterXml.Click += this.OnToolStripButtonCopyFormattedOuterXmlClick;
            this.contextMenuStripNodesItemCopyFormattedOuterXml.Click += this.OnToolStripButtonCopyFormattedOuterXmlClick;

            this.toolStripMenuItemCopy.Click += this.OnToolStripMenuItemCopyClick;
            this.contextMenuStripNodesItemCopy.Click += this.OnToolStripMenuItemCopyClick;

            this.toolStripMenuItemCopyXPath.Click += this.OnToolStripMenuItemCopyXPathClick;
            this.contextMenuStripNodesItemCopyXPath.Click += this.OnToolStripMenuItemCopyXPathClick;

            this.toolStripButtonLaunchXpath.Click += this.OnToolStripButtonLaunchXpathClick;

            this.toolStripButtonSave.Click += this.OnToolStripButtonSaveClick;
            this.toolStripMenuItemSaveWithFormatting.Click += this.OnToolStripButtonSaveClick;
            this.toolStripMenuItemSaveAs.Click += this.OnToolStripMenuItemSaveAsClick;

            this.toolStripMenuItemCopyFullPath.Click += this.OnToolStripMenuItemCopyFullPathClick;
            this.toolStripMenuItemOpenContainingFolder.Click += this.OnToolStripMenuItemOpenContainingFolderClick;

            this.toolStripStatusLabelChildCount.Text = string.Empty;

            this.toolStripButtonXPathExpression.Click += this.OnToolStripButtonXPathExpressionClick;

            // set up the expressions window
            _expressionsWindow = new ExpressionsWindow();
            _expressionsWindow.ShowHint = WeifenLuo.WinFormsUI.Docking.DockState.DockBottom;
            _expressionsWindow.SelectedExpressionChanged += this.OnExpressionsWindow_SelectedExpressionChanged;
            _expressionsWindow.ExpressionsActivated += this.OnExpressionsWindow_ExpressionsActivated;
            _expressionsWindow.Show(this.dockPanel);

            // set up the validation window
            _validationWindow = new ValidationWindow();
            _validationWindow.ValidateSchema += this.OnValidationWindow_Validate;
            _validationWindow.SchemaFileNameChanged += this.OnValidationWindow_SchemaFileNameChanged;
            _validationWindow.ShowHint = WeifenLuo.WinFormsUI.Docking.DockState.DockBottom;
            _validationWindow.Show(this.dockPanel);

            this.MdiChildActivate += this.OnMdiChildActivate;

            this.UpdateTools();
        }

        #endregion

        #region Properties

        public Font TreeFont
        {
            get { return _treeFont; }
            set { _treeFont = value; }
        }

        public Color TreeForeColor
        {
            get { return _treeForeColor; }
            set { _treeForeColor = value; }
        }

        public AutoCompleteMode AutoCompleteMode
        {
            get { return this.toolStripTextBoxXpath.AutoCompleteMode; }
            set { this.toolStripTextBoxXpath.AutoCompleteMode = value; }
        }

        /// <summary>
        /// Gets or sets whether the custom drawing of syntax highlights
        /// should be bypassed. This can be optionally used to improve 
        /// performance on large documents.
        /// </summary>
        public bool UseSyntaxHighlighting
        {
            get { return _useSyntaxHighlighting; }
            set
            {
                _useSyntaxHighlighting = value;
                this.toolStripMenuItemUseHighlighting.Checked = _useSyntaxHighlighting;
                this.SetXmlExplorerWindowHighlighting(_useSyntaxHighlighting);
            }
        }

        public XPathExpressionLibrary Expressions
        {
            get
            {
                return _expressionsWindow.Expressions;
            }

            set
            {
                _expressionsWindow.Expressions = value;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Applies any changes to the font and forecolor performed from the font dialog.
        /// </summary>
        public void ApplyFont()
        {
            // save the font and forecolor settings
            _treeFont = _fontDialog.Font;
            _treeForeColor = _fontDialog.Color;

            // apply the font and forecolor to any open windows
            this.SetXmlExplorerWindowFonts(_fontDialog.Font);
            this.SetXmlExplorerWindowForeColors(_fontDialog.Color);
        }

        /// <summary>
        /// Creates a font dialog, and initializes it with the current saved settings.
        /// </summary>
        private void InitializeFontDialog()
        {
            if (_fontDialog == null)
            {
                _fontDialog = new FontDialog();
                _fontDialog.Apply += this.OnFontDialogApply;
                _fontDialog.ShowApply = true;
                _fontDialog.ShowColor = true;
                _fontDialog.ShowEffects = true;
                _fontDialog.Font = _treeFont;
                _fontDialog.Color = _treeForeColor;
            }
        }

        // Shows the open file dialog, and opens the file(s) specified by the user, if any.
        private void Open()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "All Files (*.*)|*.*|Xml Documents (*.xml)|*.xml";
                dialog.Multiselect = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        this.Open(dialog.FileNames);
                    }
                    catch (SecurityException)
                    {
                        dialog.Multiselect = false;
                        using (Stream stream = dialog.OpenFile())
                        {
                            this.Open(stream);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Opens a window for each specified file.
        /// </summary>
        /// <param name="filenames">The full paths of the files to open.</param>
        public void Open(ReadOnlyCollection<string> filenames)
        {
            foreach (string text in filenames)
            {
                this.Open(text);
            }
        }

        /// <summary>
        /// Opens a window for each specified file.
        /// </summary>
        /// <param name="filenames">The full paths of the files to open.</param>
        public void Open(string[] filenames)
        {
            foreach (string text in filenames)
            {
                this.Open(text);
            }
        }

        /// <summary>
        /// Opens a window for the specified file.
        /// </summary>
        /// <param name="filename">The full path of the file to open.</param>
        public void Open(string filename)
        {
            // create a window
            XmlExplorerWindow window = this.CreateXmlExplorerWindow();

            //// instruct the window to open the specified file.
            window.Open(filename);

            // show the window
            window.Show(this.dockPanel);

            this.UpdateTools();
        }

        /// <summary>
        /// Opens a window for the specified stream.
        /// </summary>
        public void Open(Stream stream)
        {
            // create a window
            XmlExplorerWindow window = this.CreateXmlExplorerWindow();

            //// instruct the window to open the specified file.
            window.Open(stream);

            // show the window
            window.Show(this.dockPanel);

            this.UpdateTools();
        }

        /// <summary>
        /// Opens a window for the specified node set.  This method is used to open XPath expressions that 
        /// evaluate to a node set.
        /// </summary>
        /// <param name="iterator">An XPathNodeIterator with which to open a window.</param>
        public void Open(XPathNodeIterator iterator)
        {
            // create a window
            XmlExplorerWindow window = this.CreateXmlExplorerWindow();

            //// instruct the window to open the specified file.
            window.Open(iterator);

            // show the window
            window.Show(this.dockPanel);

            this.UpdateTools();
        }

        /// <summary>
        /// Opens the selected window's file in the default editor for it's file type.
        /// </summary>
        private void OpenInEditor()
        {
            try
            {
                // get the selected window
                XmlExplorerWindow window = this.ActiveMdiChild as XmlExplorerWindow;
                if (window == null)
                    return;

                if (string.IsNullOrEmpty(window.Filename))
                    return;

                ProcessStartInfo info = new ProcessStartInfo(window.Filename);
                info.Verb = "edit";

                Process.Start(info);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        /// <summary>
        /// Returns an initialized XmlExplorerWindow.
        /// </summary>
        /// <returns></returns>
        private XmlExplorerWindow CreateXmlExplorerWindow()
        {
            // the main window handle needs to be created before creating a new window
            if (!this.IsHandleCreated)
                this.CreateHandle();

            XmlExplorerWindow window = new XmlExplorerWindow();

            window.Icon = this.Icon;

            // read the options for the window, such as font and forecolor
            this.ReadWindowOptions(window);

            // wire to the window's events
            window.FormClosed += this.OnChildWindowFormClosed;
            window.LoadingFileCompleted += this.OnWindowLoadingFileCompleted;
            window.LoadingFileFailed += this.OnWindowLoadingFileFailed;
            window.LoadingFileStarted += this.OnWindowLoadingFileStarted;
            window.XPathNavigatorTreeView.AfterSelect += this.OnWindow_XmlTreeView_AfterSelect;
            window.XPathNavigatorTreeView.MouseUp += this.OnWindow_XmlTreeView_MouseUp;
            window.TabPageContextMenuStrip = this.contextMenuStripTabs;

            window.MdiParent = this;

            return window;
        }

        /// <summary>
        /// Initializes an XmlExplorerWindow with saved settings.
        /// </summary>
        /// <param name="window">The XmlExplorerWindow to initialize.</param>
        private void ReadWindowOptions(XmlExplorerWindow window)
        {
            window.XPathNavigatorTreeView.Font = _treeFont;
            window.XPathNavigatorTreeView.ForeColor = _treeForeColor;
            window.UseSyntaxHighlighting = _useSyntaxHighlighting;
        }

        /// <summary>
        /// Shows the font dialog, and applies any changes made
        /// </summary>
        public void ShowFontDialog()
        {
            // load any saved settings for the font dialog
            this.InitializeFontDialog();
            if (_fontDialog.ShowDialog(this) == DialogResult.OK)
            {
                // apply font changes
                this.ApplyFont();
            }
        }

        /// <summary>
        /// Toggles the use of syntax highlighing
        /// </summary>
        public void TogggleSyntaxHighlighting()
        {
            _useSyntaxHighlighting = !_useSyntaxHighlighting;
            this.toolStripMenuItemUseHighlighting.Checked = _useSyntaxHighlighting;
            this.SetXmlExplorerWindowHighlighting(_useSyntaxHighlighting);
        }

        /// <summary>
        /// Displays progress for windows that are loading xml files.
        /// </summary>
        private void UpdateLoadingStatus()
        {
            // get the number of windows that are currently loading xml files
            int count = _windowsCurrentlyLoading.Count;

            string loadingStatus = string.Empty;

            // build the status text
            if (count > 0)
            {
                string suffix = count == 1 ? string.Empty : "s";
                loadingStatus = string.Format("Loading {0} file{1}", count.ToString(), suffix);
            }
            else
            {
                TimeSpan elapsed = DateTime.Now - _startedLoading;
                loadingStatus = string.Format("Loaded in {0}", elapsed.ToString());
            }

            // update the progress bar, we want it to scroll if any windows are still loading,
            // but be hidden if there are none loading
            this.toolStripProgressBar.Style = ProgressBarStyle.Marquee;
            this.toolStripProgressBar.Enabled = count > 0;
            this.toolStripProgressBar.Visible = count > 0;

            // update the status text
            this.toolStripStatusLabelMain.Text = loadingStatus;
        }

        /// <summary>
        /// Removes a window from our list of windows currently loading xml files.
        /// </summary>
        /// <param name="window">The window that has finished loading, and needs removed from
        /// the list of currently loading windows.</param>
        private void RemoveLoadingWindow(object window)
        {
            // lock the list for thread safety
            lock (_windowsCurrentlyLoading)
            {
                // remove the window if it's in the list
                if (_windowsCurrentlyLoading.Contains(window))
                    _windowsCurrentlyLoading.Remove(window);

                // update the loading status
                this.UpdateLoadingStatus();
            }
        }

        /// <summary>
        /// Evaluates the specified XPath expression against the xml content of the currently selected window.
        /// </summary>
        /// <param name="xpath">An XPath expression to evaluate against the currently selected window.</param>
        /// <returns></returns>
        private bool FindByXpath(string xpath)
        {
            bool result = false;

            try
            {
                // get the selected window, returning if none are found
                XmlExplorerWindow window = this.ActiveMdiChild as XmlExplorerWindow;
                if (window == null)
                    return false;

                // instruct the window to perform the expression evaluation
                result = window.FindByXpath(xpath);

                if (result)
                    this.toolStripStatusLabelMain.Text = "";
                else
                    this.toolStripStatusLabelMain.Text = "No matches were found";
            }
            catch (System.Xml.XPath.XPathException ex)
            {
                // an invalid XPath expression has been attempted
                Debug.WriteLine(ex);

                // inform the user
                this.toolStripStatusLabelMain.Text = ex.Message;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }

            return result;
        }

        /// <summary>
        /// Opens a new window with the evaluated results of the specified XPath expression.
        /// </summary>
        /// <param name="xpath">An XPath expression to evaluate against the currently selected window.</param>
        /// <returns></returns>
        private bool LaunchXpathResults(string xpath)
        {
            try
            {
                // get the selected window
                XmlExplorerWindow window = this.ActiveMdiChild as XmlExplorerWindow;
                if (window == null)
                    return false;

                // evaluate the expression
                XPathNodeIterator iterator = window.SelectXmlNodes(xpath);

                // check for empty results
                if (iterator == null || iterator.Count < 1)
                    return false;

                // open the results in a new window
                this.Open(iterator);

                return true;
            }
            catch (XPathException ex)
            {
                // an invalid XPath expression has been attempted
                Debug.WriteLine(ex);

                // inform the user
                this.toolStripStatusLabelMain.Text = ex.Message;
            }
            catch (ArgumentException ex)
            {
                Debug.WriteLine(ex);
                this.toolStripStatusLabelMain.Text = ex.Message;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }

            return false;
        }

        /// <summary>
        /// Overwrites the selected window's file with XML formatting (tabs and crlf's)
        /// </summary>
        private void SaveWithFormatting()
        {
            try
            {
                // get the selected window
                XmlExplorerWindow window = this.ActiveMdiChild as XmlExplorerWindow;
                if (window == null)
                    return;

                // instruct the window to save with formatting
                window.SaveWithFormatting();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        /// <summary>
        /// Prompts the user to save a copy of the selected window's XML file.
        /// </summary>
        private void SaveAs()
        {
            try
            {
                XmlExplorerWindow window = this.ActiveMdiChild as XmlExplorerWindow;
                if (window == null)
                    return;
                window.SaveAs();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        /// <summary>
        /// Applies a fore color to all of the currently open windows.
        /// </summary>
        /// <param name="color">A color to apply as the fore color.</param>
        private void SetXmlExplorerWindowForeColors(Color color)
        {
            foreach (Form window in this.MdiChildren)
            {
                XmlExplorerWindow xmlExplorerWindow = window as XmlExplorerWindow;

                if (xmlExplorerWindow == null)
                    continue;

                xmlExplorerWindow.XPathNavigatorTreeView.ForeColor = color;
            }
        }

        /// <summary>
        /// Applies a font to all of the currently open windows.
        /// </summary>
        /// <param name="font">A font to apply.</param>
        private void SetXmlExplorerWindowFonts(Font font)
        {
            foreach (Form window in this.MdiChildren)
            {
                XmlExplorerWindow xmlExplorerWindow = window as XmlExplorerWindow;

                if (xmlExplorerWindow == null)
                    continue;

                xmlExplorerWindow.XPathNavigatorTreeView.Font = font;
            }
        }

        /// <summary>
        /// Applies syntax highlighting changes to all of the currently open windows.
        /// </summary>
        /// <param name="useSyntaxHighlighting">Whether or not to use syntax highlighting.</param>
        private void SetXmlExplorerWindowHighlighting(bool useSyntaxHighlighting)
        {
            foreach (Form window in this.MdiChildren)
            {
                XmlExplorerWindow xmlExplorerWindow = window as XmlExplorerWindow;

                if (xmlExplorerWindow == null)
                    continue;

                xmlExplorerWindow.UseSyntaxHighlighting = useSyntaxHighlighting;
            }
        }

        /// <summary>
        /// Copies the current window's selected xml node (and all of it's sub nodes) to the clipboard
        /// as formatted XML text.
        /// </summary>
        private void CopyFormattedOuterXml()
        {
            // get the selectd window
            XmlExplorerWindow window = this.ActiveMdiChild as XmlExplorerWindow;
            if (window == null)
                return;

            // instruct the window to copy
            window.CopyFormattedOuterXml();
        }

        /// <summary>
        /// Copies the current window's selected xml node to the clipboard
        /// as XML text.
        /// </summary>
        private void CopyNodeText()
        {
            // get the selectd window
            XmlExplorerWindow window = this.ActiveMdiChild as XmlExplorerWindow;
            if (window == null)
                return;

            // instruct the window to copy
            window.CopyNodeText();
        }

        private void UpdateXPathExpressionTool(string text)
        {
            if (_expressionsWindow.Expressions.Contains(text))
            {
                this.toolStripButtonXPathExpression.Image = Properties.Resources.star;
                this.toolStripButtonXPathExpression.ToolTipText = "Edit expression";
            }
            else
            {
                this.toolStripButtonXPathExpression.Image = Properties.Resources.unstarred;
                this.toolStripButtonXPathExpression.ToolTipText = "Add expression to library";
            }
        }

        private void UpdateXPathAutoComplete(string text)
        {
            try
            {
                // we'll update the autocomplete custom source every time a / is entered
                if (string.IsNullOrEmpty(text) || !text.EndsWith("/"))
                    return;

                // append a * to return all matching nodes
                string xpath = text + "*";

                // get the selected window
                XmlExplorerWindow window = this.ActiveMdiChild as XmlExplorerWindow;
                if (window == null)
                    return;

                // evaluate the expression
                XPathNodeIterator nodes = window.SelectXmlNodes(xpath);

                // return if an empty result set is returned
                if (nodes == null || nodes.Count < 1)
                    return;

                // create a new autocomplete source
                AutoCompleteStringCollection source = new AutoCompleteStringCollection();

                // add the full path of each xml node to the source
                while (nodes.MoveNext())
                {
                    string fullPath = text + nodes.Current.Name;

                    // eliminate duplicates
                    if (!source.Contains(fullPath))
                        source.Add(fullPath);
                }

                // update the autocomplete source of the textbox
                // I beleive this is the source of the exception mentioned above
                this.toolStripTextBoxXpath.AutoCompleteCustomSource = source;
            }
            catch (System.Xml.XPath.XPathException ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private void HandleExpressionKeyPress(KeyEventArgs e, string xpath)
        {
            this.toolStripTextBoxXpath.Text = xpath;
            if (e.Shift)
            {
                // Shift-Enter has been pressed, evaluate the expression
                // if successful, open results in a new window
                // if there is a problem with the expression, notify the user
                // by highlighting the XPath text box
                if (!this.LaunchXpathResults(xpath))
                    this.toolStripTextBoxXpath.BackColor = Color.LightPink;
            }
            else
            {
                // Shift-Enter has been pressed, evaluate the expression
                // if successful, the first node of the result set will be selected
                // (selection is performed by the window)
                // if there is a problem with the expression, notify the user
                // by highlighting the XPath text box
                if (!this.FindByXpath(xpath))
                    this.toolStripTextBoxXpath.BackColor = Color.LightPink;
                this.toolStripTextBoxXpath.SelectionStart = this.toolStripTextBoxXpath.TextLength;
            }
        }

        private void UpdateTools()
        {
            this.UpdateTools(false);
        }

        private void UpdateTools(bool isLastWindowClosing)
        {
            try
            {
                // update any tools that depend on one or more windows being open
                bool hasOpenWindows = this.MdiChildren.Length > 0 && !isLastWindowClosing;

                this.toolStripMenuItemOpenInEditor.Enabled = hasOpenWindows;
                this.toolStripMenuItemClose.Enabled = hasOpenWindows;
                this.toolStripMenuItemSaveAs.Enabled = hasOpenWindows;
                this.toolStripButtonSave.Enabled = hasOpenWindows;
                this.toolStripMenuItemSaveWithFormatting.Enabled = hasOpenWindows;

                this.toolStripMenuItemCopyFormattedOuterXml.Enabled = hasOpenWindows;
                this.toolStripButtonCopyFormattedOuterXml.Enabled = hasOpenWindows;
                this.toolStripMenuItemCopy.Enabled = hasOpenWindows;
                this.toolStripMenuItemCopyXPath.Enabled = hasOpenWindows;

                this.toolStripMenuItemRefresh.Enabled = hasOpenWindows;
                this.toolStripButtonRefresh.Enabled = hasOpenWindows;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Occurs when one or more file paths are dropped onto the window.
        /// </summary>
        private void OnDragDrop(object sender, DragEventArgs e)
        {
            // if it's not files being dropped, cancel the operation.
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            // get the full paths of the file(s) dropped
            string[] filenames = (string[])e.Data.GetData(DataFormats.FileDrop);

            // open the files
            this.Open(filenames);

            // allow the operation to complete
            e.Effect = DragDropEffects.Copy;
        }

        /// <summary>
        /// Occurs when one or more files are dragged over the window.
        /// </summary>
        private void OnDragOver(object sender, DragEventArgs e)
        {
            // if it's not files being dropped, cancel the operation
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.None;
                return;
            }

            // allow the operation
            e.Effect = DragDropEffects.Copy;
        }

        /// <summary>
        /// Occurs when changes in the font dialog need applied.
        /// </summary>
        private void OnFontDialogApply(object sender, EventArgs e)
        {
            try
            {
                // apply the font changes
                this.ApplyFont();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        void OnMdiChildActivate(object sender, EventArgs e)
        {
            try
            {
                // get the window
                XmlExplorerWindow window = this.ActiveMdiChild as XmlExplorerWindow;

                if (window == null)
                {
                    this.Text = "XML Explorer";
                    return;
                }

                _validationWindow.ValidationEventArgs = window.ValidationEventArgs;
                _validationWindow.SchemaFileName = window.SchemaFileName;

                // unwire from child mdi window events
                foreach (Form form in this.MdiChildren)
                {
                    XmlExplorerWindow otherWindow = form as XmlExplorerWindow;
                    if (otherWindow == null)
                        continue;

                    otherWindow.XPathNavigatorTreeView.KeyDown -= this.OnWindow_XmlTreeView_KeyDown;
                }

                // update the main window text
                this.Text = string.Format("XML Explorer - [{0}]", window.Text);

                // wire to the newly active child mdi window events
                window.XPathNavigatorTreeView.KeyDown += this.OnWindow_XmlTreeView_KeyDown;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        private void OnWindow_XmlTreeView_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                switch (e.KeyCode)
                {
                    case Keys.C:
                        if (e.Control)
                            // handle CTRL-C
                            this.CopyNodeText();
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        /// <summary>
        /// Occurs when a window has begun asynchronously loading an XML file.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnWindowLoadingFileStarted(object sender, EventArgs e)
        {
            try
            {
                // this event will likely occur on a background thread,
                // marshal the event back to the window's thread
                if (this.InvokeRequired)
                {
                    this.Invoke(new EventHandler<EventArgs>(this.OnWindowLoadingFileStarted), sender, e);
                    return;
                }

                // lock the list for thread safety
                lock (_windowsCurrentlyLoading)
                {
                    if (_windowsCurrentlyLoading.Count < 1)
                        _startedLoading = DateTime.Now;

                    // add the window
                    _windowsCurrentlyLoading.Add(sender);

                    // update the status and progress
                    this.UpdateLoadingStatus();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        /// <summary>
        /// Occurs when an exception occurs while loading an XML file.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnWindowLoadingFileFailed(object sender, EventArgs e)
        {
            try
            {
                // this event will likely occur on a background thread,
                // marshal the event back to the window's thread
                if (this.InvokeRequired)
                {
                    this.Invoke(new EventHandler<EventArgs>(this.OnWindowLoadingFileFailed), sender, e);
                    return;
                }

                // remove the window
                this.RemoveLoadingWindow(sender);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        /// <summary>
        /// Occurs when a window has completed asynchronously loading an XML file.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnWindowLoadingFileCompleted(object sender, EventArgs e)
        {
            try
            {
                // this event will likely occur on a background thread,
                // marshal the event back to the window's thread
                if (this.InvokeRequired)
                {
                    this.Invoke(new EventHandler<EventArgs>(this.OnWindowLoadingFileCompleted), sender, e);
                    return;
                }

                // remove the window
                this.RemoveLoadingWindow(sender);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        /// <summary>
        /// Occurs when the selected xml node of a window has changed.
        /// </summary>
        private void OnWindow_XmlTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            try
            {
                // get the newly selected node
                XPathNavigatorTreeNode node = e.Node as XPathNavigatorTreeNode;
                if (node == null)
                    return;

                if (node.Navigator == null)
                    return;

                XPathNavigatorTreeView treeView = sender as XPathNavigatorTreeView;
                if (treeView == null)
                    return;

                // get the xml node's child count
                int count = node.Navigator.SelectChildren(XPathNodeType.Element).Count;

                // update the status bar with the child count
                this.toolStripStatusLabelChildCount.Text = string.Format("{0} child node{1}", count, count == 1 ? string.Empty : "s");

                // update the status bar with the full path of the selected xml node
                this.toolStripStatusLabelMain.Text = treeView.GetXmlNodeFullPath(node.Navigator);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        private void OnWindow_XmlTreeView_MouseUp(object sender, MouseEventArgs e)
        {
            try
            {
                if (e.Button != MouseButtons.Right)
                    return;

                // get the selectd window
                XmlExplorerWindow window = this.ActiveMdiChild as XmlExplorerWindow;
                if (window == null)
                    return;

                // get the node that was clicked
                TreeViewHitTestInfo info = window.XPathNavigatorTreeView.HitTest(e.Location);

                if (info.Node == null)
                    return;

                XPathNavigatorTreeNode node = info.Node as XPathNavigatorTreeNode;
                if (node == null)
                    return;

                if (node.Navigator == null)
                    return;

                XPathNavigatorTreeView treeView = sender as XPathNavigatorTreeView;
                if (treeView == null)
                    return;

                treeView.SelectedNode = node;

                this.contextMenuStripNodes.Show(treeView, e.Location);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        private void OnExpressionsWindow_ExpressionsActivated(object sender, EventArgs e)
        {
            try
            {
                foreach (String xpath in _expressionsWindow.SelectedExpressions)
                {
                    // evaluate the expression
                    // if successful, open results in a new window
                    // if there is a problem with the expression, notify the user
                    // by highlighting the XPath text box
                    if (!this.LaunchXpathResults(xpath))
                        this.toolStripTextBoxXpath.BackColor = Color.LightPink;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        private void OnExpressionsWindow_SelectedExpressionChanged(object sender, EventArgs e)
        {
            try
            {
                String xpath = null;

                if (_expressionsWindow.SelectedExpressions.Count > 0)
                {
                    xpath = _expressionsWindow.SelectedExpressions[0];

                    // evaluate the expression
                    // if successful, the first node of the result set will be selected
                    // (selection is performed by the window)
                    // if there is a problem with the expression, notify the user
                    // by highlighting the XPath text box
                    if (!this.FindByXpath(xpath))
                        this.toolStripTextBoxXpath.BackColor = Color.LightPink;
                    this.toolStripTextBoxXpath.SelectionStart = this.toolStripTextBoxXpath.TextLength;
                }

                this.UpdateXPathExpressionTool(xpath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        void OnChildWindowFormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                XmlExplorerWindow window = sender as XmlExplorerWindow;

                if (window == null)
                    return;

                // unwire from the window's events
                window.FormClosed -= this.OnChildWindowFormClosed;
                window.LoadingFileCompleted -= this.OnWindowLoadingFileCompleted;
                window.LoadingFileFailed -= this.OnWindowLoadingFileFailed;
                window.LoadingFileStarted -= this.OnWindowLoadingFileStarted;
                window.XPathNavigatorTreeView.AfterSelect -= this.OnWindow_XmlTreeView_AfterSelect;
                window.XPathNavigatorTreeView.MouseUp -= this.OnWindow_XmlTreeView_MouseUp;
                window.TabPageContextMenuStrip = null;

                this.UpdateTools(this.MdiChildren.Length == 1);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        void OnValidationWindow_SchemaFileNameChanged(object sender, EventArgs e)
        {
            try
            {
                // get the active window
                XmlExplorerWindow window = this.ActiveMdiChild as XmlExplorerWindow;

                if (window == null)
                    return;

                window.SchemaFileName = _validationWindow.SchemaFileName;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        void OnValidationWindow_Validate(object sender, EventArgs e)
        {
            try
            {
                // get the active window
                XmlExplorerWindow window = this.ActiveMdiChild as XmlExplorerWindow;

                if (window == null)
                    return;

                window.SchemaFileName = _validationWindow.SchemaFileName;

                window.ValidateSchema();

                _validationWindow.ValidationEventArgs = window.ValidationEventArgs;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        #endregion

        #region Tool Event Handlers

        private void OnToolStripMenuItemFileDropDownOpening(object sender, EventArgs e)
        {
            try
            {
                this.UpdateTools();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        private void OnToolStripMenuItemEditDropDownOpening(object sender, EventArgs e)
        {
            try
            {
                this.UpdateTools();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        private void OnToolStripMenuItemViewDropDownOpening(object sender, EventArgs e)
        {
            try
            {
                this.UpdateTools();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        private void OnToolStripTextBoxXpathKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                // reset any xpath expression error indicators
                this.toolStripTextBoxXpath.BackColor = SystemColors.Window;
                this.toolStripStatusLabelMain.Text = string.Empty;

                switch (e.KeyCode)
                {
                    case Keys.Enter:
                        string xpath = this.toolStripTextBoxXpath.Text;
                        this.HandleExpressionKeyPress(e, xpath);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        private void OnToolStripTextBoxXpathTextChanged(object sender, EventArgs e)
        {
            // I have intermittently been getting exceptions
            // while updating the AutoCompleteCustomSource
            // I have found numerous articles online on the matter, 
            // yet have found no solution.  May require a call to MSDN support.
            try
            {
                // get the current XPath expression
                string text = this.toolStripTextBoxXpath.Text;

                //this.UpdateXPathAutoComplete(text);

                this.UpdateXPathExpressionTool(text);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        private void OnToolStripMenuItemCloseClick(object sender, EventArgs e)
        {
            try
            {
                // get the window for which the context menu was opened
                XmlExplorerWindow page = this.contextMenuStripTabs.Tag as XmlExplorerWindow;
                if (page == null)
                    page = this.ActiveMdiChild as XmlExplorerWindow;

                // close the window
                if (page != null)
                {
                    page.Close();
                }

                // reset the context menu's tag
                this.contextMenuStripTabs.Tag = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        private void OnToolStripMenuItemExitClick(object sender, EventArgs e)
        {
            try
            {
                // close the main application window
                base.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        private void OnToolStripMenuItemFontClick(object sender, EventArgs e)
        {
            try
            {
                this.ShowFontDialog();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        private void OnToolStripMenuItemUseHighlightingClick(object sender, EventArgs e)
        {
            try
            {
                this.TogggleSyntaxHighlighting();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        private void OnToolStripButtonOpenClick(object sender, EventArgs e)
        {
            try
            {
                this.Open();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        private void OnToolStripButtonOpenInEditorClick(object sender, EventArgs e)
        {
            try
            {
                this.OpenInEditor();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        private void OnToolStripButtonRefreshClick(object sender, EventArgs e)
        {
            try
            {
                XmlExplorerWindow page = this.ActiveMdiChild as XmlExplorerWindow;
                if (page != null)
                {
                    page.Reload();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        private void OnToolStripButtonCopyFormattedOuterXmlClick(object sender, EventArgs e)
        {
            try
            {
                this.CopyFormattedOuterXml();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        private void OnToolStripMenuItemCopyClick(object sender, EventArgs e)
        {
            try
            {
                this.CopyNodeText();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        private void OnToolStripMenuItemCopyXPathClick(object sender, EventArgs e)
        {
            try
            {
                XmlExplorerWindow window = this.ActiveMdiChild as XmlExplorerWindow;
                if (window == null)
                    return;

                XPathNavigatorTreeNode node = window.XPathNavigatorTreeView.SelectedNode as XPathNavigatorTreeNode;
                if (node == null)
                    return;

                if (node.Navigator == null)
                    return;

                this.toolStripTextBoxXpath.Text = window.XPathNavigatorTreeView.GetXmlNodeFullPath(node.Navigator);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        private void OnToolStripButtonLaunchXpathClick(object sender, EventArgs e)
        {
            try
            {
                if (!this.LaunchXpathResults(this.toolStripTextBoxXpath.Text))
                    this.toolStripTextBoxXpath.BackColor = Color.LightPink;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        private void OnToolStripMenuItemSaveAsClick(object sender, EventArgs e)
        {
            try
            {
                this.SaveAs();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        private void OnToolStripButtonSaveClick(object sender, EventArgs e)
        {
            try
            {
                this.SaveWithFormatting();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }


        private void OnToolStripMenuItemCopyFullPathClick(object sender, EventArgs e)
        {
            try
            {
                XmlExplorerWindow page = this.ActiveMdiChild as XmlExplorerWindow;
                if (page == null)
                    page = this.ActiveMdiChild as XmlExplorerWindow;

                if (page != null)
                {
                    Clipboard.SetText(page.Filename);
                }

                this.contextMenuStripTabs.Tag = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        private void OnToolStripMenuItemOpenContainingFolderClick(object sender, EventArgs e)
        {
            try
            {
                // get the window for which the context menu was opened
                XmlExplorerWindow page = this.contextMenuStripTabs.Tag as XmlExplorerWindow;
                if (page == null)
                    page = this.ActiveMdiChild as XmlExplorerWindow;

                // open an explorer window to the folder containing the selected window's file
                if (page != null && !string.IsNullOrEmpty(page.Filename))
                {
                    // open explorer to the file's parent folder, with the file selected
                    string args = string.Format("/select,\"{0}\"", page.Filename);
                    Process.Start("explorer", args);
                }

                this.contextMenuStripTabs.Tag = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        private void OnToolStripButtonXPathExpressionClick(object sender, EventArgs e)
        {
            try
            {
                _expressionsWindow.AddOrEditXPathExpression(this.toolStripTextBoxXpath.Text);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        private void OnExpressionsItemActivate(object sender, EventArgs e)
        {
            try
            {
                //foreach (ListViewItem item in this.listViewExpressions.SelectedItems)
                //{
                //    XPathExpressionListViewItem expressionItem = item as XPathExpressionListViewItem;

                //    if (expressionItem == null)
                //        continue;

                //    this.LaunchXpathResults(this.toolStripTextBoxXpath.Text);
                //}
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        private void OnListViewExpressionsSelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                //if (this.listViewExpressions.SelectedItems.Count < 1)
                //    return;

                //XPathExpressionListViewItem item = this.listViewExpressions.SelectedItems[0] as XPathExpressionListViewItem;

                //if (item == null)
                //    return;

                //string xpath = item.XPathExpression.Expression;
                //this.toolStripTextBoxXpath.Text = xpath;

                //// evaluate the expression
                //// if successful, the first node of the result set will be selected
                //// (selection is performed by the window)
                //// if there is a problem with the expression, notify the user
                //// by highlighting the XPath text box
                //if (!this.FindByXpath(xpath))
                //    this.toolStripTextBoxXpath.BackColor = Color.LightPink;
                //this.toolStripTextBoxXpath.SelectionStart = this.toolStripTextBoxXpath.TextLength;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        private void OnListViewExpressionsMouseUp(object sender, MouseEventArgs e)
        {
            try
            {
                //this.toolStripTextBoxXpath.BackColor = SystemColors.Window;

                //if (this.listViewExpressions.SelectedItems.Count < 1)
                //    return;

                //XPathExpressionListViewItem item = this.listViewExpressions.SelectedItems[0] as XPathExpressionListViewItem;

                //if (item == null)
                //    return;

                //string xpath = item.XPathExpression.Expression;
                //this.toolStripTextBoxXpath.Text = xpath;

                //// evaluate the expression
                //// if successful, the first node of the result set will be selected
                //// (selection is performed by the window)
                //// if there is a problem with the expression, notify the user
                //// by highlighting the XPath text box
                //if (!this.FindByXpath(xpath))
                //    this.toolStripTextBoxXpath.BackColor = Color.LightPink;
                //this.toolStripTextBoxXpath.SelectionStart = this.toolStripTextBoxXpath.TextLength;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                MessageBox.Show(this, ex.ToString());
            }
        }

        #endregion
    }
}

