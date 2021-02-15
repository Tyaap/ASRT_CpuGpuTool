using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CpuGpuTool
{
    public partial class MainForm : Form
    {
        public CpuFile cpuFile = new CpuFile();
        public string nameFilter = "";
        public string typeFilter = "";
        public HashSet<int> hiddenAssetNums = new HashSet<int>();
        public Dictionary<int, ListViewItem> assetNumberToListViewItem = new Dictionary<int, ListViewItem>();
        public Timer listSelectTimer = new Timer();
        public string lastCpuFilePath = "";
        public string lastDataSavePath = "";
        public string lastDataOpenPath = "";
        Dictionary<string, Tuple<int, Asset>> assetLinks = new Dictionary<string, Tuple<int, Asset>>();

        public MainForm()
        {
            InitializeComponent();
            listView1.ListViewItemSorter = null;
            listSelectTimer.Interval = 100;
            listSelectTimer.Tick += RefreshDetailsText;
            listSelectTimer.Tick += delegate (object sender, EventArgs e)
            {
                RefreshEntryStatus();
                listSelectTimer.Stop();
            };
        }

        static void AddListViewItems(ListView listView, ListViewItem[] items)
        {
            listView.BeginUpdate();
            listView.Items.AddRange(items);
            listView.EndUpdate();
        }

        private void RefreshDataTypeChoices()
        {
            comboBox1.Items.Clear();
            string[] items = new string[cpuFile.usedDataTypes.Count];
            int i = 0;
            foreach (DataType dataType in cpuFile.usedDataTypes)
            {
                items[i] = dataType.GetDescription();
                i++;
            }
            comboBox1.Items.AddRange(items);
        }

        private void RefreshCpuEntryList(bool keepSelectedEntries = true)
        {
            int[] selectedEntryNums = null;
            int focusedEntryNum = 0;
            if (keepSelectedEntries)
            {
                focusedEntryNum = listView1.FocusedItem != null ? int.Parse(listView1.FocusedItem.SubItems[0].Text) : 0;
                int nSelected = listView1.SelectedItems.Count;
                selectedEntryNums = new int[nSelected];
                for (int i = 0; i < nSelected; i++)
                {
                    selectedEntryNums[i] = int.Parse(listView1.SelectedItems[i].SubItems[0].Text);
                }
            }

            listView1.Items.Clear();
            int nEntries = cpuFile.Count;
            List<ListViewItem> items = new List<ListViewItem>();
            for (int i = 0; i < nEntries; i++)
            {
                Asset entry = cpuFile[i];
                if (!hiddenAssetNums.Contains(entry.assetNumber) &&
                    entry.name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase) && 
                    entry.dataType.GetDescription().Contains(typeFilter, StringComparison.OrdinalIgnoreCase))
                {
                    ListViewItem item = new ListViewItem(new string[]
                    {
                        entry.assetNumber.ToString(),
                        entry.dataType.GetDescription(),
                        entry.name
                    });
                    items.Add(item);
                    assetNumberToListViewItem[entry.assetNumber] = item;
                }
            }
            AddListViewItems(listView1, items.ToArray());

            if (selectedEntryNums == null)
            {
                return;
            }
            int nItems = listView1.Items.Count;
            for (int i = 0; i < nItems; i++)
            {
                ListViewItem item = listView1.Items[i];
                if (int.Parse(item.SubItems[0].Text) == focusedEntryNum)
                {
                    item.Focused = true;
                    item.EnsureVisible();
                }
                if (Array.IndexOf(selectedEntryNums, int.Parse(item.SubItems[0].Text)) != -1)
                {
                    item.Selected = true;
                }
            }
        }

        public void RefreshEntryStatus()
        {
            toolStripStatusLabel3.Text = string.Format("{0} Assets, {1} Selected", listView1.Items.Count, listView1.SelectedItems.Count);
        }

        public void RefreshDetailsText(object sender = null, EventArgs e = null)
        {
            string details = "";
            assetLinks.Clear();
            int count = Math.Min(listView1.SelectedItems.Count, 100);
            for (int i = 0; i < count; i++)
            {
                Asset entry = cpuFile[int.Parse(listView1.SelectedItems[i].Text)];
                Node node = entry as Node;
                Resource resource = entry as Resource;

                details += string.Format("----------- ASSET #{0} -----------", entry.assetNumber);
                details += (node != null) ? "\nSumo Engine Node" : "\nSumo Libraries Resource";
                details += string.Format("\nType: {0} ({0:X})", entry.dataType);
                details += string.Format("\nAsset ID: {0:X8}", entry.id);
                details += string.Format("\nName: {0}", entry.name, entry.id);
                if (node != null && !string.IsNullOrEmpty(node.shortName))
                {
                    details += string.Format("\nShort name: {0}", node.shortName, entry.id);
                }

                if (entry.dataType != DataType.SeRootFolderNode)
                {
                    details += "\n\n~~~ File Offsets ~~~";
                    details += string.Format("\nCPU: 0x{0:X}", entry.cpuOffsetDataHeader);
                    details += string.Format("\nCPU length: {0} bytes (with padding)", entry.cpuRelativeOffsetNextEntry);
                }
                if (entry.gpuDataLength > 0)
                {
                    details += string.Format(
                        "\nGPU: 0x{0:X}" +
                        "\nGPU length: {1} bytes",
                    entry.gpuOffsetData, entry.gpuDataLength);
                }

                if (node != null)
                {
                    if (node.definition.Count > 0 || node.parent.Count > 0 || node.daughters.Count > 0 || node.instances.Count > 0)
                    {
                        details += "\n\n~~~ Node Links ~~~";
                    }
                    if (node.definition.Count > 0)
                    {
                        details += "\nDefinition:\n";
                        AddEntryLink(ref details, node.definition[0]);
                    }
                    if (node.parent.Count > 0)
                    {
                        details += "\nParent:\n";
                        AddEntryLink(ref details, node.parent[0]);
                    }
                    if (node.daughters.Count > 0)
                    {
                        details += "\nDaughters:";
                        foreach (Node n in node.daughters)
                        {
                            details += "\n";
                            AddEntryLink(ref details, n);
                        }
                    }
                    if (node.instances.Count > 0)
                    {
                        details += string.Format("\nInstances:");
                        foreach(Node n in node.instances)
                        {
                            details += "\n";
                            AddEntryLink(ref details, n);
                        }
                    }
                }

                if (entry.references.Count > 0)
                {
                    details += "\n\n~~~ Assets Used ~~~";
                    AddRefs(ref details, entry.id, entry.references);
                }
                if (entry.referees.Count > 0)
                {
                    details += "\n\n~~ Used by ~~";
                    AddRefs(ref details, entry.id, entry.referees);
                }

                if (i != count - 1)
                {
                    details += "\n\n\n";
                }
            }
            if (listView1.SelectedItems.Count > 100)
            {
                details += "\n\nMore than 100 items are selected. The remaining details are not shown.";
            }
            if (listView1.SelectedItems.Count == 0)
            {
                details += "Please select an entry.";
            }
            richTextBox1.Clear();
            richTextBox1.Text = details;
            FormatAssetLinks();
            richTextBox1.SelectionStart = 0;
        }

        private void AddEntryLink(ref string details, Asset entry, int maxLength = 0)
        {
            string linkText;
            if (entry.assetNumber != -1)
            {
                linkText = "#" + entry.assetNumber + " " + entry.name;
            }
            else
            {
                linkText = entry.id.ToString("X8");
            }
            if (maxLength > 0)
            {
                TruncateString(ref linkText, maxLength);
            }
            if (!assetLinks.ContainsKey(linkText))
            {
                assetLinks[linkText] = new Tuple<int, Asset>(details.Length, entry);
            }
            details += linkText;
        }

        private void FormatAssetLinks()
        {
            foreach (var link in assetLinks)
            {
                int position = link.Value.Item1;
                richTextBox1.SelectionStart = position;
                richTextBox1.Select(position, link.Key.Length);
                richTextBox1.SetSelectionLink(true);
                richTextBox1.Select(position + link.Key.Length, 0);
            }
        }

        private void AddRefs(ref string details, uint entryId, List<Asset> refs)
        {
            if (refs.Count == 0)
            {
                return;
            }
            HashSet<uint> ids = new HashSet<uint>();
            foreach (var entries in refs.GroupBy(x => x.dataType))
            {
                details += string.Format("\n{0}: ", entries.Key);
                foreach (Asset entry in entries)
                {
                    if (ids.Contains(entry.id))
                    {
                        continue;
                    }
                    details += "\n";
                    AddEntryLink(ref details, entry);
                    /*
                    if (entryId == entry.id)
                    {
                        details += " (same ID)";
                    }
                    */
                    ids.Add(entry.id);
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog fileDialog = new OpenFileDialog() { Filter = "CPU Files|*.cpu.***", Multiselect = false, Title = "Select a CPU file", InitialDirectory = lastCpuFilePath})
            {
                if (fileDialog.ShowDialog() == DialogResult.OK)
                {
                    cpuFile = new CpuFile(fileDialog.FileName);
                    toolStripStatusLabel1.Text = string.Format("Current file: {0} (Sumo tool v{2}, {1}-endian)", fileDialog.FileName, cpuFile.isLittleEndian ? "Little" : "Big", cpuFile[1].toolVersion);
                    RefreshCpuEntryList();
                    RefreshEntryStatus();
                    RefreshDetailsText();
                    RefreshDataTypeChoices();
                    lastCpuFilePath = Path.GetDirectoryName(fileDialog.FileName);
                    button2.Enabled = button4.Enabled = textBox1.Enabled = comboBox1.Enabled = true;
                    button3.Enabled = false;
                }
            }
        }

        private void comboBox1_TextUpdate(object sender, EventArgs e)
        {
            typeFilter = comboBox1.Text;
            RefreshCpuEntryList();
            RefreshEntryStatus();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            nameFilter = textBox1.Text;
            RefreshCpuEntryList();
            RefreshEntryStatus();
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            typeFilter = comboBox1.Text;
            RefreshCpuEntryList();
            RefreshEntryStatus();
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                nameFilter = textBox1.Text;
                RefreshCpuEntryList();
                RefreshEntryStatus();
            }
        }

        private void comboBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                typeFilter = comboBox1.Text;
                RefreshCpuEntryList();
                RefreshEntryStatus();
            }
        }

        private void comboBox1_Click(object sender, EventArgs e)
        {
            comboBox1.SelectionStart = 0;
            comboBox1.SelectionLength = comboBox1.Text.Length;
        }

        private void textBox1_Click(object sender, EventArgs e)
        {
            textBox1.SelectionStart = 0;
            textBox1.SelectionLength = textBox1.Text.Length;
        }

        private void listView1_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            listSelectTimer.Stop();
            listSelectTimer.Start();
        }

        private void SaveToFile(bool cpuData, bool gpuData)
        {
            using (FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog() { Description = "Choose an output folder", SelectedPath = lastDataSavePath })
            {
                int count = listView1.SelectedItems.Count;
                List<string> fileNames = new List<string>();
                string fileName;
                if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                {
                    for (int i = 0; i < count; i++)
                    {
                        int entryIndex = int.Parse(listView1.SelectedItems[i].Text);
                        if (entryIndex == 0)
                        {
                            continue;
                        }
                        if (cpuData)
                        {
                            fileNames.Add(cpuFile.SaveCpuData(entryIndex, folderBrowserDialog.SelectedPath));
                        }
                        if (gpuData)
                        {
                            fileName = cpuFile.SaveGpuData(entryIndex, folderBrowserDialog.SelectedPath);
                            if (!string.IsNullOrEmpty(fileName))
                            {
                                fileNames.Add(fileName);
                            }
                        }
                    }
                    using (InfoForm form = new InfoForm())
                    {
                        form.richTextBox1.Text = "\nSuccessfully saved the following files:\n\n" + string.Join("\n", fileNames);
                        form.ShowDialog();
                    }
                    lastDataSavePath = folderBrowserDialog.SelectedPath;
                }
            }
        }

        private void richTextBox1_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            Asset asset = assetLinks[e.LinkText].Item2;
            SelectAssetByID(asset.id, asset as Node != null ? EntryType.Node : EntryType.Resource);
        }

        private void IDSearch(EntryType entryType)
        {
            using (DialogBoxTextEntry dialog = new DialogBoxTextEntry())
            {
                dialog.Text = string.Format("Enter a {0} ID", entryType);
                dialog.button1.Text = "Search";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    if (!ParseHexString(dialog.textBox1.Text, out uint id))
                    {
                        MessageBox.Show("Invalid ID!\nYou must enter a 32bit hex number.", "Invalid ID!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    SelectAssetByID(id, entryType);
                }
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            IDSearch(EntryType.Node);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            IDSearch(EntryType.Resource);
        }

        private bool ParseHexString(string idString, out uint id)
        {
            if (idString.StartsWith("0x", StringComparison.CurrentCultureIgnoreCase))
            {
                idString = idString.Substring(2);
            }
            return uint.TryParse(idString, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out id);
        }

        private void SelectAssetByID(uint id, EntryType type = (EntryType)3) // Default search type: nodes + resources
        {
            switch (type)
            {
                case EntryType.Node:
                    if (cpuFile.nodeDictionary.TryGetValue(id, out List<Node> nodes))
                    {
                        SelectAssetsInList(nodes);
                    }
                    else
                    {
                        MessageBox.Show("Node not found!\nIt may exist in a different CPU file.",
                            "Not found!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    return;
                case EntryType.Resource:
                    if (cpuFile.resourceDictionary.TryGetValue(id, out List<Resource> resources))
                    {
                        SelectAssetsInList(resources);
                    }
                    else
                    {
                        MessageBox.Show("Resource not found!\nIt may exist in a different CPU file.",
                            "Not found!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    return;
                default:
                    if (cpuFile.nodeDictionary.TryGetValue(id, out nodes))
                    {
                        SelectAssetsInList(nodes);
                    }
                    else if (cpuFile.resourceDictionary.TryGetValue(id, out resources))
                    {
                        SelectAssetsInList(resources);
                    }
                    else
                    {
                        MessageBox.Show("Entry not found!\nIt may exist in a different CPU file.",
                            "Not found!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    return;
            }
        }

        private void SelectAssetsInList<T>(ICollection<T> assets) where T : Asset
        {
            if (assets.Count == 0)
            {
                return;
            }

            // Unhide assets that need selecting
            int nHiddenAssets = hiddenAssetNums.Count;
            if (nHiddenAssets > 0)
            {
                foreach (Asset asset in assets)
                {
                    hiddenAssetNums.Remove(asset.assetNumber);
                }
            }

            // Remove list filtering
            listView1.SelectedItems.Clear();
            if (typeFilter != "" || nameFilter != "" || nHiddenAssets != hiddenAssetNums.Count)
            {
                typeFilter = "";
                nameFilter = "";
                RefreshCpuEntryList();
            }
            int count = 0;
            foreach(Asset asset in assets)
            {
                if (asset.assetNumber == -1)
                {
                    continue;
                }
                if (assetNumberToListViewItem.TryGetValue(asset.assetNumber, out ListViewItem item))
                {
                    item.Selected = true;
                    if (count++ == 0)
                    {
                        item.Focused = true;
                        item.EnsureVisible();
                    }
                }
            }
            listView1.Select();
        }

        private void listView1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (listView1.FocusedItem.Bounds.Contains(e.Location))
                {
                    int count = listView1.SelectedItems.Count;

                    if (count == 1 && int.Parse(listView1.SelectedItems[0].SubItems[0].Text) == 0) // Only root node selected
                    {
                        ((ToolStripMenuItem)contextMenuStrip1.Items["cutToolStripMenuItem"]).Enabled = false;
                        ((ToolStripMenuItem)contextMenuStrip1.Items["copyToolStripMenuItem"]).Enabled = false;
                        ((ToolStripMenuItem)contextMenuStrip1.Items["deleteToolStripMenuItem"]).Enabled = false;
                        ((ToolStripMenuItem)contextMenuStrip1.Items["editToolStripMenuItem"]).Enabled = false;
                        ((ToolStripMenuItem)contextMenuStrip1.Items["saveToFileToolStripMenuItem"]).Enabled = false;
                        ((ToolStripMenuItem)contextMenuStrip1.Items["fromFileToolStripMenuItem"]).DropDownItems["replaceDataToolStripMenuItem"].Enabled = false;
                        contextMenuStrip1.Show(Cursor.Position);
                        return;
                    }

                    bool gpuDataAvailable = false;
                    bool allNodes = true;
                    bool allNodesHaveDefinitions = true;
                    for (int i = 0; i < count; i++)
                    {
                        Asset asset = cpuFile[int.Parse(listView1.SelectedItems[i].SubItems[0].Text)];
                        if (!gpuDataAvailable && asset.gpuDataLength > 0)
                        {
                            gpuDataAvailable = true;
                        }
                        Node node = asset as Node;
                        if (allNodes && node == null)
                        {
                            allNodes = false;
                        }
                        if (allNodesHaveDefinitions && (!allNodes || node.definition.Count == 0))
                        {
                            allNodesHaveDefinitions = false;
                        }
                        if (gpuDataAvailable && !allNodes && !allNodesHaveDefinitions)
                        {
                            break;
                        }
                    }


                    ((ToolStripMenuItem)contextMenuStrip1.Items["cutToolStripMenuItem"]).Enabled = true;
                    ((ToolStripMenuItem)contextMenuStrip1.Items["copyToolStripMenuItem"]).Enabled = true;
                    ((ToolStripMenuItem)contextMenuStrip1.Items["deleteToolStripMenuItem"]).Enabled = true;
                    ((ToolStripMenuItem)contextMenuStrip1.Items["editToolStripMenuItem"]).Enabled = true;
                    ((ToolStripMenuItem)contextMenuStrip1.Items["saveToFileToolStripMenuItem"]).Enabled = true;
                    ((ToolStripMenuItem)contextMenuStrip1.Items["fromFileToolStripMenuItem"]).DropDownItems["replaceDataToolStripMenuItem"].Enabled = true;

                    ((ToolStripMenuItem)contextMenuStrip1.Items["editToolStripMenuItem"]).DropDownItems["changeParentToolStripMenuItem"].Enabled = allNodes;
                    ((ToolStripMenuItem)contextMenuStrip1.Items["editToolStripMenuItem"]).DropDownItems["changeDefinitionToolStripMenuItem"].Enabled = allNodesHaveDefinitions;

                    ((ToolStripMenuItem)contextMenuStrip1.Items["saveToFileToolStripMenuItem"]).DropDownItems["gpuDataToolStripMenuItem"].Enabled = gpuDataAvailable;
                    ((ToolStripMenuItem)contextMenuStrip1.Items["saveToFileToolStripMenuItem"]).DropDownItems["bothToolStripMenuItem"].Enabled = gpuDataAvailable;

                    ((ToolStripMenuItem)contextMenuStrip1.Items["fromFileToolStripMenuItem"]).DropDownItems["replaceDataToolStripMenuItem"].Enabled = (listView1.SelectedItems.Count == 1);
                    ((ToolStripMenuItem)((ToolStripMenuItem)contextMenuStrip1.Items["fromFileToolStripMenuItem"]).DropDownItems["replaceDataToolStripMenuItem"]).DropDownItems["gPUDataToolStripMenuItem1"].Enabled = gpuDataAvailable;


                    contextMenuStrip1.Items["pasteToolStripMenuItem"].Enabled = (GetDataFromClipboard() != null);

                    contextMenuStrip1.Show(Cursor.Position);
                }
            }
        }

        public void PutDataOnClipboard(MemoryStream[] data)
        {
            DataObject clipboardData = new DataObject();
            clipboardData.SetData(typeof(MemoryStream[]), data);
            Clipboard.SetDataObject(clipboardData, true);
        }

        public MemoryStream[] GetDataFromClipboard()
        {
            DataObject retrievedData = Clipboard.GetDataObject() as DataObject;
            if (retrievedData == null || !retrievedData.GetDataPresent(typeof(MemoryStream[])))
                return null;
            return (MemoryStream[]) retrievedData.GetData(typeof(MemoryStream[]));
        }

        private void CopyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MemoryStream cpuData = new MemoryStream();
            MemoryStream gpuData = new MemoryStream();

            int count = listView1.SelectedItems.Count;
            for (int i = 0; i < count; i++)
            {
                int entryIndex = int.Parse(listView1.SelectedItems[i].SubItems[0].Text);
                if (entryIndex == 0)
                {
                    continue;
                }
                cpuFile.GetCpuData(entryIndex, cpuData, -1);
                cpuFile.GetGpuData(entryIndex, gpuData, -1);
            }
            PutDataOnClipboard(new MemoryStream[] { cpuData, gpuData });
        }

        private void PasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int entryIndex = int.Parse(listView1.FocusedItem.SubItems[0].Text);
            MemoryStream[] data = GetDataFromClipboard();
            cpuFile.InsertCpuData(entryIndex, data[0]);
            cpuFile.InsertGpuData(entryIndex, data[1]);
            cpuFile.Reload();
            RefreshCpuEntryList(true);
            RefreshEntryStatus();
            RefreshDetailsText();
            RefreshDataTypeChoices();
            button3.Enabled = true;
        }

        private void DeleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int count = listView1.SelectedItems.Count;
            for (int i = count - 1; i >= 0; i--) // Process in reverse order, to ensure offsets do not change while modifying
            {
                int entryIndex = int.Parse(listView1.SelectedItems[i].SubItems[0].Text);
                if (entryIndex == 0)
                {
                    continue;
                }
                cpuFile.DeleteCpuData(entryIndex);
                cpuFile.DeleteGpuData(entryIndex);
            }
            cpuFile.Reload();
            RefreshCpuEntryList();
            RefreshEntryStatus();
            RefreshDetailsText();
            RefreshDataTypeChoices();
            button3.Enabled = true;
        }

        private void CutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyToolStripMenuItem_Click(sender, e);
            DeleteToolStripMenuItem_Click(sender, e);
            button3.Enabled = true;
        }

        private void CpuDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveToFile(true, false);
        }

        private void GpuDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveToFile(false, true);
        }

        private void BothToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveToFile(true, true);
        }

        private void RenameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (DialogBoxTextEntry dialog = new DialogBoxTextEntry())
            {
                dialog.Text = "Choose a name";
                dialog.textBox1.Text = listView1.FocusedItem.SubItems[2].Text;
                dialog.button1.Text = "Confirm";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    int count = listView1.SelectedItems.Count;
                    for (int i = 0; i < count; i++)
                    {
                        int entryIndex = int.Parse(listView1.SelectedItems[i].SubItems[0].Text);
                        if (entryIndex == 0)
                        {
                            continue;
                        }
                        cpuFile.ChangeEntryName(int.Parse(listView1.SelectedItems[i].Text), dialog.textBox1.Text);
                    }
                    cpuFile.Reload();
                    RefreshCpuEntryList(true);
                    button3.Enabled = true;
                }
            }
        }

        private void EntryIDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int entryIndex = int.Parse(listView1.FocusedItem.SubItems[0].Text);
            using (DialogBoxTextEntry dialog = new DialogBoxTextEntry())
            {
                dialog.Text = "Choose an ID";
                dialog.textBox1.Text = cpuFile[entryIndex].id.ToString("X8");
                dialog.button1.Text = "Confirm";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    if (ParseHexString(dialog.textBox1.Text, out uint id))
                    {
                        int count = listView1.SelectedItems.Count;
                        for (int i = 0; i < count; i++)
                        {
                            entryIndex = int.Parse(listView1.SelectedItems[i].SubItems[0].Text);
                            if (entryIndex == 0)
                            {
                                continue;
                            }
                            cpuFile.ChangeEntryID(entryIndex, id);
                        }
                        cpuFile.Reload();
                        RefreshDetailsText();
                        button3.Enabled = true;
                    }
                }
            }
        }

        private void ChangeDefinitionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int entryIndex = int.Parse(listView1.FocusedItem.SubItems[0].Text);
            using (DialogBoxTextEntry dialog = new DialogBoxTextEntry())
            {
                dialog.Text = "Choose a definition ID";
                Node node = cpuFile[entryIndex] as Node;
                if (node.definition.Count > 0)
                {
                    dialog.textBox1.Text = (cpuFile[entryIndex] as Node).definition[0].id.ToString("X8");
                }
                dialog.button1.Text = "Confirm";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    if (ParseHexString(dialog.textBox1.Text, out uint id))
                    {
                        int count = listView1.SelectedItems.Count;
                        for (int i = 0; i < count; i++)
                        {
                            entryIndex = int.Parse(listView1.SelectedItems[i].SubItems[0].Text);
                            if (entryIndex == 0)
                            {
                                continue;
                            }
                            cpuFile.ChangeDefinitionID(entryIndex, id);
                        }
                        cpuFile.Reload();
                        RefreshDetailsText();
                        button3.Enabled = true;
                    }
                }
            }
        }

        private void ChangeParentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int entryIndex = int.Parse(listView1.FocusedItem.SubItems[0].Text);
            using (DialogBoxTextEntry dialog = new DialogBoxTextEntry())
            {
                dialog.Text = "Choose a parent ID";
                Node node = cpuFile[entryIndex] as Node;
                if (node.parent.Count > 0)
                {
                    dialog.textBox1.Text = (cpuFile[entryIndex] as Node).parent[0].id.ToString("X8");
                }      
                dialog.button1.Text = "Confirm";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    if (ParseHexString(dialog.textBox1.Text, out uint id))
                    {
                        int count = listView1.SelectedItems.Count;
                        for (int i = 0; i < count; i++)
                        {
                            entryIndex = int.Parse(listView1.SelectedItems[i].SubItems[0].Text);
                            if (entryIndex == 0)
                            {
                                continue;
                            }
                            cpuFile.ChangeParentID(entryIndex, id);
                        }
                        cpuFile.Reload();
                        RefreshDetailsText();
                        button3.Enabled = true;
                    }
                }
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            cpuFile.Save();
            button3.Enabled = false;
        }

        private void cPUDataToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            ReplaceFromFile(true, false);
        }

        private void gPUDataToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            ReplaceFromFile(false, true);
        }

        private void bothToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            ReplaceFromFile(true, true);
        }

        private void ReplaceFromFile(bool cpuData, bool gpuData)
        {
            int entryIndex = int.Parse(listView1.FocusedItem.SubItems[0].Text);
            string cpuDataPath = null;
            string gpuDataPath = null;
            using (OpenFileDialog fileDialog = new OpenFileDialog() { Multiselect = false, InitialDirectory = lastDataOpenPath })
            {      
                if (cpuData)
                {
                    fileDialog.Title = "Select CPU data file";
                    if (fileDialog.ShowDialog() == DialogResult.OK)
                    {
                        cpuDataPath = fileDialog.FileName;
                    }
                    else
                    {
                        return;
                    }
                }
                if (gpuData)
                {
                    fileDialog.Title = "Select GPU data file";
                    if (fileDialog.ShowDialog() != DialogResult.OK)
                    {
                        return;
                    }
                    gpuDataPath = fileDialog.FileName;
                }

                CpuFile tmpCpuFile = null;
                if (cpuData)
                {
                    tmpCpuFile = new CpuFile(cpuDataPath); // Read in data to find how many entries it contains
                    cpuFile.ReplaceCpuData(entryIndex, tmpCpuFile.msCpuFile, updateHeader: tmpCpuFile.Count == 1);
                }
                if (gpuData)
                {
                    cpuFile.ReplaceGpuData(entryIndex, gpuDataPath, updateHeader: tmpCpuFile == null || tmpCpuFile.Count == 1);
                }

                cpuFile.Reload();
                if (cpuData)
                {
                    RefreshCpuEntryList();
                    RefreshEntryStatus();
                    RefreshDataTypeChoices();
                }
                RefreshDetailsText();
                button3.Enabled = true;
                lastDataOpenPath = Path.GetDirectoryName(fileDialog.FileName);
            }
        }

        private void insertDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int entryIndex = int.Parse(listView1.FocusedItem.SubItems[0].Text);
            using (OpenFileDialog fileDialog = new OpenFileDialog() { Multiselect = false, InitialDirectory = lastDataOpenPath })
            {
                CpuFile tmpCpuFile;
                fileDialog.Title = "Select CPU data file";
                if (fileDialog.ShowDialog() == DialogResult.OK)
                {
                    tmpCpuFile = new CpuFile(fileDialog.FileName);
                }
                else
                {
                    return;
                }
                Asset lastEntry = tmpCpuFile.entriesList.Last();
                if (lastEntry.gpuOffsetData + lastEntry.gpuRelativeOffsetNextEntry > 0)
                {
                    fileDialog.Title = "Select GPU data file";
                    if (fileDialog.ShowDialog() != DialogResult.OK)
                    {
                        return;
                    }
                    cpuFile.InsertCpuData(entryIndex, tmpCpuFile.msCpuFile);
                    int length = cpuFile.InsertGpuData(entryIndex, fileDialog.FileName);
                    if (tmpCpuFile.Count == 1)
                    {
                        cpuFile.ChangeGpuDataInfo(entryIndex, length, length);
                    }
                }
                else
                {
                    cpuFile.InsertCpuData(entryIndex, tmpCpuFile.msCpuFile);
                }

                cpuFile.Reload();
                RefreshCpuEntryList();
                RefreshEntryStatus();
                RefreshDataTypeChoices();
                RefreshDetailsText();
                button3.Enabled = true;
                lastDataOpenPath = Path.GetDirectoryName(fileDialog.FileName);
            }
        }

        private void parentToolStripMenuItem_Click(object sender, EventArgs e)
        {    
            List<Node> parents = new List<Node>();
            foreach(ListViewItem item in listView1.SelectedItems)
            {
                Node node = cpuFile[int.Parse(item.SubItems[0].Text)] as Node;
                if (node != null)
                {
                    parents.AddRange(node.parent);
                }
            }
            SelectAssetsInList(parents);
        }

        private void daughtersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<Node> daughters = new List<Node>();
            foreach (ListViewItem item in listView1.SelectedItems)
            {
                Node node = cpuFile[int.Parse(item.SubItems[0].Text)] as Node;
                if (node != null)
                {
                    daughters.AddRange(node.daughters);
                }
            }       
            SelectAssetsInList(daughters);
        }

        private void dependeiciesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<Asset> dependencies = new List<Asset>();
            foreach (ListViewItem item in listView1.SelectedItems)
            {
                Asset asset = cpuFile[int.Parse(item.SubItems[0].Text)];
                ColectDependencies(asset, dependencies);
            }
            SelectAssetsInList(dependencies);
        }

        private void ColectDependencies(Asset asset, List<Asset> outList)
        {
            foreach(Asset reference in asset.references)
            {
                outList.Add(reference);
                ColectDependencies(reference, outList);
            }
            Node node = asset as Node;
            if (node != null)
            {
                foreach(Node definition in node.definition)
                {
                    outList.Add(definition);
                    ColectDependencies(definition, outList);
                }
                foreach(Node daughter in node.daughters)
                {
                    outList.Add(daughter);
                    ColectDependencies(daughter, outList);
                }
            }
        }

        private void definitionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<Node> definitions = new List<Node>();
            foreach (ListViewItem item in listView1.SelectedItems)
            {
                Node node = cpuFile[int.Parse(item.SubItems[0].Text)] as Node;
                if (node != null)
                {
                    definitions.AddRange(node.definition);
                }
            }
            SelectAssetsInList(definitions);
        }

        private void instancesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<Node> instances = new List<Node>();
            foreach (ListViewItem item in listView1.SelectedItems)
            {
                Node node = cpuFile[int.Parse(item.SubItems[0].Text)] as Node;
                if (node != null)
                {
                    instances.AddRange(node.instances);
                }
            }
            SelectAssetsInList(instances);
        }

        public static void TruncateString(ref string s, int maxlength)
        {
            if (s.Length > maxlength)
            {
                s = s.Substring(0, maxlength - 3) + "...";
            }
        }

        private void selectedEntriesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in listView1.SelectedItems)
            {
                hiddenAssetNums.Add(int.Parse(item.SubItems[0].Text));
            }
            RefreshCpuEntryList();
            RefreshEntryStatus();
        }

        private void unhideAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            hiddenAssetNums.Clear();
            RefreshCpuEntryList();
        }

        private void unselectedEntriesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem item in listView1.Items)
            {
                if (!item.Selected)
                {
                    hiddenAssetNums.Add(int.Parse(item.SubItems[0].Text));
                }
            }
            RefreshCpuEntryList();
        }
    }
}
