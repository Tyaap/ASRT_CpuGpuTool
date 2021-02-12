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
        public Timer listSelectTimer = new Timer();
        public string lastCpuFilePath = "";
        public string lastDataSavePath = "";
        public string lastDataOpenPath = "";
        Dictionary<uint, Tuple<int, EntryType>> entryLinks = new Dictionary<uint, Tuple<int, EntryType>>();

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
                CpuEntry entry = cpuFile[i];
                if (entry.name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase) && 
                    entry.dataType.GetDescription().Contains(typeFilter, StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(new ListViewItem(new string[]
                    {
                        entry.entryNumber.ToString(),
                        entry.dataType.GetDescription(),
                        entry.name
                    }));
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
            entryLinks.Clear();
            int count = Math.Min(listView1.SelectedItems.Count, 100);
            for (int i = 0; i < count; i++)
            {
                CpuEntry entry = cpuFile[int.Parse(listView1.SelectedItems[i].Text)];
                Node node = entry as Node;
                Resource resource = entry as Resource;

                details += string.Format("----------- ASSET #{0} -----------", entry.entryNumber);
                details += (node != null) ? "\nSumo Engine Node" : "\nSumo Libraries Resource";
                details += string.Format("\nType: {0} ({0:X})", entry.dataType);
                details += string.Format("\nAsset ID: {0:X8}", entry.id);
                details += string.Format("\nName: {0}", entry.name, entry.id);
                if (node != null && !string.IsNullOrEmpty(node.shortName))
                {
                    details += string.Format("\nShort name: {0}", node.shortName, entry.id);
                }

                if (entry.dataType != DataType.SeRootNode)
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
                    if (node.definition != null || node.parent != null || node.daughters.Count > 0 || node.instances.Count > 0)
                    {
                        details += "\n\n~~~ Node Links ~~~";
                    }
                    if (node.definition != null)
                    {
                        details += "\nDefinition: ";
                        AddEntryLink(ref details, node.definition);
                    }
                    if (node.parent != null)
                    {
                        details += "\nParent: ";
                        AddEntryLink(ref details, node.parent);
                    }
                    if (node.daughters.Count == 1)
                    {
                        details += "\nDaughter: ";
                        AddEntryLink(ref details, node.daughters.Values.Single());

                    }
                    if (node.daughters.Count > 1)
                    {
                        details += "\nDaughters:";
                        foreach (Node n in node.daughters.Values)
                        {
                            details += "\n    ";
                            AddEntryLink(ref details, n);
                        }
                    }
                    if (node.instances.Count == 1)
                    {
                        details += "\nInstance: ";
                        AddEntryLink(ref details, node.instances.Values.Single());
                    }
                    if (node.instances.Count > 1)
                    {
                        details += string.Format("\nInstances:");
                        int count2 = node.instances.Count;
                        foreach(Node n in node.instances.Values)
                        {
                            details += "\n    ";
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
                    details += "\n\n";
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
            FormatEntryLinks();
        }

        private void AddEntryLink(ref string details, CpuEntry entry)
        {
            entryLinks[entry.id] = new Tuple<int, EntryType>(details.Length, entry as Node != null ? EntryType.Node : EntryType.Resource);
            details += entry.id.ToString("X8");
        }

        private void FormatEntryLinks()
        {
            foreach (var link in entryLinks)
            {
                int position = link.Value.Item1;
                richTextBox1.SelectionStart = position;
                richTextBox1.Select(position, 8);
                richTextBox1.SetSelectionLink(true);
                richTextBox1.Select(position + 8, 0);
            }
        }

        private void AddRefs(ref string details, uint entryId, Dictionary<uint, CpuEntry> refs)
        {
            if (refs.Count == 0)
            {
                return;
            }
            foreach (var entries in refs.Values.GroupBy(x => x.dataType))
            {
                details += string.Format("\n{0}: ", entries.Key);
                foreach (CpuEntry entry in entries)
                {
                    details += "\n    ";
                    AddEntryLink(ref details, entry);
                    if (entryId == entry.id)
                    {
                        details += " (same ID)";
                    }
                }
            }
        }

        private void AddReferees(ref string details, Resource resource)
        {
            if (resource.referees.Count == 0)
            {
                return;
            }
            foreach (var entries in resource.referees.Values.GroupBy(x => x.dataType))
            {
                details += string.Format("\n{0}: ", entries.Key);
                foreach (CpuEntry entry in entries)
                {
                    details += "\n    ";
                    AddEntryLink(ref details, entry);
                    if (resource.id == entry.id)
                    {
                        details += " (same ID)";
                    }
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
            ParseHexString(e.LinkText, out uint id);
            SelectEntryByID(id, entryLinks[id].Item2);
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
                    SelectEntryByID(id, entryType);
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

        private void SelectEntryByID(uint id, EntryType type = (EntryType)3) // Default search type: nodes + resources
        {
            CpuEntry entry;
            switch (type)
            {
                case EntryType.Node:
                    if (!cpuFile.nodeDictionary.TryGetValue(id, out Node node))
                    {
                        MessageBox.Show("Node not found!\nIt may exist in a different CPU file.",
                        "Not found!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    entry = node;
                    break;
                case EntryType.Resource:
                    if (!cpuFile.resourceDictionary.TryGetValue(id, out Resource resource))
                    {
                        MessageBox.Show("Resource not found!\nIt may exist in a different CPU file.",
                            "Not found!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    entry = resource;
                    break;
                default:
                    if (cpuFile.nodeDictionary.TryGetValue(id, out node))
                    {
                        entry = node;
                    }
                    else if (cpuFile.resourceDictionary.TryGetValue(id, out resource))
                    {
                        entry = resource;
                    }
                    else
                    {
                        MessageBox.Show("Entry not found!\nIt may exist in a different CPU file.",
                            "Not found!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    break;
            }

            // Clear other searches
            typeFilter = "";
            nameFilter = "";
            RefreshCpuEntryList();
            RefreshEntryStatus();

            var item = listView1.Items[entry.entryNumber];
            listView1.SelectedItems.Clear();
            item.Selected = true;
            item.Focused = true;
            item.EnsureVisible();
            listView1.Select();
            return;
        }

        private void listView1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (listView1.FocusedItem.Bounds.Contains(e.Location))
                {
                    int count = listView1.SelectedItems.Count;
                    bool gpuDataAvailable = false;
                    bool allNodesHaveDefinitions = true;
                    bool allNodesHaveParents = true;
                    bool containsRootNode = false;
                    for (int i = 0; i < count; i++)
                    {
                        int entryIndex = int.Parse(listView1.SelectedItems[i].SubItems[0].Text);
                        if(entryIndex == 0)
                        {
                            containsRootNode = true;
                            break;
                        }
                        if (!gpuDataAvailable && cpuFile[entryIndex].gpuDataLength > 0)
                        {
                            gpuDataAvailable = true;
                        }
                        Node node = cpuFile[entryIndex] as Node;
                        if (allNodesHaveDefinitions && (node == null || node.definition == null))
                        {
                            allNodesHaveDefinitions = false;
                        }
                        if (allNodesHaveParents && (node == null || node.parent == null))
                        {
                            allNodesHaveParents = false;
                        }
                        if (gpuDataAvailable && !allNodesHaveDefinitions && !allNodesHaveParents)
                        {
                            break;
                        }
                    }


                    if (containsRootNode)
                    {
                        ((ToolStripMenuItem)contextMenuStrip1.Items["cutToolStripMenuItem"]).Enabled = false;
                        ((ToolStripMenuItem)contextMenuStrip1.Items["copyToolStripMenuItem"]).Enabled = false;
                        ((ToolStripMenuItem)contextMenuStrip1.Items["deleteToolStripMenuItem"]).Enabled = false;
                        ((ToolStripMenuItem)contextMenuStrip1.Items["editToolStripMenuItem"]).Enabled = false;
                        ((ToolStripMenuItem)contextMenuStrip1.Items["saveToFileToolStripMenuItem"]).Enabled = false;
                        ((ToolStripMenuItem)contextMenuStrip1.Items["fromFileToolStripMenuItem"]).DropDownItems["replaceDataToolStripMenuItem"].Enabled = false;
                    }
                    else
                    {
                        ((ToolStripMenuItem)contextMenuStrip1.Items["cutToolStripMenuItem"]).Enabled = true;
                        ((ToolStripMenuItem)contextMenuStrip1.Items["copyToolStripMenuItem"]).Enabled = true;
                        ((ToolStripMenuItem)contextMenuStrip1.Items["deleteToolStripMenuItem"]).Enabled = true;
                        ((ToolStripMenuItem)contextMenuStrip1.Items["editToolStripMenuItem"]).Enabled = true;
                        ((ToolStripMenuItem)contextMenuStrip1.Items["saveToFileToolStripMenuItem"]).Enabled = true;
                        ((ToolStripMenuItem)contextMenuStrip1.Items["fromFileToolStripMenuItem"]).DropDownItems["replaceDataToolStripMenuItem"].Enabled = true;

                        ((ToolStripMenuItem)contextMenuStrip1.Items["editToolStripMenuItem"]).DropDownItems["changeDefinitionToolStripMenuItem"].Enabled = allNodesHaveDefinitions;
                        ((ToolStripMenuItem)contextMenuStrip1.Items["editToolStripMenuItem"]).DropDownItems["changeParentToolStripMenuItem"].Enabled = allNodesHaveParents;

                        ((ToolStripMenuItem)contextMenuStrip1.Items["saveToFileToolStripMenuItem"]).DropDownItems["gpuDataToolStripMenuItem"].Enabled = gpuDataAvailable;
                        ((ToolStripMenuItem)contextMenuStrip1.Items["saveToFileToolStripMenuItem"]).DropDownItems["bothToolStripMenuItem"].Enabled = gpuDataAvailable;

                        ((ToolStripMenuItem)contextMenuStrip1.Items["fromFileToolStripMenuItem"]).DropDownItems["replaceDataToolStripMenuItem"].Enabled = (listView1.SelectedItems.Count == 1);
                        ((ToolStripMenuItem)((ToolStripMenuItem)contextMenuStrip1.Items["fromFileToolStripMenuItem"]).DropDownItems["replaceDataToolStripMenuItem"]).DropDownItems["gPUDataToolStripMenuItem1"].Enabled = gpuDataAvailable;
                    }


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
                dialog.textBox1.Text = (cpuFile[entryIndex] as Node).definition.id.ToString("X8");
                dialog.button1.Text = "Confirm";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    if (ParseHexString(dialog.textBox1.Text, out uint id))
                    {
                        int count = listView1.SelectedItems.Count;
                        for (int i = 0; i < count; i++)
                        {
                            entryIndex = int.Parse(listView1.SelectedItems[i].SubItems[0].Text);
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
                dialog.textBox1.Text = (cpuFile[entryIndex] as Node).parent.id.ToString("X8");
                dialog.button1.Text = "Confirm";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    if (ParseHexString(dialog.textBox1.Text, out uint id))
                    {
                        int count = listView1.SelectedItems.Count;
                        for (int i = 0; i < count; i++)
                        {
                            entryIndex = int.Parse(listView1.SelectedItems[i].SubItems[0].Text);
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
                CpuEntry lastEntry = tmpCpuFile.entriesList.Last();
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

    }
}
